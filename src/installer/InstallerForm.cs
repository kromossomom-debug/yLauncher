using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class InstallerForm : Form
{
	// Paleta alinhada ao design system do launcher (index.html)
	private static readonly Color Bg = Color.FromArgb(9, 11, 18);
	private static readonly Color Surface = Color.FromArgb(17, 20, 31);
	private static readonly Color SurfaceHi = Color.FromArgb(25, 29, 44);
	private static readonly Color Line = Color.FromArgb(38, 44, 64);
	private static readonly Color Fg = Color.FromArgb(240, 243, 250);
	private static readonly Color Muted = Color.FromArgb(148, 157, 179);
	private static readonly Color Accent = Color.FromArgb(59, 130, 246);
	private static readonly Color AccentDeep = Color.FromArgb(37, 99, 235);
	private static readonly Color Success = Color.FromArgb(34, 197, 94);
	private static readonly Color Danger = Color.FromArgb(248, 113, 113);

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
	private readonly bool isRu;
	private readonly bool isPt;
	private bool isFinished;

	private string L(string pt, string en, string ru)
	{
		return isRu ? ru : isPt ? pt : en;
	}

	public InstallerForm(string defaultPath, bool isUpgrade)
	{
		string sysLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
		isRu = sysLang == "ru";
		isPt = sysLang == "pt";
		string installedVersion = InstallerProgram.GetInstalledVersion();

		Text = L("Instalar yLauncher ", "Install yLauncher ", "Установка yLauncher ") + AppVersion.Display;
		ClientSize = new Size(620, 500);
		StartPosition = FormStartPosition.CenterScreen;
		FormBorderStyle = FormBorderStyle.FixedSingle;
		MaximizeBox = false;
		BackColor = Bg;
		ForeColor = Fg;
		Font = new Font("Segoe UI", 9.75f, FontStyle.Regular);
		DoubleBuffered = true;
		try
		{
			Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
		}

		// ── Cabeçalho com gradiente ──────────────────────────
		var header = new Panel { Dock = DockStyle.Top, Height = 124, BackColor = Surface };
		header.Paint += (s, e) =>
		{
			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			using (var brush = new LinearGradientBrush(header.ClientRectangle, Color.FromArgb(22, 31, 58), Color.FromArgb(10, 12, 20), LinearGradientMode.ForwardDiagonal))
			{
				g.FillRectangle(brush, header.ClientRectangle);
			}
			using (var glow = new SolidBrush(Color.FromArgb(40, Accent)))
			{
				g.FillEllipse(glow, header.Width - 220, -120, 320, 260);
			}
			// Marca
			var mark = new Rectangle(28, 30, 54, 54);
			using (var path = RoundedRect(mark, 14))
			using (var brush = new LinearGradientBrush(mark, Accent, Color.FromArgb(99, 102, 241), 45f))
			{
				g.FillPath(brush, path);
			}
			using (var white = new SolidBrush(Color.White))
			{
				var play = new[] { new Point(mark.X + 22, mark.Y + 16), new Point(mark.X + 22, mark.Y + 38), new Point(mark.X + 39, mark.Y + 27) };
				g.FillPolygon(white, play);
			}
			using (var line = new Pen(Color.FromArgb(60, 255, 255, 255)))
			{
				g.DrawLine(line, 0, header.Height - 1, header.Width, header.Height - 1);
			}
		};
		header.Controls.Add(new Label
		{
			Text = "yLauncher",
			Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
			ForeColor = Fg,
			BackColor = Color.Transparent,
			Location = new Point(96, 26),
			AutoSize = true
		});
		string subtitle = isUpgrade && !string.IsNullOrEmpty(installedVersion)
			? L("Atualizar da versão ", "Update from version ", "Обновление с версии ") + installedVersion + L(" para ", " to ", " до ") + AppVersion.Display
			: L("Versão ", "Version ", "Версия ") + AppVersion.Display + L(" · sua biblioteca de jogos em tela cheia", " · your fullscreen game library", " · ваша полноэкранная библиотека игр");
		header.Controls.Add(new Label
		{
			Text = subtitle,
			ForeColor = Color.FromArgb(165, 196, 252),
			BackColor = Color.Transparent,
			Location = new Point(99, 68),
			AutoSize = true
		});
		Controls.Add(header);

		// ── Pasta de instalação ──────────────────────────────
		Controls.Add(new Label
		{
			Text = L("Pasta de instalação", "Installation folder", "Папка установки"),
			Location = new Point(28, 146),
			AutoSize = true,
			ForeColor = Muted,
			Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold)
		});
		txtPath = new TextBox
		{
			Text = defaultPath,
			Location = new Point(28, 170),
			Size = new Size(446, 28),
			BackColor = SurfaceHi,
			ForeColor = Fg,
			BorderStyle = BorderStyle.FixedSingle,
			Enabled = !isUpgrade
		};
		Controls.Add(txtPath);
		btnBrowse = CreateButton(L("Procurar", "Browse", "Обзор"), new Point(484, 168), new Size(108, 30), SurfaceHi, false);
		btnBrowse.Enabled = !isUpgrade;
		btnBrowse.Click += delegate
		{
			using (var dialog = new FolderBrowserDialog { SelectedPath = txtPath.Text, Description = L("Escolha a pasta do yLauncher", "Choose the yLauncher folder", "Выберите папку для yLauncher") })
			{
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					txtPath.Text = dialog.SelectedPath;
				}
			}
		};
		Controls.Add(btnBrowse);

		// ── Opções ───────────────────────────────────────────
		var options = new Panel { Location = new Point(28, 216), Size = new Size(564, 150), BackColor = Surface };
		options.Paint += (s, e) =>
		{
			using (var pen = new Pen(Line))
			{
				e.Graphics.DrawRectangle(pen, 0, 0, options.Width - 1, options.Height - 1);
			}
		};
		chkDesktop = CreateOption(L("Criar atalho na Área de Trabalho", "Create Desktop shortcut", "Создать ярлык на рабочем столе"), 14, true);
		chkStartMenu = CreateOption(L("Criar atalhos no Menu Iniciar", "Create Start Menu shortcuts", "Создать ярлык в меню Пуск"), 46, true);
		chkAutoStart = CreateOption(L("Iniciar com o Windows (modo console)", "Start with Windows (console mode)", "Запускать вместе с Windows"), 78, false);
		chkProtocols = CreateOption(L("Registrar integração ylauncher://", "Register ylauncher:// integration", "Зарегистрировать интеграцию ylauncher://"), 110, true);
		options.Controls.AddRange(new Control[] { chkDesktop, chkStartMenu, chkAutoStart, chkProtocols });
		Controls.Add(options);

		progressBar = new ProgressBar
		{
			Location = new Point(28, 384),
			Size = new Size(564, 6),
			Style = ProgressBarStyle.Marquee,
			MarqueeAnimationSpeed = 24,
			Visible = false
		};
		Controls.Add(progressBar);

		lblStatus = new Label
		{
			Text = isUpgrade
				? L("Biblioteca, capas e configurações serão preservadas.", "Library, covers and settings will be preserved.", "Библиотека, обложки и настройки будут сохранены.")
				: L("Pronto para instalar. Atualizações futuras serão automáticas.", "Ready to install. Future updates are automatic.", "Готово к установке. Обновления будут автоматическими."),
			Location = new Point(28, 398),
			Size = new Size(380, 22),
			ForeColor = Muted
		};
		Controls.Add(lblStatus);

		chkLaunchNow = new CheckBox
		{
			Text = L("Abrir o yLauncher agora", "Launch yLauncher now", "Запустить yLauncher сейчас"),
			Location = new Point(412, 396),
			Size = new Size(190, 26),
			Checked = true,
			ForeColor = Fg,
			Visible = false,
			FlatStyle = FlatStyle.Flat
		};
		Controls.Add(chkLaunchNow);

		// ── Rodapé ───────────────────────────────────────────
		var footer = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Surface };
		footer.Paint += (s, e) =>
		{
			using (var pen = new Pen(Line))
			{
				e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
			}
		};
		btnInstall = CreateButton(isUpgrade ? L("Atualizar", "Update", "Обновить") : L("Instalar", "Install", "Установить"), new Point(334, 16), new Size(126, 38), Accent, true);
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
		btnCancel = CreateButton(L("Cancelar", "Cancel", "Отмена"), new Point(468, 16), new Size(124, 38), SurfaceHi, false);
		btnCancel.Click += (sender, args) => Close();
		footer.Controls.Add(btnCancel);
		Controls.Add(footer);
		AcceptButton = btnInstall;
		CancelButton = btnCancel;
	}

	private static GraphicsPath RoundedRect(Rectangle r, int radius)
	{
		int d = radius * 2;
		var path = new GraphicsPath();
		path.AddArc(r.X, r.Y, d, d, 180, 90);
		path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
		path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
		path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
		path.CloseFigure();
		return path;
	}

	private CheckBox CreateOption(string text, int y, bool isChecked)
	{
		return new CheckBox
		{
			Text = text,
			Location = new Point(18, y),
			Size = new Size(530, 26),
			Checked = isChecked,
			ForeColor = Fg,
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand
		};
	}

	private static Button CreateButton(string text, Point location, Size size, Color background, bool primary)
	{
		var button = new Button
		{
			Text = text,
			Location = location,
			Size = size,
			BackColor = background,
			ForeColor = Color.White,
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand,
			Font = new Font("Segoe UI Semibold", 9.75f, primary ? FontStyle.Bold : FontStyle.Regular)
		};
		button.FlatAppearance.BorderColor = primary ? background : Line;
		button.FlatAppearance.BorderSize = primary ? 0 : 1;
		button.FlatAppearance.MouseOverBackColor = primary ? AccentDeep : Color.FromArgb(34, 40, 60);
		return button;
	}

	private async Task StartInstallAsync()
	{
		string chosenTarget;
		try
		{
			chosenTarget = Path.GetFullPath(txtPath.Text.Trim());
			if (string.Equals(chosenTarget.TrimEnd(Path.DirectorySeparatorChar), Path.GetPathRoot(chosenTarget).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(L("Escolha uma pasta, não a raiz do disco.", "Choose a folder, not the drive root.", "Выберите папку, а не корень диска."));
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, L("Pasta inválida", "Invalid folder", "Неверная папка"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		if (IsLauncherRunning())
		{
			var answer = MessageBox.Show(
				L("O yLauncher está aberto. Feche-o para continuar.\n\nFechar automaticamente agora?", "yLauncher is running. Close it to continue.\n\nClose it automatically now?", "yLauncher запущен. Закройте его, чтобы продолжить.\n\nЗакрыть автоматически?"),
				"yLauncher", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
			if (answer != DialogResult.Yes)
			{
				return;
			}
			StopLauncher();
		}

		SetControlsEnabled(false);
		progressBar.Visible = true;
		lblStatus.Text = L("Instalando arquivos e integrações...", "Installing files and integrations...", "Установка файлов и интеграций...");
		lblStatus.ForeColor = Color.FromArgb(165, 196, 252);

		bool success = await Task.Run(() => InstallerProgram.PerformInstallation(
			chosenTarget, chkDesktop.Checked, chkStartMenu.Checked, chkAutoStart.Checked, chkProtocols.Checked));

		progressBar.Visible = false;
		if (!success)
		{
			lblStatus.Text = L("Não foi possível concluir a instalação.", "Could not complete the installation.", "Не удалось завершить установку.");
			lblStatus.ForeColor = Danger;
			SetControlsEnabled(true);
			return;
		}

		isFinished = true;
		lblStatus.Text = L("Instalação concluída com sucesso.", "Installation completed successfully.", "Установка успешно завершена.");
		lblStatus.ForeColor = Success;
		chkLaunchNow.Visible = true;
		btnInstall.Text = L("Concluir", "Finish", "Завершить");
		btnInstall.BackColor = Color.FromArgb(22, 163, 74);
		btnInstall.FlatAppearance.MouseOverBackColor = Color.FromArgb(21, 128, 61);
		btnInstall.Enabled = true;
		btnCancel.Visible = false;
	}

	private static bool IsLauncherRunning()
	{
		try
		{
			return Process.GetProcessesByName("yLauncher").Length > 0;
		}
		catch
		{
			return false;
		}
	}

	private static void StopLauncher()
	{
		foreach (Process process in Process.GetProcessesByName("yLauncher"))
		{
			try
			{
				process.Kill();
				process.WaitForExit(3000);
			}
			catch
			{
			}
		}
	}

	private void SetControlsEnabled(bool enabled)
	{
		txtPath.Enabled = enabled && InstallerProgram.GetExistingInstallDir() == null;
		btnBrowse.Enabled = txtPath.Enabled;
		chkDesktop.Enabled = enabled;
		chkStartMenu.Enabled = enabled;
		chkAutoStart.Enabled = enabled;
		chkProtocols.Enabled = enabled;
		btnInstall.Enabled = enabled;
		btnCancel.Enabled = enabled;
	}

	private void LaunchInstalledApp()
	{
		if (!chkLaunchNow.Checked)
		{
			return;
		}
		try
		{
			string launcherPath = Path.Combine(txtPath.Text, "yLauncher.exe");
			if (File.Exists(launcherPath))
			{
				Process.Start(new ProcessStartInfo(launcherPath) { UseShellExecute = true, WorkingDirectory = txtPath.Text });
			}
		}
		catch
		{
		}
	}
}
