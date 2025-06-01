using Microsoft.Win32;

namespace BatteryFella
{
	public class AutostartManager
	{
		private const string _key = Strings.TechincalAppName;

		private static readonly string _appPath =
			'"' + Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, Strings.TechincalAppName + ".exe") + '"';

		public static bool IsAutostartSet => (string)GetKey().GetValue(_key) == _appPath;

		public static void SetAutostart()
		{
			if (!IsAutostartSet)
			{
				GetKey().SetValue(_key, _appPath);
			}
		}

		public static void ResetAutostart()
		{
			if (IsAutostartSet)
			{
				GetKey().DeleteValue(_key);
			}
		}

		private static RegistryKey GetKey() =>
			Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
	}
}
