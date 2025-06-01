using NAudio.Wave;
using System.Drawing.Imaging;
using Timer = System.Windows.Forms.Timer;

namespace SimpleBatteryDisplay
{
	public class MainController : ApplicationContext
	{
		private const int _updateInterval = 1000; //ms

		private int[] _digitSep = new int[10];


		private readonly PowerStatus _pow = SystemInformation.PowerStatus;

		// Note that we cannot use CurrentDirectory here as it'll be pointing to system32
		// when the program autostarts.
		private string _resourceDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res");

		private readonly Timer _timer = new Timer();

		private readonly NotifyIcon _trayIcon = new NotifyIcon();
		private Image _digits;
		private int _digitWidth;

		private int _percentagePrev = -1;
		private int _percentageCurrent = -1;

		private WaveStream _waveStream;
		private WaveOutEvent _soundPlayer;

		private bool _reminderDisabledUntilShutdown = false;
		private bool _reminderDisabledUntilCharging = false;
		private ToolStripMenuItem[] _reminderPercentItems;

		private ToolStripMenuItem _reminderDisableUntilChargingItem;

		private ToolStripMenuItem[] _skinContextMenu;

		private ToolStripMenuItem _autostartItem;

		/// <summary>
		/// Initializing stuff.
		/// </summary>
		public MainController()
		{
			try
			{
				_trayIcon.Visible = true;

				// Loading save info.
				try
				{
					AppSettingsManager.Load();

					if (AppSettingsManager.Settings.AutostartEnabled)
					{
						SetAutostart(null, null);
					}
					else
					{
						ResetAutostart(null, null);
					}
				}
				catch (FileNotFoundException) // Happens when some idiot deletes the save file.
				{
					SetAutostart(null, null);
					AppSettingsManager.Settings.SkinName = GetDefaultSkin();
				}
				catch (DirectoryNotFoundException) // Happens on first launch. 
				{
					AppSettingsManager.Settings.SkinName = GetDefaultSkin();
					Directory.CreateDirectory(AppSettingsManager.ConfigDirectory);
					SetAutostart(null, null);
					ShowGreeting();
				}
				// Loading save info.

				if (!InitSkin(AppSettingsManager.Settings.SkinName, true)) // If something fails, abort.
				{
					AppSettingsManager.Settings.SkinName = GetDefaultSkin();
					ShowError("Failed to load custom skin. Resetting to default and trying again.", ":c");

					if (!InitSkin(AppSettingsManager.Settings.SkinName, false))
					{
						Application.ExitThread();
						Environment.Exit(1);
					}
					else
					{
						AppSettingsManager.Save();
					}
				}

				InitContextMenu();


				_waveStream = new WaveFileReader(
					Path.Combine(_resourceDir, "low_battery.wav")
				);
				_soundPlayer = new WaveOutEvent();
				_soundPlayer.Init(_waveStream);

				UpdateBattery(null, null);

				// Timer.
				_timer.Interval = _updateInterval;
				_timer.Tick += UpdateBattery;
				_timer.Enabled = true;
			}
			catch (Exception e)
			{
				ShowError(e.Message + "\n" + e.StackTrace, ":c");
				Application.ExitThread();
				Application.Exit();
			}
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
				if (AppSettingsManager.Settings.ReminderTriggerValue == reminderPercentValues[iInv])
				{
					_reminderPercentItems[i].Checked = true;
				}
			}

			_reminderDisableUntilChargingItem = new ToolStripMenuItem("Disable until plugged in");
			_reminderDisableUntilChargingItem.Click += DisableReminderUntilChargingToggle;

			var reminderMenu = new ToolStripMenuItem[]
			{
				new ToolStripMenuItem("Enable", null, ReminderToggle),
				new ToolStripMenuItem("Disable until shutdown", null, DisableReminderUntilShutdownToggle),
				_reminderDisableUntilChargingItem,
				new ToolStripMenuItem("Ring when the battery is lower than", null, _reminderPercentItems),
				new ToolStripMenuItem("Help", null, ShowReminderHelp),
			};
			reminderMenu[0].Checked = AppSettingsManager.Settings.ReminderEnabled;
			// Reminder.

			_skinContextMenu = GetSkinList();
			foreach (ToolStripMenuItem item in _skinContextMenu)
			{
				item.Checked = (item.Text == AppSettingsManager.Settings.SkinName);
			}

			_autostartItem = new ToolStripMenuItem("Launch on startup", null, ToggleAutostart);
			_trayIcon.ContextMenuStrip = new ContextMenuStrip();
			// Fixes checkboxes being cut off.
			_trayIcon.ContextMenuStrip.ShowCheckMargin = true;

			_trayIcon.ContextMenuStrip.Items.AddRange(
				new[]
				{
					new ToolStripMenuItem("About", null, ShowAbout),
					_autostartItem,
					new ToolStripMenuItem("Skins", null, _skinContextMenu),
					new ToolStripMenuItem("Reminder", null, reminderMenu),
					new ToolStripMenuItem("Quit", null, Close)
				}
			);
			_autostartItem.Checked = AppSettingsManager.Settings.AutostartEnabled;
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
			if (AppSettingsManager.Settings.ReminderEnabled
				&& !_reminderDisabledUntilShutdown
				&& !_reminderDisabledUntilCharging
				&& _percentageCurrent <= AppSettingsManager.Settings.ReminderTriggerValue
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

		private void ToggleAutostart(object sender, EventArgs args)
		{
			if (AppSettingsManager.Settings.AutostartEnabled)
			{
				ResetAutostart(null, null);
			}
			else
			{
				SetAutostart(null, null);	
			}

			if (_autostartItem != null)
			{
				_autostartItem.Checked = AppSettingsManager.Settings.AutostartEnabled;
			}
		}


		/// <summary>
		/// Checks registry. If there's no autostart key or it's different, sets it to proper value.
		/// Also writes 1 to savefile.
		/// </summary>
		private void SetAutostart(object sender, EventArgs args)
		{
			AutostartManager.SetAutostart();

			AppSettingsManager.Settings.AutostartEnabled = true;
			AppSettingsManager.Save();
		}


		/// <summary>
		/// Deletes registry key and writes 0 to savefile
		/// </summary>
		/// <param name="sender">Sender of the event</param>
		/// <param name="args">Event arguments</param>
		private void ResetAutostart(object sender, EventArgs args)
		{
			AutostartManager.ResetAutostart();

			AppSettingsManager.Settings.AutostartEnabled = false;
			AppSettingsManager.Save();
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
				_digits = Image.FromFile(
					Path.Combine(_resourceDir, fontName + ".png")
				);
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
					ShowError("The image is too small to be a font.", ":c");
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

			var dirInfo = new DirectoryInfo(_resourceDir);
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
				AppSettingsManager.Settings.SkinName = skinNameBuf;

				_percentagePrev = -1;
				UpdateBattery(null, null);
				AppSettingsManager.Save();

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

			AppSettingsManager.Settings.ReminderEnabled = !item.Checked;
			item.Checked = !item.Checked;

			AppSettingsManager.Save();
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

			AppSettingsManager.Settings.ReminderTriggerValue = Int32.Parse(currentItem.Text.Replace("%", ""));

			AppSettingsManager.Save();
		}

		#endregion Reminder.



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
				Strings.GreetingContent.Replace("{0}", AppSettingsManager.Settings.SkinName),
				Strings.GreetingTitle
			);
		}


		private static void ShowAbout(object sender, EventArgs e)
		{
			MessageBox.Show(Strings.AboutContent, Strings.AboutTitle);
		}


		private static void ShowReminderHelp(object sender, EventArgs e)
		{
			MessageBox.Show(Strings.ReminderContent, Strings.ReminderTitle);
		}

		#endregion Messages.
	}
}