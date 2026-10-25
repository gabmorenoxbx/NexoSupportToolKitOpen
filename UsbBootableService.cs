using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace NexoSupportToolKit.Services
{
    public enum PartitionScheme
    {
        Gpt,
        Mbr
    }

    public enum BootTarget
    {
        Uefi,
        BiosOrCsm
    }

    public enum UsbFileSystem
    {
        Fat32,
        Ntfs,
        ExFat
    }

    /// <summary>Avance de la operación: porcentaje (-1 = sin cambios), estado y línea de registro.</summary>
    public sealed record UsbProgress(int Percent, string? Status, string? LogLine);

    public sealed record UsbVolumeInfo(
        string Letter,
        string Label,
        string FileSystem,
        long SizeBytes);

    public sealed class UsbDeviceInfo
    {
        public int DiskNumber { get; init; }
        public string Model { get; init; } = "";
        public long SizeBytes { get; init; }
        public List<UsbVolumeInfo> Volumes { get; init; } = new();

        public string LettersText =>
            Volumes.Count == 0
                ? "sin letra"
                : string.Join(", ", Volumes.Select(v => v.Letter + ":"));

        public string FileSystemText
        {
            get
            {
                var systems = Volumes
                    .Select(v => v.FileSystem)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();

                return systems.Count == 0 ? "sin formato" : string.Join("/", systems);
            }
        }

        public string SizeText => UsbBootableService.FormatSize(SizeBytes);

        public override string ToString() =>
            $"Disco {DiskNumber}  |  {Model}  |  {SizeText}  |  {LettersText}  |  {FileSystemText}";
    }

    public sealed class IsoInfo
    {
        public string Path { get; init; } = "";
        public long SizeBytes { get; init; }
        public bool IsValidIso9660 { get; init; }
        public bool IsHybrid { get; init; }
        public string VolumeLabel { get; init; } = "";
    }

    public sealed class UsbBootOptions
    {
        public int DiskNumber { get; set; }
        public string ExpectedModel { get; set; } = "";
        public long ExpectedSizeBytes { get; set; }
        public string IsoPath { get; set; } = "";
        public PartitionScheme Scheme { get; set; } = PartitionScheme.Gpt;
        public BootTarget Target { get; set; } = BootTarget.Uefi;
        public UsbFileSystem FileSystem { get; set; } = UsbFileSystem.Fat32;
        public string VolumeLabel { get; set; } = "NEXO_USB";

        /// <summary>0 = tamaño predeterminado.</summary>
        public int ClusterSizeBytes { get; set; }
    }

    /// <summary>Validaciones y utilidades generales.</summary>
    public static class UsbBootableService
    {
        public static string FormatSize(long bytes)
        {
            double gb = bytes / 1024d / 1024d / 1024d;

            if (gb >= 1)
                return gb.ToString("0.0", CultureInfo.CurrentCulture) + " GB";

            double mb = bytes / 1024d / 1024d;
            return mb.ToString("0", CultureInfo.CurrentCulture) + " MB";
        }

        public static bool IsAdministrator()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Lee el sector 0 y el descriptor ISO 9660 para saber si la ISO es válida,
        /// si es híbrida (arranca copiada tal cual, típico de Linux) y cuál es su etiqueta.
        /// </summary>
        public static IsoInfo InspectIso(string path)
        {
            var info = new FileInfo(path);

            if (!info.Exists)
                throw new FileNotFoundException("No se encontró el archivo ISO.", path);

            bool hybrid = false;
            bool valid = false;
            string label = "";

            // Sistema de archivos ISO 9660: descriptor primario en el sector 16.
            if (info.Length >= 32768 + 2048)
            {
                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.Read);

                byte[] first = new byte[512];
                stream.ReadExactly(first, 0, first.Length);

                // La firma 55AA por sí sola no demuestra que una ISO sea híbrida:
                // comprobamos también que el área MBR contenga una entrada de partición.
                bool mbrSignature = first[510] == 0x55 && first[511] == 0xAA;
                bool hasPartitionEntry = false;

                if (mbrSignature)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int entry = 446 + (i * 16);
                        byte partitionType = first[entry + 4];
                        uint sectorCount = BitConverter.ToUInt32(first, entry + 12);

                        if (partitionType != 0 && sectorCount > 0)
                        {
                            hasPartitionEntry = true;
                            break;
                        }
                    }
                }

                hybrid = mbrSignature && hasPartitionEntry;

                stream.Seek(32768, SeekOrigin.Begin);
                byte[] descriptor = new byte[2048];
                stream.ReadExactly(descriptor, 0, descriptor.Length);

                valid = descriptor[0] == 1 &&
                        Encoding.ASCII.GetString(descriptor, 1, 5) == "CD001";

                if (valid)
                    label = Encoding.ASCII.GetString(descriptor, 40, 32).Trim();
            }

            return new IsoInfo
            {
                Path = path,
                SizeBytes = info.Length,
                IsValidIso9660 = valid,
                IsHybrid = valid && hybrid,
                VolumeLabel = label
            };
        }

        /// <summary>Devuelve un mensaje de error si la configuración no es válida, o null.</summary>
        public static string? Validate(
            UsbBootOptions options,
            IsoInfo? iso,
            UsbDeviceInfo? device)
        {
            if (iso == null)
                return "Seleccioná una imagen ISO.";

            if (!iso.IsValidIso9660)
                return "El archivo seleccionado no parece una imagen ISO válida.";

            if (device != null && iso.SizeBytes > device.SizeBytes)
            {
                return $"La ISO ({FormatSize(iso.SizeBytes)}) no entra en la unidad ({device.SizeText}).";
            }

            // ISO híbrida: se escribe tal cual, las demás opciones no se usan.
            if (iso.IsHybrid)
                return null;

            if (options.FileSystem == UsbFileSystem.ExFat)
            {
                return "En la copia de archivos, exFAT no permite arrancar. " +
                       "Elegí FAT32 (UEFI o BIOS) o NTFS (solo BIOS).";
            }

            if (options.Target == BootTarget.Uefi && options.FileSystem != UsbFileSystem.Fat32)
            {
                return "El arranque UEFI requiere FAT32. " +
                       "Elegí FAT32 o cambiá el sistema de destino a BIOS.";
            }

            if (options.Scheme == PartitionScheme.Gpt && options.Target == BootTarget.BiosOrCsm)
                return "El arranque BIOS requiere el esquema de partición MBR.";

            string label = options.VolumeLabel.Trim();

            if (label.Length == 0)
                return "Ingresá una etiqueta para el volumen.";

            if (!Regex.IsMatch(label, @"^[A-Za-z0-9 _\-]+$"))
            {
                return "La etiqueta solo puede tener letras, números, espacios, guiones y guiones bajos.";
            }

            int maxLength = options.FileSystem == UsbFileSystem.Fat32 ? 11 : 32;

            if (label.Length > maxLength)
                return $"La etiqueta puede tener hasta {maxLength} caracteres con este sistema de archivos.";

            return null;
        }
    }

    /// <summary>Detecta las memorias USB conectadas (nunca el disco del sistema).</summary>
    public static class UsbDeviceScanner
    {
        private const ushort BusTypeUsb = 7;
        private const string StorageScope = @"\\.\root\Microsoft\Windows\Storage";

        public static List<UsbDeviceInfo> GetUsbDevices()
        {
            var result = new List<UsbDeviceInfo>();

            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
            string systemLetter = systemRoot.Substring(0, 1).ToUpperInvariant();

            var scope = new ManagementScope(StorageScope);
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery("SELECT * FROM MSFT_Disk"));

            using ManagementObjectCollection disks = searcher.Get();

            foreach (ManagementObject disk in disks)
            {
                using (disk)
                {
                    if (Convert.ToUInt16(disk["BusType"] ?? 0) != BusTypeUsb)
                        continue;

                    // Por seguridad, nunca se ofrece un disco del sistema o de arranque.
                    if (GetBool(disk, "IsSystem") || GetBool(disk, "IsBoot"))
                        continue;

                    long size = Convert.ToInt64(disk["Size"] ?? 0);

                    // Lectores sin tarjeta.
                    if (size <= 0)
                        continue;

                    int number = Convert.ToInt32(disk["Number"] ?? -1);

                    if (number < 0)
                        continue;

                    List<UsbVolumeInfo> volumes = GetVolumes(number);

                    if (volumes.Any(v => v.Letter.Equals(systemLetter, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    string model = (disk["FriendlyName"]?.ToString() ?? "").Trim();

                    result.Add(new UsbDeviceInfo
                    {
                        DiskNumber = number,
                        Model = model.Length == 0 ? "Unidad USB" : model,
                        SizeBytes = size,
                        Volumes = volumes
                    });
                }
            }

            return result.OrderBy(d => d.DiskNumber).ToList();
        }

        public static List<string> GetDriveLetters(int diskNumber)
        {
            var letters = new List<string>();

            var scope = new ManagementScope(StorageScope);
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery($"SELECT * FROM MSFT_Partition WHERE DiskNumber = {diskNumber}"));

            using ManagementObjectCollection partitions = searcher.Get();

            foreach (ManagementObject partition in partitions)
            {
                using (partition)
                {
                    object? value = partition["DriveLetter"];

                    char letter = value switch
                    {
                        char c => c,
                        ushort u => (char)u,
                        _ => '\0'
                    };

                    if (char.IsLetter(letter))
                        letters.Add(char.ToUpperInvariant(letter).ToString());
                }
            }

            return letters;
        }

        private static List<UsbVolumeInfo> GetVolumes(int diskNumber)
        {
            var volumes = new List<UsbVolumeInfo>();

            foreach (string letter in GetDriveLetters(diskNumber))
            {
                try
                {
                    var drive = new DriveInfo(letter);

                    if (drive.IsReady)
                    {
                        volumes.Add(new UsbVolumeInfo(
                            letter,
                            drive.VolumeLabel,
                            drive.DriveFormat,
                            drive.TotalSize));
                    }
                    else
                    {
                        volumes.Add(new UsbVolumeInfo(letter, "", "", 0));
                    }
                }
                catch (IOException)
                {
                    volumes.Add(new UsbVolumeInfo(letter, "", "", 0));
                }
                catch (UnauthorizedAccessException)
                {
                    volumes.Add(new UsbVolumeInfo(letter, "", "", 0));
                }
            }

            return volumes;
        }

        private static bool GetBool(ManagementBaseObject obj, string property)
        {
            return obj[property] is bool value && value;
        }
    }

    /// <summary>Crea el USB booteable: formatea y copia, o escribe la ISO directamente.</summary>
    public sealed class UsbBootCreator
    {
        // Windows no formatea FAT32 en volúmenes de más de 32 GB.
        private const long Fat32MaxVolumeBytes = 32L * 1024 * 1024 * 1024;
        private const int Fat32PartitionMb = 30 * 1024;

        // Un archivo FAT32 puede medir como máximo 4 GB - 1 byte.
        private const long Fat32MaxFileBytes = 4294967295L;

        private const int CopyProgressStart = 22;
        private const int CopyProgressSpan = 70;

        private readonly AppLogger logger;
        private readonly object logLock = new();
        private IProgress<UsbProgress>? progress;

        public UsbBootCreator(AppLogger logger)
        {
            this.logger = logger;
        }

        public async Task CreateAsync(
            UsbBootOptions options,
            IProgress<UsbProgress> progressReporter,
            CancellationToken ct)
        {
            progress = progressReporter;

            try
            {
                Report(0, "Verificando la unidad...", "Inicio de la creación del USB booteable.");

                if (!UsbBootableService.IsAdministrator())
                {
                    throw new InvalidOperationException(
                        "Se necesitan permisos de administrador para formatear y escribir en la unidad.");
                }

                IsoInfo iso = UsbBootableService.InspectIso(options.IsoPath);

                string? validation = UsbBootableService.Validate(options, iso, null);

                if (validation != null)
                    throw new InvalidOperationException(validation);

                if (iso.SizeBytes > options.ExpectedSizeBytes)
                {
                    throw new InvalidOperationException(
                        "La ISO es más grande que la capacidad de la unidad seleccionada.");
                }

                EnsureSafeTarget(options);
                ct.ThrowIfCancellationRequested();

                Report(
                    1,
                    "Preparando...",
                    $"ISO: {Path.GetFileName(iso.Path)} ({UsbBootableService.FormatSize(iso.SizeBytes)}), " +
                    $"etiqueta \"{iso.VolumeLabel}\".");

                if (iso.IsHybrid)
                    await WriteDirectAsync(options, iso, ct);
                else
                    await CopyFilesAsync(options, ct);

                Report(100, "Finalizado.", "USB booteable creado correctamente.");
            }
            catch (OperationCanceledException)
            {
                WriteLog("Operación cancelada por el usuario. La unidad puede haber quedado sin formato o incompleta.");
                progress?.Report(new UsbProgress(-1, "Cancelado.",
                    "Operación cancelada. La unidad puede haber quedado sin formato o incompleta."));
                throw;
            }
            catch (Exception ex)
            {
                WriteLog("ERROR: " + ex);
                progress?.Report(new UsbProgress(-1, "Error.", "ERROR: " + ex.Message));
                throw;
            }
        }

        // ---------------------------------------------------------------
        // Seguridad
        // ---------------------------------------------------------------

        /// <summary>Última verificación antes de borrar: el disco sigue siendo el mismo USB.</summary>
        private static void EnsureSafeTarget(UsbBootOptions options)
        {
            UsbDeviceInfo? device = UsbDeviceScanner
                .GetUsbDevices()
                .FirstOrDefault(d => d.DiskNumber == options.DiskNumber);

            if (device == null)
            {
                throw new InvalidOperationException(
                    "La unidad seleccionada ya no está disponible o no es un dispositivo USB seguro.");
            }

            if (device.SizeBytes != options.ExpectedSizeBytes ||
                !string.Equals(device.Model, options.ExpectedModel, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "La unidad cambió desde que se seleccionó. Actualizá la lista y volvé a intentarlo.");
            }
        }

        // ---------------------------------------------------------------
        // Modo 1: copia de archivos (ISO de Windows y similares)
        // ---------------------------------------------------------------

        private async Task CopyFilesAsync(UsbBootOptions options, CancellationToken ct)
        {
            string fsName = FileSystemName(options.FileSystem);

            Report(2, "Montando la imagen ISO...", "Montando la ISO para validar su estructura antes de modificar la memoria.");
            string isoLetter = await MountIsoAsync(options.IsoPath, ct);

            try
            {
                string isoRoot = isoLetter + @":\";

                // Validar el método de arranque antes de borrar las particiones de destino.
                if (options.Target == BootTarget.Uefi)
                {
                    string efiBoot = Path.Combine(isoRoot, "efi", "boot");
                    bool hasEfiLoader = Directory.Exists(efiBoot) &&
                        (File.Exists(Path.Combine(efiBoot, "bootx64.efi")) ||
                         File.Exists(Path.Combine(efiBoot, "bootia32.efi")) ||
                         File.Exists(Path.Combine(efiBoot, "bootaa64.efi")));

                    if (!hasEfiLoader)
                    {
                        throw new InvalidOperationException(
                            $"No encontré un cargador EFI estándar en el volumen {isoLetter}: (EFI\\BOOT\\BOOTX64.EFI, BOOTIA32.EFI o BOOTAA64.EFI). " +
                            "Se revisó la ISO montada y no se modificó el USB. Confirmá que la aplicación esté usando esta versión del servicio.");
                    }
                }
                else if (!File.Exists(Path.Combine(isoRoot, "boot", "bootsect.exe")))
                {
                    throw new InvalidOperationException(
                        "La ISO no contiene boot\\bootsect.exe. El motor propio no puede preparar el arranque BIOS para esta ISO no híbrida. " +
                        "Para Linux, usá una ISO híbrida compatible con escritura directa. No se modificó el USB.");
                }

                Report(10, "Formateando la unidad...",
                    $"Modo: copia de archivos · {options.Scheme.ToString().ToUpperInvariant()} · " +
                    $"{(options.Target == BootTarget.Uefi ? "UEFI" : "BIOS/CSM")} · {fsName}.");

                if (options.FileSystem == UsbFileSystem.Fat32 &&
                    options.ExpectedSizeBytes > Fat32MaxVolumeBytes)
                {
                    WriteLog(
                        "La unidad supera 32 GB: Windows solo formatea FAT32 hasta 32 GB. " +
                        "Se crea una partición de 30 GB y el resto queda sin asignar.");
                    progress?.Report(new UsbProgress(-1, null,
                        "La unidad supera 32 GB: se crea una partición FAT32 de 30 GB (límite de Windows)."));
                }

                await RunDiskpartAsync(BuildDiskpartScript(options), ct);

                Report(15, "Esperando que Windows monte la unidad...", "Unidad formateada.");
                string usbLetter = await WaitForVolumeAsync(options, ct);
                Report(18, null, $"Unidad lista en {usbLetter}:.");

                ct.ThrowIfCancellationRequested();
                string usbRoot = usbLetter + @":\";

                Report(CopyProgressStart, "Analizando la ISO...", $"ISO montada en {isoLetter}:.");

                List<FileInfo> files = await Task.Run(
                    () => new DirectoryInfo(isoRoot)
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .ToList(),
                    ct);

                string wimPath = Path.Combine(isoRoot, "sources", "install.wim");
                bool fat32 = options.FileSystem == UsbFileSystem.Fat32;

                bool splitWim = fat32 &&
                                File.Exists(wimPath) &&
                                new FileInfo(wimPath).Length > Fat32MaxFileBytes;

                bool IsWimToSplit(FileInfo file) =>
                    splitWim &&
                    string.Equals(file.FullName, wimPath, StringComparison.OrdinalIgnoreCase);

                if (fat32)
                {
                    foreach (FileInfo file in files)
                    {
                        if (!IsWimToSplit(file) && file.Length > Fat32MaxFileBytes)
                        {
                            throw new InvalidOperationException(
                                $"El archivo \"{file.Name}\" ({UsbBootableService.FormatSize(file.Length)}) " +
                                "supera el límite de 4 GB de FAT32. Usá NTFS con destino BIOS o una ISO distinta.");
                        }
                    }
                }

                long totalBytes = Math.Max(1, files.Sum(f => f.Length));
                long copiedBytes = 0;
                long lastReportTick = 0;

                void ReportCopy()
                {
                    long now = Environment.TickCount64;

                    if (now - lastReportTick < 250)
                        return;

                    lastReportTick = now;

                    Report(
                        CopyProgressStart + (int)(copiedBytes * CopyProgressSpan / totalBytes),
                        $"Copiando archivos... {UsbBootableService.FormatSize(copiedBytes)} " +
                        $"de {UsbBootableService.FormatSize(totalBytes)}");
                }

                Report(CopyProgressStart, "Copiando archivos...", $"Copiando {files.Count} archivos a {usbLetter}:.");

                await Task.Run(() =>
                {
                    foreach (DirectoryInfo directory in new DirectoryInfo(isoRoot)
                                 .EnumerateDirectories("*", SearchOption.AllDirectories))
                    {
                        Directory.CreateDirectory(
                            Path.Combine(usbRoot, Path.GetRelativePath(isoRoot, directory.FullName)));
                    }

                    byte[] buffer = new byte[4 * 1024 * 1024];

                    foreach (FileInfo file in files)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (IsWimToSplit(file))
                            continue;

                        string destination = Path.Combine(
                            usbRoot,
                            Path.GetRelativePath(isoRoot, file.FullName));

                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                        using var input = new FileStream(
                            file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1,
                            FileOptions.SequentialScan);

                        using var output = new FileStream(
                            destination, FileMode.Create, FileAccess.Write, FileShare.None, 1);

                        int read;

                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, read);
                            copiedBytes += read;
                            ct.ThrowIfCancellationRequested();
                            ReportCopy();
                        }
                    }
                }, ct);

                Report(
                    CopyProgressStart + (int)(copiedBytes * CopyProgressSpan / totalBytes),
                    "Archivos copiados.",
                    "Archivos copiados.");

                if (splitWim)
                    await SplitWimAsync(wimPath, usbRoot, copiedBytes, totalBytes, ct);

                if (options.Scheme == PartitionScheme.Mbr && options.Target == BootTarget.BiosOrCsm)
                    await WriteBootSectorAsync(isoRoot, usbLetter, ct);

                Report(98, "Finalizando...", "Escritura terminada.");
            }
            finally
            {
                await DismountIsoAsync(options.IsoPath);
            }
        }

        private async Task SplitWimAsync(
            string wimPath,
            string usbRoot,
            long copiedBytes,
            long totalBytes,
            CancellationToken ct)
        {
            long wimLength = new FileInfo(wimPath).Length;

            Report(
                -1,
                "Dividiendo install.wim...",
                "install.wim supera 4 GB: se divide en partes .swm para FAT32 (puede tardar varios minutos).");

            string sources = Path.Combine(usbRoot, "sources");
            Directory.CreateDirectory(sources);

            string arguments =
                $"/Split-Image /ImageFile:\"{wimPath}\" " +
                $"/SWMFile:\"{Path.Combine(sources, "install.swm")}\" /FileSize:3800";

            (int code, string output) = await RunProcessAsync(
                "dism.exe",
                arguments,
                chunk =>
                {
                    double percent = ParseLastPercent(chunk);

                    if (percent < 0)
                        return;

                    long done = copiedBytes + (long)(wimLength * percent / 100.0);

                    Report(
                        CopyProgressStart + (int)(done * CopyProgressSpan / totalBytes),
                        $"Dividiendo install.wim... {percent:0}%");
                },
                ct,
                killOnCancel: true);

            if (code != 0)
            {
                throw new InvalidOperationException(
                    $"DISM no pudo dividir install.wim (código {code}). {Tail(output)}");
            }

            Report(CopyProgressStart + CopyProgressSpan, "install.wim dividido.", "install.wim dividido correctamente.");
        }

        private async Task WriteBootSectorAsync(string isoRoot, string usbLetter, CancellationToken ct)
        {
            string bootsect = Path.Combine(isoRoot, "boot", "bootsect.exe");

            if (!File.Exists(bootsect))
            {
                throw new InvalidOperationException(
                    "La ISO no incluye bootsect.exe. No se puede garantizar el arranque BIOS con este método.");
            }

            Report(94, "Escribiendo el sector de arranque...", "Escribiendo el sector de arranque BIOS (bootsect).");

            (int code, string output) = await RunProcessAsync(
                bootsect,
                $"/nt60 {usbLetter}: /force /mbr",
                null,
                ct,
                killOnCancel: true);

            if (code != 0)
            {
                throw new InvalidOperationException(
                    $"bootsect no pudo escribir el sector de arranque (código {code}). {Tail(output)}");
            }
        }

        private static string BuildDiskpartScript(UsbBootOptions options)
        {
            string label = options.VolumeLabel.Trim();

            if (options.FileSystem == UsbFileSystem.Fat32)
                label = label.ToUpperInvariant();

            string fs = options.FileSystem switch
            {
                UsbFileSystem.Fat32 => "fat32",
                UsbFileSystem.Ntfs => "ntfs",
                _ => "exfat"
            };

            string unit = options.ClusterSizeBytes > 0
                ? $" unit={options.ClusterSizeBytes}"
                : "";

            var script = new StringBuilder();

            script.AppendLine($"select disk {options.DiskNumber}");
            script.AppendLine("clean");
            script.AppendLine(options.Scheme == PartitionScheme.Gpt ? "convert gpt" : "convert mbr");

            if (options.FileSystem == UsbFileSystem.Fat32 &&
                options.ExpectedSizeBytes > Fat32MaxVolumeBytes)
            {
                script.AppendLine($"create partition primary size={Fat32PartitionMb}");
            }
            else
            {
                script.AppendLine("create partition primary");
            }

            script.AppendLine($"format fs={fs} label=\"{label}\" quick{unit}");

            if (options.Scheme == PartitionScheme.Mbr)
                script.AppendLine("active");

            script.AppendLine("assign");
            script.AppendLine("exit");

            return script.ToString();
        }

        private async Task<string> WaitForVolumeAsync(UsbBootOptions options, CancellationToken ct)
        {
            string expected = FileSystemName(options.FileSystem);

            for (int attempt = 0; attempt < 40; attempt++)
            {
                try
                {
                    foreach (string letter in UsbDeviceScanner.GetDriveLetters(options.DiskNumber))
                    {
                        var drive = new DriveInfo(letter);

                        if (!drive.IsReady)
                            continue;

                        if (!drive.DriveFormat.Equals(expected, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"La unidad quedó formateada como {drive.DriveFormat} en lugar de {expected}.");
                        }

                        return letter;
                    }
                }
                catch (IOException)
                {
                    // El volumen todavía no está listo.
                }
                catch (ManagementException)
                {
                    // Windows todavía está actualizando los datos del disco.
                }

                await Task.Delay(500, ct);
            }

            throw new InvalidOperationException(
                "Windows no asignó una letra a la unidad formateada. Desconectala, volvé a conectarla e intentá de nuevo.");
        }

        // ---------------------------------------------------------------
        // Modo 2: escritura directa (ISO híbrida, típico de Linux)
        // ---------------------------------------------------------------

        private async Task WriteDirectAsync(UsbBootOptions options, IsoInfo iso, CancellationToken ct)
        {
            Report(
                2,
                "Limpiando la unidad...",
                "Modo: escritura directa (ISO híbrida). Se escribe la imagen tal cual, con sus propias particiones.");

            string script = $"select disk {options.DiskNumber}\r\nclean\r\nexit\r\n";
            await RunDiskpartAsync(script, ct);

            ct.ThrowIfCancellationRequested();

            Report(8, "Escribiendo la imagen...", "Escribiendo la ISO en la unidad...");

            await Task.Run(() => WriteIsoToDisk(options.DiskNumber, iso, ct), ct);

            Report(98, "Finalizando...", "Escritura terminada.");
        }

        private void WriteIsoToDisk(int diskNumber, IsoInfo iso, CancellationToken ct)
        {
            string devicePath = $@"\\.\PhysicalDrive{diskNumber}";

            using SafeFileHandle handle = NativeMethods.CreateFile(
                devicePath,
                NativeMethods.GenericRead | NativeMethods.GenericWrite,
                NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
                IntPtr.Zero,
                NativeMethods.OpenExisting,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();

                throw new IOException(
                    $"No se pudo abrir {devicePath} para escritura: {new Win32Exception(error).Message}");
            }

            // Mejor esfuerzo: permite escribir en todo el disco.
            NativeMethods.DeviceIoControl(
                handle, NativeMethods.FsctlAllowExtendedDasdIo,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

            // Los discos exigen escrituras alineadas al tamaño de sector (512 o 4096).
            const int alignment = 4096;
            byte[] buffer = new byte[1024 * 1024];

            long written = 0;
            long lastReportTick = 0;

            using var input = new FileStream(
                iso.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1,
                FileOptions.SequentialScan);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                int filled = 0;

                while (filled < buffer.Length)
                {
                    int read = input.Read(buffer, filled, buffer.Length - filled);

                    if (read == 0)
                        break;

                    filled += read;
                }

                if (filled == 0)
                    break;

                int toWrite = (filled + alignment - 1) / alignment * alignment;

                if (toWrite > filled)
                    Array.Clear(buffer, filled, toWrite - filled);

                if (!NativeMethods.WriteFile(handle, buffer, (uint)toWrite, out uint bytesWritten, IntPtr.Zero) ||
                    bytesWritten != (uint)toWrite)
                {
                    int error = Marshal.GetLastWin32Error();

                    throw new IOException(
                        $"Error al escribir en la unidad (posición {UsbBootableService.FormatSize(written)}): " +
                        new Win32Exception(error).Message);
                }

                written += filled;

                long now = Environment.TickCount64;

                if (now - lastReportTick >= 250)
                {
                    lastReportTick = now;

                    int percent = 8 + (int)(written * 90 / Math.Max(1, iso.SizeBytes));

                    Report(
                        Math.Min(percent, 98),
                        $"Escribiendo la imagen... {UsbBootableService.FormatSize(written)} " +
                        $"de {UsbBootableService.FormatSize(iso.SizeBytes)}");
                }

                if (filled < buffer.Length)
                    break;
            }

            NativeMethods.FlushFileBuffers(handle);

            // Mejor esfuerzo: Windows vuelve a leer las particiones de la unidad.
            NativeMethods.DeviceIoControl(
                handle, NativeMethods.IoctlDiskUpdateProperties,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
        }

        // ---------------------------------------------------------------
        // Procesos externos (diskpart, dism, bootsect, PowerShell)
        // ---------------------------------------------------------------

        private async Task RunDiskpartAsync(string script, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string file = Path.Combine(
                Path.GetTempPath(),
                "nexosupporttoolkit_diskpart_" + Guid.NewGuid().ToString("N") + ".txt");

            File.WriteAllText(file, script, Encoding.ASCII);

            try
            {
                WriteLog("diskpart, script:" + Environment.NewLine + script);

                (int code, string output) = await RunProcessAsync(
                    "diskpart.exe",
                    $"/s \"{file}\"",
                    null,
                    ct,
                    killOnCancel: false);

                WriteLog("diskpart, salida:" + Environment.NewLine + output.Trim());

                if (code != 0)
                {
                    throw new InvalidOperationException(
                        $"diskpart no pudo preparar la unidad (código {code}). {Tail(output)}");
                }
            }
            finally
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }

        private static async Task<string> MountIsoAsync(string isoPath, CancellationToken ct)
        {
            string escaped = isoPath.Replace("'", "''");

            // Obtener letras desde el disco virtual concreto de esta ISO. No consultar
            // volúmenes globales: eso podría devolver la unidad del sistema (por ejemplo C:).
            string script =
                "$ProgressPreference = 'SilentlyContinue'; " +
                "$ErrorActionPreference = 'Stop'; " +
                $"Mount-DiskImage -ImagePath '{escaped}' -ErrorAction Stop | Out-Null; " +
                "$letters = @(); " +
                "for ($i = 0; $i -lt 30; $i++) { " +
                $"  $image = Get-DiskImage -ImagePath '{escaped}' -ErrorAction Stop; " +
                "  $volumes = @($image | Get-Volume -ErrorAction Stop); " +
                "  foreach ($volume in $volumes) { " +
                "    if ($volume.DriveLetter) { $letters += [string]$volume.DriveLetter }; " +
                "  }; " +
                "  $letters = @($letters | Select-Object -Unique); " +
                "  if ($letters.Count -gt 0) { break }; " +
                "  Start-Sleep -Milliseconds 500; " +
                "}; " +
                "if ($letters.Count -eq 0) { throw 'Windows montó la ISO, pero no asignó letra a sus propias particiones.' }; " +
                "foreach ($letter in $letters) { Write-Output ('NEXO_ISO_LETTER=' + $letter) }";

            string output = await RunPowerShellAsync(script, ct);

            string[] reportedLetters = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("NEXO_ISO_LETTER=", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.Substring("NEXO_ISO_LETTER=".Length).Trim().ToUpperInvariant())
                .Where(letter => letter.Length == 1 && char.IsLetter(letter[0]))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
            string systemLetter = systemRoot.Substring(0, 1).ToUpperInvariant();
            string[] letters = reportedLetters
                .Where(letter => !letter.Equals(systemLetter, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (letters.Length == 0)
                throw new InvalidOperationException(
                    "Windows no devolvió una letra válida para la ISO, o solo devolvió la unidad del sistema. " +
                    "No se usará C: como ubicación de la ISO.");

            foreach (string letter in letters)
            {
                string root = letter + @":\";
                if (File.Exists(Path.Combine(root, "efi", "boot", "bootx64.efi")) ||
                    File.Exists(Path.Combine(root, "efi", "boot", "bootia32.efi")) ||
                    File.Exists(Path.Combine(root, "efi", "boot", "bootaa64.efi")))
                    return letter;
            }

            foreach (string letter in letters)
            {
                string root = letter + @":\";
                if (File.Exists(Path.Combine(root, "sources", "boot.wim")) ||
                    File.Exists(Path.Combine(root, "setup.exe")))
                    return letter;
            }

            return letters[0];
        }
        private async Task DismountIsoAsync(string isoPath)
        {
            try
            {
                string escaped = isoPath.Replace("'", "''");

                await RunPowerShellAsync(
                    $"$ErrorActionPreference = 'Stop'; Dismount-DiskImage -ImagePath '{escaped}' | Out-Null",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                WriteLog("No se pudo desmontar la ISO: " + ex.Message);
            }
        }

        private static async Task<string> RunPowerShellAsync(string script, CancellationToken ct)
        {
            // PowerShell 5.1 puede serializar errores no controlados como CLIXML
            // cuando stderr está redirigido. Capturamos la excepción dentro del
            // propio proceso para devolver un mensaje legible a la interfaz.
            string wrappedScript =
                "$ErrorActionPreference = 'Stop'; " +
                "try { " + script + " } " +
                "catch { " +
                "  [Console]::Error.WriteLine('NEXO_PS_ERROR=' + $_.Exception.Message); " +
                "  exit 1 " +
                "}";

            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrappedScript));

            (int code, string output) = await RunProcessAsync(
                "powershell.exe",
                "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                null,
                ct,
                killOnCancel: true);

            if (code != 0)
            {
                string message = output
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .LastOrDefault(line => line.StartsWith("NEXO_PS_ERROR=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("NEXO_PS_ERROR=".Length)
                    ?? Tail(output);

                throw new InvalidOperationException("PowerShell devolvió un error: " + message);
            }

            return output.Trim();
        }

        private static readonly Encoding OemEncoding = CreateOemEncoding();

        private static Encoding CreateOemEncoding()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            }
            catch (Exception)
            {
                return Encoding.UTF8;
            }
        }

        private static async Task<(int ExitCode, string Output)> RunProcessAsync(
            string fileName,
            string arguments,
            Action<string>? onChunk,
            CancellationToken ct,
            bool killOnCancel)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = OemEncoding,
                StandardErrorEncoding = OemEncoding
            };

            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();

            process.Start();

            using CancellationTokenRegistration registration = killOnCancel
                ? ct.Register(() =>
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                })
                : default;

            async Task Pump(StreamReader reader)
            {
                char[] buffer = new char[512];
                int read;

                while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    string chunk = new string(buffer, 0, read);

                    lock (output)
                    {
                        output.Append(chunk);
                    }

                    onChunk?.Invoke(chunk);
                }
            }

            await Task.WhenAll(Pump(process.StandardOutput), Pump(process.StandardError));
            await process.WaitForExitAsync();

            if (killOnCancel)
                ct.ThrowIfCancellationRequested();

            return (process.ExitCode, output.ToString());
        }

        // ---------------------------------------------------------------
        // Utilidades
        // ---------------------------------------------------------------

        private static string FileSystemName(UsbFileSystem fileSystem) => fileSystem switch
        {
            UsbFileSystem.Fat32 => "FAT32",
            UsbFileSystem.Ntfs => "NTFS",
            _ => "exFAT"
        };

        private static double ParseLastPercent(string text)
        {
            MatchCollection matches = Regex.Matches(text, @"(\d{1,3}(?:[.,]\d+)?)\s*%");

            if (matches.Count == 0)
                return -1;

            string value = matches[matches.Count - 1].Groups[1].Value.Replace(',', '.');

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)
                ? Math.Clamp(percent, 0, 100)
                : -1;
        }

        private static string Tail(string text)
        {
            string[] lines = text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();

            return string.Join(" ", lines.Skip(Math.Max(0, lines.Length - 3)));
        }

        private void Report(int percent, string? status, string? logLine = null)
        {
            if (logLine != null)
                WriteLog(logLine);

            progress?.Report(new UsbProgress(percent, status, logLine));
        }

        private void WriteLog(string line)
        {
            lock (logLock)
            {
                try
                {
                    logger.Write("[USB] " + line);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static class NativeMethods
        {
            public const uint GenericRead = 0x80000000;
            public const uint GenericWrite = 0x40000000;
            public const uint FileShareRead = 0x00000001;
            public const uint FileShareWrite = 0x00000002;
            public const uint OpenExisting = 3;

            public const uint FsctlAllowExtendedDasdIo = 0x00090083;
            public const uint IoctlDiskUpdateProperties = 0x00070140;

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern SafeFileHandle CreateFile(
                string fileName,
                uint desiredAccess,
                uint shareMode,
                IntPtr securityAttributes,
                uint creationDisposition,
                uint flagsAndAttributes,
                IntPtr templateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool WriteFile(
                SafeFileHandle file,
                byte[] buffer,
                uint bytesToWrite,
                out uint bytesWritten,
                IntPtr overlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FlushFileBuffers(SafeFileHandle file);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(
                SafeFileHandle device,
                uint controlCode,
                IntPtr inBuffer,
                uint inBufferSize,
                IntPtr outBuffer,
                uint outBufferSize,
                out uint bytesReturned,
                IntPtr overlapped);
        }
    }
}
