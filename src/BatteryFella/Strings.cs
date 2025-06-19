
namespace BatteryFella
{
	public static class Strings
	{
		public const string TechincalAppName = "batteryfella";
		public const string AppName = "Battery Fella";


		public const string GreetingTitle = "Hi!";
		public const string GreetingContent = @"Thanks for choosing " + AppName + "!"
				+ "\nYour default font is set to {0}."
				+ "\nIf it looks blurry or has the same color as the background,"
				+ "\nyou can try out other fonts in the context menu.";


		public const string AboutTitle = "About";
		public const string AboutContent = AppName + " " + Program.Version + " Copyright (C) 2025 minkberry." 
			+ "\nSFX by Ne1gh."
			+ "\nSpecial thanks to Konstantin Luzgin and Hans Passant."
			+ "\nContact: https://thefoxsociety.net";


		public const string ReminderTitle = "Oh no, I forgot to plug in my laptop! Again!";
		public const string ReminderContent = AppName + " will ring an alarm if your battery gets below a certain percentage "
			+ "so that you definitely remember to plug in your laptop and don't have it suddenly die on you.";
	}
}
