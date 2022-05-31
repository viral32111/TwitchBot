using System;

namespace TwitchBot {
	public class Program {
		public static async Task Main( string[] arguments ) {
			Shared.ApplicationDataDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), Config.ApplicationDataDirectory );
			Shared.UserSecrets = UserSecrets.Load();

			//UserAccessToken userAccessToken = await UserAccessToken.Fetch();
			//Console.WriteLine( userAccessToken.AccessToken );
		}
	}
}