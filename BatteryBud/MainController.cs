using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using NAudio.Wave;

namespace BatteryBud
{
	public class MainController : ApplicationContext
	{
		const int _updateInterval = 1000; //ms

		int[] _digitSep = new int[10];

		
		readonly PowerStatus _pow = SystemInformation.PowerStatus;

		readonly string _saveFileName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
			"\\Battery Bud\\save.sav";
		readonly string _resourceDir = "Resources\\";
		readonly string _registryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

		readonly Timer _timer = new Timer();

		readonly NotifyIcon _trayIcon = new NotifyIcon();
		Image _digits;
		int _digitWidth;

		int _percentagePrev = -1;
		int _percentageCurrent = -1;

		bool _autostartEnabled;
		string _skinName;
		
		WaveStream _waveStream;
		WaveOutEvent _soundPlayer;
		int _reminderTriggerValue = 100;
		int _reminderDefaultTriggerValue = 7;

		
		bool _reminderEnabled = true;
		bool _reminderDisabledUntilShutdown = false;

		MenuItem[] _skinContextMenu;
		MenuItem _itemAdd;
		MenuItem _itemRemove;


		/// <summary>
		/// Initializing stuff.
		/// </summary>
		public MainController() 
		{
			/* Got a report from one user that it doesn't work properly. Disabled for now.
			if (_pow.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery)
			{ 
				// If a user tries to run program from computer with no battery to track... this is stupid. And sad.
				ShowError("You're trying to run Battery Bud from desktop PC. What were you thinking? :|","wut");
				Application.ExitThread();
				Environment.Exit(1);
			}
			*/
			
			
			_trayIcon.Visible = true;

			// Loading save info.
			try
			{
				Load();

				if (_autostartEnabled)
				{
					SetAutostart(null, null);
				}
				else
				{
					ResetAutostart(null, null);
				}
			}
			catch(FileNotFoundException) // Happens when some idiot deletes save file.
			{
				SetAutostart(null, null);
			}
			catch(DirectoryNotFoundException) // Happens on first launch. 
			{
				_skinName = GetDefaultSkin();
				Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Battery Bud");
				SetAutostart(null, null);
				ShowGreeting();
			}
			// Loading save info.

			if (!InitSkin(_skinName, true)) // If something failed, abort.
			{
				_skinName = GetDefaultSkin();
				ShowError("Failed to load custom skin. Resetting to default and trying again.",":c");

				if (!InitSkin(_skinName, false)) 
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


			_waveStream = new WaveFileReader(Environment.CurrentDirectory + "\\" + _resourceDir + "low_battery.wav");
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
			int[] reminderPercents = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75};
			var reminderItems = new MenuItem[reminderPercents.Length];
			for(var i = 0; i < reminderItems.Length; i += 1)
			{
				reminderItems[i] = new MenuItem(reminderPercents[i] + "%");
			}
			
			var reminderMenu = new MenuItem[]
			{
				new MenuItem("Enable", ReminderToggle),
				new MenuItem("Disable until shutdown", DisableReminderUntilShutdownToggle),
				new MenuItem("Ring when battery is lower than", reminderItems),
				new MenuItem("OwO what's this?", ShowReminderHelp),
			};
			reminderMenu[0].Checked = _reminderEnabled;
			// Reminder.


			_itemAdd = new MenuItem("Add to autostart", SetAutostart);
			_itemRemove = new MenuItem("Remove from autostart", ResetAutostart);
			
			_skinContextMenu = GetSkinList();
			foreach(MenuItem item in _skinContextMenu)
			{
				item.Checked = (item.Text == _skinName);
			}

			_trayIcon.ContextMenu = new ContextMenu(
				new []
				{
					new MenuItem("About", ShowAbout),
					new MenuItem("Autostart", new []{_itemAdd, _itemRemove}),
					new MenuItem("Skins", _skinContextMenu),
					new MenuItem("Reminder", reminderMenu),
					new MenuItem("Close", Close)
				}
			);


			
			_itemAdd.Checked = _autostartEnabled;
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
			if (_reminderEnabled
				&& !_reminderDisabledUntilShutdown
				&& _percentageCurrent <= _reminderTriggerValue 
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
					catch(Exception){}
				}
				regKey.Close();
			}

			_autostartEnabled = true;
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
					catch(Exception){}
				}
				regKey.Close();
			}

			_autostartEnabled = false;
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
			catch(FileNotFoundException)
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
				for(var i = 0; i < 10; i += 1)
				{
					var baseX = i * _digitWidth;
					var found = false;

					for(var x = 0; x < _digitWidth; x += 1)
					{
						for(var y = 0; y < _digits.Height; y += 1)
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
			catch(ArgumentOutOfRangeException) // For dumbasses who will try to give microscopic images to the program.
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
		/// array of MenuItems with filenames witout extension.
		/// </summary>
		private MenuItem[] GetSkinList()
		{
			var skinItems = new List<MenuItem>();
			
			var dirInfo = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + _resourceDir);
			foreach(FileInfo file in dirInfo.GetFiles("*.png"))
			{
				skinItems.Add(new MenuItem(Path.GetFileNameWithoutExtension(file.Name), SetSkin));
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
			var skinNameBuf = ((MenuItem)sender).Text;
			
			if (InitSkin(skinNameBuf, false))
			{
				_skinName = skinNameBuf;
				
				_percentagePrev = -1;
				UpdateBattery(null, null);
				Save();    
				
				foreach(MenuItem item in _skinContextMenu)
				{
					item.Checked = false;
				}

				((MenuItem)sender).Checked = true;
			}
		}

		#endregion Skins.



		#region Reminder.
		
		private void ReminderToggle(object sender, EventArgs e)
		{
			var item = ((MenuItem)sender);
			
			_reminderEnabled = !item.Checked;
			item.Checked = !item.Checked;

			Save();
		}



		private void DisableReminderUntilShutdownToggle(object sender, EventArgs e)
		{
			var item = ((MenuItem)sender);
			
			_reminderDisabledUntilShutdown = !item.Checked;
			item.Checked = !item.Checked;
		}



		#endregion Reminder.



		#region Saves handling.

		//TODO: Add reminder data handling, migrate to JSON.

		private void Load()
		{
			var lines = File.ReadAllLines(_saveFileName);
			try
			{
				_autostartEnabled = (lines[0][0] == '1');
				_skinName = lines[1];
			}
			catch(IndexOutOfRangeException) //Support for older save files.
			{
				_autostartEnabled = true;
				_skinName = GetDefaultSkin();
				Save();
			}
		}



		private void Save()
		{
			var autostartChar = '0';
			if (_autostartEnabled)
			{
				autostartChar = '1';
			}
			
			string buf = autostartChar + Environment.NewLine + _skinName;
			File.WriteAllText(_saveFileName, buf, System.Text.Encoding.UTF8);
		}

		#endregion Saves handling.



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

			using(Graphics surf = Graphics.FromImage(image))
			{
				while(number != 0)
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
			MemoryStream ms = new MemoryStream( );
			BinaryWriter bw = new BinaryWriter(ms);
			// Header
			bw.Write((short) 0); // 0 : reserved
			bw.Write((short) 1); // 2 : 1=ico, 2=cur
			bw.Write((short) 1); // 4 : number of images
			// Image directory
			int w = image.Width;
			if (w >= 256)
			{
				w = 0;
			}
			bw.Write((byte) w); // 0 : width of image
			int h = image.Height;
			if (h >= 256)
			{
				h = 0;
			}
			bw.Write((byte) h); // 1 : height of image
			bw.Write((byte) 0); // 2 : number of colors in palette
			bw.Write((byte) 0); // 3 : reserved
			bw.Write((short) 0); // 4 : number of color planes
			bw.Write((short) 0); // 6 : bits per pixel
			long sizeHere = ms.Position;
			bw.Write(0); // 8 : image size
			int start = (int) ms.Position + 4;
			bw.Write(start); // 12: offset of image data
			// Image data
			image.Save(ms, ImageFormat.Png);
			int imageSize = (int) ms.Position - start;
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
				"Your default font is set to " + _skinName + "." + Environment.NewLine + 
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
				"Battery Bud v" + Program.Version + " by gn.fur." + Environment.NewLine + 
				"Thanks to Konstantin Luzgin and Hans Passant." + 
				"\nContact: foxoftgames@gmail.com", 
				
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