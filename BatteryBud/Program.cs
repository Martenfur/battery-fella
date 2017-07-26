using Microsoft.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BatteryBud
{
  static class Program
  {
    public static String version="1.0";
    
    [STAThread]
    static void Main()
    {
      IconContext c=new IconContext();
      Application.Run();
    }
  }


  public class IconContext:ApplicationContext
  {
    //
    private NotifyIcon trayIcon=new NotifyIcon();
    private System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    private PowerStatus pow = SystemInformation.PowerStatus;
    //
    private double percentagePrev=   -1,
                   percentageCurrent=-1;
    //
    MenuItem item_add;
    MenuItem item_remove;
    //
    int updateInterval=1000; //ms
    //
    String autostartLinkLocation=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\BatteryBud.lnk";
    String savefileName=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"\\Battery Bud\\save.sav";
    //
    Image digits;
    int digitWidth;
    int[] digitSep=new int[10];
    //

    /*
     * Initializing stuff.
     */
    public IconContext()
    {
       
      if (pow.BatteryChargeStatus==BatteryChargeStatus.NoSystemBattery)
      {
        //If a user tries to run program from computer with no battery to track... this is stupid. And sad.
        MessageBox.Show("You're trying to run Battery Bud from desktop PC. What were you thinking? :|", "wut", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.ExitThread();
        System.Environment.Exit(1);
        //If a user tries to run program from computer with no battery to track... this is stupid. And sad.
      }
      else
      {
        digitsInit();

        updateBattery(null,null);
        
        //Context menu.
        item_add=   new MenuItem("Add to autostart.",     autostartSet);
        item_remove=new MenuItem("Remove from autostart.",autostartReset);

        MenuItem[] autostart=new MenuItem[] {item_add,item_remove};

        trayIcon.ContextMenu=new ContextMenu(new MenuItem[] {
                             new MenuItem("About",    About),
                             new MenuItem("Autostart",autostart),
                             new MenuItem("Close",    Close)
                                                            });
        //Context menu.

        trayIcon.Visible=true;

        //Loading autostart info.
        try
        {
          FileStream file=System.IO.File.OpenRead(savefileName);
          char autostartEnabled=(char)file.ReadByte();
          file.Close();
          if (autostartEnabled=='1')
          {autostartSet(null,null);}
          else
          {autostartReset(null,null);}
        }
        catch(System.IO.FileNotFoundException e) //Happens when some idiot deletes save file.
        {autostartSet(null,null);}
        catch(System.IO.DirectoryNotFoundException e) //Happens on first launch. 
        {
          System.IO.Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)+"\\Battery Bud");
          autostartSet(null,null);
        }
        //Loading autostart info.

        //TIMER
        timer.Interval=updateInterval;
        timer.Tick+=updateBattery;
        timer.Enabled=true;
        //TIMER
      }
    }

    //MAIN UPDATE FUNCTION////////////////////////////////////////////////////////////////////////////
    private void updateBattery(object sender,EventArgs e)
    {
      percentageCurrent=Math.Round(pow.BatteryLifePercent*100.0);

      if (percentagePrev!=percentageCurrent)
      { 
        //Updating icon.
        if (trayIcon.Icon!=null)
        {trayIcon.Icon.Dispose();}
        
        Image image=iconRender(percentageCurrent);
        trayIcon.Icon=IconFromImage(image);
        image.Dispose();
        //Updating icon.
      }

      percentagePrev=percentageCurrent;
    }
    //MAIN UPDATE FUNCTION////////////////////////////////////////////////////////////////////////////

    //Menu options.
    void About(object sender, EventArgs e)
    {
      MessageBox.Show("Battery Bud v"+Program.version+" by gn.fur.\n"
                      +"Thanks to Konstantin Luzgin and Hans Passant."
                      +"\nContact: foxoftgames@gmail.com","About"); 
    }

    void Close(object sender,EventArgs e)
    {
      trayIcon.Visible=false;
      Application.ExitThread();
      Application.Exit();
    }
    
    /**
     * Checks registry. If there's no autostart key or it's defferent, sets it to proper value.
     * Also writes 1 to savefile.
     */
    void autostartSet(object sender,EventArgs args)
    {
      item_add.Checked=   true;
      item_remove.Checked=false;
      
      RegistryKey rkApp=Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run",true);
      String regVal=(String)rkApp.GetValue("BatteryBud");

      if (regVal==null || !Application.ExecutablePath.Equals(regVal,StringComparison.OrdinalIgnoreCase))
      {
        try
        {rkApp.SetValue("BatteryBud",Application.ExecutablePath);}
        catch(Exception e){}
      }
      rkApp.Close();

      FileStream file=System.IO.File.OpenWrite(savefileName);
      file.WriteByte((byte)'1');
      file.Close();
    }

    /*
     * Deletes registry key and writes 0 to savefile.
     */
    void autostartReset(object sender,EventArgs args)
    {
      item_add.Checked=   false;
      item_remove.Checked=true;
      
      RegistryKey rkApp=Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run",true);

      if (rkApp.GetValue("BatteryBud")!=null)
      {
        try
        {rkApp.DeleteValue("BatteryBud");}
        catch(Exception e){}
      }
      rkApp.Close();

      FileStream file=System.IO.File.OpenWrite(savefileName);
      file.WriteByte((byte)'0');
      file.Close();
    }
    //Menu options.
    
    /**
     * Renders icon using loaded font.
     * Render works from right to left.
     */ 
    public Image iconRender(double number_arg)
    {
      double number=number_arg;

      int x=16;
      Image image=new Bitmap(16,16);
  
      using(Graphics surf=Graphics.FromImage(image))
      {
        while(number!=0)
        {
          double digit=number % 10; //Getting last digit.
          number=(number-digit)/10;

          int xadd=digitWidth-digitSep[(int)digit];
          x-=xadd;
          
          surf.DrawImage(digits,x,0,new Rectangle((int)(digit*digitWidth)+digitSep[(int)digit],0,xadd,16),GraphicsUnit.Pixel); //Some sick math here. : - )       
        }
      }
      return image;
    }

    /*
     * Loads font file and measures digit's width. 
     */
    public void digitsInit()
    {
      try
      {digits=Image.FromFile(AppDomain.CurrentDomain.BaseDirectory+"res\\digits.png");}   
      catch(System.IO.FileNotFoundException e)
      {
        MessageBox.Show("It seems, there's no font file in res directory.",":c", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.ExitThread();
        System.Environment.Exit(1);
      }
      
      digitWidth=(int)Math.Round(digits.Width/10f);

      //Measuring each digit width.
      Bitmap imgBuf=new Bitmap(digits);
      try
      {
        for(int i=0; i<10; i+=1)
        {
          int base_x=i*digitWidth;
          bool found=false;
          for(int x=0; x<digitWidth; x+=1)
          {
            for(int y=0; y<digits.Height; y+=1)
            {
              if (imgBuf.GetPixel(base_x+x,y).A!=0)
              {
                found=true;
                break;
              }
            }

            if (found)
            { 
              digitSep[i]=x;
              break;
            }
          }
        }
      }
      catch(ArgumentOutOfRangeException e) //For retards who will try to give microscopic images to the program.
      {
        MessageBox.Show("This picture is too small to be a font.",":c", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.ExitThread();
        System.Environment.Exit(1);
      }
      //Measuring each digit width.

      imgBuf.Dispose();
    }

    /*
     * Converts Image to Icon using magic I don't really care about at this point.
     * Standart conversion messes up with transparency. Not cool, Microsoft, not cool.
     * Author: Hans Passant
     * https://stackoverflow.com/questions/21387391/how-to-convert-an-image-to-an-icon-without-losing-transparency
     */
    public Icon IconFromImage(Image img)
    {
      var ms = new System.IO.MemoryStream();
      var bw = new System.IO.BinaryWriter(ms);
      // Header
      bw.Write((short)0);   // 0 : reserved
      bw.Write((short)1);   // 2 : 1=ico, 2=cur
      bw.Write((short)1);   // 4 : number of images
      // Image directory
      var w = img.Width;
      if (w >= 256) w = 0;
      bw.Write((byte)w);    // 0 : width of image
      var h = img.Height;
      if (h >= 256) h = 0;
      bw.Write((byte)h);    // 1 : height of image
      bw.Write((byte)0);    // 2 : number of colors in palette
      bw.Write((byte)0);    // 3 : reserved
      bw.Write((short)0);   // 4 : number of color planes
      bw.Write((short)0);   // 6 : bits per pixel
      var sizeHere = ms.Position;
      bw.Write((int)0);     // 8 : image size
      var start = (int)ms.Position + 4;
      bw.Write(start);      // 12: offset of image data
      // Image data
      img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
      var imageSize = (int)ms.Position - start;
      ms.Seek(sizeHere, System.IO.SeekOrigin.Begin);
      bw.Write(imageSize);
      ms.Seek(0, System.IO.SeekOrigin.Begin);

      // And load it
      return new Icon(ms);
    }

  }

}
