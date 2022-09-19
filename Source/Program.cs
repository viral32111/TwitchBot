using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TwitchBot.Features;
using TwitchBot.Twitch.OAuth;
using TwitchBot.Twitch;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace TwitchBot {
	public class Program {

		// Windows-only
		[DllImport( "Kernel32" )]
		private static extern bool SetConsoleCtrlHandler( EventHandler handler, bool add );
		private delegate bool EventHandler( CtrlType signal );
		private static EventHandler? consoleCtrlHandler;

		private static readonly Client client = new();

		// The main entry-point of the program
		public static async Task Main( string[] arguments ) {

			// Display application name and version
			AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
			Log.Info( "This is {0}, version {1}.{2}.{3}.", assemblyName.Name, assemblyName.Version?.Major, assemblyName.Version?.Minor, assemblyName.Version?.Build );

			// Display directory paths for convenience
			Log.Info( "Data directory is: '{0}'.", Config.DataDirectory );
			Log.Info( "Cache directory is: '{0}'.", Config.CacheDirectory );

			// Create required directories
			Shared.CreateDirectories();

			// Deprecation notice for the stream history file
			string streamHistoryFile = Path.Combine( Config.DataDirectory, "stream-history.json" );
			if ( File.Exists( streamHistoryFile ) ) Log.Warn( "The stream history file ({0}) is deprecated, it can safely be deleted.", streamHistoryFile );

			// Exit now if this launch was only to initialize files
			if ( arguments.Contains( "--init" ) ) {
				Log.Info( "Initialized configuration & directories, exiting..." );
				Environment.Exit( 0 );
				return;
			}

			// Ensure the OAuth identifier & secret exists
			if ( string.IsNullOrEmpty( Config.TwitchOAuthIdentifier ) || string.IsNullOrEmpty( Config.TwitchOAuthSecret ) ) {
				Console.WriteLine( "Could not load Twitch application Client ID and/or secret from the configuration file!" );
				Environment.Exit( 1 );
				return;
			}

			// Download the Cloudflare Tunnel client
			if ( !Cloudflare.IsClientDownloaded( Config.CloudflareTunnelVersion, Config.CloudflareTunnelChecksum ) ) {
				Log.Info( "Cloudflare Tunnel client does not exist or is corrupt, downloading version {0}...", Config.CloudflareTunnelVersion );
				await Cloudflare.DownloadClient( Config.CloudflareTunnelVersion, Config.CloudflareTunnelChecksum );
				Log.Info( "Cloudflare Tunnel client downloaded to: '{0}'.", Cloudflare.GetClientPath( Config.CloudflareTunnelVersion ) );
			} else {
				Log.Info( "Using cached Cloudflare Tunnel client at: '{0}'.", Cloudflare.GetClientPath( Config.CloudflareTunnelVersion ) );
			}

			// Open the connection to the database
			await Database.Open();
			Log.Info( "Opened connection to the database ({0}).", Database.GetServerVersion() );

			// Setup tables in the database
			await Database.SetupTables();
			Log.Info( "Setup tables in the database." );

			// Open Redis connection
			await Redis.Open();
			Log.Info( "Connected to Redis." );

			// Attempt to load an existing user access token from disk
			try {
				Log.Info( "Loading user access token from: '{0}'...", Shared.UserAccessTokenFilePath );
				Shared.UserAccessToken = UserAccessToken.Load( Shared.UserAccessTokenFilePath );

				// If the token is no longer valid, then refresh & save it
				if ( !await Shared.UserAccessToken.Validate() ) {

					Log.Info( "The user access token is no longer valid, refreshing it..." );
					await Shared.UserAccessToken.DoRefresh();

					Log.Info( "Saving the refreshed user access token..." );
					Shared.UserAccessToken.Save( Shared.UserAccessTokenFilePath );

				} else {
					Log.Info( "The user access token is still valid, no refresh required." );
				}

			} catch ( FileNotFoundException ) {
				Log.Info( "User access token file does not exist, requesting fresh token..." );
				Shared.UserAccessToken = await UserAccessToken.RequestAuthorization( Config.TwitchOAuthRedirectURL, Config.TwitchOAuthScopes );
				Shared.UserAccessToken.Save( Shared.UserAccessTokenFilePath );
			}

			// Fetch this account's name
			JsonObject userResponse = await API.Request( "users" );
			string? accountName = userResponse[ "data" ]?[ 0 ]?[ "display_name" ]?.ToString();
			if ( string.IsNullOrEmpty( accountName ) ) throw new Exception( "Failed to fetch account name." );
			Shared.MyAccountName = accountName;
			Log.Info( "My account name is: '{0}' ({1}, {2}).", Shared.MyAccountName, userResponse[ "data" ]?[ 0 ]?[ "id" ]?.ToString(), userResponse[ "data" ]?[ 0 ]?[ "created_at" ]?.ToString() );

			// Register event handlers for the Twitch client
			client.OnConnect += OnConnect;
			client.OnSecureCommunication += OnSecureCommunication;
			client.OnReady += OnReady;
			client.OnChannelJoin += OnChannelJoin;
			client.OnChannelLeave += OnChannelLeave;
			client.OnChatMessage += OnChatMessage;
			client.OnUserUpdate += OnUserUpdate;
			client.OnChannelUpdate += OnChannelUpdate;
			Log.Info( "Registered Twitch client event handlers." );

			// TODO: Solution for Linux & Docker environment stop signal
			if ( Shared.IsWindows() ) {
				consoleCtrlHandler += new EventHandler( OnApplicationExit );
				SetConsoleCtrlHandler( consoleCtrlHandler, true );
				Log.Info( "Registered application exit event handler." );
			}

			// Connect to Twitch chat
			Log.Info( "Connecting to Twitch chat..." );
			await client.ConnectAsync( Config.TwitchChatBaseURL );

		}

		private static bool OnApplicationExit( CtrlType signal ) {

			//Log.Info( "Stopping Cloudflare Tunnel client..." );
			//Cloudflare.StopTunnel();

			// Close the connection to the database
			Database.Close().Wait();
			Log.Info( "Closed connection to the database." );

			// Close Redis connection
			Redis.Close().Wait();
			Log.Info( "Disconnected from Redis." );

			// Close chat connection
			Log.Info( "Disconnecting..." );
			client.CloseAsync().Wait();

			// Exit application
			Log.Info( "Exiting..." );
			Environment.Exit( 0 );

			return false;

		}

		private static async Task OnConnect( Client client ) {

			if ( Shared.UserAccessToken == null ) throw new Exception( "Connect event ran without previously fetching user access token" );

			Log.Info( "Requesting capabilities..." );
			await client.RequestCapabilities( new string[] {
				Capability.Commands,
				Capability.Membership,
				Capability.Tags
			} );

			Log.Info( "Authenticating..." );

			await client.Authenticate( Shared.MyAccountName!, Shared.UserAccessToken.Access );
		}

		private static async Task OnSecureCommunication( InternetRelayChat.Client client, X509Certificate serverCertificate, SslProtocols protocol, CipherAlgorithmType cipherAlgorithm, int cipherStrength ) {
			
			string protocolName = Shared.SslProtocolNames[ protocol ];
			string cipherName = Shared.CipherAlgorithmNames[ cipherAlgorithm ];

			Console.WriteLine( $"Started secure communication with '{serverCertificate.Subject}' (verified by '{serverCertificate.Issuer}' until {serverCertificate.GetExpirationDateString()}), using {protocolName} ({cipherName}-{cipherStrength})." );

		}

		private static async Task OnReady( Client client, GlobalUser user ) {

			Log.Info( "Ready as user '{0}' ({1}).", user.Name, user.Identifier );

			if ( !string.IsNullOrEmpty( Config.TwitchChatPrimaryChannelName ) ) {
				Log.Info( "Joining channel '{0}'...", Config.TwitchChatPrimaryChannelName );
				await client.JoinChannel( Config.TwitchChatPrimaryChannelName );
			} else {
				Log.Warn( "No primary channel configured to join." );
			}

		}

		private static async Task OnChannelJoin( Client client, User user, Channel channel, bool isMe ) {

			Log.Info( "User '{0}' joined channel '{1}'.", user.Global.Name, user.Channel.Name );

			//if ( e.IsMe ) await e.User.Channel.Send( twitchClient, "Hello World" );
			
		}

		private static async Task OnChannelLeave( Client client, User user, Channel channel ) {

			Log.Info( "User '{0}' left channel '{1}'.", user.Global.Name, user.Channel.Name );

		}

		private static async Task OnChatMessage( Client client, Message message ) {

			Log.Info( "User '{0}' in '{1}' said '{2}'.",message.User.Global.Name, message.Channel.Name, message.Content );

			if ( message.Content == "!hello" ) {
				await message.Channel.Send( client, "Hello World!" );
			} else if ( message.Content == "!random" ) {
				Random random = new();
				await message.Channel.Send( client, $"Your random number is {random.Next( 100 )}" );
			} else if ( message.Content == "!cake" ) {
				await message.Channel.Send( client, $"This was a triumph!\nI'm making a note here: Huge success!\nIt's hard to overstate my satisfaction.\n\nWe do what we must because we can. For the good of all of us. Except the ones who are dead.\n\nBut there's no sense crying over every mistake.\nYou just keep on trying 'til you run out of cake." );
			/*} else if ( e.Message.Content == "!socials" ) {
				await e.Message.Channel.Send( twitchClient, "You can find me on Twitter! https://twitter.com/RawrelTV" );*/
			} else if ( message.Content == "!whoami" ) {
				await message.Channel.Send( client, $"You are {message.User.Global.Name}, your name color is {message.User.Global.Color}, your account identifier is {message.User.Global.Identifier}, you are {( message.User.IsSubscriber == true ? "subscribed" : "not subscribed" )}, you are {( message.User.IsModerator == true ? "a moderator" : "not a moderator" )}." ); // , you {( tagTurbo == "1" ? "have Turbo" : "do not have Turbo" )}

			// Streaming streak
			} else if ( message.Content == "!streak" ) {
				int channelIdentifier = message.Channel.Identifier.GetValueOrDefault( 127154290 ); // Rawreltv, cus .Channel.Identifier is probably broken tbh

				try {
					Console.WriteLine( "Checking stream history for channel '{0}' ({1})...", message.Channel.Name, channelIdentifier );
					Streak? streak = await Streak.FetchCurrentStreak( channelIdentifier );
					
					if ( streak != null ) {
						int durationDays = streak.GetDuration();
						int streamCount = streak.GetStreamCount();
						int totalStreamHours = streak.GetStreamDuration() / 60 / 60;
						DateTimeOffset startedAt = streak.GetStartDate();

						Console.WriteLine( "Duration (Days): {0}, Streams: {1}, Total Hours: {2} ({3}s), Started: {4}", durationDays, streamCount, totalStreamHours, streak.GetStreamDuration(), startedAt );
						await message.Channel.Send( client, $"During the month of September I will be doing my best to be live everyday! So far I have been live everyday for the last {durationDays} day(s), with a total of {totalStreamHours} hour(s) across {streamCount} stream(s)!" );
					
					} else {
						Console.WriteLine( "There is no streak yet :c" );
						await message.Channel.Send( client, $"During the month of September I will be doing my best to be live everyday!" );
					}
				
				} catch ( Exception exception ) {
					Console.WriteLine( exception.Message );
					await message.Channel.Send( client, $"Sorry, something went wrong!" );
				}
			}

		}

		private static async Task OnUserUpdate( Client client, User user ) {

			Log.Info( "User '{0}' ({1}) updated.", user.Global.Name, user.Global.Identifier );
			Log.Info( " Type: '{0}'", user.Global.Type );
			Log.Info( " Color: '{0}'", user.Global.Color );
			Log.Info( " Badges: '{0}'", user.Global.Badges != null ? string.Join( ',', user.Global.Badges ) : null );
			Log.Info( " Badge Information: '{0}'", user.Global.BadgeInformation );
			Log.Info( " Emote Sets: '{0}'", user.Global.EmoteSets != null ? string.Join( ',', user.Global.EmoteSets ) : null );
			Log.Info( " Channel: '{0}'", user.Channel.Name );
			Log.Info( "  Is Moderator: '{0}'", user.IsModerator );
			Log.Info( "  Is Subscriber: '{0}'", user.IsSubscriber );

		}

		private static async Task OnChannelUpdate( Client client, Channel channel ) {

			Log.Info( "Channel '{0}' ({1}) updated.", channel.Name, channel.Identifier );
			Log.Info( " Is Emote Only: '{0}'", channel.IsEmoteOnly );
			Log.Info( " Is Followers Only: '{0}'", channel.IsFollowersOnly );
			Log.Info( " Is Subscribers Only: '{0}'", channel.IsSubscribersOnly );
			Log.Info( " Is R9K: '{0}'", channel.IsR9K );
			Log.Info( " Is Rituals: '{0}'", channel.IsRituals );

		}

		private static async Task OnError( Client client, string message ) {

			Log.Info( "An error has occurred: '{0}'.", message );

			//Log.Info( "Stopping Cloudflare Tunnel client..." );
			//Cloudflare.StopTunnel(); // TODO: Kill tunnel on error?

			// Close the connection to the database
			Database.Close().Wait();
			Log.Info( "Closed connection to the database." );

			// Close Redis connection
			Redis.Close().Wait();
			Log.Info( "Disconnected from Redis." );

			// Close chat connection
			Log.Info( "Disconnecting..." );
			await client.CloseAsync();

			// Exit application
			Log.Info( "Exiting..." );
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