using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class InstallerForm : Form
{
	private readonly TextBox txtPath;
	private readonly Button btnBrowse;
	private readonly CheckBox chkDesktop;
	private readonly CheckBox chkStartMenu;
	private readonly CheckBox chkAutoStart;
	private readonly CheckBox chkProtocols;
	private readonly CheckBox chkLaunchNow;
	private readonly ProgressBar progressBar;
	private readonly Label lblStatus;
	private readonly Button btnInstall;
	private readonly Button btnCancel;
	private bool isFinished;

	public InstallerForm(string defaultPath)
	{
		string sysLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
		bool isRu = sysLang == "ru";
		bool isPt = sysLang == "pt";

		Text = isRu ? "Установка yLauncher 2.1" : isPt ? "Instalar yLauncher 2.1" : "Install yLauncher 2.1";
		Size = new Size(610, 520);
		StartPosition = FormStartPosition.CenterScreen;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		BackColor = Color.FromArgb(7, 10, 20);
		ForeColor = Color.White;
		Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

		var headerPanel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 104,
			BackColor = Color.FromArgb(14, 23, 42)
		};
		headerPanel.Paint += (sender, e) =>
		{
			using (var accentPen = new Pen(Color.FromArgb(59, 130, 246), 3f))
			{
				e.Graphics.DrawLine(accentPen, 0, headerPanel.Height - 2, headerPanel.Width, headerPanel.Height - 2);
			}
		};
		headerPanel.Controls.Add(new Label
		{
			Text = "yLauncher 2.1",
			Font = new Font("Segoe UI", 19f, FontStyle.Bold),
			ForeColor = Color.White,
			Location = new Point(26, 18),
			AutoSize = true
		});
		headerPanel.Controls.Add(new Label
		{
			Text = isRu ? "Быстрый запуск, чистые данные и защищенный локальный мост."
				: isPt ? "Mais rápido ao iniciar, dados limpos e ponte local protegida."
				: "Faster startup, clean data and secure local bridge.",
			Font = new Font("Segoe UI", 9.5f),
			ForeColor = Color.FromArgb(147, 197, 253),
			Location = new Point(29, 61),
			AutoSize = true
		});
		Controls.Add(headerPanel);

		Controls.Add(new Label
		{
			Text = isRu ? "Папка установки" : isPt ? "Pasta de instalação" : "Installation folder",
			Location = new Point(28, 124),
			AutoSize = true,
			ForeColor = Color.FromArgb(203, 213, 225),
			Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
		});

		txtPath = new TextBox
		{
			Text = defaultPath,
			Location = new Point(28, 149),
			Size = new Size(430, 27),
			BackColor = Color.FromArgb(20, 27, 43),
			ForeColor = Color.White,
			BorderStyle = BorderStyle.FixedSingle
		};
		Controls.Add(txtPath);

		btnBrowse = CreateButton(isRu ? "Обзор" : isPt ? "Procurar" : "Browse", new Point(468, 147), new Size(105, 31), Color.FromArgb(30, 41, 59));
		btnBrowse.Click += delegate
		{
			string desc = isRu ? "Выберите папку для yLauncher" : isPt ? "Escolha a pasta do yLauncher" : "Choose the yLauncher folder";
			using (var dialog = new FolderBrowserDialog { SelectedPath = txtPath.Text, Description = desc })
			{
				if (dialog.ShowDialog() == DialogResult.OK) txtPath.Text = dialog.SelectedPath;
			}
		};
		Controls.Add(btnBrowse);

		var optionsPanel = new Panel
		{
			Location = new Point(28, 198),
			Size = new Size(545, 152),
			BackColor = Color.FromArgb(13, 19, 33),
			BorderStyle = BorderStyle.FixedSingle
		};
		chkDesktop = CreateOption(isRu ? "Создать ярлык на рабочем столе" : isPt ? "Criar atalho na Área de Trabalho" : "Create Desktop shortcut", 14, true);
		chkStartMenu = CreateOption(isRu ? "Создать ярлык в меню Пуск" : isPt ? "Criar atalhos no Menu Iniciar" : "Create Start Menu shortcuts", 46, true);
		chkAutoStart = CreateOption(isRu ? "Запускать вместе с Windows" : isPt ? "Iniciar com o Windows" : "Start with Windows", 78, false);
		chkProtocols = CreateOption(isRu ? "Зарегистрировать интеграцию ylauncher://" : isPt ? "Registrar integração ylauncher://" : "Register ylauncher:// integration", 110, true);
		optionsPanel.Controls.Add(chkDesktop);
		optionsPanel.Controls.Add(chkStartMenu);
		optionsPanel.Controls.Add(chkAutoStart);
		optionsPanel.Controls.Add(chkProtocols);
		Controls.Add(optionsPanel);

		progressBar = new ProgressBar
		{
			Location = new Point(28, 371),
			Size = new Size(545, 12),
			Style = ProgressBarStyle.Marquee,
			MarqueeAnimationSpeed = 24,
			Visible = false
		};
		Controls.Add(progressBar);

		lblStatus = new Label
		{
			Text = isRu ? "Готово к установке." : isPt ? "Pronto para instalar." : "Ready to install.",
			Location = new Point(28, 393),
			Size = new Size(335, 25),
			ForeColor = Color.FromArgb(148, 163, 184)
		};
		Controls.Add(lblStatus);

		chkLaunchNow = new CheckBox
		{
			Text = isRu ? "Запустить yLauncher сейчас" : isPt ? "Abrir o yLauncher agora" : "Launch yLauncher now",
			Location = new Point(370, 390),
			Size = new Size(205, 26),
			Checked = true,
			ForeColor = Color.FromArgb(147, 197, 253),
			Visible = false
		};
		Controls.Add(chkLaunchNow);

		var footer = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Color.FromArgb(13, 19, 33) };
		btnInstall = CreateButton(isRu ? "Установить" : isPt ? "Instalar" : "Install", new Point(308, 16), new Size(126, 39), Color.FromArgb(37, 99, 235));
		btnInstall.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
		btnInstall.FlatAppearance.BorderSize = 0;
		btnInstall.Click += async (sender, args) =>
		{
			if (isFinished)
			{
				LaunchInstalledApp();
				Close();
				return;
			}
			await StartInstallAsync();
		};
		footer.Controls.Add(btnInstall);

		btnCancel = CreateButton(isRu ? "Отмена" : isPt ? "Cancelar" : "Cancel", new Point(447, 16), new Size(126, 39), Color.FromArgb(30, 41, 59));
		btnCancel.Click += (sender, args) => Close();
		footer.Controls.Add(btnCancel);
		Controls.Add(footer);
	}

	private CheckBox CreateOption(string text, int y, bool isChecked)
	{
		return new CheckBox
		{
			Text = text,
			Location = new Point(17, y),
			Size = new Size(500, 25),
			Checked = isChecked,
			ForeColor = Color.FromArgb(226, 232, 240)
		};
	}

	private Button CreateButton(string text, Point location, Size size, Color background)
	{
		var button = new Button
		{
			Text = text,
			Location = location,
			Size = size,
			BackColor = background,
			ForeColor = Color.White,
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand
		};
		button.FlatAppearance.BorderColor = Color.FromArgb(71, 85, 105);
		return button;
	}

	private async Task StartInstallAsync()
	{
		string sysLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
		bool isRu = sysLang == "ru";
		bool isPt = sysLang == "pt";

		string chosenTarget;
		try
		{
			chosenTarget = Path.GetFullPath(txtPath.Text.Trim());
			if (string.Equals(chosenTarget.TrimEnd(Path.DirectorySeparatorChar), Path.GetPathRoot(chosenTarget).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(isRu ? "Выберите папку, а не корень диска." : isPt ? "Escolha uma pasta, não a raiz do disco." : "Choose a folder, not the drive root.");
			}
		}
		catch (Exception ex)
		{
			string title = isRu ? "Неверная папка" : isPt ? "Pasta inválida" : "Invalid folder";
			MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		SetControlsEnabled(false);
		progressBar.Visible = true;
		lblStatus.Text = isRu ? "Установка файлов и интеграций..." : isPt ? "Instalando arquivos e integrações..." : "Installing files and integrations...";
		lblStatus.ForeColor = Color.FromArgb(147, 197, 253);

		bool success = await Task.Run(() => InstallerProgram.PerformInstallation(
			chosenTarget,
			chkDesktop.Checked,
			chkStartMenu.Checked,
			chkAutoStart.Checked,
			chkProtocols.Checked));

		progressBar.Visible = false;
		if (!success)
		{
			lblStatus.Text = isRu ? "Не удалось завершить установку." : isPt ? "Não foi possível concluir a instalação." : "Could not complete the installation.";
			lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
			SetControlsEnabled(true);
			return;
		}

		isFinished = true;
		lblStatus.Text = isRu ? "Установка успешно завершена." : isPt ? "Instalação concluída com sucesso." : "Installation completed successfully.";
		lblStatus.ForeColor = Color.FromArgb(74, 222, 128);
		chkLaunchNow.Visible = true;
		btnInstall.Text = isRu ? "Завершить" : isPt ? "Concluir" : "Finish";
		btnInstall.BackColor = Color.FromArgb(22, 163, 74);
		btnInstall.Enabled = true;
		btnCancel.Visible = false;
	}

	private void SetControlsEnabled(bool enabled)
	{
		txtPath.Enabled = enabled;
		btnBrowse.Enabled = enabled;
		chkDesktop.Enabled = enabled;
		chkStartMenu.Enabled = enabled;
		chkAutoStart.Enabled = enabled;
		chkProtocols.Enabled = enabled;
		btnInstall.Enabled = enabled;
		btnCancel.Enabled = enabled;
	}

	private void LaunchInstalledApp()
	{
		if (!chkLaunchNow.Checked) return;
		try
		{
			string launcherPath = Path.Combine(txtPath.Text, "yLauncher.exe");
			if (File.Exists(launcherPath))
			{
				Process.Start(new ProcessStartInfo(launcherPath) { UseShellExecute = true });
			}
		}
		catch
		{
		}
	}
}
