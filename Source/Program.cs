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
			// NOTE: Does not work on Linux!
			Shared.ApplicationDataDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), Config.ApplicationDataDirectory );
			if ( !Directory.Exists( Shared.ApplicationDataDirectory ) ) Directory.CreateDirectory( Shared.ApplicationDataDirectory );
			Log.Write( "Persistent data directory is: '{0}'.", Shared.ApplicationDataDirectory );

			// Load .NET user secrets (application credentials)
			// TODO: Only run this in debug mode, otherwise load secrets from command-line arguments
			try {
                Shared.UserSecrets = UserSecrets.Load();
                Log.Write( "Loaded the user secrets for this application." );
            } catch ( Exception exception ) {
                Log.Write( "Failed to load user secrets: '{0}'.", exception.Message );
				Environment.Exit( 1 );
				return;
            }
			
			// Download Cloudflare Tunnel client
			if ( Cloudflare.IsClientDownloaded( Config.CloudflareTunnelVersion, Config.CloudflareTunnelChecksum ) == false ) {
				Log.Write( "Cloudflare Tunnel client does not exist or is invalid, downloading version {0}...", Config.CloudflareTunnelVersion );
				await Cloudflare.DownloadClient( Config.CloudflareTunnelVersion, Config.CloudflareTunnelChecksum );
				Log.Write( "Cloudflare Tunnel client downloaded to: '{0}'.", Cloudflare.GetClientPath( Config.CloudflareTunnelVersion ) );
			} else {
				Log.Write( "Using existing Cloudflare Tunnel client at: '{0}'.", Cloudflare.GetClientPath( Config.CloudflareTunnelVersion ) );
			}

			// Start a Cloudflare Tunnel
			//Log.Write( "Starting Cloudflare Tunnel..." );
			//Uri tunnelUrl = Cloudflare.StartTunnel( Config.CloudflareTunnelVersion );
			//Log.Write( "Cloudflare Tunnel running at: '{0}'.", tunnelUrl.ToString() );

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
			twitchClient.OnChannelLeave += OnChannelLeave;
			twitchClient.OnChatMessage += OnChatMessage;
			twitchClient.OnUserUpdate += OnUserUpdate;
			twitchClient.OnChannelUpdate += OnChannelUpdate;
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

			// TODO: Gracefully stop Cloudflare Tunnel client

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

		private static async Task OnChannelJoin( object sender, Twitch.OnChannelJoinLeaveEventArgs e ) {
			Log.Write( "User '{0}' joined channel '{1}'.", e.User.Global.Name, e.User.Channel.Name );

			//if ( e.IsMe ) await e.User.Channel.Send( twitchClient, "Hello World" );
		}

		private static async Task OnChannelLeave( object sender, Twitch.OnChannelJoinLeaveEventArgs e ) {
			Log.Write( "User '{0}' left channel '{1}'.", e.User.Global.Name, e.User.Channel.Name );
		}

		private static async Task OnChatMessage( object sender, Twitch.OnChatMessageEventArgs e ) {
			Log.Write( "User '{0}' in '{1}' said '{2}'.", e.Message.User.Global.Name, e.Message.Channel.Name, e.Message.Content );

			if ( e.Message.Content == "!hello" ) {
				await e.Message.Channel.Send( twitchClient, "Hello World!" );
			} else if ( e.Message.Content == "!random" ) {
				Random random = new();
				await e.Message.Channel.Send( twitchClient, $"Your random number is {random.Next( 100 )}" );
			} else if ( e.Message.Content == "!cake" ) {
				await e.Message.Channel.Send( twitchClient, $"This was a triumph!\nI'm making a note here: Huge success!\nIt's hard to overstate my satisfaction.\n\nWe do what we must because we can. For the good of all of us. Except the ones who are dead.\n\nBut there's no sense crying over every mistake.\nYou just keep on trying 'til you run out of cake." );
			} else if ( e.Message.Content == "!socials" ) {
				await e.Message.Channel.Send( twitchClient, "You can find me on Twitter! https://twitter.com/RawrelTV" );
			} else if ( e.Message.Content == "!whoami" ) {
				await e.Message.Channel.Send( twitchClient, $"You are {e.Message.User.Global.Name}, your name color is {e.Message.User.Global.Color}, your account identifier is {e.Message.User.Global.Identifier}, you are {( e.Message.User.IsSubscriber == true ? "subscribed" : "not subscribed" )}, you are {( e.Message.User.IsModerator == true ? "a moderator" : "not a moderator" )}." ); // , you {( tagTurbo == "1" ? "have Turbo" : "do not have Turbo" )}
			}
		}

		private static async Task OnUserUpdate( object sender, Twitch.OnUserUpdateEventArgs e ) {
			Log.Write( "User '{0}' ({1}) updated.", e.User.Global.Name, e.User.Global.Identifier );
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
			Log.Write( "Channel '{0}' ({1}) updated.", e.Channel.Name, e.Channel.Identifier );
			Log.Write( " Is Emote Only: '{0}'", e.Channel.IsEmoteOnly );
			Log.Write( " Is Followers Only: '{0}'", e.Channel.IsFollowersOnly );
			Log.Write( " Is Subscribers Only: '{0}'", e.Channel.IsSubscribersOnly );
			Log.Write( " Is R9K: '{0}'", e.Channel.IsR9K );
			Log.Write( " Is Rituals: '{0}'", e.Channel.IsRituals );
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