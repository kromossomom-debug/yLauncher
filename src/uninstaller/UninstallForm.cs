using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class UninstallForm : Form
{
	private readonly CheckBox cleanDataCheckBox;
	private readonly Button uninstallButton;
	private readonly Button cancelButton;
	private readonly Label statusLabel;

	public UninstallForm()
	{
		Text = "Desinstalar yLauncher";
		ClientSize = new Size(560, 390);
		StartPosition = FormStartPosition.CenterScreen;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		BackColor = Color.FromArgb(12, 12, 20);
		ForeColor = Color.White;
		Font = new Font("Segoe UI", 9.5f);

		try
		{
			string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yLauncher.ico");
			if (File.Exists(iconPath)) Icon = new Icon(iconPath);
		}
		catch
		{
		}

		var header = new Panel
		{
			Dock = DockStyle.Top,
			Height = 92,
			BackColor = Color.FromArgb(20, 20, 32)
		};
		header.Paint += delegate(object sender, PaintEventArgs args)
		{
			using (var pen = new Pen(Color.FromArgb(239, 68, 68), 2f))
			{
				args.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
			}
		};
		header.Controls.Add(new Label
		{
			Text = "Desinstalar yLauncher",
			Font = new Font("Segoe UI", 15f, FontStyle.Bold),
			ForeColor = Color.White,
			Location = new Point(26, 18),
			AutoSize = true
		});
		header.Controls.Add(new Label
		{
			Text = "Remova o aplicativo sem perder sua biblioteca pessoal.",
			ForeColor = Color.FromArgb(148, 163, 184),
			Location = new Point(28, 54),
			AutoSize = true
		});
		Controls.Add(header);

		Controls.Add(new Label
		{
			Text = "O aplicativo, os atalhos, a inicialização automática e os protocolos\nregistrados serão removidos deste computador.",
			Location = new Point(28, 120),
			Size = new Size(500, 52),
			ForeColor = Color.FromArgb(226, 232, 240)
		});

		cleanDataCheckBox = new CheckBox
		{
			Text = "Excluir também capas, preferências e cache local",
			Location = new Point(28, 184),
			Size = new Size(500, 28),
			ForeColor = Color.FromArgb(203, 213, 225),
			Checked = false
		};
		Controls.Add(cleanDataCheckBox);

		Controls.Add(new Label
		{
			Text = "Deixe esta opção desmarcada para manter seus dados em uma futura reinstalação.",
			Location = new Point(48, 216),
			Size = new Size(478, 38),
			ForeColor = Color.FromArgb(148, 163, 184),
			Font = new Font("Segoe UI", 8.5f)
		});

		statusLabel = new Label
		{
			Location = new Point(28, 264),
			Size = new Size(500, 24),
			ForeColor = Color.FromArgb(134, 239, 172),
			Font = new Font("Segoe UI", 9f, FontStyle.Bold)
		};
		Controls.Add(statusLabel);

		var footer = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 70,
			BackColor = Color.FromArgb(16, 16, 26)
		};
		uninstallButton = new Button
		{
			Text = "Desinstalar",
			Size = new Size(132, 40),
			Location = new Point(278, 15),
			BackColor = Color.FromArgb(220, 38, 38),
			ForeColor = Color.White,
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand,
			Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
		};
		uninstallButton.FlatAppearance.BorderSize = 0;
		uninstallButton.Click += async delegate { await UninstallAsync(); };

		cancelButton = new Button
		{
			Text = "Cancelar",
			Size = new Size(112, 40),
			Location = new Point(422, 15),
			BackColor = Color.FromArgb(30, 30, 44),
			ForeColor = Color.White,
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand
		};
		cancelButton.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 80);
		cancelButton.Click += delegate { Close(); };

		footer.Controls.Add(uninstallButton);
		footer.Controls.Add(cancelButton);
		Controls.Add(footer);
	}

	private async Task UninstallAsync()
	{
		uninstallButton.Enabled = false;
		cancelButton.Enabled = false;
		cleanDataCheckBox.Enabled = false;
		statusLabel.Text = "Removendo o yLauncher...";

		try
		{
			bool deleteUserData = cleanDataCheckBox.Checked;
			await Task.Run(() => UninstallProgram.PerformUninstall(deleteUserData));
			MessageBox.Show(
				deleteUserData
					? "O yLauncher e seus dados locais foram removidos."
					: "O yLauncher foi removido. Sua biblioteca pessoal foi preservada.",
				"Desinstalação concluída",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information);
			Close();
		}
		catch (Exception exception)
		{
			statusLabel.Text = "Não foi possível concluir a desinstalação.";
			MessageBox.Show(exception.Message, "Falha na desinstalação", MessageBoxButtons.OK, MessageBoxIcon.Error);
			uninstallButton.Enabled = true;
			cancelButton.Enabled = true;
			cleanDataCheckBox.Enabled = true;
		}
	}
}
