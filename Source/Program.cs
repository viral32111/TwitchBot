#pragma warning disable CS1998

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TwitchBot.Twitch;
using TwitchBot.Twitch.OAuth;

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
				Log.Warn( "Cloudflare Tunnel client does not exist or is corrupt, downloading version {0}...", Config.CloudflareTunnelVersion );
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

					Log.Warn( "The user access token is no longer valid, refreshing it..." );
					await Shared.UserAccessToken.DoRefresh();

					Log.Info( "Saving the refreshed user access token..." );
					Shared.UserAccessToken.Save( Shared.UserAccessTokenFilePath );

				} else {
					Log.Info( "The user access token is still valid, no refresh required." );
				}

			} catch ( FileNotFoundException ) {
				Log.Warn( "User access token file does not exist, requesting fresh token..." );
				Shared.UserAccessToken = await UserAccessToken.RequestAuthorization( Config.TwitchOAuthRedirectURL, Config.TwitchOAuthScopes );
				Shared.UserAccessToken.Save( Shared.UserAccessTokenFilePath );
			}

			// Fetch this account's information
			client.User = await GlobalUser.FetchFromAPI();
			Log.Info( "I am {0}.", client.User.ToString() );
			Shared.MyAccountName = client.User.DisplayName; // DEPRECATED ASS SHIT

			// Register event handlers for the Twitch client
			client.OnSecureCommunication += OnSecureCommunication;
			client.OnOpen += OnOpen;
			client.OnReady += OnReady;
			client.OnGlobalUserJoinChannel += OnGlobalUserJoinChannel;
			client.OnGlobalUserLeaveChannel += OnGlobalUserLeaveChannel;
			client.OnChannelChatMessage += OnChannelChatMessage;
			client.OnChannelUserUpdate += OnChannelUserUpdate;
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

			// Keep the program running until we disconnect from Twitch chat
			await client.WaitAsync();

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

		private static async Task OnSecureCommunication( InternetRelayChat.Client client, X509Certificate serverCertificate, SslProtocols protocol, CipherAlgorithmType cipherAlgorithm, int cipherStrength ) {

			string protocolName = Shared.SslProtocolNames[ protocol ];
			string cipherName = Shared.CipherAlgorithmNames[ cipherAlgorithm ];

			Log.Debug( $"Established secure communication with '{serverCertificate.Subject}' (verified by '{serverCertificate.Issuer}' until {serverCertificate.GetExpirationDateString()}), using {protocolName} ({cipherName}-{cipherStrength})." );

		}

		// Fires after the underlying connection is ready (i.e. TLS established & receiving data)
		private static async Task OnOpen( Client client ) {

			if ( Shared.UserAccessToken == null ) throw new Exception( "Open event ran without previously fetching user access token" );

			// Request all of Twitch's IRC capabilities
			Log.Info( "Requesting capabilities..." );
			await client.RequestCapabilities( new string[] {
				Capability.Commands,
				Capability.Membership,
				Capability.Tags
			} );

			// Send our credentials to authenticate
			Log.Info( "Authenticating..." );
			if ( await client.Authenticate( client.User!.DisplayName, Shared.UserAccessToken.Access ) ) {
				Log.Info( "Successfully authenticated." );
			} else {
				Log.Error( "Authentication failed!" );
				await client.CloseAsync();
			}

		}

		// Fires after authentication is successful & we have been informed about ourselves...
		private static async Task OnReady( Client client, GlobalUser user ) {
			Log.Info( "Ready as user {0}.", user.ToString() );

			// Fetch the primary channel
			Channel primaryChannel = await Channel.FetchFromAPI( Config.TwitchChatPrimaryChannelIdentifier, client );

			// Join the primary channel
			Log.Info( "Joining primary channel {0}...", primaryChannel.ToString() );
			if ( await client.JoinChannel( primaryChannel ) ) {
				Log.Info( "Joined primary channel {0}.", primaryChannel.ToString() );
			} else {
				Log.Error( "Failed to join primary channel!" );
			}
		}

		// Fires when a global user joins a channel's chat
		// NOTE: Can be ourselves after calling Client.JoinChannel() or other users on Twitch when they join the stream
		private static async Task OnGlobalUserJoinChannel( Client client, GlobalUser globalUser, Channel channel, bool isMe ) {
			Log.Info( "Global user {0} joined channel {1}.", globalUser.ToString(), channel.ToString() );
		}

		// Fires when a global user leaves a channel's chat
		private static async Task OnGlobalUserLeaveChannel( Client client, GlobalUser globalUser, Channel channel ) {
			Log.Info( "Global user {0} left channel {1}.", globalUser.ToString(), channel.ToString() );
		}

		// Fires when a message in a channel's chat is received
		private static async Task OnChannelChatMessage( Client client, Message message ) {
			Log.Info( "Channel user {0} in channel {1} said {2}.", message.Author.ToString(), message.Author.Channel.ToString(), message.ToString() );

			// Run chat command, if this message is one
			if ( message.Content[ 0 ] == '!' ) {
				string command = message.Content[ 1.. ];
				if ( ChatCommand.Exists( command ) ) await ChatCommand.Invoke( command, message );
				else Log.Warn( $"Chat command '{command}' is unknown" );
			}
		}

		// Fires after a channel user is updated in state...
		private static async Task OnChannelUserUpdate( Client client, ChannelUser user ) {
			Log.Info( "Channel user {0} updated.", user.Global.ToString() );
		}

		// Fires after a channel is updated in state...
		private static async Task OnChannelUpdate( Client client, Channel channel ) {
			Log.Info( "Channel {0} updated.", channel.ToString() );
		}

		/*private static async Task OnError( Client client, string message ) {

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

		}*/

	}

	public enum CtrlType {
		CTRL_C_EVENT = 0,
		CTRL_BREAK_EVENT = 1,
		CTRL_CLOSE_EVENT = 2,
		CTRL_LOGOFF_EVENT = 5,
		CTRL_SHUTDOWN_EVENT = 6
	}

}