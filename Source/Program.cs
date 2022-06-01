using System;
using System.Net.WebSockets;
using System.Text;

namespace TwitchBot {
	public class Program {
		public static async Task Main( string[] arguments ) {
			Shared.ApplicationDataDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), Config.ApplicationDataDirectory );
			Shared.UserSecrets = UserSecrets.Load();

			UserAccessToken userAccessToken = await UserAccessToken.Fetch();

			ChatClient chatClient = new();
			await chatClient.Connect( Config.ChatServerURI );
			await chatClient.RequestCapabilities( new string[] { "twitch.tv/commands", "twitch.tv/tags", "twitch.tv/membership" } );
			await chatClient.Authenticate( Shared.UserSecrets.AccountName, userAccessToken.AccessToken );
		}
	}
}