
using System;
using System.Collections.Generic;
using System.IO;

namespace NexoSupportToolKit.Services
{
    public class CleanerService
    {
        private readonly AppLogger logger;

        public CleanerService(AppLogger appLogger)
        {
            logger = appLogger;
        }

        // LIMPIEZA AUTOMÁTICA POR ANTIGÜEDAD.

        // Compatibilidad con código anterior.
        public int CleanOldFiles(int retentionMonths = 3)
        {
            throw new InvalidOperationException(
                "Debés indicar la carpeta que querés limpiar.");
        }

        // Limpieza automática indicando los meses de retención.
        public int CleanOldFiles(
            string folderPath,
            int retentionMonths = 3)
        {
            string frequency = retentionMonths switch
            {
                1 => "1month",
                3 => "3months",
                6 => "6months",
                12 => "12months",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(retentionMonths),
                    "La retención debe ser de 1, 3, 6 o 12 meses.")
            };

            return CleanOldFiles(folderPath, frequency);
        }

        // Limpieza automática por frecuencia.
        // Solo admite 1, 3, 6 o 12 meses.
        public int CleanOldFiles(
            string folderPath,
            string frequency)
        {
            string root = ValidateFolder(folderPath);
            DateTime cutoff = GetCutoff(frequency);

            int deleted = 0;
            int skipped = 0;

            var folders = new Stack<string>();
            folders.Push(root);

            logger.Write($"Inicio de limpieza automática: {root}");
            logger.Write($"Frecuencia: {frequency}");
            logger.Write($"Fecha límite: {cutoff}");

            while (folders.Count > 0)
            {
                string current = folders.Pop();

                try
                {
                    foreach (string file in Directory.GetFiles(current))
                    {
                        try
                        {
                            FileAttributes attributes =
                                File.GetAttributes(file);

                            if ((attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                skipped++;
                                logger.Write($"Enlace omitido: {file}");
                                continue;
                            }

                            if (File.GetLastWriteTime(file) < cutoff)
                            {
                                DeleteFileSafely(file);
                                deleted++;

                                logger.Write(
                                    $"Archivo antiguo eliminado: {file}");
                            }
                        }
                        catch (Exception ex)
                        {
                            skipped++;

                            logger.Write(
                                $"Error con archivo {file}: {ex.Message}");
                        }
                    }

                    foreach (string subfolder in
                             Directory.GetDirectories(current))
                    {
                        try
                        {
                            FileAttributes attributes =
                                File.GetAttributes(subfolder);

                            if ((attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                skipped++;

                                logger.Write(
                                    $"Carpeta enlazada omitida: {subfolder}");

                                continue;
                            }

                            folders.Push(subfolder);
                        }
                        catch (Exception ex)
                        {
                            skipped++;

                            logger.Write(
                                $"Error con carpeta {subfolder}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    skipped++;

                    logger.Write(
                        $"Error recorriendo {current}: {ex.Message}");
                }
            }

            logger.Write(
                $"Fin de limpieza automática. " +
                $"Archivos eliminados: {deleted}. Omitidos: {skipped}.");

            return deleted;
        }

        // LIMPIEZA MANUAL COMPLETA.
        // Elimina el contenido, pero conserva la carpeta raíz.
        public (int Deleted, int Skipped) CleanAllContents(
            string folderPath)
        {
            string root = ValidateFolder(folderPath);

            int deleted = 0;
            int skipped = 0;

            var folders = new Stack<(string Path, bool Visited)>();
            folders.Push((root, false));

            logger.Write(
                $"Inicio de limpieza manual completa: {root}");

            while (folders.Count > 0)
            {
                var item = folders.Pop();
                string current = item.Path;

                try
                {
                    if (!item.Visited)
                    {
                        string[] files = Directory.GetFiles(current);
                        string[] subfolders =
                            Directory.GetDirectories(current);

                        logger.Write(
                            $"Revisando {current}: " +
                            $"{files.Length} archivos, " +
                            $"{subfolders.Length} subcarpetas.");

                        foreach (string file in files)
                        {
                            try
                            {
                                FileAttributes attributes =
                                    File.GetAttributes(file);

                                if ((attributes & FileAttributes.ReparsePoint) != 0)
                                {
                                    skipped++;
                                    logger.Write($"Enlace omitido: {file}");
                                    continue;
                                }

                                DeleteFileSafely(file);
                                deleted++;

                                logger.Write($"Archivo eliminado: {file}");
                            }
                            catch (Exception ex)
                            {
                                skipped++;

                                logger.Write(
                                    $"No se pudo eliminar {file}: {ex.Message}");
                            }
                        }

                        folders.Push((current, true));

                        foreach (string subfolder in subfolders)
                        {
                            try
                            {
                                FileAttributes attributes =
                                    File.GetAttributes(subfolder);

                                if ((attributes & FileAttributes.ReparsePoint) != 0)
                                {
                                    skipped++;

                                    logger.Write(
                                        $"Carpeta enlazada omitida: {subfolder}");

                                    continue;
                                }

                                folders.Push((subfolder, false));
                            }
                            catch (Exception ex)
                            {
                                skipped++;

                                logger.Write(
                                    $"No se pudo revisar {subfolder}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        if (SamePath(current, root))
                            continue;

                        try
                        {
                            Directory.Delete(current, false);
                            deleted++;

                            logger.Write(
                                $"Subcarpeta eliminada: {current}");
                        }
                        catch (DirectoryNotFoundException)
                        {
                            // La carpeta ya no existe.
                        }
                        catch (Exception ex)
                        {
                            skipped++;

                            logger.Write(
                                $"No se pudo eliminar la carpeta " +
                                $"{current}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    skipped++;

                    logger.Write(
                        $"Error procesando {current}: {ex.Message}");
                }
            }

            logger.Write(
                $"Fin de limpieza manual. " +
                $"Elementos eliminados: {deleted}. Omitidos: {skipped}.");

            return (deleted, skipped);
        }

        // LIMPIEZA DE ARCHIVOS TEMPORALES.
        public (int Deleted, int Skipped) CleanTemporaryFiles()
        {
            int deleted = 0;
            int skipped = 0;

            string userTemp = Path.GetFullPath(Path.GetTempPath());

            string windowsFolder = Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

            if (string.IsNullOrWhiteSpace(windowsFolder))
            {
                throw new InvalidOperationException(
                    "No se pudo localizar la carpeta de Windows.");
            }

            string windowsTemp = Path.GetFullPath(
                Path.Combine(windowsFolder, "Temp"));

            string[] tempFolders =
            {
                userTemp,
                windowsTemp
            };

            logger.Write("Inicio de limpieza de temporales.");

            foreach (string folder in tempFolders)
            {
                try
                {
                    string root = Path.GetFullPath(folder);
                    string driveRoot = Path.GetPathRoot(root) ?? "";

                    if (SamePath(root, driveRoot))
                    {
                        skipped++;

                        logger.Write(
                            $"Ruta temporal rechazada: {root}");

                        continue;
                    }

                    if (!Directory.Exists(root))
                    {
                        logger.Write(
                            $"Carpeta temporal inexistente: {root}");

                        continue;
                    }

                    CleanDirectoryContents(
                        root,
                        ref deleted,
                        ref skipped);
                }
                catch (Exception ex)
                {
                    skipped++;

                    logger.Write(
                        $"Error limpiando temporales {folder}: {ex.Message}");
                }
            }

            logger.Write(
                $"Fin de limpieza de temporales. " +
                $"Elementos eliminados: {deleted}. Omitidos: {skipped}.");

            return (deleted, skipped);
        }

        private void CleanDirectoryContents(
            string root,
            ref int deleted,
            ref int skipped)
        {
            var folders = new Stack<(string Path, bool Visited)>();
            folders.Push((root, false));

            while (folders.Count > 0)
            {
                var item = folders.Pop();
                string current = item.Path;

                if (!item.Visited)
                {
                    try
                    {
                        foreach (string file in
                                 Directory.GetFiles(current))
                        {
                            try
                            {
                                FileAttributes attributes =
                                    File.GetAttributes(file);

                                if ((attributes & FileAttributes.ReparsePoint) != 0)
                                {
                                    skipped++;
                                    continue;
                                }

                                DeleteFileSafely(file);
                                deleted++;

                                logger.Write(
                                    $"Temporal eliminado: {file}");
                            }
                            catch (Exception ex)
                            {
                                skipped++;

                                logger.Write(
                                    $"Temporal omitido {file}: {ex.Message}");
                            }
                        }

                        folders.Push((current, true));

                        foreach (string subfolder in
                                 Directory.GetDirectories(current))
                        {
                            try
                            {
                                FileAttributes attributes =
                                    File.GetAttributes(subfolder);

                                if ((attributes & FileAttributes.ReparsePoint) != 0)
                                {
                                    skipped++;
                                    continue;
                                }

                                folders.Push((subfolder, false));
                            }
                            catch (Exception ex)
                            {
                                skipped++;

                                logger.Write(
                                    $"Error revisando {subfolder}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        skipped++;

                        logger.Write(
                            $"Error accediendo a {current}: {ex.Message}");
                    }
                }
                else
                {
                    if (SamePath(current, root))
                        continue;

                    try
                    {
                        Directory.Delete(current, false);
                        deleted++;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // La carpeta ya no existe.
                    }
                    catch (Exception ex)
                    {
                        skipped++;

                        logger.Write(
                            $"No se pudo eliminar carpeta temporal " +
                            $"{current}: {ex.Message}");
                    }
                }
            }
        }

        // Elimina archivos sin seguir enlaces simbólicos.
        private static void DeleteFileSafely(string file)
        {
            FileAttributes attributes = File.GetAttributes(file);

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "Se rechazó eliminar un enlace.");
            }

            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(
                    file,
                    attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(file);
        }

        // Valida rutas y bloquea ubicaciones críticas del sistema.
        private static string ValidateFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException(
                    "La ruta está vacía.");
            }

            string root = Path.GetFullPath(folderPath);

            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException(
                    "La carpeta no existe: " + root);
            }

            string driveRoot = Path.GetPathRoot(root) ?? "";

            if (SamePath(root, driveRoot))
            {
                throw new InvalidOperationException(
                    "No se permite limpiar la raíz de una unidad.");
            }

            string windowsFolder = Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

            string programFiles = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);

            string programFilesX86 = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);

            string programData = Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);

            if (IsInside(root, windowsFolder) ||
                IsInside(root, programFiles) ||
                IsInside(root, programFilesX86) ||
                IsInside(root, programData))
            {
                throw new InvalidOperationException(
                    "No se permite limpiar carpetas del sistema.");
            }

            return root;
        }

        // Solo admite las cuatro frecuencias mensuales.
        private static DateTime GetCutoff(string frequency)
        {
            if (string.IsNullOrWhiteSpace(frequency))
            {
                throw new ArgumentException(
                    "Debés indicar una frecuencia.");
            }

            DateTime now = DateTime.Now;

            return frequency.Trim().ToLowerInvariant() switch
            {
                "1month" => now.AddMonths(-1),
                "3months" => now.AddMonths(-3),
                "6months" => now.AddMonths(-6),
                "12months" => now.AddMonths(-12),

                _ => throw new ArgumentException(
                    "Frecuencia no válida: " + frequency)
            };
        }

        private static bool SamePath(
            string first,
            string second)
        {
            string a = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(first));

            string b = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(second));

            return string.Equals(
                a,
                b,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInside(
            string path,
            string protectedPath)
        {
            if (string.IsNullOrWhiteSpace(protectedPath))
                return false;

            string fullPath = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(path));

            string fullProtected =
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(protectedPath));

            return fullPath.Equals(
                       fullProtected,
                       StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(
                       fullProtected + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(
                       fullProtected + Path.AltDirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
