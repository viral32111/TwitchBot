using System.Runtime.InteropServices;

namespace TwitchBot {
	public class Program {
		[DllImport( "Kernel32" )]
		private static extern bool SetConsoleCtrlHandler( EventHandler handler, bool add );
		private delegate bool EventHandler( CtrlType signal );
		private static EventHandler? consoleCtrlHandler;

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
			twitchClient.OnReady += OnReady;
			twitchClient.OnChannelJoin += OnChannelJoin;
			twitchClient.OnChatMessage += OnChatMessage;
			twitchClient.OnUserUpdate += OnUserUpdate;
			twitchClient.OnChannelUpdate += OnChannelUpdate;
			twitchClient.OnUserChannelJoin += OnUserChannelJoin;
			twitchClient.OnUserChannelLeave += OnUserChannelLeave;
			Log.Write( "Registered Twitch client event handlers." );

			consoleCtrlHandler += new EventHandler( OnApplicationExit );
			SetConsoleCtrlHandler( consoleCtrlHandler, true );
			Log.Write( "Registered application exit event handler." );

			// Connect to Twitch chat
			// NOTE: Blocks until the connection is closed
			Log.Write( "Connecting to Twitch chat..." );
			twitchClient.Connect( Config.ChatServerAddress, Config.ChatServerPort );

		}

		private static bool OnApplicationExit( CtrlType signal ) {
			Log.Write( "Disconnecting..." );
			Task disconnectTask = twitchClient.Disconnect();
			disconnectTask.Wait();

			Environment.Exit( 0 );

			return false;
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
		}

		private static async Task OnReady( object sender, Twitch.OnReadyEventArgs e ) {
			Log.Write( "Ready as user '{0}' ({1}).", e.User.Name, e.User.Identifier );

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
				Random random = new();
				await e.Channel.Send( $"Your random number is {random.Next( 100 )}" );
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

				await e.Channel.Send( $"You are {tagDisplayName}, your name color is {tagColor}, your account identifier is {tagUserId}, you are {( tagSubscriber == "1" ? "subscribed" : "not subscribed" )}, you are {( tagMod == "1" ? "a moderator" : "not a moderator" )}, you {( tagTurbo == "1" ? "have Turbo" : "do not have Turbo" )}." );
			}
		}

		private static async Task OnUserUpdate( object sender, Twitch.OnUserUpdateEventArgs e ) {
			Log.Write( "User '{0}' updated.", e.User.Global.Name );
			Log.Write( " Identifier: '{0}'", e.User.Global.Identifier );
			Log.Write( " Type: '{0}'", e.User.Global.Type );
			Log.Write( " Color: '{0}'", e.User.Global.Color );
			Log.Write( " Badges: '{0}'", e.User.Global.Badges != null ? string.Join( ',', e.User.Global.Badges ) : null );
			Log.Write( " Badge Information: '{0}'", e.User.Global.BadgeInformation );
			Log.Write( " Emote Sets: '{0}'", e.User.Global.EmoteSets != null ? string.Join( ',', e.User.Global.EmoteSets ) : null );
			Log.Write( " Channel: '{0}'", e.User.Channel.Name );
			Log.Write( "  Is Moderator: '{0}'", e.User.IsModerator );
			Log.Write( "  Is Subscriber: '{0}'", e.User.IsSubscriber );
		}

		private static async Task OnChannelUpdate( object sender, Twitch.OnChannelUpdateEventArgs e ) {
			Log.Write( "Channel '{0}' updated.", e.Channel.Name );
			Log.Write( " Is Emote Only: '{0}'", e.Channel.IsEmoteOnly );
			Log.Write( " Is Followers Only: '{0}'", e.Channel.IsFollowersOnly );
			Log.Write( " Is Subscribers Only: '{0}'", e.Channel.IsSubscribersOnly );
			Log.Write( " Is R9K: '{0}'", e.Channel.IsR9K );
			Log.Write( " Is Rituals: '{0}'", e.Channel.IsRituals );
		}

		private static async Task OnUserChannelJoin( object sender, Twitch.OnUserChannelJoinEventArgs e ) {
			Log.Write( "User '{0}' joined channel '{1}'.", e.User.Global.Name, e.User.Channel.Name );
		}

		private static async Task OnUserChannelLeave( object sender, Twitch.OnUserChannelLeaveEventArgs e ) {
			Log.Write( "User '{0}' left channel '{1}'.", e.User.Global.Name, e.User.Channel.Name );
		}

		private static async Task OnError( object sender, Twitch.OnErrorEventArgs e ) {
			Log.Write( "An error has occurred: '{0}'.", e.Message );

			Log.Write( "Disconnecting..." );
			await twitchClient.Disconnect();

			Log.Write( "Exiting..." );
			Environment.Exit( 1 );
		}
	}

	public enum CtrlType {
		CTRL_C_EVENT = 0,
		CTRL_BREAK_EVENT = 1,
		CTRL_CLOSE_EVENT = 2,
		CTRL_LOGOFF_EVENT = 5,
		CTRL_SHUTDOWN_EVENT = 6
	}
}