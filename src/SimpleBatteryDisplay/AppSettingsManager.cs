using System.Text.Json;

namespace SimpleBatteryDisplay
{
	public static class AppSettingsManager
	{
		public static string ConfigDirectory => 
			Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				Strings.TechincalAppName
			);

		private static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");


		public static AppSettings Settings = new AppSettings
		{
			AutostartEnabled = true,
			SkinName = "default",
			ReminderEnabled = true,
			ReminderTriggerValue = 5
		};

		public static void Load()
		{
			if (!File.Exists(ConfigPath))
			{
				return;
			}

			var jsonText = File.ReadAllText(ConfigPath);
			var settings = JsonSerializer.Deserialize<AppSettings>(jsonText);

			if (settings != null)
			{
				Settings.AutostartEnabled = settings.AutostartEnabled;
				Settings.SkinName = settings.SkinName;
				Settings.ReminderEnabled = settings.ReminderEnabled;
				Settings.ReminderTriggerValue = settings.ReminderTriggerValue;
			}
		}

		public static void Save()
		{
			var settings = new AppSettings
			{
				AutostartEnabled = Settings.AutostartEnabled,
				SkinName = Settings.SkinName,
				ReminderEnabled = Settings.ReminderEnabled,
				ReminderTriggerValue = Settings.ReminderTriggerValue
			};

			var options = new JsonSerializerOptions { WriteIndented = true };
			var jsonText = JsonSerializer.Serialize(settings, options);
			File.WriteAllText(ConfigPath, jsonText);
		}
	}
}
