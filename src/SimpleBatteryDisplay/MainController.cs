using Microsoft.Win32;
using NAudio.Wave;
using System.Drawing.Imaging;
using System.Text.Json;
using Timer = System.Windows.Forms.Timer;

namespace SimpleBatteryDisplay
{
	public class MainController : ApplicationContext
	{
		const int _updateInterval = 1000; //ms

		int[] _digitSep = new int[10];


		readonly PowerStatus _pow = SystemInformation.PowerStatus;

		readonly string _saveFileName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
			"\\Battery Bud\\config.json";
		readonly string _resourceDir = "Resources\\";
		readonly string _registryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		readonly Timer _timer = new Timer();

		readonly NotifyIcon _trayIcon = new NotifyIcon();
		Image _digits;
		int _digitWidth;

		int _percentagePrev = -1;
		int _percentageCurrent = -1;

		private AppSettings _settings = new AppSettings
		{
			AutostartEnabled = true,
			SkinName = "default",
			ReminderEnabled = true,
			ReminderTriggerValue = 5
		};

		WaveStream _waveStream;
		WaveOutEvent _soundPlayer;

		bool _reminderDisabledUntilShutdown = false;
		bool _reminderDisabledUntilCharging = false;
		ToolStripMenuItem[] _reminderPercentItems;

		ToolStripMenuItem _reminderDisableUntilChargingItem;

		ToolStripMenuItem[] _skinContextMenu;
		ToolStripMenuItem _itemAdd;
		ToolStripMenuItem _itemRemove;



		/// <summary>
		/// Initializing stuff.
		/// </summary>
		public MainController()
		{
			if (_pow.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery)
			{
				// If a user tries to run program from computer with no battery to track... this is stupid. And sad.
				ShowError("You're trying to run Battery Bud from desktop PC. What were you thinking? :|", "wut");
				//Application.ExitThread();
				//Environment.Exit(1);
			}

			_trayIcon.Visible = true;

			// Loading save info.
			try
			{
				Load();

				if (_settings.AutostartEnabled)
				{
					SetAutostart(null, null);
				}
				else
				{
					ResetAutostart(null, null);
				}
			}
			catch (FileNotFoundException) // Happens when some idiot deletes save file.
			{
				SetAutostart(null, null);
				_settings.SkinName = GetDefaultSkin();
			}
			catch (DirectoryNotFoundException) // Happens on first launch. 
			{
				_settings.SkinName = GetDefaultSkin();
				Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Battery Bud");
				SetAutostart(null, null);
				ShowGreeting();
			}
			// Loading save info.

			if (!InitSkin(_settings.SkinName, true)) // If something failed, abort.
			{
				_settings.SkinName = GetDefaultSkin();
				ShowError("Failed to load custom skin. Resetting to default and trying again.", ":c");

				if (!InitSkin(_settings.SkinName, false))
				{
					Application.ExitThread();
					Environment.Exit(1);
				}
				else
				{
					Save();
				}
			}

			InitContextMenu();


			_waveStream = new WaveFileReader(AppDomain.CurrentDomain.BaseDirectory + "\\" + _resourceDir + "low_battery.wav");
			_soundPlayer = new WaveOutEvent();
			_soundPlayer.Init(_waveStream);

			UpdateBattery(null, null);

			// Timer.
			_timer.Interval = _updateInterval;
			_timer.Tick += UpdateBattery;
			_timer.Enabled = true;

		}



		private void InitContextMenu()
		{
			// Reminder.
			int[] reminderPercentValues = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75 };
			_reminderPercentItems = new ToolStripMenuItem[reminderPercentValues.Length];
			for (var i = 0; i < _reminderPercentItems.Length; i += 1)
			{
				var iInv = _reminderPercentItems.Length - i - 1;
				_reminderPercentItems[i] = new ToolStripMenuItem(reminderPercentValues[iInv] + "%");
				_reminderPercentItems[i].Click += ReminderChangeTriggerValue;
				if (_settings.ReminderTriggerValue == reminderPercentValues[iInv])
				{
					_reminderPercentItems[i].Checked = true;
				}
			}

			_reminderDisableUntilChargingItem = new ToolStripMenuItem("Disable until next charging");
			_reminderDisableUntilChargingItem.Click += DisableReminderUntilChargingToggle;

			var reminderMenu = new ToolStripMenuItem[]
			{
				new ToolStripMenuItem("Enable", null, ReminderToggle),
				new ToolStripMenuItem("Disable until shutdown", null, DisableReminderUntilShutdownToggle),
				_reminderDisableUntilChargingItem,
				new ToolStripMenuItem("Ring when battery is lower than", null, _reminderPercentItems),
				new ToolStripMenuItem("OwO what's this?", null, ShowReminderHelp),
			};
			reminderMenu[0].Checked = _settings.ReminderEnabled;
			// Reminder.


			_itemAdd = new ToolStripMenuItem("Add to autostart", null, SetAutostart);
			_itemRemove = new ToolStripMenuItem("Remove from autostart", null, ResetAutostart);

			_skinContextMenu = GetSkinList();
			foreach (ToolStripMenuItem item in _skinContextMenu)
			{
				item.Checked = (item.Text == _settings.SkinName);
			}

			_trayIcon.ContextMenuStrip = new ContextMenuStrip();
			_trayIcon.ContextMenuStrip.Items.AddRange(
				new[]
				{
					new ToolStripMenuItem("About", null, ShowAbout),
					new ToolStripMenuItem("Autostart", null, new []{_itemAdd, _itemRemove}),
					new ToolStripMenuItem("Skins", null, _skinContextMenu),
					new ToolStripMenuItem("Reminder", null, reminderMenu),
					new ToolStripMenuItem("Close", null, Close)
				}
			);



			_itemAdd.Checked = _settings.AutostartEnabled;
			_itemRemove.Checked = !_itemAdd.Checked;
		}



		/// <summary>
		/// Main update event handler.
		/// </summary>
		/// <param name="sender">Sender of the event.</param>
		/// <param name="e">Event arguments.</param>
		private void UpdateBattery(object sender, EventArgs e)
		{
			//Icon caption.
			if (_pow.BatteryLifeRemaining != -1)
			{
				var timeSpan = TimeSpan.FromSeconds(_pow.BatteryLifeRemaining);
				var remainingTime = string.Format("{0:D2}h {1:D2}m", timeSpan.Hours, timeSpan.Minutes);
				_trayIcon.Text = "Remaining: " + remainingTime + ".";
			}
			else
			{
				_reminderDisabledUntilCharging = false;
				_reminderDisableUntilChargingItem.Checked = false;

				if (_pow.PowerLineStatus != 0)
				{
					_trayIcon.Text = "Charging.";
				}
				else
				{
					_trayIcon.Text = "Calculating remaining time...";
				}
			}
			//Icon caption.

			_percentageCurrent = (int)Math.Round(_pow.BatteryLifePercent * 100.0);

			if (_percentagePrev != _percentageCurrent)
			{
				//Updating icon.
				if (_trayIcon.Icon != null)
				{
					_trayIcon.Icon.Dispose();
				}

				Image image = RenderIcon(_percentageCurrent);
				_trayIcon.Icon = ToIcon(image);
				image.Dispose();
			}

			_percentagePrev = _percentageCurrent;

			// Playing reminder sound.
			if (_settings.ReminderEnabled
				&& !_reminderDisabledUntilShutdown
				&& !_reminderDisabledUntilCharging
				&& _percentageCurrent <= _settings.ReminderTriggerValue
				&& _pow.PowerLineStatus == 0
			)
			{
				_waveStream.Seek(0, SeekOrigin.Begin);
				_soundPlayer.Play();
			}
			// Playing reminder sound.
		}



		private void Close(object sender, EventArgs e)
		{
			_trayIcon.Visible = false;
			_trayIcon.Dispose();

			Application.ExitThread();
			Application.Exit();

			_waveStream.Close();
			_soundPlayer.Dispose();
		}



		#region Autostart.

		/// <summary>
		/// Checks registry. If there's no autostart key or it's defferent, sets it to proper value.
		/// Also writes 1 to savefile.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="args">Event arguments</param>
		private void SetAutostart(object sender, EventArgs args)
		{
			if (_itemAdd != null && _itemRemove != null)
			{
				_itemAdd.Checked = true;
				_itemRemove.Checked = false;
			}

			RegistryKey regKey = Registry.CurrentUser.OpenSubKey(_registryPath, true);
			if (regKey != null)
			{
				var regVal = (string)regKey.GetValue("BatteryBud");

				if (regVal == null || !Application.ExecutablePath.Equals(regVal, StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						regKey.SetValue("BatteryBud", Application.ExecutablePath);
					}
					catch (Exception) { }
				}
				regKey.Close();
			}

			_settings.AutostartEnabled = true;
			Save();
		}



		/// <summary>
		/// Deletes registry key and writes 0 to savefile
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="args">Event arguments</param>
		private void ResetAutostart(object sender, EventArgs args)
		{
			if (_itemAdd != null && _itemRemove != null)
			{
				_itemAdd.Checked = false;
				_itemRemove.Checked = true;
			}

			RegistryKey regKey = Registry.CurrentUser.OpenSubKey(_registryPath, true);

			if (regKey != null)
			{
				if (regKey.GetValue("BatteryBud") != null)
				{
					try
					{
						regKey.DeleteValue("BatteryBud");
					}
					catch (Exception) { }
				}
				regKey.Close();
			}

			_settings.AutostartEnabled = false;
			Save();
		}

		#endregion Autostart.



		#region Skins.

		/// <summary>
		/// Loads font file and measures digit's width
		/// </summary>
		/// <param name="fontName">Name of the font, without extension and full path.</param>
		/// <param name="silent">If true, runs function silently, without error messages.</param>
		/// <returns>true, if load was successful.</returns>
		public bool InitSkin(string fontName, bool silent)
		{
			try
			{
				_digits = Image.FromFile(AppDomain.CurrentDomain.BaseDirectory + _resourceDir + fontName + ".png");
			}
			catch (FileNotFoundException)
			{
				if (!silent)
				{
					ShowError("No font file!", ":c");
				}
				return false;
			}

			_digitWidth = (int)Math.Round(_digits.Width / 10f);



			// Measuring each digit width.
			var imgBuf = new Bitmap(_digits);
			var digitSepBuf = new int[10];

			try
			{
				for (var i = 0; i < 10; i += 1)
				{
					var baseX = i * _digitWidth;
					var found = false;

					for (var x = 0; x < _digitWidth; x += 1)
					{
						for (var y = 0; y < _digits.Height; y += 1)
						{
							if (imgBuf.GetPixel(baseX + x, y).A == 0)
							{
								continue;
							}

							found = true;
							break;
						}

						if (!found)
						{
							continue;
						}

						digitSepBuf[i] = x;
						break;
					}
				}

				digitSepBuf.CopyTo(_digitSep, 0);
			}
			catch (ArgumentOutOfRangeException) // For dumbasses who will try to give microscopic images to the program.
			{
				if (!silent)
				{
					ShowError("Image is too small to be a font.", ":c");
				}
				return false;
			}

			imgBuf.Dispose();

			return true;
		}



		/// <summary>
		/// Scans resource directory for png files and generates 
		/// array of ToolStripMenuItems with filenames witout extension.
		/// </summary>
		private ToolStripMenuItem[] GetSkinList()
		{
			var skinItems = new List<ToolStripMenuItem>();

			var dirInfo = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + _resourceDir);
			foreach (FileInfo file in dirInfo.GetFiles("*.png"))
			{
				skinItems.Add(new ToolStripMenuItem(Path.GetFileNameWithoutExtension(file.Name), null, SetSkin));
			}

			return skinItems.ToArray();
		}



		public string GetDefaultSkin()
		{

			/*
			DPI table:
			100% -  96 dpi - 16 px
			125% - 120 dpi - 24 px // Yeah, this one is kinda ignored.
			150% - 144 dpi - 24 px
			200% - 192 dpi - 32 px
			*/

			Image bmp = new Bitmap(1, 1);
			float dpi = bmp.VerticalResolution;
			bmp.Dispose();

			double iconSize = Math.Min(16 + 8 * Math.Ceiling((dpi - 96f) / 48f), 32);

			return "default" + iconSize;
		}



		/// <summary>
		/// Sets new skin from file, if it exists.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="args">Event arguments</param>
		private void SetSkin(object sender, EventArgs args)
		{
			var skinNameBuf = ((ToolStripMenuItem)sender).Text;

			if (InitSkin(skinNameBuf, false))
			{
				_settings.SkinName = skinNameBuf;

				_percentagePrev = -1;
				UpdateBattery(null, null);
				Save();

				foreach (ToolStripMenuItem item in _skinContextMenu)
				{
					item.Checked = false;
				}

				((ToolStripMenuItem)sender).Checked = true;
			}
		}

		#endregion Skins.



		#region Reminder.

		private void ReminderToggle(object sender, EventArgs e)
		{
			var item = ((ToolStripMenuItem)sender);

			_settings.ReminderEnabled = !item.Checked;
			item.Checked = !item.Checked;

			Save();
		}



		private void DisableReminderUntilShutdownToggle(object sender, EventArgs e)
		{
			var item = ((ToolStripMenuItem)sender);

			_reminderDisabledUntilShutdown = !item.Checked;
			item.Checked = !item.Checked;
		}

		private void DisableReminderUntilChargingToggle(object sender, EventArgs e)
		{
			var item = ((ToolStripMenuItem)sender);

			_reminderDisabledUntilCharging = !item.Checked;
			item.Checked = !item.Checked;
		}


		private void ReminderChangeTriggerValue(object sender, EventArgs e)
		{
			foreach (ToolStripMenuItem item in _reminderPercentItems)
			{
				item.Checked = false;
			}

			var currentItem = ((ToolStripMenuItem)sender);
			currentItem.Checked = true;

			_settings.ReminderTriggerValue = Int32.Parse(currentItem.Text.Replace("%", ""));

			Save();
		}

		#endregion Reminder.



		#region Saving/Loading.

		//TODO: Add reminder data handling, migrate to JSON.
		private void Load()
		{
			if (!File.Exists(_saveFileName))
			{ 
				return;
			}

			var jsonText = File.ReadAllText(_saveFileName);
			var settings = JsonSerializer.Deserialize<AppSettings>(jsonText);

			if (settings != null)
			{
				_settings.AutostartEnabled = settings.AutostartEnabled;
				_settings.SkinName = settings.SkinName;
				_settings.ReminderEnabled = settings.ReminderEnabled;
				_settings.ReminderTriggerValue = settings.ReminderTriggerValue;
			}
		}

		private void Save()
		{
			var settings = new AppSettings
			{
				AutostartEnabled = _settings.AutostartEnabled,
				SkinName = _settings.SkinName,
				ReminderEnabled = _settings.ReminderEnabled,
				ReminderTriggerValue = _settings.ReminderTriggerValue
			};

			var options = new JsonSerializerOptions { WriteIndented = true };
			var jsonText = JsonSerializer.Serialize(settings, options);
			File.WriteAllText(_saveFileName, jsonText);
		}

		#endregion Saving/Loading.



		#region Tray icon.

		/// <summary>
		/// Renders icon using loaded font.
		/// Render works from right to left.
		/// </summary>
		/// <param name="numberToRender">Number to render</param>
		/// <returns>Rendered icon</returns>
		public Image RenderIcon(int numberToRender)
		{
			var number = numberToRender;

			var size = (int)Math.Ceiling(_digits.Height / 8.0) * 8;
			var x = size;

			var bmp = new Bitmap(size, size);
			bmp.SetResolution(_digits.HorizontalResolution, _digits.VerticalResolution);
			Image image = bmp;

			using (Graphics surf = Graphics.FromImage(image))
			{
				while (number != 0)
				{
					int digit = number % 10; // Getting last digit.
					number = (number - digit) / 10;

					int xadd = _digitWidth - _digitSep[digit];
					x -= xadd;
					surf.DrawImage(
						_digits,
						x,
						0,
						new Rectangle(digit * _digitWidth + _digitSep[digit], 0, xadd, size),
						GraphicsUnit.Pixel
					); //Some sick math here. : - )
				}
			}
			return image;
		}



		/// <summary>
		/// Converts Image to Icon using magic I don't really care about at this point.
		/// Standart conversion messes up with transparency. Not cool, Microsoft, not cool.
		/// Author: Hans Passant
		/// https://stackoverflow.com/questions/21387391/how-to-convert-an-image-to-an-icon-without-losing-transparency
		/// </summary>
		/// <param name="image">Image to convert</param>
		/// <returns>Converted icon</returns>
		public Icon ToIcon(Image image)
		{
			MemoryStream ms = new MemoryStream();
			BinaryWriter bw = new BinaryWriter(ms);
			// Header
			bw.Write((short)0); // 0 : reserved
			bw.Write((short)1); // 2 : 1=ico, 2=cur
			bw.Write((short)1); // 4 : number of images
								// Image directory
			int w = image.Width;
			if (w >= 256)
			{
				w = 0;
			}
			bw.Write((byte)w); // 0 : width of image
			int h = image.Height;
			if (h >= 256)
			{
				h = 0;
			}
			bw.Write((byte)h); // 1 : height of image
			bw.Write((byte)0); // 2 : number of colors in palette
			bw.Write((byte)0); // 3 : reserved
			bw.Write((short)0); // 4 : number of color planes
			bw.Write((short)0); // 6 : bits per pixel
			long sizeHere = ms.Position;
			bw.Write(0); // 8 : image size
			int start = (int)ms.Position + 4;
			bw.Write(start); // 12: offset of image data
							 // Image data
			image.Save(ms, ImageFormat.Png);
			int imageSize = (int)ms.Position - start;
			ms.Seek(sizeHere, SeekOrigin.Begin);
			bw.Write(imageSize);
			ms.Seek(0, SeekOrigin.Begin);

			// And load it
			return new Icon(ms);
		}

		#endregion Tray icon.



		#region Messages.

		public void ShowError(string str, string header) =>
			MessageBox.Show(str, header, MessageBoxButtons.OK, MessageBoxIcon.Error);



		public void ShowGreeting()
		{
			MessageBox.Show(
				@"Thanks for choosing Battery Bud! \^0^/" + Environment.NewLine +
				"Your default font is set to " + _settings.SkinName + "." + Environment.NewLine +
				"If it looks blurry or has the same color as background," + Environment.NewLine +
				"you can try out other fonts in context menu. You can also make your own fonts, if you want to.",

				"Sup."
			);
		}



		/// <summary>
		/// About onClick handler.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="e">Event arguments</param>
		private static void ShowAbout(object sender, EventArgs e)
		{
			MessageBox.Show(
				"Battery Bud v2.0 Copyright (C) 2025 minkberry." + Environment.NewLine +
				"Thanks to Konstantin Luzgin, Hans Passant and freesound.org." +
				"\nContact: https://thefoxsociety.net",

				"About"
			);
		}



		/// <summary>
		/// About onClick handler.
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="e">Event arguments</param>
		private static void ShowReminderHelp(object sender, EventArgs e)
		{
			MessageBox.Show(
				"You know, it happens sometimes. Windows tells your battery is almost dead, " +
				"you click OK and continue to watch that important video, until the laptop " +
				"suddenly shuts down. But fear not! Now Battery Bud will ring the alarm when " +
				"battery will be lower than certain percentage.",

				"Oh no, I forgot to plug in my laptop! Again!"
			);
		}

		#endregion Messages.

	}
}