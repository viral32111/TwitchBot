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
			twitchClient.OnError += OnError;
			twitchClient.OnConnect += OnConnect;
			twitchClient.OnChannelJoin += OnChannelJoin;
			twitchClient.OnChatMessage += OnChatMessage;
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

			Log.Write( "Joining channel '{0}'...", Config.ChannelName );
			await twitchClient.JoinChannel( Config.ChannelName );
		}

		private static async Task OnChannelJoin( object sender, Twitch.OnChannelJoinEventArgs e ) {
			Log.Write( "Joined channel '{0}'.", e.Channel.Name );

			//await e.Channel.Send( "Heyyyy" );
		}

		private static async Task OnChatMessage( object sender, Twitch.OnChatMessageEventArgs e ) {
			Log.Write( "Viewer '{0}' in '{1}' said '{2}'.", e.User, e.Channel.Name, e.Content );

			if ( e.Content == "!hello" ) {
				await e.Channel.Send( "Hello World!" );
			} else if ( e.Content == "!random" ) {
				Random random = new Random();
				await e.Channel.Send( $"Your random number is { random.Next( 100 ) }" );
			} else if ( e.Content == "!cake" ) {
				await e.Channel.Send( $"This was a triumph!\nI'm making a note here: Huge success!\nIt's hard to overstate my satisfaction.\n\nWe do what we must because we can. For the good of all of us. Except the ones who are dead.\n\nBut there's no sense crying over every mistake.\nYou just keep on trying 'til you run out of cake." );
			} else if ( e.Content == "!socials" ) {
				await e.Channel.Send( "You can find me on Twitter! https://twitter.com/RawrelTV" );
			} else if ( e.Content == "!whoami" ) {
				e.Tags.TryGetValue( "mod", out string? tagMod );
				e.Tags.TryGetValue( "subscriber", out string? tagSubscriber );
				e.Tags.TryGetValue( "turbo", out string? tagTurbo );
				e.Tags.TryGetValue( "user-id", out string? tagUserId );
				e.Tags.TryGetValue( "color", out string? tagColor );
				e.Tags.TryGetValue( "display-name", out string? tagDisplayName );

				await e.Channel.Send( $"You are {tagDisplayName}, your name color is {tagColor}, your account identifier is {tagUserId}, you are {( tagSubscriber == "1" ? "subscribed" : "not subscribed")}, you are {( tagMod == "1" ? "a moderator" : "not a moderator" )}, you {( tagTurbo == "1" ? "have Turbo" : "do not have Turbo" )}." );
			}
		}

		private static async Task OnError( object sender, Twitch.OnErrorEventArgs e ) {
			Log.Write( "An error has occurred: '{0}'.", e.Message );

			Log.Write( "Disconnecting..." );
			if ( twitchClient.Connected ) await twitchClient.Disconnect();

			Log.Write( "Exiting..." );
			Environment.Exit( 1 );
		}
	}
}