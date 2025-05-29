
namespace SimpleBatteryDisplay
{
	public static class Strings
	{
		public const string AppName = "Simple Battery Display";


		public const string GreetingTitle = "Hi!";
		public const string GreetingContent = @"Thanks for choosing Battery Bud!"
				+ "\nYour default font is set to {0}."
				+ "\nIf it looks blurry or has the same color as background,"
				+ "\nyou can try out other fonts in context menu. You can also make your own fonts, if you want to.";


		public const string AboutTitle = "About";
		public const string AboutContent = AppName + " " + Program.Version + " Copyright (C) 2025 minkberry." 
			+ "\nThanks to Konstantin Luzgin, Hans Passant and freesound.org."
			+ "\nContact: https://thefoxsociety.net";


		public const string ReminderTitle = "Oh no, I forgot to plug in my laptop! Again!";
		public const string ReminderContent = AppName + " will ring an alarm if your battery gets below a certain percentage "
			+ "so that you definitely remember to plug in your laptop and don't have it suddenly die on you.";
	}
}
