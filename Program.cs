using System;
using NexoSupportToolKit.Models;
using NexoSupportToolKit.Services;
using System.Windows.Forms;
using System.Text;
using System.Text.Json;

namespace NexoSupportToolKit
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Este modo se ejecuta en una segunda instancia elevada por UAC.
            // No muestra ventanas adicionales: realiza la operación solicitada.
            if (args.Length >= 2 && args[0].Equals("--create-bootable-usb", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string json = Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));
                    UsbBootOptions options = JsonSerializer.Deserialize<UsbBootOptions>(json)
                        ?? throw new InvalidOperationException("No se pudieron leer las opciones de creación USB.");
                    ApplicationConfiguration.Initialize();
                    Application.Run(new UsbCreationElevatedForm(options));
                }
                catch (Exception ex)
                {
                    try
                    {
                        MessageBox.Show("No se pudo iniciar la creación del USB.\n\n" + ex.Message,
                            "Nexo Support Toolkit - USB", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch { }
                    Environment.ExitCode = 1;
                }
                return;
            }

            // Las limpiezas automáticas no muestran ventanas.
            if (args.Length > 0 &&
                args[0].Equals(
                    "--clean",
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    AppSettings settings = AppSettings.Load();
                    AppLogger logger = new AppLogger();
                    CleanerService cleaner = new CleanerService(logger);

                    string frequency;

                    if (args.Length > 1)
                    {
                        frequency = args[1];
                    }
                    else
                    {
                        frequency = settings.RetentionMonths + "months";
                    }

                    int deleted = cleaner.CleanOldFiles(
                        settings.FolderPath,
                        frequency);

                    logger.Write(
                        "Limpieza automática completada. " +
                        "Frecuencia: " + frequency +
                        ". Eliminados: " + deleted);
                }
                catch (Exception ex)
                {
                    try
                    {
                        new AppLogger().Write(
                            "Error en limpieza automática: " + ex);
                    }
                    catch
                    {
                        // No mostrar ventanas durante la limpieza.
                    }

                    Environment.ExitCode = 1;
                }

                return;
            }

            // Inicio normal de la aplicación.
            ApplicationConfiguration.Initialize();

            // Abrir la ventana principal.
            Application.Run(new Form1());
        }
    }
}