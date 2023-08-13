using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using MongoDB.Driver;

using viral32111.InternetRelayChat;

using TwitchBot.Database;
using TwitchBot.Twitch;
using TwitchBot.Twitch.OAuth;

namespace TwitchBot;

public class Program {

	/// <summary>
	/// Global instance of the configuration.
	/// </summary>
	public static Configuration Configuration { get; private set; } = new();

	// Windows-only
	[ DllImport( "Kernel32" ) ]
	private static extern bool SetConsoleCtrlHandler( EventHandler handler, bool add );
	private delegate bool EventHandler( CtrlType signal );
	private static EventHandler? consoleCtrlHandler;

	private static readonly Twitch.Client client = new();
	private static readonly Twitch.EventSubscription.Client eventSubClient = new();

	// The main entry-point of the program
	public static async Task Main( string[] arguments ) {

		// Display application name and version
		AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
		Log.Info( "Running version {1}.{2}.{3}.", assemblyName.Version?.Major, assemblyName.Version?.Minor, assemblyName.Version?.Build );

		// Display directory paths for convenience
		Log.Info( "Data directory is: '{0}'.", Configuration.DataDirectory );
		Log.Info( "Cache directory is: '{0}'.", Configuration.CacheDirectory );

		// Create required directories
		Shared.CreateDirectories();

		// Deprecation notice for the stream history file
		string streamHistoryFile = Path.Combine( Configuration.DataDirectory, "stream-history.json" );
		if ( File.Exists( streamHistoryFile ) ) Log.Warn( "The stream history file ({0}) is deprecated, it can safely be deleted.", streamHistoryFile );

		// Exit now if this launch was only to initialize files
		if ( arguments.Contains( "--init" ) ) {
			Log.Info( "Initialized configuration & directories, exiting..." );
			Environment.Exit( 0 );
			return;
		}

		// Ensure the OAuth identifier & secret exists
		if ( string.IsNullOrWhiteSpace( Configuration.TwitchOAuthClientIdentifier ) || string.IsNullOrWhiteSpace( Configuration.TwitchOAuthClientSecret ) ) {
			Console.WriteLine( "Could not load Twitch application Client ID and/or secret from the configuration file!" );
			Environment.Exit( 1 );
			return;
		}

		// Download the Cloudflare Tunnel client
		/*
		if ( !Cloudflare.IsClientDownloaded( Configuration.CloudflareTunnelVersion, Configuration.CloudflareTunnelChecksum ) ) {
			Log.Warn( "Cloudflare Tunnel client does not exist or is corrupt, downloading version {0}...", Configuration.CloudflareTunnelVersion );
			await Cloudflare.DownloadClient( Configuration.CloudflareTunnelVersion, Configuration.CloudflareTunnelChecksum );
			Log.Info( "Cloudflare Tunnel client downloaded to: '{0}'.", Cloudflare.GetClientPath( Configuration.CloudflareTunnelVersion ) );
		} else {
			Log.Info( "Using cached Cloudflare Tunnel client at: '{0}'.", Cloudflare.GetClientPath( Configuration.CloudflareTunnelVersion ) );
		}
		*/

		// List all collections in MongoDB
		List<string> databaseCollectionNames = await Mongo.Database.ListCollectionNames().ToListAsync();
		Log.Info( "Found {0} collection(s) in the database: {1}.", databaseCollectionNames.Count, string.Join( ", ", databaseCollectionNames ) );

		// Open Redis connection
		// TODO: What are we even using Redis for??
		/*
		await Redis.Open();
		Log.Info( "Connected to Redis." );
		*/

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
			Shared.UserAccessToken = await UserAccessToken.RequestAuthorization( Configuration.TwitchOAuthRedirectURL, Configuration.TwitchOAuthScopes );
			Shared.UserAccessToken.Save( Shared.UserAccessTokenFilePath );
		}

		// Fetch this account's information
		client.User = await GlobalUser.FetchFromAPI();
		Log.Info( "I am {0}.", client.User.ToString() );

		// Register event handlers for the Twitch client
		client.SecuredEvent += OnSecureCommunication;
		client.OpenedEvent += OnOpen;
		client.OnReady += OnReady;
		client.OnGlobalUserJoinChannel += OnGlobalUserJoinChannel;
		client.OnGlobalUserLeaveChannel += OnGlobalUserLeaveChannel;
		client.OnChannelChatMessage += OnChannelChatMessage;
		client.OnChannelUserUpdate += OnChannelUserUpdate;
		client.OnChannelUpdate += OnChannelUpdate;
		Log.Info( "Registered Twitch client event handlers." );

		// Register event handlers for the EventSub client
		eventSubClient.OnReady += OnEventSubClientReady;
		eventSubClient.OnChannelUpdate += OnEventSubClientChannelUpdate;
		eventSubClient.OnStreamStart += OnEventSubClientStreamStart;
		eventSubClient.OnStreamFinish += OnEventSubClientStreamFinish;
		Log.Info( "Registered Twitch EventSub client event handlers." );

		// TODO: Solution for Linux & Docker environment stop signal
		if ( Shared.IsWindows() ) {
			consoleCtrlHandler += new EventHandler( OnApplicationExit );
			SetConsoleCtrlHandler( consoleCtrlHandler, true );
			Log.Info( "Registered application exit event handler." );
		}

		// Connect to Twitch chat
		Log.Info( "Connecting to Twitch chat..." );
		await client.OpenAsync( Configuration.TwitchChatAddress, Configuration.TwitchChatPort, true );

		// Keep the program running until we disconnect from Twitch chat
		await client.WaitAsync();

	}

	private static bool OnApplicationExit( CtrlType signal ) {
		//Log.Info( "Stopping Cloudflare Tunnel client..." );
		//Cloudflare.StopTunnel();

		// Close the connection to the database
		// TODO: I guess Mongo does this automatically
		/*Database.Close().Wait();
		Log.Info( "Closed connection to the database." );*/

		// Close Redis connection
		/*
		Redis.Close().Wait();
		Log.Info( "Disconnected from Redis." );
		*/

		// Close the EventSub websocket connection
		//Log.Info( "Closing EventSub websocket connection..." );
		//eventSubClient.CloseAsync( WebSocketCloseStatus.NormalClosure, CancellationToken.None ).Wait();

		// Close chat connection
		Log.Info( "Disconnecting..." );
		client.CloseAsync().Wait();

		// Exit application
		Log.Info( "Exiting..." );
		Environment.Exit( 0 );

		return false;
	}

	private static async void OnSecureCommunication( object sender, SecuredEventArgs e ) {
		string protocolName = Shared.SslProtocolNames[ e.Protocol ];
		string cipherName = Shared.CipherAlgorithmNames[ e.CipherAlgorithm ];

		Log.Debug( $"Established secure communication with '{ e.RemoteCertificate.Subject }' (verified by '{ e.RemoteCertificate.Issuer }' until { e.RemoteCertificate.GetExpirationDateString()}), using { protocolName } ({ cipherName }-{ e.CipherStrength })." );
	}

	// Fires after the underlying connection is ready (i.e. TLS established & receiving data)
	private static async void OnOpen( object sender, OpenedEventArgs e ) {
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
	private static async Task OnReady( Twitch.Client client, GlobalUser user ) {
		Log.Info( "Ready as user {0}.", user.ToString() );

		// Fetch the primary channel
		Channel primaryChannel = await Channel.FetchFromAPI( Configuration.TwitchPrimaryChannelIdentifier, client );

		// Join the primary channel
		Log.Info( "Joining primary channel {0}...", primaryChannel.ToString() );
		if ( await client.JoinChannel( primaryChannel ) ) {
			Log.Info( "Joined primary channel {0}.", primaryChannel.ToString() );

			//await eventSubClient.ConnectAsync( Configuration.TwitchEventSubWebSocketURL, new( 0, 0, 10 ), CancellationToken.None );

			// TODO: Start time streamed goal thing

		} else {
			Log.Error( "Failed to join primary channel!" );
		}
	}

	// Fires when a global user joins a channel's chat
	// NOTE: Can be ourselves after calling Client.JoinChannel() or other users on Twitch when they join the stream
	private static async Task OnGlobalUserJoinChannel( Twitch.Client client, GlobalUser globalUser, Channel channel, bool isMe ) {
		Log.Info( "Global user {0} joined channel {1}.", globalUser.ToString(), channel.ToString() );
	}

	// Fires when a global user leaves a channel's chat
	private static async Task OnGlobalUserLeaveChannel( Twitch.Client client, GlobalUser globalUser, Channel channel ) {
		Log.Info( "Global user {0} left channel {1}.", globalUser.ToString(), channel.ToString() );
	}

	// Fires when a message in a channel's chat is received
	private static async Task OnChannelChatMessage( Twitch.Client client, Twitch.Message message ) {
		Log.Info( "Channel user {0} in channel {1} said {2}.", message.Author.ToString(), message.Author.Channel.ToString(), message.ToString() );

		// Run chat command, if this message is one
		if ( message.Content[ 0 ] == '!' ) {
			string command = message.Content[ 1.. ];
			if ( ChatCommand.Exists( command ) ) await ChatCommand.Invoke( command, message );
			else Log.Warn( $"Chat command '{command}' is unknown" );
		}
	}

	// Fires after a channel user is updated in state...
	private static async Task OnChannelUserUpdate( Twitch.Client client, ChannelUser user ) {
		Log.Info( "Channel user {0} updated.", user.Global.ToString() );
	}

	// Fires after a channel is updated in state...
	private static async Task OnChannelUpdate( Twitch.Client client, Channel channel ) {
		Log.Info( "Channel {0} updated.", channel.ToString() );
	}

	// Fires when the EventSub client is ready
	private static async Task OnEventSubClientReady( Twitch.EventSubscription.Client eventSubClient ) {
		Log.Info( "EventSub client is ready, our session identifier is '{0}'.", eventSubClient.SessionIdentifier );

		Channel? channel = State.GetChannel( Configuration.TwitchPrimaryChannelIdentifier );
		if ( channel == null ) throw new Exception( "Cannot find channel" );

		await eventSubClient.SubscribeForChannel( Twitch.EventSubscription.SubscriptionType.ChannelUpdate, channel );
		await eventSubClient.SubscribeForChannel( Twitch.EventSubscription.SubscriptionType.StreamStart, channel );
		await eventSubClient.SubscribeForChannel( Twitch.EventSubscription.SubscriptionType.StreamFinish, channel );
	}

	// Fires when the EventSub client receives a channel update notification
	private static async Task OnEventSubClientChannelUpdate( Twitch.EventSubscription.Client eventSubClient, Channel channel, string title, string language, int categoryId, string categoryName, bool isMature ) {
		Log.Info( "Channel {0} updated their information ('{1}', '{2}', '{3}', '{4}', '{5}')", channel.ToString(), title, language, categoryId, categoryName, isMature );
	}

	// Fires when the EventSub client receives a stream start notification
	private static async Task OnEventSubClientStreamStart( Twitch.EventSubscription.Client eventSubClient, Channel channel, DateTimeOffset startedAt ) {
		Log.Info( "Channel {0} started streaming at '{1}'.", channel.ToString(), startedAt.ToString() );
	}

	// Fires when the EventSub client receives a stream finish notification
	private static async Task OnEventSubClientStreamFinish( Twitch.EventSubscription.Client eventSubClient, Channel channel ) {
		Log.Info( "Channel {0} finished streaming.", channel.ToString() );
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
