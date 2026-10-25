
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace NexoSupportToolKit.Services
{
    public class TaskSchedulerService
    {
        private const string CleanupTaskName =
            "Nexo Support Toolkit - Limpieza";

        private const string StartupTaskName =
            "Nexo Support Toolkit - Inicio";

        public void ConfigureTasks(string frequency = "3months")
        {
            frequency = (frequency ?? "")
                .Trim()
                .ToLowerInvariant();

            int intervalMonths = frequency switch
            {
                "1month" => 1,
                "3months" => 3,
                "6months" => 6,
                "12months" => 12,
                _ => throw new ArgumentException(
                    "Frecuencia no válida. Usá 1month, 3months, 6months o 12months.",
                    nameof(frequency))
            };

            string exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException(
                    "No se pudo determinar la ruta del ejecutable.");

            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException(
                    "No se encontró el ejecutable de Nexo Support Toolkit.",
                    exePath);
            }

            string appFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NexoSupportToolKit");

            Directory.CreateDirectory(appFolder);

            string scriptPath = Path.Combine(
                appFolder,
                "ConfigureTasks.ps1");

            string escapedExe = exePath.Replace("'", "''");

            string script = $@"
$ErrorActionPreference = 'Stop'

try {{
    $exe = '{escapedExe}'
    $frequency = '{frequency}'
    $interval = {intervalMonths}

    # El comando se ejecutará el día 1, a las 08:00,
    # cada N meses, según la frecuencia seleccionada.
    $cleanupCommand = '""' + $exe + '"" --clean ' + $frequency

    & schtasks.exe /Create `
        /TN '{CleanupTaskName}' `
        /TR $cleanupCommand `
        /SC MONTHLY `
        /MO $interval `
        /D 1 `
        /ST 08:00 `
        /F

    if ($LASTEXITCODE -ne 0) {{
        throw ""No se pudo registrar la tarea de limpieza. Código: $LASTEXITCODE""
    }}

    # Inicia la interfaz cuando el usuario inicia sesión.
    $startupCommand = '""' + $exe + '""'

    & schtasks.exe /Create `
        /TN '{StartupTaskName}' `
        /TR $startupCommand `
        /SC ONLOGON `
        /F

    if ($LASTEXITCODE -ne 0) {{
        throw ""No se pudo registrar la tarea de inicio. Código: $LASTEXITCODE""
    }}

    exit 0
}}
catch {{
    $message = $_ | Out-String
    $logPath = Join-Path $env:LOCALAPPDATA 'NexoSupportToolKit\TaskSchedulerError.txt'
    [System.IO.File]::WriteAllText($logPath, $message)
    Write-Error $message
    exit 1
}}
";

            File.WriteAllText(scriptPath, script);

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);

            try
            {
                using Process? process = Process.Start(startInfo);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "No se pudo iniciar PowerShell como administrador.");
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string errorPath = Path.Combine(
                        appFolder,
                        "TaskSchedulerError.txt");

                    string details = File.Exists(errorPath)
                        ? File.ReadAllText(errorPath)
                        : "No se generó un registro de error.";

                    throw new InvalidOperationException(
                        "No se pudieron registrar las tareas programadas.\n\n" +
                        details);
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new InvalidOperationException(
                    "Se canceló la solicitud de permisos de administrador.");
            }
        }
    }
}
