
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using NexoSupportToolKit.Models;
using NexoSupportToolKit.Services;

namespace NexoSupportToolKit
{
    public partial class Form1 : Form
    {
        private readonly TaskSchedulerService taskScheduler = new();

        private AppSettings settings;
        private AppLogger logger;
        private CleanerService cleaner;

        private TextBox txtFolder = null!;
        private SegmentedSelector cmbMonths = null!;
        private Label lblStatus = null!;
        private Label lblFrequencyInfo = null!;
        private NotifyIcon trayIcon = null!;
        private System.Windows.Forms.Timer timer = null!;

        private Button btnSave = null!;
        private Button btnClean = null!;
        private Button btnTemp = null!;

        private Button btnCleaningNav = null!;
        private Button btnOptimizationNav = null!;
        private Button btnDiagnosticsNav = null!;
        private Button btnUsbBootableNav = null!;
        private Button btnOperatingSystemsNav = null!;

        private Panel sidebar = null!;
        private Panel contentPanel = null!;
        private Panel cleaningView = null!;
        private Panel usbBootableView = null!;
        private Panel operatingSystemsView = null!;
        private WindowsOptimizationForm optimizationView = null!;

        // Controles y estado del creador de USB booteables.
        private ComboBox cmbUsbDevice = null!;
        private TextBox txtIsoPath = null!;
        private ComboBox cmbPartition = null!;
        private ComboBox cmbTarget = null!;
        private ComboBox cmbFileSystem = null!;
        private ComboBox cmbClusterSize = null!;
        private TextBox txtVolumeLabel = null!;
        private ProgressBar usbProgress = null!;
        private Label lblUsbProgress = null!;
        private ListBox usbLog = null!;
        private Button btnRefreshUsb = null!;
        private Button btnCreateUsb = null!;
        private Button btnCancelUsb = null!;
        private CancellationTokenSource? usbCancellation;
        private bool usbCreationRunning;

        private bool exitRequested;
        private bool savingConfiguration;

        private readonly string frequencyFile = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "NexoSupportToolKit",
            "frequency.txt");

        private readonly Color backgroundColor = Color.FromArgb(15, 18, 25);
        private readonly Color sidebarColor = Color.FromArgb(20, 24, 33);
        private readonly Color cardColor = Color.FromArgb(27, 32, 43);
        private readonly Color inputColor = Color.FromArgb(36, 42, 55);
        private readonly Color textColor = Color.FromArgb(235, 239, 246);
        private readonly Color mutedColor = Color.FromArgb(153, 164, 181);
        private readonly Color accentColor = Color.FromArgb(63, 132, 245);
        private readonly Color successColor = Color.FromArgb(50, 190, 130);
        private readonly Color dangerColor = Color.FromArgb(220, 80, 90);

        public Form1()
        {
            InitializeComponent();

            Icon = LoadAppIcon();

            settings = AppSettings.Load();
            logger = new AppLogger();
            cleaner = new CleanerService(logger);

            Directory.CreateDirectory(
                Path.GetDirectoryName(frequencyFile)!);

            BuildInterface();
            ConfigureTray();
            ConfigureTimer();
            CheckUpcomingCleanup();
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "logo.ico");

                if (File.Exists(path))
                    return new Icon(path);

                Icon? exeIcon =
                    Icon.ExtractAssociatedIcon(Application.ExecutablePath);

                if (exeIcon != null)
                    return exeIcon;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "No se pudo cargar el icono: " + ex.Message);
            }

            return SystemIcons.Application;
        }

        private void BuildInterface()
        {
            Text = "Nexo Support Toolkit";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 620);
            Size = new Size(1120, 740);
            BackColor = backgroundColor;
            ForeColor = textColor;
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Controls.Clear();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = backgroundColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            root.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 220));

            root.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            root.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            sidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = sidebarColor,
                Padding = new Padding(18, 24, 18, 18)
            };

            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = backgroundColor,
                Padding = Padding.Empty
            };

            root.Controls.Add(sidebar, 0, 0);
            root.Controls.Add(contentPanel, 1, 0);
            Controls.Add(root);

            BuildSidebar();
            BuildMainContent();
            ShowSection("cleaning");
        }

        private void BuildSidebar()
        {
            sidebar.Controls.Clear();

            var logo = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = 105,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = sidebarColor,
                Margin = Padding.Empty
            };

            string logoPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "logo.jpg");

            if (File.Exists(logoPath))
            {
                try
                {
                    using Image original = Image.FromFile(logoPath);
                    logo.Image = new Bitmap(original);
                }
                catch (Exception ex)
                {
                    logger.Write(
                        "No se pudo cargar logo.jpg: " + ex.Message);
                }
            }

            var brand = new Label
            {
                Text = "NEXO\nSUPPORT TOOLKIT",
                Dock = DockStyle.Top,
                Height = 65,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = textColor,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var brandLine = new Panel
            {
                Dock = DockStyle.Top,
                Height = 2,
                BackColor = accentColor
            };

            var navTitle = new Label
            {
                Text = "MENÚ PRINCIPAL",
                Dock = DockStyle.Top,
                Height = 38,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 286,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = sidebarColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            btnCleaningNav = CreateNavButton("   Limpieza");
            btnOptimizationNav =
                CreateNavButton("   Optimización de Windows");
            btnDiagnosticsNav =
                CreateNavButton("   Diagnóstico del sistema");
            btnUsbBootableNav =
                CreateNavButton("   USB Booteable");
            btnOperatingSystemsNav = CreateNavButton("   Sistemas operativos");

            btnCleaningNav.Click += (_, _) => ShowSection("cleaning");
            btnOptimizationNav.Click += (_, _) => ShowSection("optimization");
            btnDiagnosticsNav.Click += (_, _) => ShowSection("diagnostics");
            btnUsbBootableNav.Click += (_, _) => ShowSection("usb");
            btnOperatingSystemsNav.Click += (_, _) => ShowSection("operating-systems");

            nav.Controls.Add(btnCleaningNav);
            nav.Controls.Add(btnOptimizationNav);
            nav.Controls.Add(btnDiagnosticsNav);
            nav.Controls.Add(btnUsbBootableNav);
            nav.Controls.Add(btnOperatingSystemsNav);

            var sidebarFooter = new Label
            {
                Text = "Limpieza y mantenimiento\nVersión de escritorio",
                Dock = DockStyle.Bottom,
                Height = 52,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.BottomLeft
            };

            var githubLink = new LinkLabel
            {
                Text = "GitHub: gabmorenoxbx",
                Dock = DockStyle.Bottom,
                Height = 22,
                ForeColor = Color.FromArgb(100, 160, 255),
                LinkColor = Color.FromArgb(100, 160, 255),
                ActiveLinkColor = Color.White,
                VisitedLinkColor = Color.FromArgb(100, 160, 255),
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                LinkBehavior = LinkBehavior.HoverUnderline
            };

            githubLink.LinkClicked += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/gabmorenoxbx",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    logger.Write(
                        "No se pudo abrir GitHub: " + ex.Message);
                }
            };

            sidebar.Controls.Add(sidebarFooter);
            sidebar.Controls.Add(githubLink);
            sidebar.Controls.Add(nav);
            sidebar.Controls.Add(navTitle);
            sidebar.Controls.Add(brandLine);
            sidebar.Controls.Add(brand);
            sidebar.Controls.Add(logo);
        }

        private Button CreateNavButton(string text)
        {
            var button = new Button
            {
                Text = text,
                Width = 184,
                Height = 46,
                Margin = new Padding(0, 0, 0, 7),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = sidebarColor,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(35, 48, 70);

            return button;
        }

        private void ShowSection(string section)
        {
            bool showCleaning = section == "cleaning";
            bool showOptimization = section == "optimization";
            bool showDiagnostics = section == "diagnostics";
            bool showUsb = section == "usb";
            bool showOperatingSystems = section == "operating-systems";

            cleaningView.Visible = showCleaning;
            optimizationView.Visible = showOptimization || showDiagnostics;
            usbBootableView.Visible = showUsb;
            operatingSystemsView.Visible = showOperatingSystems;

            if (showOptimization)
                optimizationView.ShowSection("optimization");
            else if (showDiagnostics)
                optimizationView.ShowSection("diagnostics");

            SetNavButtonState(btnCleaningNav, showCleaning);
            SetNavButtonState(btnOptimizationNav, showOptimization);
            SetNavButtonState(btnDiagnosticsNav, showDiagnostics);
            SetNavButtonState(btnUsbBootableNav, showUsb);
            SetNavButtonState(btnOperatingSystemsNav, showOperatingSystems);

            if (showCleaning)
            {
                cleaningView.BringToFront();
                contentPanel.Controls.SetChildIndex(cleaningView, 0);
                txtFolder.Focus();
            }
            else if (showOperatingSystems)
            {
                operatingSystemsView.BringToFront();
                contentPanel.Controls.SetChildIndex(operatingSystemsView, 0);
            }
            else if (showUsb)
            {
                usbBootableView.BringToFront();
                contentPanel.Controls.SetChildIndex(usbBootableView, 0);

                if (cmbUsbDevice != null &&
                    cmbUsbDevice.Items.Count <= 1 &&
                    !usbCreationRunning)
                {
                    _ = RefreshUsbDevicesAsync();
                }
            }
            else
            {
                optimizationView.BringToFront();
                contentPanel.Controls.SetChildIndex(optimizationView, 0);
            }
        }

        private void SetNavButtonState(Button button, bool selected)
        {
            button.BackColor = selected
                ? Color.FromArgb(35, 48, 70)
                : sidebarColor;

            button.ForeColor = selected ? Color.White : textColor;
        }

        private void BuildMainContent()
        {
            contentPanel.Controls.Clear();

            cleaningView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = backgroundColor,
                Padding = new Padding(30, 26, 30, 20),
                AutoScroll = true
            };

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = backgroundColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            main.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 4; i++)
                main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 78,
                BackColor = backgroundColor,
                Margin = new Padding(0, 0, 0, 18)
            };

            header.Controls.Add(new Label
            {
                Text = "Limpieza",
                Location = new Point(0, 0),
                AutoSize = true,
                Font = new Font("Segoe UI", 23F, FontStyle.Bold),
                ForeColor = textColor
            });

            header.Controls.Add(new Label
            {
                Text = "Administrá la limpieza automática de archivos y temporales.",
                Location = new Point(2, 43),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = mutedColor
            });

            var folderCard = CreateCard(
                "CARPETA A LIMPIAR",
                "Elegí la carpeta donde se encuentran los archivos que querés depurar.");

            var folderLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = cardColor,
                Margin = new Padding(0, 15, 0, 0)
            };

            folderLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            folderLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 120));

            txtFolder = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = settings.FolderPath ?? "",
                BackColor = inputColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F),
                Margin = new Padding(0, 0, 10, 0)
            };

            var btnBrowse = CreateButton("Examinar", accentColor);
            btnBrowse.Dock = DockStyle.Top;
            btnBrowse.Height = 36;
            btnBrowse.Click += BtnBrowse_Click;

            folderLayout.Controls.Add(txtFolder, 0, 0);
            folderLayout.Controls.Add(btnBrowse, 1, 0);
            folderCard.Controls.Add(folderLayout);

            var retentionCard = CreateCard(
                "ANTIGÜEDAD DE LOS ARCHIVOS",
                "Elegí cuánto tiempo deben conservarse los archivos. " +
                "La limpieza manual elimina todo el contenido de la carpeta.");

            cmbMonths = new SegmentedSelector(
                inputColor,
                accentColor,
                textColor)
            {
                Dock = DockStyle.Top,
                BackColor = cardColor,
                Margin = new Padding(0, 15, 0, 0)
            };

            // Solo se permiten estas cuatro frecuencias.
            cmbMonths.SetItems(
                "1 mes",
                "3 meses",
                "6 meses",
                "12 meses");

            cmbMonths.SelectedItem = LoadFrequencyLabel();

            if (cmbMonths.SelectedIndex < 0)
                cmbMonths.SelectedIndex = 1;

            lblFrequencyInfo = new Label
            {
                Text = "",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 12, 0, 0)
            };

            cmbMonths.SelectionChanged += (_, _) => UpdateRetentionInfo();
            UpdateRetentionInfo();

            retentionCard.Controls.Add(lblFrequencyInfo);
            retentionCard.Controls.Add(cmbMonths);

            var actionCard = CreateCard(
                "ACCIONES",
                "Guardá la configuración automática o ejecutá una limpieza manual.");

            var actionLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = cardColor,
                Margin = new Padding(0, 15, 0, 0)
            };

            btnSave = CreateButton("Guardar configuración", accentColor);
            btnSave.Width = 190;
            btnSave.Height = 42;
            btnSave.Click += BtnSave_Click;

            btnClean = CreateButton("Limpiar ahora", successColor);
            btnClean.Width = 150;
            btnClean.Height = 42;
            btnClean.Click += BtnClean_Click;

            btnTemp = CreateButton("Limpiar temporales", inputColor);
            btnTemp.Width = 180;
            btnTemp.Height = 42;
            btnTemp.ForeColor = textColor;
            btnTemp.Click += BtnTemp_Click;

            actionLayout.Controls.Add(btnSave);
            actionLayout.Controls.Add(btnClean);
            actionLayout.Controls.Add(btnTemp);
            actionCard.Controls.Add(actionLayout);

            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = sidebarColor,
                Padding = new Padding(14, 0, 14, 0),
                Margin = new Padding(0, 4, 0, 0)
            };

            lblStatus = new Label
            {
                Text = "Listo.",
                Dock = DockStyle.Fill,
                ForeColor = mutedColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5F)
            };

            statusPanel.Controls.Add(lblStatus);

            folderCard.Margin = new Padding(0, 0, 0, 16);
            retentionCard.Margin = new Padding(0, 0, 0, 16);
            actionCard.Margin = new Padding(0, 0, 0, 16);

            main.Controls.Add(header, 0, 0);
            main.Controls.Add(folderCard, 0, 1);
            main.Controls.Add(retentionCard, 0, 2);
            main.Controls.Add(actionCard, 0, 3);

            cleaningView.Controls.Add(main);
            cleaningView.Controls.Add(statusPanel);
            contentPanel.Controls.Add(cleaningView);

            optimizationView = new WindowsOptimizationForm
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill,
                Visible = false
            };

            optimizationView.ShowSection("optimization");
            contentPanel.Controls.Add(optimizationView);
            optimizationView.Show();
            optimizationView.Visible = false;

            usbBootableView = BuildUsbBootableView();
            contentPanel.Controls.Add(usbBootableView);
            usbBootableView.Visible = false;

            operatingSystemsView = BuildOperatingSystemsView();
            contentPanel.Controls.Add(operatingSystemsView);
            operatingSystemsView.Visible = false;
        }


        private Panel BuildOperatingSystemsView()
        {
            var view = new Panel { Dock = DockStyle.Fill, BackColor = backgroundColor, Padding = new Padding(30, 26, 30, 20), AutoScroll = true };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = backgroundColor };
            flow.Controls.Add(new Label { Text = "Sistemas operativos", AutoSize = true, ForeColor = textColor, Font = new Font("Segoe UI", 23F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) });
            flow.Controls.Add(new Label { Text = "Enlaces oficiales de descarga e instalación.", AutoSize = true, ForeColor = mutedColor, Margin = new Padding(0, 0, 0, 18) });
            AddOperatingSystemLinks(flow);
            flow.Controls.Add(new Label
            {
                Text = "La arquitectura varía según el equipo. Preparar un USB puede borrar su contenido.",
                AutoSize = true,
                MaximumSize = new Size(760, 0),
                ForeColor = mutedColor,
                Margin = new Padding(0, 18, 0, 0)
            });
            view.Controls.Add(flow);
            return view;
        }
        private void AddOperatingSystemLinks(FlowLayoutPanel flow)
        {
            var systems = new (string Name, string Url)[]
            {
                ("Windows 11", "https://www.microsoft.com/software-download/windows11"),
                ("Windows 10 (soporte finalizado)", "https://www.microsoft.com/software-download/windows10ISO"),
                ("Ubuntu Desktop", "https://ubuntu.com/download/desktop"),
                ("Linux Mint", "https://linuxmint.com/download.php"),
                ("Fedora Workstation", "https://fedoraproject.org/workstation/download/"),
                ("Debian", "https://www.debian.org/distrib/"),
                ("macOS (Mac compatibles)", "https://support.apple.com/es-es/102662")
            };
            foreach (var item in systems)
            {
                var link = new LinkLabel
                {
                    Text = item.Name + "  ↗",
                    AutoSize = true,
                    LinkColor = Color.FromArgb(100, 160, 255),
                    ActiveLinkColor = Color.White,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    Margin = new Padding(0, 8, 0, 8),
                    Tag = item.Url
                };
                link.LinkArea = new LinkArea(0, link.Text.Length);
                link.LinkClicked += OpenOperatingSystemLink;
                flow.Controls.Add(link);
            }
        }
        private void OpenOperatingSystemLink(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is not LinkLabel link || link.Tag is not string url) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { logger.Write("No se pudo abrir el enlace: " + ex.Message); }
        }
        private Panel BuildUsbBootableView()
        {
            var view = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = backgroundColor,
                Padding = new Padding(26, 22, 26, 18),
                AutoScroll = true
            };

            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = backgroundColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 3; i++)
                main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ---------------------------------------------------------
            // ENCABEZADO
            // ---------------------------------------------------------
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = backgroundColor,
                Margin = new Padding(0, 0, 0, 14)
            };

            header.Controls.Add(new Label
            {
                Text = "USB Booteable",
                Location = new Point(0, 0),
                AutoSize = true,
                Font = new Font("Segoe UI", 23F, FontStyle.Bold),
                ForeColor = textColor
            });

            header.Controls.Add(new Label
            {
                Text = "Creá una unidad USB de arranque desde una imagen ISO.",
                Location = new Point(2, 42),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = mutedColor
            });

            // ---------------------------------------------------------
            // TARJETA PRINCIPAL: DISTRIBUCIÓN COMPACTA TIPO RUFUS
            // ---------------------------------------------------------
            var configCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = cardColor,
                Padding = new Padding(18),
                Margin = new Padding(0, 0, 0, 14)
            };

            configCard.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            configCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            configCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            configCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var configTitle = new Label
            {
                Text = "PROPIEDADES DE LA UNIDAD",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 5)
            };

            var configDescription = new Label
            {
                Text = "Seleccioná el dispositivo, la imagen y el esquema de arranque.",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 15)
            };

            // Contenedor de campos. Las filas se definen explícitamente
            // para evitar el apilado incorrecto de controles Dock.Top.
            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = cardColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            fields.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 50));

            fields.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 50));

            for (int i = 0; i < 5; i++)
                fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Crea un campo con etiqueta y control.
            Panel MakeField(string caption, Control control)
            {
                var field = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    BackColor = cardColor,
                    Margin = new Padding(0, 0, 12, 12),
                    Padding = Padding.Empty
                };

                var label = new Label
                {
                    Text = caption,
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ForeColor = mutedColor,
                    Font = new Font("Segoe UI", 9F),
                    Margin = new Padding(0, 0, 0, 5)
                };

                control.Dock = DockStyle.Top;
                control.Margin = Padding.Empty;
                control.Height = 32;

                field.Controls.Add(control);
                field.Controls.Add(label);

                return field;
            }

            // Dispositivo USB + actualizar.
            var deviceRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = cardColor,
                Margin = new Padding(0, 0, 0, 5)
            };

            deviceRow.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            deviceRow.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 118));

            cmbUsbDevice = new ComboBox
            {
                Name = "cmbUsbDevice",
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputColor,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                IntegralHeight = false,
                DropDownHeight = 240
            };

            cmbUsbDevice.Items.Add(
                "Presioná Actualizar para buscar unidades USB");

            cmbUsbDevice.SelectedIndex = 0;

            btnRefreshUsb = CreateButton("Actualizar", inputColor);
            btnRefreshUsb.ForeColor = textColor;
            btnRefreshUsb.Dock = DockStyle.Top;
            btnRefreshUsb.Height = 32;
            btnRefreshUsb.Margin = new Padding(8, 0, 0, 0);
            btnRefreshUsb.Click += async (_, _) =>
                await RefreshUsbDevicesAsync();

            deviceRow.Controls.Add(cmbUsbDevice, 0, 0);
            deviceRow.Controls.Add(btnRefreshUsb, 1, 0);

            // Selector de ISO + botón Elegir.
            var isoRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = cardColor,
                Margin = new Padding(0, 0, 0, 5)
            };

            isoRow.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            isoRow.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 118));

            txtIsoPath = new TextBox
            {
                Name = "txtIsoPath",
                Dock = DockStyle.Top,
                ReadOnly = true,
                BackColor = inputColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F),
                PlaceholderText = "Seleccioná una imagen ISO..."
            };

            var btnBrowseIso = CreateButton("Elegir ISO", accentColor);
            btnBrowseIso.Dock = DockStyle.Top;
            btnBrowseIso.Height = 32;
            btnBrowseIso.Margin = new Padding(8, 0, 0, 0);

            btnBrowseIso.Click += (_, _) =>
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Seleccionar imagen ISO",
                    Filter = "Imágenes ISO (*.iso)|*.iso",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    txtIsoPath.Text = dialog.FileName;
                    AddUsbLog("ISO seleccionada: " + dialog.FileName);
                }
            };

            isoRow.Controls.Add(txtIsoPath, 0, 0);
            isoRow.Controls.Add(btnBrowseIso, 1, 0);

            // Selectores de configuración.
            ComboBox MakeCombo(string name, params string[] items)
            {
                var combo = new ComboBox
                {
                    Name = name,
                    Dock = DockStyle.Top,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = inputColor,
                    ForeColor = textColor,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9.5F),
                    IntegralHeight = false
                };

                combo.Items.AddRange(items);

                if (combo.Items.Count > 0)
                    combo.SelectedIndex = 0;

                return combo;
            }

            cmbPartition = MakeCombo(
                "cmbPartition",
                "GPT",
                "MBR");

            cmbTarget = MakeCombo(
                "cmbTarget",
                "UEFI (no CSM)",
                "BIOS o UEFI-CSM");

            cmbFileSystem = MakeCombo(
                "cmbFileSystem",
                "FAT32",
                "NTFS");

            cmbClusterSize = MakeCombo(
                "cmbClusterSize",
                "Predeterminado",
                "512 bytes",
                "1 KB",
                "2 KB",
                "4 KB",
                "8 KB",
                "16 KB",
                "32 KB",
                "64 KB");

            txtVolumeLabel = new TextBox
            {
                Name = "txtVolumeLabel",
                Text = "NEXO",
                Dock = DockStyle.Top,
                BackColor = inputColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F),
                MaxLength = 11
            };

            // Primera fila: dispositivo USB.
            var deviceLabel = new Label
            {
                Text = "Dispositivo",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 5)
            };

            var deviceField = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = cardColor,
                Margin = new Padding(0, 0, 12, 12)
            };

            deviceField.Controls.Add(deviceRow);
            deviceField.Controls.Add(deviceLabel);
            fields.Controls.Add(deviceField, 0, 0);
            fields.SetColumnSpan(deviceField, 2);

            // Segunda fila: ISO.
            var isoLabel = new Label
            {
                Text = "Selección de arranque (imagen ISO)",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 5)
            };

            var isoField = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = cardColor,
                Margin = new Padding(0, 0, 12, 14)
            };

            isoField.Controls.Add(isoRow);
            isoField.Controls.Add(isoLabel);
            fields.Controls.Add(isoField, 0, 1);
            fields.SetColumnSpan(isoField, 2);

            fields.Controls.Add(
                MakeField("Esquema de partición", cmbPartition), 0, 2);

            fields.Controls.Add(
                MakeField("Sistema de destino", cmbTarget), 1, 2);

            fields.Controls.Add(
                MakeField("Sistema de archivos", cmbFileSystem), 0, 3);

            fields.Controls.Add(
                MakeField("Tamaño de clúster", cmbClusterSize), 1, 3);

            fields.Controls.Add(
                MakeField("Etiqueta del volumen (máx. 11 caracteres)",
                    txtVolumeLabel), 0, 4);

            var lblModeNote = new Label
            {
                Text = "GPT suele utilizarse con UEFI; MBR permite configuraciones BIOS/CSM. " +
                       "La compatibilidad final depende de la ISO y del método de escritura.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 0, 0, 12)
            };

            fields.Controls.Add(lblModeNote, 1, 4);

            var lblUsbWarning = new Label
            {
                Text = "ADVERTENCIA: crear el USB elimina los datos del disco seleccionado. " +
                       "Verificá cuidadosamente el dispositivo y su capacidad antes de continuar.",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 190, 95),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 2)
            };

            configCard.Controls.Add(configTitle, 0, 0);
            configCard.Controls.Add(configDescription, 0, 1);
            configCard.Controls.Add(fields, 0, 2);

            // ---------------------------------------------------------
            // ESTADO, PROGRESO, REGISTRO Y BOTONES
            // ---------------------------------------------------------
            var actionCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = cardColor,
                Padding = new Padding(18),
                Margin = new Padding(0, 0, 0, 12)
            };

            actionCard.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 5; i++)
                actionCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            actionCard.Controls.Add(new Label
            {
                Text = "ESTADO DE LA OPERACIÓN",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10)
            }, 0, 0);

            lblUsbProgress = new Label
            {
                Text = "En espera. Seleccioná el dispositivo y la ISO.",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 8)
            };

            actionCard.Controls.Add(lblUsbProgress, 0, 1);

            usbProgress = new ProgressBar
            {
                Name = "usbProgress",
                Dock = DockStyle.Top,
                Height = 14,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                Margin = new Padding(0, 0, 0, 10)
            };

            actionCard.Controls.Add(usbProgress, 0, 2);

            usbLog = new ListBox
            {
                Name = "usbLog",
                Dock = DockStyle.Top,
                Height = 94,
                BackColor = inputColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5F),
                IntegralHeight = false,
                Margin = new Padding(0, 0, 0, 12)
            };

            usbLog.Items.Add("Motor USB inicializado.");
            usbLog.Items.Add("Esperando selección de dispositivo e ISO.");

            actionCard.Controls.Add(usbLog, 0, 3);

            var buttonLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = cardColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            btnCreateUsb = CreateButton("CREAR USB BOOTEABLE", successColor);
            btnCreateUsb.Width = 220;
            btnCreateUsb.Height = 42;
            btnCreateUsb.Margin = new Padding(0, 0, 10, 0);
            btnCreateUsb.Click += async (_, _) => await CreateUsbAsync();

            btnCancelUsb = CreateButton("Cancelar", dangerColor);
            btnCancelUsb.Width = 115;
            btnCancelUsb.Height = 42;
            btnCancelUsb.Enabled = false;

            btnCancelUsb.Click += (_, _) =>
            {
                if (usbCancellation != null &&
                    !usbCancellation.IsCancellationRequested)
                {
                    btnCancelUsb.Enabled = false;
                    lblUsbProgress.Text =
                        "Cancelando... la unidad puede quedar incompleta.";

                    AddUsbLog("Se solicitó cancelar la operación.");
                    usbCancellation.Cancel();
                }
            };

            buttonLayout.Controls.Add(btnCreateUsb);
            buttonLayout.Controls.Add(btnCancelUsb);

            actionCard.Controls.Add(buttonLayout, 0, 4);

            // ---------------------------------------------------------
            // ADVERTENCIA BAJO LA CONFIGURACIÓN
            // ---------------------------------------------------------
            var warningPanel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = cardColor,
                Padding = new Padding(18, 0, 18, 14),
                Margin = new Padding(0, 0, 0, 14)
            };

            warningPanel.Controls.Add(lblUsbWarning);

            main.Controls.Add(header, 0, 0);
            main.Controls.Add(configCard, 0, 1);
            main.Controls.Add(warningPanel, 0, 2);

            // El panel de operación queda debajo de la configuración.
            // Agregamos un contenedor vertical para mantener el orden.
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = backgroundColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            body.Controls.Add(configCard, 0, 0);
            body.Controls.Add(warningPanel, 0, 1);

            // Reconstruimos el contenedor raíz para que el estado
            // permanezca debajo de la configuración.
            var page = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = backgroundColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            page.Controls.Add(header, 0, 0);
            page.Controls.Add(configCard, 0, 1);
            page.Controls.Add(actionCard, 0, 2);

            // La advertencia ya está dentro de la tarjeta de configuración.
            // Se mantiene en el panel de advertencia para que destaque.
            configCard.Controls.Add(warningPanel, 0, 3);
            configCard.RowCount = 4;
            configCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            view.Controls.Add(page);

            return view;
        }


        private async Task RefreshUsbDevicesAsync()
        {
            if (usbCreationRunning ||
                btnRefreshUsb == null ||
                cmbUsbDevice == null)
            {
                return;
            }

            btnRefreshUsb.Enabled = false;
            lblUsbProgress.Text = "Buscando dispositivos USB físicos...";

            bool comboUpdateStarted = false;

            try
            {
                List<UsbDeviceInfo> devices =
                    await Task.Run(UsbDeviceScanner.GetUsbDevices);

                cmbUsbDevice.BeginUpdate();
                comboUpdateStarted = true;
                cmbUsbDevice.Items.Clear();

                foreach (UsbDeviceInfo device in devices)
                    cmbUsbDevice.Items.Add(device);

                if (devices.Count == 0)
                {
                    cmbUsbDevice.Items.Add(
                        "No se detectaron discos USB físicos.");

                    cmbUsbDevice.SelectedIndex = 0;

                    AddUsbLog(
                        "No se detectaron discos USB. Comprobá que estén conectados y que Windows los reconozca.");
                }
                else
                {
                    cmbUsbDevice.SelectedIndex = 0;
                    AddUsbLog(
                        $"Dispositivos USB detectados: {devices.Count}.");
                }

                lblUsbProgress.Text = devices.Count == 0
                    ? "No se encontraron dispositivos USB."
                    : $"Se encontraron {devices.Count} dispositivo(s) USB.";
            }
            catch (Exception ex)
            {
                logger.Write("Error al detectar discos USB: " + ex);

                lblUsbProgress.Text =
                    "No se pudo consultar la lista de discos USB.";

                AddUsbLog("ERROR al detectar USB: " + ex.Message);

                MessageBox.Show(
                    this,
                    "No se pudo enumerar los discos USB. Comprobá que el servicio de almacenamiento de Windows esté disponible.\n\n" +
                    ex.Message,
                    "Detección de USB",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (comboUpdateStarted &&
                    cmbUsbDevice != null &&
                    !cmbUsbDevice.IsDisposed)
                {
                    cmbUsbDevice.EndUpdate();
                }

                if (btnRefreshUsb != null &&
                    !btnRefreshUsb.IsDisposed)
                {
                    btnRefreshUsb.Enabled = true;
                }
            }
        }

        private async Task CreateUsbAsync()
        {
            if (usbCreationRunning)
                return;

            if (cmbUsbDevice.SelectedItem is not UsbDeviceInfo device)
            {
                MessageBox.Show(
                    this,
                    "Seleccioná un disco USB físico válido.",
                    "Falta seleccionar USB",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (string.IsNullOrWhiteSpace(txtIsoPath.Text) ||
                !File.Exists(txtIsoPath.Text))
            {
                MessageBox.Show(
                    this,
                    "Seleccioná una imagen ISO existente.",
                    "Falta seleccionar ISO",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            UsbFileSystem fileSystem =
                cmbFileSystem.SelectedItem?.ToString() switch
                {
                    "NTFS" => UsbFileSystem.Ntfs,
                    "exFAT" => UsbFileSystem.ExFat,
                    _ => UsbFileSystem.Fat32
                };

            BootTarget target =
                (cmbTarget.SelectedItem?.ToString() ?? "")
                    .StartsWith(
                        "BIOS",
                        StringComparison.OrdinalIgnoreCase)
                    ? BootTarget.BiosOrCsm
                    : BootTarget.Uefi;

            PartitionScheme scheme =
                cmbPartition.SelectedItem?.ToString() == "MBR"
                    ? PartitionScheme.Mbr
                    : PartitionScheme.Gpt;

            int clusterBytes =
                cmbClusterSize.SelectedItem?.ToString() switch
                {
                    "512 bytes" => 512,
                    "1 KB" => 1024,
                    "2 KB" => 2048,
                    "4 KB" => 4096,
                    "8 KB" => 8192,
                    "16 KB" => 16384,
                    "32 KB" => 32768,
                    "64 KB" => 65536,
                    _ => 0
                };

            string volumeLabel = txtVolumeLabel.Text.Trim();

            if (volumeLabel.Length > 11)
            {
                MessageBox.Show(
                    this,
                    "La etiqueta del volumen no puede superar los 11 caracteres.",
                    "Etiqueta no válida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            var options = new UsbBootOptions
            {
                DiskNumber = device.DiskNumber,
                ExpectedModel = device.Model,
                ExpectedSizeBytes = device.SizeBytes,
                IsoPath = txtIsoPath.Text,
                Scheme = scheme,
                Target = target,
                FileSystem = fileSystem,
                VolumeLabel = volumeLabel,
                ClusterSizeBytes = clusterBytes
            };

            IsoInfo iso;

            try
            {
                iso = await Task.Run(
                    () => UsbBootableService.InspectIso(options.IsoPath));

                string? validation =
                    UsbBootableService.Validate(options, iso, device);

                if (validation != null)
                {
                    MessageBox.Show(
                        this,
                        validation,
                        "Configuración no válida",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "No se pudo validar la imagen ISO.\n\n" + ex.Message,
                    "ISO no válida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            string mode = iso.IsHybrid
                ? "Escritura directa de la imagen híbrida. Las opciones de partición y formato se ignorarán para esta ISO."
                : $"Preparación de particiones y copia de archivos ({scheme}, {target}, {fileSystem}).";

            string confirmText =
                $"SE BORRARÁ TODO EL CONTENIDO DEL DISCO SELECCIONADO.\n\n" +
                $"Disco físico: {device.DiskNumber}\n" +
                $"Modelo: {device.Model}\n" +
                $"Capacidad: {device.SizeText}\n" +
                $"Volúmenes actuales: {device.LettersText}\n\n" +
                $"ISO: {Path.GetFileName(iso.Path)} ({UsbBootableService.FormatSize(iso.SizeBytes)})\n\n" +
                $"Esquema: {scheme}\n" +
                $"Sistema de destino: {target}\n" +
                $"Sistema de archivos: {fileSystem}\n" +
                $"Etiqueta: {options.VolumeLabel}\n\n" +
                mode + "\n\n" +
                "Después de confirmar, Windows solicitará permiso de administrador (UAC) para preparar la memoria.\n\n" +
                "¿Confirmás que querés continuar?";

            if (MessageBox.Show(
                    this,
                    confirmText,
                    "Confirmar creación del USB",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2)
                != DialogResult.Yes)
            {
                return;
            }

            try
            {
                string json = JsonSerializer.Serialize(options);

                string encoded = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(json));

                string exePath = Environment.ProcessPath
                    ?? Application.ExecutablePath;

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--create-bootable-usb " + encoded,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppContext.BaseDirectory
                };

                Process.Start(startInfo);

                AddUsbLog(
                    $"Se solicitó elevación de Windows para preparar el disco {device.DiskNumber}.");

                lblUsbProgress.Text =
                    "Se abrió el asistente elevado de creación USB.";
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                AddUsbLog(
                    "El usuario canceló el permiso de administrador (UAC).");

                MessageBox.Show(
                    this,
                    "Se canceló la solicitud de permisos. La memoria USB no fue modificada.",
                    "Permiso cancelado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AddUsbLog(
                    "No se pudo iniciar el proceso elevado: " + ex.Message);

                MessageBox.Show(
                    this,
                    "No se pudo abrir el proceso con permisos de administrador.\n\n" +
                    ex.Message,
                    "Error de permisos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void AddUsbLog(string message)
        {
            if (usbLog == null || usbLog.IsDisposed)
                return;

            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";

            usbLog.Items.Add(line);

            while (usbLog.Items.Count > 500)
                usbLog.Items.RemoveAt(0);

            if (usbLog.Items.Count > 0)
                usbLog.TopIndex = usbLog.Items.Count - 1;

            logger?.Write("USB: " + message);
        }

        private Panel CreateCard(string heading, string description)
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = cardColor,
                Padding = new Padding(20),
                Margin = new Padding(0, 0, 0, 16)
            };

            var title = new Label
            {
                Text = heading,
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8)
            };

            var desc = new Label
            {
                Text = description,
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(750, 0),
                ForeColor = mutedColor,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 8, 0, 0)
            };

            card.Controls.Add(desc);
            card.Controls.Add(title);

            return card;
        }

        private Button CreateButton(string text, Color color)
        {
            var button = new Button
            {
                Text = text,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(10, 5, 10, 5),
                Margin = new Padding(0, 0, 10, 0),
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderSize = 0;

            button.FlatAppearance.MouseOverBackColor =
                ControlPaint.Light(color, 0.12f);

            return button;
        }

        private void UpdateRetentionInfo()
        {
            string label = cmbMonths.SelectedItem ?? "3 meses";

            lblFrequencyInfo.Text =
                $"Limpieza automática: archivos con más de {label}. " +
                "Limpiar ahora vacía toda la carpeta inmediatamente.";
        }

        // Carga solo frecuencias válidas. Los valores antiguos se migran.
        private string LoadFrequencyLabel()
        {
            try
            {
                if (File.Exists(frequencyFile))
                {
                    string frequency =
                        File.ReadAllText(frequencyFile)
                            .Trim()
                            .ToLowerInvariant();

                    switch (frequency)
                    {
                        case "1month":
                            return "1 mes";

                        case "3months":
                            return "3 meses";

                        case "6months":
                            return "6 meses";

                        case "12months":
                            return "12 meses";

                        // Compatibilidad con configuraciones anteriores.
                        case "1hour":
                        case "1day":
                        case "1week":
                            logger.Write(
                                "Se detectó una frecuencia antigua. Se utilizará la configuración mensual guardada.");

                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Write(
                    "No se pudo leer la frecuencia guardada: " +
                    ex.Message);
            }

            return GetLabelFromSettings();
        }

        private string GetLabelFromSettings()
        {
            return settings.RetentionMonths switch
            {
                1 => "1 mes",
                6 => "6 meses",
                12 => "12 meses",
                _ => "3 meses"
            };
        }

        private static Dictionary<string, string> GetFrequencyMap()
        {
            return new Dictionary<string, string>
            {
                ["1 mes"] = "1month",
                ["3 meses"] = "3months",
                ["6 meses"] = "6months",
                ["12 meses"] = "12months"
            };
        }

        private string GetSelectedFrequency()
        {
            string label = cmbMonths.SelectedItem ?? "3 meses";
            var map = GetFrequencyMap();

            return map.TryGetValue(label, out string? frequency)
                ? frequency
                : "3months";
        }

        private string LoadValidFrequency()
        {
            try
            {
                if (File.Exists(frequencyFile))
                {
                    string frequency =
                        File.ReadAllText(frequencyFile)
                            .Trim()
                            .ToLowerInvariant();

                    if (frequency is
                        "1month" or
                        "3months" or
                        "6months" or
                        "12months")
                    {
                        return frequency;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Write(
                    "Error leyendo la frecuencia: " + ex.Message);
            }

            return settings.RetentionMonths switch
            {
                1 => "1month",
                6 => "6months",
                12 => "12months",
                _ => "3months"
            };
        }

        private void SaveConfiguration()
        {
            string folder = txtFolder.Text.Trim();

            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new InvalidOperationException(
                    "Seleccioná una carpeta para limpiar.");
            }

            if (!Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException(
                    "La carpeta seleccionada no existe: " + folder);
            }

            string frequency = GetSelectedFrequency();

            settings.FolderPath = Path.GetFullPath(folder);

            settings.RetentionMonths = frequency switch
            {
                "1month" => 1,
                "3months" => 3,
                "6months" => 6,
                "12months" => 12,
                _ => 3
            };

            Directory.CreateDirectory(
                Path.GetDirectoryName(frequencyFile)!);

            // Se guarda la configuración de la aplicación.
            settings.Save();

            // Se guarda una única vez la frecuencia de las tareas.
            File.WriteAllText(frequencyFile, frequency);
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Seleccioná la carpeta que querés limpiar",
                ShowNewFolderButton = false
            };

            if (Directory.Exists(txtFolder.Text))
                dialog.SelectedPath = txtFolder.Text;

            if (dialog.ShowDialog(this) == DialogResult.OK)
                txtFolder.Text = dialog.SelectedPath;
        }

        // La operación de PowerShell se ejecuta en segundo plano.
        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (savingConfiguration)
                return;

            savingConfiguration = true;
            btnSave.Enabled = false;
            btnSave.Text = "Guardando...";

            try
            {
                // Guardar la configuración local antes de registrar las tareas.
                SaveConfiguration();

                string frequency = GetSelectedFrequency();

                logger.Write(
                    "Configuración guardada. Ruta: " +
                    settings.FolderPath +
                    ". Frecuencia: " + frequency);

                lblStatus.Text =
                    "Configuración guardada. Registrando tareas...";

                lblStatus.ForeColor = mutedColor;
                lblStatus.Refresh();

                await Task.Run(
                    () => taskScheduler.ConfigureTasks(frequency));

                if (IsDisposed || Disposing)
                    return;

                lblStatus.Text =
                    "Configuración guardada y tareas actualizadas.";

                lblStatus.ForeColor = successColor;

                logger.Write(
                    "Tareas programadas configuradas: " + frequency);

                MessageBox.Show(
                    this,
                    "La configuración se guardó correctamente y las tareas programadas se actualizaron.",
                    "Configuración guardada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                {
                    lblStatus.Text =
                        "No se pudo completar la configuración.";

                    lblStatus.ForeColor = dangerColor;

                    MessageBox.Show(
                        this,
                        ex.Message,
                        "Error al guardar",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                logger.Write(
                    "Error al guardar configuración: " + ex);
            }
            finally
            {
                savingConfiguration = false;

                if (!IsDisposed && !Disposing && btnSave != null)
                {
                    btnSave.Enabled = true;
                    btnSave.Text = "Guardar configuración";
                }
            }
        }

        // LIMPIEZA MANUAL COMPLETA.
        // Elimina archivos y subcarpetas sin importar su antigüedad.
        // Conserva la carpeta principal seleccionada.
        private async void BtnClean_Click(object? sender, EventArgs e)
        {
            string folder = txtFolder.Text.Trim();

            if (string.IsNullOrWhiteSpace(folder))
            {
                MessageBox.Show(
                    this,
                    "Seleccioná una carpeta primero.",
                    "Nexo Support Toolkit",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (!Directory.Exists(folder))
            {
                MessageBox.Show(
                    this,
                    "La carpeta seleccionada no existe.",
                    "Nexo Support Toolkit",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            DialogResult confirmation = MessageBox.Show(
                this,
                $"Se eliminará todo el contenido de esta carpeta:\n\n{folder}\n\n" +
                "Esta acción no se puede deshacer. La carpeta principal se conservará.\n\n¿Querés continuar?",
                "Confirmar limpieza completa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmation != DialogResult.Yes)
                return;

            try
            {
                Cursor = Cursors.WaitCursor;
                btnClean.Enabled = false;
                btnTemp.Enabled = false;
                btnSave.Enabled = false;

                var result = await Task.Run(
                    () => cleaner.CleanAllContents(folder));

                string message =
                    $"Limpieza finalizada.\n\n" +
                    $"Elementos eliminados: {result.Deleted}\n" +
                    $"Elementos omitidos o con error: {result.Skipped}";

                lblStatus.Text = message.Replace("\n\n", " ");
                lblStatus.ForeColor =
                    result.Skipped > 0 ? dangerColor : successColor;

                MessageBox.Show(
                    this,
                    message,
                    "Resultado de limpieza",
                    MessageBoxButtons.OK,
                    result.Skipped > 0
                        ? MessageBoxIcon.Warning
                        : MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                logger.Write("Error en limpieza manual: " + ex);

                MessageBox.Show(
                    this,
                    ex.ToString(),
                    "Error durante la limpieza",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;

                if (!IsDisposed && !Disposing)
                {
                    btnClean.Enabled = true;
                    btnTemp.Enabled = true;
                    btnSave.Enabled = !savingConfiguration;
                }
            }
        }

        private void BtnTemp_Click(object? sender, EventArgs e)
        {
            RunTemporaryCleanup(true);
        }

        private async void RunTemporaryCleanup(bool showConfirmation)
        {
            if (showConfirmation)
            {
                DialogResult answer = MessageBox.Show(
                    this,
                    "Se intentará eliminar archivos temporales de Windows y del usuario. " +
                    "Algunos archivos en uso no podrán eliminarse.\n\n¿Continuar?",
                    "Limpiar temporales",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (answer != DialogResult.Yes)
                    return;
            }

            try
            {
                btnTemp.Enabled = false;

                var result = await Task.Run(
                    () => cleaner.CleanTemporaryFiles());

                if (IsDisposed || Disposing)
                    return;

                lblStatus.Text =
                    $"Temporales eliminados: {result.Deleted}. " +
                    $"Omitidos: {result.Skipped}.";

                lblStatus.ForeColor =
                    result.Skipped > 0 ? dangerColor : successColor;

                if (showConfirmation)
                {
                    MessageBox.Show(
                        this,
                        $"Limpieza de temporales finalizada.\n\n" +
                        $"Eliminados: {result.Deleted}\n" +
                        $"Omitidos: {result.Skipped}",
                        "Resultado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text =
                    "No se pudo completar la limpieza de temporales.";

                lblStatus.ForeColor = dangerColor;

                logger.Write(
                    "Error al limpiar temporales: " + ex);

                if (showConfirmation && !IsDisposed && !Disposing)
                {
                    MessageBox.Show(
                        this,
                        ex.Message,
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (!IsDisposed && !Disposing && btnTemp != null)
                    btnTemp.Enabled = true;
            }
        }

        // Umbral de antigüedad: exclusivamente las cuatro opciones válidas.
        private static DateTime GetCutoff(string frequency)
        {
            DateTime now = DateTime.Now;

            return (frequency ?? "").Trim().ToLowerInvariant() switch
            {
                "1month" => now.AddMonths(-1),
                "3months" => now.AddMonths(-3),
                "6months" => now.AddMonths(-6),
                "12months" => now.AddMonths(-12),
                _ => now.AddMonths(-3)
            };
        }

        private void ConfigureTray()
        {
            var trayMenu = new ContextMenuStrip();

            trayMenu.Items.Add(
                "Abrir Nexo Support Toolkit",
                null,
                (_, _) => ShowWithAdminLogin());

            trayMenu.Items.Add(
                "Limpiar por antigüedad",
                null,
                (_, _) => RunAutomaticCleanup());

            trayMenu.Items.Add(
                "Limpiar temporales",
                null,
                (_, _) => RunTemporaryCleanup(true));

            trayMenu.Items.Add(new ToolStripSeparator());

            trayMenu.Items.Add("Salir", null, (_, _) =>
            {
                exitRequested = true;

                if (trayIcon != null)
                    trayIcon.Visible = false;

                Close();
            });

            trayIcon = new NotifyIcon
            {
                Text = "Nexo Support Toolkit",
                Icon = Icon ?? SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            trayIcon.DoubleClick += (_, _) => ShowWithAdminLogin();
        }

        private void ConfigureTimer()
        {
            timer = new System.Windows.Forms.Timer
            {
                Interval = 30 * 60 * 1000
            };

            timer.Tick += (_, _) => CheckUpcomingCleanup();
            timer.Start();
        }

        private void CheckUpcomingCleanup()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settings.FolderPath) ||
                    !Directory.Exists(settings.FolderPath))
                {
                    return;
                }

                if (DateTime.TryParse(
                        settings.LastAlertDate,
                        out DateTime lastAlertDate) &&
                    lastAlertDate.Date == DateTime.Today)
                {
                    return;
                }

                string frequency = LoadValidFrequency();

                if (!HasFilesReachingLimit(
                        settings.FolderPath,
                        GetCutoff(frequency)))
                {
                    return;
                }

                trayIcon?.ShowBalloonTip(
                    5000,
                    "Nexo Support Toolkit",
                    "Hay archivos que alcanzaron la antigüedad configurada. Podés ejecutar la limpieza.",
                    ToolTipIcon.Info);

                settings.LastAlertDate =
                    DateTime.Today.ToString("yyyy-MM-dd");

                settings.Save();
            }
            catch (Exception ex)
            {
                logger.Write(
                    "Error al revisar próxima limpieza: " + ex.Message);
            }
        }

        private bool HasFilesReachingLimit(
            string folder,
            DateTime cutoff)
        {
            try
            {
                foreach (string file in Directory.GetFiles(folder))
                {
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(file);

                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            continue;

                        if (File.GetLastWriteTime(file) <= cutoff)
                            return true;
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                foreach (string directory in Directory.GetDirectories(folder))
                {
                    try
                    {
                        var info = new DirectoryInfo(directory);

                        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                            continue;

                        if (HasFilesReachingLimit(directory, cutoff))
                            return true;
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }

            return false;
        }

        // Limpieza por antigüedad desde la bandeja del sistema.
        private async void RunAutomaticCleanup()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settings.FolderPath) ||
                    !Directory.Exists(settings.FolderPath))
                {
                    trayIcon?.ShowBalloonTip(
                        4000,
                        "Nexo Support Toolkit",
                        "Configurá una carpeta válida antes de limpiar.",
                        ToolTipIcon.Warning);

                    return;
                }

                string frequency = LoadValidFrequency();
                string folder = settings.FolderPath;

                int deleted = await Task.Run(
                    () => cleaner.CleanOldFiles(folder, frequency));

                if (IsDisposed || Disposing)
                    return;

                trayIcon?.ShowBalloonTip(
                    4000,
                    "Nexo Support Toolkit",
                    $"Limpieza por antigüedad finalizada. Eliminados: {deleted}.",
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                logger.Write("Error en limpieza automática: " + ex);

                trayIcon?.ShowBalloonTip(
                    4000,
                    "Nexo Support Toolkit",
                    "No se pudo completar la limpieza. Revisá el registro.",
                    ToolTipIcon.Error);
            }
        }

        private void ShowWithAdminLogin()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ShowWithAdminLogin));
                return;
            }

            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (WindowState == FormWindowState.Minimized)
                Hide();
        }

        protected override void OnFormClosing(
            FormClosingEventArgs e)
        {
            if (!exitRequested &&
                e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();

                trayIcon?.ShowBalloonTip(
                    2500,
                    "Nexo Support Toolkit",
                    "La aplicación continúa ejecutándose en segundo plano.",
                    ToolTipIcon.Info);

                return;
            }

            usbCancellation?.Cancel();

            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
            }

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            base.OnFormClosing(e);
        }
    }

    [ToolboxItem(false)]
    [DesignerCategory("Code")]
    internal sealed class SegmentedSelector : FlowLayoutPanel
    {
        private static readonly Color ButtonBorder =
            Color.FromArgb(52, 60, 76);

        private readonly List<Button> buttons = new();
        private readonly Color normalBack;
        private readonly Color selectedBack;
        private readonly Color hoverBack;
        private readonly Color normalFore;

        private int selectedIndex = -1;

        public event EventHandler? SelectionChanged;

        public SegmentedSelector(
            Color normalBack,
            Color selectedBack,
            Color normalFore)
        {
            this.normalBack = normalBack;
            this.selectedBack = selectedBack;
            this.normalFore = normalFore;

            hoverBack = ControlPaint.Light(normalBack, 0.15f);

            FlowDirection = FlowDirection.LeftToRight;
            WrapContents = true;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (value < 0 || value >= buttons.Count)
                    value = -1;

                if (value == selectedIndex)
                    return;

                selectedIndex = value;
                ApplyState();

                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(
            DesignerSerializationVisibility.Hidden)]
        public string? SelectedItem
        {
            get => selectedIndex >= 0
                ? buttons[selectedIndex].Text
                : null;

            set => SelectedIndex =
                buttons.FindIndex(b => b.Text == value);
        }

        public void SetItems(params string[] items)
        {
            SuspendLayout();

            foreach (Button old in buttons)
            {
                Controls.Remove(old);
                old.Dispose();
            }

            buttons.Clear();
            selectedIndex = -1;

            for (int i = 0; i < items.Length; i++)
            {
                int index = i;

                var button = new Button
                {
                    Text = items[i],
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowOnly,
                    MinimumSize = new Size(88, 38),
                    Padding = new Padding(12, 0, 12, 0),
                    Margin = new Padding(0, 0, 8, 8),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font(
                        "Segoe UI",
                        9.5F,
                        FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    UseVisualStyleBackColor = false
                };

                button.FlatAppearance.BorderSize = 1;
                button.Click += (_, _) => SelectedIndex = index;

                buttons.Add(button);
                Controls.Add(button);
            }

            ApplyState();
            ResumeLayout(true);
        }

        private void ApplyState()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                bool selected = i == selectedIndex;

                button.BackColor = selected
                    ? selectedBack
                    : normalBack;

                button.ForeColor = selected
                    ? Color.White
                    : normalFore;

                button.FlatAppearance.BorderColor = selected
                    ? selectedBack
                    : ButtonBorder;

                button.FlatAppearance.MouseOverBackColor = selected
                    ? selectedBack
                    : hoverBack;
            }
        }
    }
}