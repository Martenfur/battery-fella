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
      IconContext iconContext = new IconContext();
      Application.Run();
    }
  }
}