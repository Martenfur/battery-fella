using System.Text.Json;

namespace SimpleBatteryDisplay
{
	public static class AppSettingsManager
	{
		private readonly static string _saveFileName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
			"/Battery Bud/config.json";


		public static AppSettings Settings = new AppSettings
		{
			AutostartEnabled = true,
			SkinName = "default",
			ReminderEnabled = true,
			ReminderTriggerValue = 5
		};

		public static void Load()
		{
			if (!File.Exists(_saveFileName))
			{
				return;
			}

			var jsonText = File.ReadAllText(_saveFileName);
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
			File.WriteAllText(_saveFileName, jsonText);
		}
	}
}
