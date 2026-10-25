
using System;
using System.Drawing;
using System.Windows.Forms;
using NexoSupportToolKit.Services;

namespace NexoSupportToolKit
{
    public class AdminLoginForm : Form
    {
        private readonly TextBox txtUser;
        private readonly TextBox txtPassword;
        private readonly Button btnLogin;

        public AdminLoginForm()
        {
            Text = "Acceso de administrador";
            Size = new Size(420, 280);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;

            Label title = new Label
            {
                Text = "Nexo Support Toolkit",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(25, 20)
            };

            Label lblUser = new Label
            {
                Text = "Usuario",
                AutoSize = true,
                Location = new Point(25, 65)
            };

            txtUser = new TextBox
            {
                Location = new Point(25, 85),
                Width = 350,
                Text = ""
            };

            Label lblPassword = new Label
            {
                Text = "Contraseña",
                AutoSize = true,
                Location = new Point(25, 120)
            };

            txtPassword = new TextBox
            {
                Location = new Point(25, 140),
                Width = 350,
                UseSystemPasswordChar = true
            };

            btnLogin = new Button
            {
                Text = "Ingresar",
                Location = new Point(225, 185),
                Size = new Size(150, 35)
            };

            btnLogin.Click += Submit_Click;
            AcceptButton = btnLogin;

            Controls.Add(title);
            Controls.Add(lblUser);
            Controls.Add(txtUser);
            Controls.Add(lblPassword);
            Controls.Add(txtPassword);
            Controls.Add(btnLogin);
        }

        private void Submit_Click(object? sender, EventArgs e)
        {
            bool success = AdminAuthService.ValidateLogin(
                txtUser.Text,
                txtPassword.Text);

            if (!success)
            {
                MessageBox.Show(
                    "Usuario o contraseña incorrectos.",
                    "Acceso denegado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtPassword.Clear();
                txtPassword.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
