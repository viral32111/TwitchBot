using System;
using System.Net.WebSockets;
using System.Text;

namespace TwitchBot {
	public class Program {
		private static readonly ChatClient chatClient = new();
		private static UserAccessToken? userAccessToken;

		public static async Task Main( string[] arguments ) {
			Shared.ApplicationDataDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), Config.ApplicationDataDirectory );
			Shared.UserSecrets = UserSecrets.Load();

			userAccessToken = await UserAccessToken.Fetch();

			chatClient.OnConnect += OnConnect;
			//chatClient.OnMessageReceive += OnMessageReceive;
			
			await chatClient.Connect( Config.ChatServerURI );
		}

		private static async Task OnConnect( object sender, OnConnectEventArgs eventArgs ) {
			if ( userAccessToken == null ) throw new Exception( "Connect called without having fetched user access token" );

			bool hasCapabilities = await chatClient.RequestCapabilities( new string[] { "twitch.tv/commands", "twitch.tv/tags", "twitch.tv/membership" } );
			bool isAuthenticated = await chatClient.Authenticate( Shared.UserSecrets.AccountName, userAccessToken.AccessToken );

			//await chatClient.JoinChannel( "streamer chat channel" );
		}
	}
}