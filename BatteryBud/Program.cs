using System;
using System.Windows.Forms;

namespace BatteryBud
{
  internal static class Program
  {
    public static string Version = "1.1";

    [STAThread]
    private static void Main()
    {
      // ReSharper disable once UnusedVariable
      if (Environment.OSVersion.Version.Major >= 6) // Makes context menus look fabulous on any DPI.
      {
				SetProcessDPIAware();
			}
      new MainController();
      Application.Run();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();
  }
}