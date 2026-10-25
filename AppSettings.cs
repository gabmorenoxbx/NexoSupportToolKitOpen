using System;
using System.IO;
using System.Text.Json;

namespace NexoSupportToolKit.Models
{
    public class AppSettings
    {
        public string FolderPath { get; set; } = @"C:\NexoSupportToolKit";
        public int RetentionMonths { get; set; } = 3;
        public string LastAlertDate { get; set; } = "";

        private static readonly string AppFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "NexoSupportToolKit");

        private static readonly string SettingsFile =
            Path.Combine(AppFolder, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);

                    return JsonSerializer.Deserialize<AppSettings>(json)
                           ?? new AppSettings();
                }
            }
            catch
            {
                // Se utilizan valores predeterminados.
            }

            return new AppSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(AppFolder);

            string json = JsonSerializer.Serialize(
                this,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(SettingsFile, json);
        }
    }
}