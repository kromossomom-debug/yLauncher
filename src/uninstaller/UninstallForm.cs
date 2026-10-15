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
		string sysLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
		bool isRu = sysLang == "ru";
		bool isPt = sysLang == "pt";

		Text = isRu ? "Удаление yLauncher" : isPt ? "Desinstalar yLauncher" : "Uninstall yLauncher";
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
			Text = isRu ? "Удаление yLauncher" : isPt ? "Desinstalar yLauncher" : "Uninstall yLauncher",
			Font = new Font("Segoe UI", 15f, FontStyle.Bold),
			ForeColor = Color.White,
			Location = new Point(26, 18),
			AutoSize = true
		});
		header.Controls.Add(new Label
		{
			Text = isRu ? "Удалите приложение без потери личной библиотеки."
				: isPt ? "Remova o aplicativo sem perder sua biblioteca pessoal."
				: "Remove the application without losing your personal library.",
			ForeColor = Color.FromArgb(148, 163, 184),
			Location = new Point(28, 54),
			AutoSize = true
		});
		Controls.Add(header);

		Controls.Add(new Label
		{
			Text = isRu ? "Приложение, ярлыки, автозапуск и зарегистрированные протоколы\nбудут удалены с этого компьютера."
				: isPt ? "O aplicativo, os atalhos, a inicialização automática e os protocolos\nregistrados serão removidos deste computador."
				: "The application, shortcuts, startup items and registered\nprotocols will be removed from this computer.",
			Location = new Point(28, 120),
			Size = new Size(500, 52),
			ForeColor = Color.FromArgb(226, 232, 240)
		});

		cleanDataCheckBox = new CheckBox
		{
			Text = isRu ? "Удалить также обложки, настройки и локальный кэш"
				: isPt ? "Excluir também capas, preferências e cache local"
				: "Also delete covers, preferences and local cache",
			Location = new Point(28, 184),
			Size = new Size(500, 28),
			ForeColor = Color.FromArgb(203, 213, 225),
			Checked = false
		};
		Controls.Add(cleanDataCheckBox);

		Controls.Add(new Label
		{
			Text = isRu ? "Оставьте этот флажок снятым, чтобы сохранить данные для будущей переустановки."
				: isPt ? "Deixe esta opção desmarcada para manter seus dados em uma futura reinstalação."
				: "Leave unchecked to preserve your library for a future reinstall.",
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
			Text = isRu ? "Удалить" : isPt ? "Desinstalar" : "Uninstall",
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
			Text = isRu ? "Отмена" : isPt ? "Cancelar" : "Cancel",
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
		string sysLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
		bool isRu = sysLang == "ru";
		bool isPt = sysLang == "pt";

		uninstallButton.Enabled = false;
		cancelButton.Enabled = false;
		cleanDataCheckBox.Enabled = false;
		statusLabel.Text = isRu ? "Удаление yLauncher..." : isPt ? "Removendo o yLauncher..." : "Removing yLauncher...";

		try
		{
			bool deleteUserData = cleanDataCheckBox.Checked;
			await Task.Run(() => UninstallProgram.PerformUninstall(deleteUserData));
			string doneMsg = deleteUserData
				? (isRu ? "yLauncher и локальные данные были удалены." : isPt ? "O yLauncher e seus dados locais foram removidos." : "yLauncher and its local data have been removed.")
				: (isRu ? "yLauncher удален. Ваша личная библиотека сохранена." : isPt ? "O yLauncher foi removido. Sua biblioteca pessoal foi preservada." : "yLauncher has been removed. Your personal library has been preserved.");
			string doneTitle = isRu ? "Удаление завершено" : isPt ? "Desinstalação concluída" : "Uninstallation complete";

			MessageBox.Show(
				doneMsg,
				doneTitle,
				MessageBoxButtons.OK,
				MessageBoxIcon.Information);
			Close();
		}
		catch (Exception exception)
		{
			statusLabel.Text = isRu ? "Не удалось завершить удаление." : isPt ? "Não foi possível concluir a desinstalação." : "Could not complete the uninstallation.";
			string errTitle = isRu ? "Ошибка удаления" : isPt ? "Falha na desinstalação" : "Uninstallation failed";
			MessageBox.Show(exception.Message, errTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
			uninstallButton.Enabled = true;
			cancelButton.Enabled = true;
			cleanDataCheckBox.Enabled = true;
		}
	}
}
