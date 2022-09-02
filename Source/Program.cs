﻿using System;
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

namespace TwitchBot {
	public class Program {

		// Windows-only
		[DllImport( "Kernel32" )]
		private static extern bool SetConsoleCtrlHandler( EventHandler handler, bool add );
		private delegate bool EventHandler( CtrlType signal );
		private static EventHandler? consoleCtrlHandler;

		private static readonly Twitch.Client twitchClient = new();

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
			JsonObject userResponse = await Twitch.API.Request( "users" );
			string? accountName = userResponse[ "data" ]?[ 0 ]?[ "display_name" ]?.ToString();
			if ( string.IsNullOrEmpty( accountName ) ) throw new Exception( "Failed to fetch account name." );
			Shared.MyAccountName = accountName;
			Log.Info( "My account name is: '{0}' ({1}, {2}).", Shared.MyAccountName, userResponse[ "data" ]?[ 0 ]?[ "id" ]?.ToString(), userResponse[ "data" ]?[ 0 ]?[ "created_at" ]?.ToString() );

			// Register event handlers for the Twitch client
			twitchClient.OnError += OnError;
			twitchClient.OnConnect += OnConnect;
			twitchClient.OnReady += OnReady;
			twitchClient.OnChannelJoin += OnChannelJoin;
			twitchClient.OnChannelLeave += OnChannelLeave;
			twitchClient.OnChatMessage += OnChatMessage;
			twitchClient.OnUserUpdate += OnUserUpdate;
			twitchClient.OnChannelUpdate += OnChannelUpdate;
			Log.Info( "Registered Twitch client event handlers." );

			// TODO: Solution for Linux & Docker environment stop signal
			if ( Shared.IsWindows() ) {
				consoleCtrlHandler += new EventHandler( OnApplicationExit );
				SetConsoleCtrlHandler( consoleCtrlHandler, true );
				Log.Info( "Registered application exit event handler." );
			}

			// Connect to Twitch chat
			// NOTE: Blocks until the connection is closed
			Log.Info( "Connecting to Twitch chat..." );
			twitchClient.Connect( Config.TwitchChatBaseURL );

		}

		private static bool OnApplicationExit( CtrlType signal ) {

			//Log.Info( "Stopping Cloudflare Tunnel client..." );
			//Cloudflare.StopTunnel();

			// Close the connection to the database
			Database.Close().Wait();
			Log.Info( "Closed connection to the database." );

			Log.Info( "Disconnecting..." );
			twitchClient.Disconnect().Wait();

			Log.Info( "Exiting..." );
			Environment.Exit( 0 );

			return false;

		}

		private static async Task OnConnect( object sender, EventArgs e ) {

			if ( Shared.UserAccessToken == null ) throw new Exception( "Connect event ran without previously fetching user access token" );

			Log.Info( "Requesting capabilities..." );
			await twitchClient.RequestCapabilities( new string[] {
				Twitch.Capability.Commands,
				Twitch.Capability.Membership,
				Twitch.Capability.Tags
			} );

			Log.Info( "Authenticating..." );

			await twitchClient.Authenticate( Shared.MyAccountName!, Shared.UserAccessToken.Access );
		}

		private static async Task OnReady( object sender, Twitch.OnReadyEventArgs e ) {

			Log.Info( "Ready as user '{0}' ({1}).", e.User.Name, e.User.Identifier );

			if ( !string.IsNullOrEmpty( Config.TwitchChatPrimaryChannelName ) ) {
				Log.Info( "Joining channel '{0}'...", Config.TwitchChatPrimaryChannelName );
				await twitchClient.JoinChannel( Config.TwitchChatPrimaryChannelName );
			} else {
				Log.Warn( "No primary channel configured to join." );
			}

		}

		private static async Task OnChannelJoin( object sender, Twitch.OnChannelJoinLeaveEventArgs e ) {

			Log.Info( "User '{0}' joined channel '{1}'.", e.User.Global.Name, e.User.Channel.Name );

			//if ( e.IsMe ) await e.User.Channel.Send( twitchClient, "Hello World" );
			
		}

		private static async Task OnChannelLeave( object sender, Twitch.OnChannelJoinLeaveEventArgs e ) {

			Log.Info( "User '{0}' left channel '{1}'.", e.User.Global.Name, e.User.Channel.Name );

		}

		private static async Task OnChatMessage( object sender, Twitch.OnChatMessageEventArgs e ) {

			Log.Info( "User '{0}' in '{1}' said '{2}'.", e.Message.User.Global.Name, e.Message.Channel.Name, e.Message.Content );

			if ( e.Message.Content == "!hello" ) {
				await e.Message.Channel.Send( twitchClient, "Hello World!" );
			} else if ( e.Message.Content == "!random" ) {
				Random random = new();
				await e.Message.Channel.Send( twitchClient, $"Your random number is {random.Next( 100 )}" );
			} else if ( e.Message.Content == "!cake" ) {
				await e.Message.Channel.Send( twitchClient, $"This was a triumph!\nI'm making a note here: Huge success!\nIt's hard to overstate my satisfaction.\n\nWe do what we must because we can. For the good of all of us. Except the ones who are dead.\n\nBut there's no sense crying over every mistake.\nYou just keep on trying 'til you run out of cake." );
			/*} else if ( e.Message.Content == "!socials" ) {
				await e.Message.Channel.Send( twitchClient, "You can find me on Twitter! https://twitter.com/RawrelTV" );*/
			} else if ( e.Message.Content == "!whoami" ) {
				await e.Message.Channel.Send( twitchClient, $"You are {e.Message.User.Global.Name}, your name color is {e.Message.User.Global.Color}, your account identifier is {e.Message.User.Global.Identifier}, you are {( e.Message.User.IsSubscriber == true ? "subscribed" : "not subscribed" )}, you are {( e.Message.User.IsModerator == true ? "a moderator" : "not a moderator" )}." ); // , you {( tagTurbo == "1" ? "have Turbo" : "do not have Turbo" )}

			// Streaming streak
			} else if ( e.Message.Content == "!streak" ) {
				int channelIdentifier = e.Message.Channel.Identifier.GetValueOrDefault( 127154290 ); // Rawreltv, cus .Channel.Identifier is probably broken tbh
				
				try {
					Console.WriteLine( "Checking stream history for channel '{0}' ({1})...", e.Message.Channel.Name, channelIdentifier );
					Streak? streak = await Streak.FetchCurrentStreak( channelIdentifier );
					
					if ( streak != null ) {
						int durationDays = streak.GetDuration();
						int streamCount = streak.GetStreamCount();
						int totalStreamHours = streak.GetStreamDuration() / 60 / 60;
						DateTimeOffset startedAt = streak.GetStartDate();

						Console.WriteLine( "Duration (Days): {0}, Streams: {1}, Total Hours: {2} ({3}s), Started: {4}", durationDays, streamCount, totalStreamHours, streak.GetStreamDuration(), startedAt );
						await e.Message.Channel.Send( twitchClient, $"During the month of September I will be doing my best to be live everyday! So far I have been live everyday for the last {durationDays} day(s), with a total of {totalStreamHours} hour(s) across {streamCount} stream(s)!" );
					
					} else {
						Console.WriteLine( "There is no streak yet :c" );
						await e.Message.Channel.Send( twitchClient, $"During the month of September I will be doing my best to be live everyday!" );
					}
				
				} catch ( Exception exception ) {
					Console.WriteLine( exception.Message );
					await e.Message.Channel.Send( twitchClient, $"Sorry, something went wrong!" );
				}
			}

		}

		private static async Task OnUserUpdate( object sender, Twitch.OnUserUpdateEventArgs e ) {

			Log.Info( "User '{0}' ({1}) updated.", e.User.Global.Name, e.User.Global.Identifier );
			Log.Info( " Type: '{0}'", e.User.Global.Type );
			Log.Info( " Color: '{0}'", e.User.Global.Color );
			Log.Info( " Badges: '{0}'", e.User.Global.Badges != null ? string.Join( ',', e.User.Global.Badges ) : null );
			Log.Info( " Badge Information: '{0}'", e.User.Global.BadgeInformation );
			Log.Info( " Emote Sets: '{0}'", e.User.Global.EmoteSets != null ? string.Join( ',', e.User.Global.EmoteSets ) : null );
			Log.Info( " Channel: '{0}'", e.User.Channel.Name );
			Log.Info( "  Is Moderator: '{0}'", e.User.IsModerator );
			Log.Info( "  Is Subscriber: '{0}'", e.User.IsSubscriber );

		}

		private static async Task OnChannelUpdate( object sender, Twitch.OnChannelUpdateEventArgs e ) {

			Log.Info( "Channel '{0}' ({1}) updated.", e.Channel.Name, e.Channel.Identifier );
			Log.Info( " Is Emote Only: '{0}'", e.Channel.IsEmoteOnly );
			Log.Info( " Is Followers Only: '{0}'", e.Channel.IsFollowersOnly );
			Log.Info( " Is Subscribers Only: '{0}'", e.Channel.IsSubscribersOnly );
			Log.Info( " Is R9K: '{0}'", e.Channel.IsR9K );
			Log.Info( " Is Rituals: '{0}'", e.Channel.IsRituals );

		}

		private static async Task OnError( object sender, Twitch.OnErrorEventArgs e ) {

			Log.Info( "An error has occurred: '{0}'.", e.Message );

			//Log.Info( "Stopping Cloudflare Tunnel client..." );
			//Cloudflare.StopTunnel(); // TODO: Kill tunnel on error?

			// Close the connection to the database
			Database.Close().Wait();
			Log.Info( "Closed connection to the database." );

			Log.Info( "Disconnecting..." );
			await twitchClient.Disconnect();

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