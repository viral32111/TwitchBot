namespace TwitchBot {
	public class Program {
		private static readonly Twitch.Client twitchClient = new();
		private static UserAccessToken? userAccessToken = null;

		public static async Task Main( string[] arguments ) {

			// Set the persistent data directory
			Shared.ApplicationDataDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), Config.ApplicationDataDirectory );
			Log.Write( "Persistent data directory is: '{0}'.", Shared.ApplicationDataDirectory );

			// Load .NET user secrets (application credentials)
			// TODO: Only run this in debug mode, otherwise load secrets from command-line arguments
			Shared.UserSecrets = UserSecrets.Load();
			Log.Write( "Loaded the user secrets for this application." );

			// Attempt to load an existing user access token from disk
			try {
				userAccessToken = await UserAccessToken.Load();

				// If the token is no longer valid, then refresh & save it
				if ( !await userAccessToken.IsValid() ) {
					Log.Write( "The user access token is no longer valid. Refreshing it..." );
					await userAccessToken.Refresh();

					Log.Write( "Saving the updated user access token..." );
					await userAccessToken.Save();
				}

			// If loading an existing token fails, then request & save a fresh one
			} catch ( Exception exception ) {
				Log.Write( "Failed to load the user access token: '{0}'. Requesting a new one...", exception.Message );
				userAccessToken = await UserAccessToken.Request( new string[] { "chat:read", "chat:edit" } );

				Log.Write( "Saving the new user access token..." );
				await userAccessToken.Save();
			}

			// Register event handlers for the Twitch client
			twitchClient.OnConnect += OnConnect;
			Log.Write( "Registered Twitch client event handlers." );

			// Connect to Twitch chat
			// NOTE: Blocks until the connection is closed
			Log.Write( "Connecting to Twitch chat..." );
			twitchClient.Connect( Config.ChatServerAddress, Config.ChatServerPort );

		}

		private static async Task OnConnect( object sender, EventArgs e ) {
			if ( userAccessToken == null ) throw new Exception( "Connect event ran without previously fetching user access token" );

			Log.Write( "Requesting capabilities..." );
			await twitchClient.RequestCapabilities( new string[] {
				Twitch.Capability.Commands,
				Twitch.Capability.Membership,
				Twitch.Capability.Tags
			} );

			Log.Write( "Authenticating..." );
			await twitchClient.Authenticate( Shared.UserSecrets.AccountName, userAccessToken.AccessToken );

			//Log.Write( "Joining channel '{0}'...", Config.ChannelName );
			//Twitch.Channel channel = await twitchClient.JoinChannel( Config.ChannelName );
		}
	}
}