using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NexoSupportToolKit.Services;

namespace NexoSupportToolKit
{
    /// <summary>
    /// Ventana auxiliar que se ejecuta elevada únicamente durante la creación
    /// del USB. La aplicación principal continúa ejecutándose sin elevación.
    /// </summary>
    internal sealed class UsbCreationElevatedForm : Form
    {
        private readonly UsbBootOptions options;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly ListBox logList;
        private readonly Button cancelButton;
        private readonly CancellationTokenSource cancellation = new();
        private bool operationFinished;
        private bool started;

        private readonly Color background = Color.FromArgb(15, 18, 25);
        private readonly Color card = Color.FromArgb(27, 32, 43);
        private readonly Color input = Color.FromArgb(36, 42, 55);
        private readonly Color foreground = Color.FromArgb(235, 239, 246);
        private readonly Color muted = Color.FromArgb(153, 164, 181);

        public UsbCreationElevatedForm(UsbBootOptions options)
        {
            this.options = options;
            Text = "Nexo Support Toolkit - Creación USB booteable";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(680, 430);
            MinimumSize = new Size(620, 400);
            BackColor = background;
            ForeColor = foreground;
            Font = new Font("Segoe UI", 9F);
            ShowInTaskbar = true;

            var heading = new Label
            {
                Text = "Creando USB booteable",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = foreground,
                AutoSize = true,
                Location = new Point(22, 18)
            };
            Controls.Add(heading);

            var detail = new Label
            {
                Text = $"Disco {options.DiskNumber}  •  {options.ExpectedModel}\n{options.Scheme} / {options.Target} / {options.FileSystem}  •  Etiqueta: {options.VolumeLabel}",
                ForeColor = muted,
                AutoSize = true,
                Location = new Point(24, 58),
                MaximumSize = new Size(630, 0)
            };
            Controls.Add(detail);

            progressBar = new ProgressBar
            {
                Location = new Point(24, 110),
                Size = new Size(632, 20),
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };
            Controls.Add(progressBar);

            statusLabel = new Label
            {
                Text = "Esperando...",
                ForeColor = muted,
                AutoSize = true,
                Location = new Point(24, 140)
            };
            Controls.Add(statusLabel);

            logList = new ListBox
            {
                Location = new Point(24, 168),
                Size = new Size(632, 190),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = input,
                ForeColor = foreground,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                IntegralHeight = false
            };
            Controls.Add(logList);

            cancelButton = new Button
            {
                Text = "Cancelar",
                Location = new Point(24, 374),
                Size = new Size(120, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.FromArgb(220, 80, 90),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.Click += (_, _) =>
            {
                if (!operationFinished && !cancellation.IsCancellationRequested)
                {
                    cancelButton.Enabled = false;
                    statusLabel.Text = "Cancelando... la unidad puede quedar incompleta.";
                    AddLog("Cancelación solicitada.");
                    cancellation.Cancel();
                }
                else if (operationFinished)
                {
                    Close();
                }
            };
            Controls.Add(cancelButton);
            Controls.Add(new Label
            {
                Text = "No retires la memoria mientras la operación esté en curso.",
                ForeColor = muted,
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(255, 384)
            });

            Shown += async (_, _) =>
            {
                if (started) return;
                started = true;
                await RunCreationAsync();
            };
        }

        private async Task RunCreationAsync()
        {
            try
            {
                if (!UsbBootableService.IsAdministrator())
                    throw new InvalidOperationException("Windows no concedió permisos de administrador.");

                AddLog("Verificando la imagen ISO y el disco físico...");
                var reporter = new Progress<UsbProgress>(item =>
                {
                    if (IsDisposed) return;
                    if (item.Percent >= 0)
                        progressBar.Value = Math.Max(progressBar.Minimum, Math.Min(progressBar.Maximum, item.Percent));
                    if (!string.IsNullOrWhiteSpace(item.Status))
                        statusLabel.Text = item.Status;
                    if (!string.IsNullOrWhiteSpace(item.LogLine))
                        AddLog(item.LogLine);
                });

                var creator = new UsbBootCreator(new AppLogger());
                await creator.CreateAsync(options, reporter, cancellation.Token);
                progressBar.Value = 100;
                statusLabel.Text = "USB booteable creado correctamente.";
                AddLog("Proceso finalizado correctamente.");
                MessageBox.Show(this,
                    "La memoria USB se preparó correctamente. Expulsala de forma segura antes de retirarla.",
                    "USB booteable creado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                statusLabel.Text = "Operación cancelada.";
                AddLog("Se canceló la operación. La unidad puede haber quedado incompleta.");
                MessageBox.Show(this,
                    "La operación fue cancelada. Volvé a crear el USB antes de intentar arrancar desde él.",
                    "Creación cancelada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Falló la creación del USB.";
                AddLog("ERROR: " + ex.Message);
                try { new AppLogger().Write("Error al crear USB booteable: " + ex); } catch { }
                MessageBox.Show(this,
                    "No se pudo crear el USB. La unidad podría haber quedado parcialmente preparada.\n\n" + ex.Message,
                    "Error al crear USB", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                operationFinished = true;
                cancelButton.Text = "Cerrar";
                cancelButton.Enabled = true;
                cancellation.Dispose();
            }
        }

        private void AddLog(string message)
        {
            if (IsDisposed || logList.IsDisposed) return;
            logList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (logList.Items.Count > 300) logList.Items.RemoveAt(0);
            if (logList.Items.Count > 0) logList.TopIndex = logList.Items.Count - 1;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!operationFinished && started)
            {
                e.Cancel = true;
                if (!cancellation.IsCancellationRequested)
                {
                    cancellation.Cancel();
                    statusLabel.Text = "Cancelando... la unidad puede quedar incompleta.";
                    cancelButton.Enabled = false;
                }
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
