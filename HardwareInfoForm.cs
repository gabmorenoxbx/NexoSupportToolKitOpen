
using System;
using System.Drawing;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;

namespace NexoSupportToolKit
{
    public class HardwareInfoForm : Form
    {
        private readonly TextBox txtHardware = new();
        private readonly Label lblStatus = new();
        private readonly Button btnRefresh = new();
        private readonly Button btnCopy = new();

        private static readonly Color BackgroundColor = Color.FromArgb(15, 18, 25);
        private static readonly Color CardColor = Color.FromArgb(27, 32, 43);
        private static readonly Color InputColor = Color.FromArgb(36, 42, 55);
        private static readonly Color TextColor = Color.FromArgb(235, 239, 246);
        private static readonly Color MutedColor = Color.FromArgb(153, 164, 181);
        private static readonly Color AccentColor = Color.FromArgb(63, 132, 245);

        public HardwareInfoForm()
        {
            BuildInterface();
        }

        private void BuildInterface()
        {
            Text = "Nexo Support Toolkit - Componentes del equipo";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(700, 550);
            Size = new Size(800, 680);
            Font = new Font("Segoe UI", 9F);
            BackColor = BackgroundColor;
            ForeColor = TextColor;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 75,
                BackColor = BackgroundColor,
                Padding = new Padding(22, 14, 22, 0)
            };

            var lblTitle = new Label
            {
                Text = "Componentes del equipo",
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = TextColor
            };

            var lblDescription = new Label
            {
                Text = "Información detectada por Windows mediante WMI.",
                Dock = DockStyle.Top,
                Height = 25,
                ForeColor = MutedColor
            };

            header.Controls.Add(lblDescription);
            header.Controls.Add(lblTitle);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                BackColor = BackgroundColor,
                Padding = new Padding(22, 10, 22, 10)
            };

            var footerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = BackgroundColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            ConfigureButton(btnRefresh, "Actualizar");
            ConfigureButton(btnCopy, "Copiar informe");

            btnRefresh.Click += async (_, _) => await LoadHardwareAsync();

            btnCopy.Click += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(txtHardware.Text))
                {
                    try
                    {
                        Clipboard.SetText(txtHardware.Text);

                        MessageBox.Show(
                            this,
                            "El informe se copió al portapapeles.",
                            "Componentes",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            this,
                            "No se pudo copiar el informe.\n\n" + ex.Message,
                            "Componentes",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            };

            lblStatus.Dock = DockStyle.Fill;
            lblStatus.ForeColor = MutedColor;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Text = "Listo para consultar.";
            lblStatus.Padding = new Padding(12, 0, 0, 0);

            footerLayout.Controls.Add(btnRefresh, 0, 0);
            footerLayout.Controls.Add(btnCopy, 1, 0);
            footerLayout.Controls.Add(lblStatus, 2, 0);
            footer.Controls.Add(footerLayout);

            txtHardware.Dock = DockStyle.Fill;
            txtHardware.Multiline = true;
            txtHardware.ReadOnly = true;
            txtHardware.ScrollBars = ScrollBars.Both;
            txtHardware.WordWrap = false;
            txtHardware.Font = new Font("Consolas", 9.5F);
            txtHardware.BackColor = InputColor;
            txtHardware.ForeColor = TextColor;
            txtHardware.BorderStyle = BorderStyle.FixedSingle;
            txtHardware.Margin = Padding.Empty;

            var reportPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackgroundColor,
                Padding = new Padding(22, 7, 22, 10)
            };

            reportPanel.Controls.Add(txtHardware);

            Controls.Add(reportPanel);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private void ConfigureButton(Button button, string text)
        {
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 0, 10, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = AccentColor;
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(43, 112, 220);
        }

        public async Task LoadHardwareAsync()
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            btnRefresh.Enabled = false;
            lblStatus.Text = "Consultando componentes...";
            txtHardware.Text = "Obteniendo información del equipo...";

            try
            {
                string report = await Task.Run(BuildHardwareReport);

                if (IsDisposed)
                    return;

                txtHardware.Text = report;
                txtHardware.SelectionStart = 0;
                txtHardware.SelectionLength = 0;
                lblStatus.Text = "Actualizado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    txtHardware.Text =
                        "No se pudo consultar el hardware.\r\n\r\n" + ex.Message;

                    lblStatus.Text = "Error al consultar.";
                }
            }
            finally
            {
                if (!IsDisposed)
                    btnRefresh.Enabled = true;
            }
        }

        private static string BuildHardwareReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine("INFORME DE HARDWARE");
            sb.AppendLine("===================");
            sb.AppendLine("Fecha: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            sb.AppendLine("Windows: " + Environment.OSVersion);
            sb.AppendLine("Sistema de 64 bits: " + Environment.Is64BitOperatingSystem);
            sb.AppendLine();

            AppendSection(
                sb,
                "EQUIPO",
                "SELECT Manufacturer, Model, SystemFamily, TotalPhysicalMemory FROM Win32_ComputerSystem",
                obj =>
                {
                    AddProperty(sb, "Fabricante", obj, "Manufacturer");
                    AddProperty(sb, "Modelo", obj, "Model");
                    AddProperty(sb, "Familia", obj, "SystemFamily");

                    ulong ram = GetUInt64(obj, "TotalPhysicalMemory");

                    if (ram > 0)
                        sb.AppendLine("RAM total: " + FormatBytes(ram));
                });

            AppendSection(
                sb,
                "PROCESADOR",
                "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor",
                obj =>
                {
                    AddProperty(sb, "Modelo", obj, "Name");
                    AddProperty(sb, "Fabricante", obj, "Manufacturer");
                    AddProperty(sb, "Núcleos", obj, "NumberOfCores");
                    AddProperty(sb, "Procesadores lógicos", obj, "NumberOfLogicalProcessors");
                    AddProperty(sb, "Frecuencia actual informada por Windows", obj, "CurrentClockSpeed", " MHz");
                    AddProperty(sb, "Frecuencia máxima informada", obj, "MaxClockSpeed", " MHz");
                });

            AppendSection(
                sb,
                "PLACA MADRE",
                "SELECT Manufacturer, Product, Model, Version FROM Win32_BaseBoard",
                obj =>
                {
                    AddProperty(sb, "Fabricante", obj, "Manufacturer");
                    AddProperty(sb, "Producto", obj, "Product");
                    AddProperty(sb, "Modelo", obj, "Model");
                    AddProperty(sb, "Versión", obj, "Version");
                });

            AppendSection(
                sb,
                "MEMORIA RAM INSTALADA",
                "SELECT DeviceLocator, Manufacturer, Capacity, Speed, ConfiguredClockSpeed, PartNumber FROM Win32_PhysicalMemory",
                obj =>
                {
                    AddProperty(sb, "Ranura", obj, "DeviceLocator");
                    AddProperty(sb, "Fabricante", obj, "Manufacturer");

                    ulong capacity = GetUInt64(obj, "Capacity");

                    if (capacity > 0)
                        sb.AppendLine("Capacidad: " + FormatBytes(capacity));

                    AddProperty(sb, "Velocidad nominal", obj, "Speed", " MHz");
                    AddProperty(sb, "Velocidad configurada", obj, "ConfiguredClockSpeed", " MHz");
                    AddProperty(sb, "Número de parte", obj, "PartNumber");
                    sb.AppendLine();
                });

            AppendSection(
                sb,
                "TARJETA GRÁFICA",
                "SELECT Name, AdapterCompatibility, VideoProcessor, AdapterRAM, DriverVersion FROM Win32_VideoController",
                obj =>
                {
                    AddProperty(sb, "Modelo", obj, "Name");
                    AddProperty(sb, "Fabricante", obj, "AdapterCompatibility");
                    AddProperty(sb, "Procesador gráfico", obj, "VideoProcessor");

                    ulong vram = GetUInt64(obj, "AdapterRAM");

                    if (vram > 0)
                        sb.AppendLine("Memoria gráfica informada: " + FormatBytes(vram));

                    AddProperty(sb, "Versión del controlador", obj, "DriverVersion");
                    sb.AppendLine();
                });

            AppendSection(
                sb,
                "UNIDADES FÍSICAS",
                "SELECT Model, Manufacturer, InterfaceType, MediaType, Size, FirmwareRevision FROM Win32_DiskDrive",
                obj =>
                {
                    AddProperty(sb, "Modelo", obj, "Model");
                    AddProperty(sb, "Fabricante", obj, "Manufacturer");
                    AddProperty(sb, "Interfaz", obj, "InterfaceType");
                    AddProperty(sb, "Tipo informado por Windows", obj, "MediaType");

                    ulong size = GetUInt64(obj, "Size");

                    if (size > 0)
                        sb.AppendLine("Capacidad: " + FormatBytes(size));

                    AddProperty(sb, "Firmware", obj, "FirmwareRevision");
                    sb.AppendLine();
                });

            AppendSection(
                sb,
                "BIOS",
                "SELECT Manufacturer, SMBIOSBIOSVersion, Version, ReleaseDate FROM Win32_BIOS",
                obj =>
                {
                    AddProperty(sb, "Fabricante", obj, "Manufacturer");
                    AddProperty(sb, "Versión SMBIOS", obj, "SMBIOSBIOSVersion");
                    AddProperty(sb, "Versión", obj, "Version");

                    string release = GetString(obj, "ReleaseDate");

                    if (!string.IsNullOrWhiteSpace(release))
                        sb.AppendLine("Fecha de lanzamiento informada: " + release);
                });

            AppendLiveClockSensors(sb);

            sb.AppendLine();
            sb.AppendLine("NOTAS");
            sb.AppendLine("-----");
            sb.AppendLine("- Algunos fabricantes no informan todos los datos.");
            sb.AppendLine("- La velocidad de RAM configurada se informa en MHz; algunos equipos muestran la frecuencia efectiva de forma distinta según DDR.");
            sb.AppendLine("- Los relojes en tiempo real de CPU/GPU dependen de que el hardware y el controlador expongan sensores compatibles.");
            sb.AppendLine("- La memoria gráfica informada puede ser aproximada.");
            sb.AppendLine("- Windows no siempre identifica de forma concluyente si un disco es HDD o SSD.");

            return sb.ToString();
        }

        private static void AppendLiveClockSensors(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("FRECUENCIAS DE RELOJ EN TIEMPO REAL");
            sb.AppendLine("-----------------------------------");

            Computer? computer = null;
            try
            {
                computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true
                };

                computer.Open();
                bool found = false;

                foreach (IHardware hardware in computer.Hardware)
                    found |= AppendClockSensors(sb, hardware);

                if (!found)
                    sb.AppendLine("El controlador no expuso sensores de frecuencia en esta consulta.");
            }
            catch (Exception ex)
            {
                sb.AppendLine("Sensores en tiempo real no disponibles: " + ex.Message);
            }
            finally
            {
                try { computer?.Close(); }
                catch { /* El informe ya contiene los datos que se pudieron leer. */ }
            }
        }

        private static bool AppendClockSensors(StringBuilder sb, IHardware hardware)
        {
            bool found = false;

            try
            {
                hardware.Update();
                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Clock || !sensor.Value.HasValue)
                        continue;

                    sb.AppendLine($"{hardware.Name} - {sensor.Name}: {sensor.Value.Value:0.0} MHz");
                    found = true;
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{hardware.Name}: no se pudieron leer los sensores ({ex.Message})");
            }

            foreach (IHardware child in hardware.SubHardware)
                found |= AppendClockSensors(sb, child);

            return found;
        }

        private static void AppendSection(
            StringBuilder sb,
            string title,
            string query,
            Action<ManagementObject> writeProperties)
        {
            sb.AppendLine(title);
            sb.AppendLine(new string('-', title.Length));

            try
            {
                using var searcher = new ManagementObjectSearcher(query);
                using ManagementObjectCollection results = searcher.Get();

                bool found = false;

                foreach (ManagementObject obj in results)
                {
                    using (obj)
                    {
                        found = true;

                        try
                        {
                            writeProperties(obj);
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine("No se pudieron leer todos los datos: " + ex.Message);
                        }
                    }
                }

                if (!found)
                    sb.AppendLine("No se encontró información.");
            }
            catch (Exception ex)
            {
                sb.AppendLine("No disponible: " + ex.Message);
            }

            sb.AppendLine();
        }

        private static void AddProperty(
            StringBuilder sb,
            string label,
            ManagementBaseObject obj,
            string property,
            string suffix = "")
        {
            string value = GetString(obj, property);

            if (!string.IsNullOrWhiteSpace(value))
                sb.AppendLine(label + ": " + value + suffix);
        }

        private static string GetString(ManagementBaseObject obj, string property)
        {
            try
            {
                return Convert.ToString(obj[property])?.Trim() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static ulong GetUInt64(ManagementBaseObject obj, string property)
        {
            try
            {
                object? value = obj[property];

                if (value == null)
                    return 0;

                return Convert.ToUInt64(value);
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatBytes(ulong bytes)
        {
            double gb = bytes / 1024d / 1024d / 1024d;
            return $"{gb:0.00} GB ({bytes:N0} bytes)";
        }
    }
}
