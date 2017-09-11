using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Collections.Generic;

namespace BatteryBud
{
  // TODO: make localization and delete this line
  [SuppressMessage("ReSharper", "LocalizableElement")]
  public class IconContext : ApplicationContext
  {
    private const int UPDATE_INTERVAL = 1000; //ms

    private int[] _digitSep = new int[10];

    private readonly MenuItem _itemAdd;
    private readonly MenuItem _itemRemove;

    private readonly PowerStatus _pow = SystemInformation.PowerStatus;

    private readonly string _saveFileName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                            "\\Battery Bud\\save.sav";
    private readonly string _resDir="res\\";

    private readonly Timer _timer = new Timer();

    private readonly NotifyIcon _trayIcon = new NotifyIcon();

    private Image _digits;

    private int _digitWidth;

    private int
      _percentagePrev = -1,
      _percentageCurrent = -1;

    private char _autostartState;
    private string _skinName;

    MenuItem[] _skinContextMenu;

    /// <summary>
    ///   Initializing stuff
    /// </summary>
    public IconContext() 
    {
      
      if (_pow.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery)
      { 
        // If a user tries to run program from computer with no battery to track... this is stupid. And sad.
        ShowError("You're trying to run Battery Bud from desktop PC. What were you thinking? :|","wut");
        Application.ExitThread();
        Environment.Exit(1);
      }
      
      // Context menu.
      _itemAdd = new MenuItem("Add to autostart.", SetAutostart);
      _itemRemove = new MenuItem("Remove from autostart.", ResetAutostart);

      MenuItem[] autostart = { _itemAdd, _itemRemove };

      _skinContextMenu = ContextMenuGetFromResFolder();

      _trayIcon.ContextMenu = new ContextMenu(new[]
      {
        new MenuItem("About", About),
        new MenuItem("Autostart", autostart),
        new MenuItem("Skins", _skinContextMenu),
        new MenuItem("Close", Close)
      });
      _trayIcon.Visible = true;

      // Loading save info.
      try
      {
        Load();

        if (_autostartState == '1')
        {SetAutostart(null, null);}
        else
        {ResetAutostart(null, null);}
      }
      catch (FileNotFoundException) // Happens when some idiot deletes save file.
      {SetAutostart(null, null);}
      catch (DirectoryNotFoundException) // Happens on first launch. 
      {
        _skinName=DefaultSkinGet();
        Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                  "\\Battery Bud");
        SetAutostart(null, null);
        ShowGreeting();
      }
      // Loading save info.

      if (!InitSkin(_skinName,true)) // If something failed, abort.
      {
        _skinName=DefaultSkinGet();
        ShowError("Failed to load custom skin. Resetting to default and trying again.",":c");

        if (!InitSkin(_skinName,false)) 
        {
          Application.ExitThread();
          Environment.Exit(1);
        }
        else
        {Save();}
      }
      
      for(int i=0; i<_skinContextMenu.Length; i+=1)
      {_skinContextMenu[i].Checked = _skinContextMenu[i].Text.Equals(_skinName);}

      UpdateBattery(null, null);

      // Timer
      _timer.Interval = UPDATE_INTERVAL;
      _timer.Tick += UpdateBattery;
      _timer.Enabled = true;
    }



    public void ShowError(string str, string header)
    {
      MessageBox.Show(str, header,
                       MessageBoxButtons.OK, MessageBoxIcon.Error);
    }



    public void ShowGreeting()
    {
      MessageBox.Show("Thanks for using Battery Bud! \\^0^/" + Environment.NewLine +
                      "Your default font is set to " + _skinName + "." + Environment.NewLine + 
                      "If it looks blurry or has the same color as background," + Environment.NewLine + 
                      "you can try out other fonts in context menu. You can also make your own fonts, if you want to.", "Sup.");
    }




    /// <summary>
    ///   Main update event handler.
    /// </summary>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="e">Event arguments.</param>
    private void UpdateBattery(object sender, EventArgs e)
    {
      //Icon caption.
      if (_pow.BatteryLifeRemaining!=-1)
      {_trayIcon.Text = "Remaining: " + SecondsToTimeStr(_pow.BatteryLifeRemaining) + ".";}
      else
      {
        if (_pow.PowerLineStatus != 0)
        {_trayIcon.Text = "Charging." + SecondsToTimeStr(120);}
        else
        {_trayIcon.Text = "Calculating remaining time...";}
      }
      //Icon caption.

      _percentageCurrent = (int) Math.Round(_pow.BatteryLifePercent * 100.0);

      if (_percentagePrev != _percentageCurrent)
      {
        //Updating icon.
        if (_trayIcon.Icon!=null)
        {_trayIcon.Icon.Dispose();}

        Image image = RenderIcon(_percentageCurrent);
        _trayIcon.Icon = ToIcon(image);
        image.Dispose();
      }

      _percentagePrev = _percentageCurrent;
    }



    /// <summary>
    ///   Converts given amount of seconds into a string it the format "0h 0m."  
    /// </summary>
    /// <param name="seconds">Amount of time.</param>
    private String SecondsToTimeStr(int seconds)
    {
      double minutes=Math.Floor((double)seconds/60);
      double hours=Math.Floor(minutes/60);
      minutes-=hours*60;

      if (hours==0)
      {return minutes + "m";}

      return hours + "h " + minutes + "m";
    }



    /// <summary>
    ///   About onClick handler
    /// </summary>
    /// <param name="sender">Sender of the event</param>
    /// <param name="e">Event arguments</param>
    private static void About(object sender, EventArgs e)
    {
      MessageBox.Show("Battery Bud v" + Program.Version + " by gn.fur.\n"
                      + "Thanks to Konstantin Luzgin and Hans Passant."
                      + "\nContact: foxoftgames@gmail.com", "About");
    }



    private void Close(object sender, EventArgs e)
    {
      _trayIcon.Visible = false;
      Application.ExitThread( );
      Application.Exit( );
    }



    /// <summary>
    ///   Checks registry. If there's no autostart key or it's defferent, sets it to proper value.
    ///   Also writes 1 to savefile.
    /// </summary>
    /// <param name="sender">Sender of the event</param>
    /// <param name="args">Event arguments</param>
    private void SetAutostart(object sender, EventArgs args)
    {
      _itemAdd.Checked = true;
      _itemRemove.Checked = false;

      RegistryKey rkApp =
        Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
      if (rkApp != null)
      {
        string regVal = (string) rkApp.GetValue("BatteryBud");

        if (regVal == null || !Application.ExecutablePath.Equals(regVal, StringComparison.OrdinalIgnoreCase))
        {
          try
          {rkApp.SetValue("BatteryBud", Application.ExecutablePath);}
          catch (Exception)
          {
            // ignored
          }
        }
        rkApp.Close();
      }

      _autostartState='1';
      Save();
    }



    /// <summary>
    ///   Deletes registry key and writes 0 to savefile
    /// </summary>
    /// <param name="sender">Sender of the event</param>
    /// <param name="args">Event arguments</param>
    private void ResetAutostart(object sender, EventArgs args)
    {
      _itemAdd.Checked = false;
      _itemRemove.Checked = true;

      RegistryKey rkApp =
        Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

      if (rkApp != null)
      {
        if (rkApp.GetValue("BatteryBud") != null)
        {
          try
          {rkApp.DeleteValue("BatteryBud");}
          catch (Exception)
          {
            // ignored
          }
        }
        rkApp.Close();
      }

      _autostartState='0';
      Save();
    }



    /// <summary>
    ///   Scans resource directory for png files and generates 
    ///   array of MenuItems with filenames witout extension.
    /// </summary>
    private MenuItem[] ContextMenuGetFromResFolder()
    {
      string[] skins = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + _resDir, "*.png", System.IO.SearchOption.TopDirectoryOnly);
      List<MenuItem> skinItems = new List<MenuItem>();
      
      int resDirPathL=AppDomain.CurrentDomain.BaseDirectory.Length + _resDir.Length;

      for(int i=0; i<skins.Length; i+=1)
      {skinItems.Add(new MenuItem(skins[i].Substring(resDirPathL, skins[i].Length - resDirPathL - 4),SetSkin));} //4 is length of ".png" string.

      return skinItems.ToArray();
      
    }



    /// <summary>
    ///   Sets new skin from file, if it exists.
    /// </summary>
    /// <param name="sender">Sender of the event</param>
    /// <param name="args">Event arguments</param>
    private void SetSkin(object sender, EventArgs args)
    {
      string skinNameBuf = ((MenuItem)sender).Text;
      
      if (InitSkin(skinNameBuf,false))
      {
        _skinName = skinNameBuf;
        
        _percentagePrev = -1;
        UpdateBattery(null, null);
        Save();    
        
        for(int i=0; i<_skinContextMenu.Length; i+=1)
        {_skinContextMenu[i].Checked = false;}
        ((MenuItem)sender).Checked = true;
      }
    }



    public string DefaultSkinGet()
    {

      /*
      DPI table:
      100% -  96 dpi - 16 px
      125% - 120 dpi - 24 px //Yeah, this one is kinda ignored.
      150% - 144 dpi - 24 px
      200% - 192 dpi - 32 px
      */ 
       
      Image bmp = new Bitmap(1,1);
      float dpi = bmp.VerticalResolution;
      bmp.Dispose();
      
      double iconSize = Math.Min(16 + 8*Math.Ceiling((dpi - 96f) / 48f), 32);

      return "default" + iconSize;
    }



    /// <summary>
    ///   Renders icon using loaded font.
    ///   Render works from right to left.
    /// </summary>
    /// <param name="numberToRender">Number to render</param>
    /// <returns>Rendered icon</returns>
    public Image RenderIcon(int numberToRender)
    {
      int number = numberToRender;

      int size = (int)Math.Ceiling(((double)_digits.Height)/8.0)*8;
      int x = size;

      Bitmap bmp = new Bitmap(size, size);
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
          surf.DrawImage(_digits, x, 0,
                         new Rectangle(digit * _digitWidth + _digitSep[digit], 0, xadd, size),
                         GraphicsUnit.Pixel); //Some sick math here. : - )
        }
      }
      return image;
    }



    /// <summary>
    ///   Loads font file and measures digit's width
    /// </summary>
    /// <param name="fontName">Name of the font, without extension and full path.</param>
    /// <param name="silent">If true, runs function silently, without error messages.</param>
    /// <returns>true, if load was successful.</returns>
    public bool InitSkin(string fontName,bool silent)
    {
      try
      {_digits = Image.FromFile(AppDomain.CurrentDomain.BaseDirectory + _resDir + fontName + ".png");}
      catch (FileNotFoundException)
      {
        if (!silent)
        {ShowError("No font file!",":c");}
        return false;
      }

      _digitWidth = (int)Math.Round(_digits.Width / 10f);



      // Measuring each digit width.
      Bitmap imgBuf = new Bitmap(_digits);
      int[] digitSepBuf = new int[10];
      
      try
      {
        for (int i = 0; i < 10; i += 1)
        {
          int baseX = i * _digitWidth;
          bool found = false;
          for (int x = 0; x < _digitWidth; x += 1)
          {
            for (int y = 0; y < _digits.Height; y += 1)
            {
              if (imgBuf.GetPixel(baseX + x, y).A == 0)
              {continue;}

              found = true;
              break;
            }

            if (!found)
            {continue;}
            
            digitSepBuf[i] = x;
            break;
          }
        }

        digitSepBuf.CopyTo(_digitSep,0);
      }
      catch (ArgumentOutOfRangeException) // For retards who will try to give microscopic images to the program.
      {
        if (!silent)
        {ShowError("Image is too small to be a font.",":c");}
        return false;
      }

      imgBuf.Dispose();

      return true;
    }



    private void Load()
    {
      string[] lines=File.ReadAllLines(_saveFileName);
      try
      {
        _autostartState = lines[0][0];
        _skinName = lines[1];
      }
      catch(IndexOutOfRangeException) //Support for older save files.
      {
        _autostartState = '1';
        _skinName = DefaultSkinGet();
        Save();
      }
    }



    private void Save()
    {
      string buf = _autostartState + Environment.NewLine + _skinName;
      File.WriteAllText(_saveFileName, buf, System.Text.Encoding.UTF8);
    }



    /// <summary>
    ///   Converts Image to Icon using magic I don't really care about at this point.
    ///   Standart conversion messes up with transparency. Not cool, Microsoft, not cool.
    ///   Author: Hans Passant
    ///   https://stackoverflow.com/questions/21387391/how-to-convert-an-image-to-an-icon-without-losing-transparency
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
  }
}