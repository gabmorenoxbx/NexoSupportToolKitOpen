using System;
using System.IO;

namespace NexoSupportToolKit.Services
{
    public class AppLogger
    {
        private readonly string logFolder;

        public AppLogger()
        {
            logFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NexoSupportToolKit",
                "Logs");

            Directory.CreateDirectory(logFolder);
        }

        public void Write(string message)
        {
            string file = Path.Combine(
                logFolder,
                $"Cleaner_{DateTime.Now:yyyy-MM-dd}.log");

            string line =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}"
                + Environment.NewLine;

            File.AppendAllText(file, line);
        }
    }
}