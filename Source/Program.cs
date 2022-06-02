using System;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TwitchBot {
	public class Program {
		private static readonly ChatClient chatClient = new();
		private static UserAccessToken? userAccessToken;

		public static async Task Main( string[] arguments ) {
			//Logger.LogInformation( "Hello World!" );

			/*using ( ILoggerFactory loggerFactory = LoggerFactory.Create( factoryBuilder => {
				factoryBuilder.AddFilter( "System", LogLevel.Information );
				factoryBuilder.AddConsole();
			} ) ) {
				ILogger logger = loggerFactory.CreateLogger<Program>();
				logger.LogInformation( "Hello World!" );
				logger.LogInformation( "Hello World!" );
				logger.LogWarning( "Hello World!" );
				logger.LogCritical( "Hello World!" );
				logger.LogDebug( "Hello World!" );
				logger.LogDebug( "Hello World!" );
				logger.LogError( "Hello World!" );
				logger.LogTrace( "Hello World!" );
			}*/

			Shared.ApplicationDataDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), Config.ApplicationDataDirectory );
			Shared.UserSecrets = UserSecrets.Load();

			userAccessToken = await UserAccessToken.Fetch();
			Console.WriteLine( "IsValid: {0}", await userAccessToken.IsValid() );

			chatClient.OnConnect += OnConnect;
			//chatClient.OnMessageReceive += OnMessageReceive;
			
			await chatClient.Connect( Config.ChatServerURI );
		}

		private static async Task OnConnect( object sender, OnConnectEventArgs eventArgs ) {
			if ( userAccessToken == null ) throw new Exception( "Connect called without having fetched user access token" );

			await chatClient.RequestCapabilities( new Twitch.Capability[] {
				Twitch.Capability.Commands,
				Twitch.Capability.Membership,
				Twitch.Capability.Tags
			} );

			await chatClient.Authenticate( Shared.UserSecrets.AccountName, userAccessToken.AccessToken );

			//await chatClient.JoinChannel( "streamer chat channel" );
		}
	}
}