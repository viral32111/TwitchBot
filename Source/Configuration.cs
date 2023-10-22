using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TwitchBot;

/// <summary>
/// Represents the configuration.
/// </summary>
public class Configuration {

	// Defaults
	private const string fileName = "config.json";
	private const string windowsDirectoryName = "TwitchBot";
	private const string linuxDirectoryName = "twitch-bot";
	private const string environmentVariablePrefix = "TWITCH_BOT_";

	/// <summary>
	/// Indicates whether the configuration has been loaded.
	/// </summary>
	[ JsonIgnore ]
	public bool IsLoaded { get; private set; } = false;

	/// <summary>
	/// Gets the path to the system configuration file.
	/// On Windows, it is: C:\ProgramData\TwitchBot\config.json.
	/// On Linux, it is: /etc/twitch-bot/config.json.
	/// </summary>
	/// <param name="fileName">The name of the configuration file.</param>
	/// <param name="windowsDirectoryName">The name of the directory on Windows.</param>
	/// <param name="linuxDirectoryName">The name of the directory on Linux.</param>
	/// <returns>The path to the system configuration file.</returns>
	/// <exception cref="PlatformNotSupportedException">Thrown if the operating system is not handled.</exception>
	public static string GetSystemPath( string fileName, string windowsDirectoryName, string linuxDirectoryName ) {
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData ), windowsDirectoryName, fileName );
		} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
			return Path.Combine( "/etc", linuxDirectoryName, fileName ); // No enumeration for /etc
		} else throw new PlatformNotSupportedException( "Unsupported operating system" );
	}

	/// <summary>
	/// Gets the path to the user's configuration file.
	/// On Windows, it is %LOCALAPPDATA%\TwitchBot\config.json.
	/// On Linux, it is ~/.config/twitch-bot/config.json.
	/// </summary>
	/// <param name="fileName">The name of the configuration file.</param>
	/// <param name="windowsDirectoryName">The name of the directory on Windows.</param>
	/// <param name="linuxDirectoryName">The name of the directory on Linux.</param>
	/// <returns>The path to the user's configuration file.</returns>
	/// <exception cref="PlatformNotSupportedException">Thrown if the operating system is not handled.</exception>
	public static string GetUserPath( string fileName, string windowsDirectoryName, string linuxDirectoryName ) {
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), windowsDirectoryName, fileName );
		} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
			return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), linuxDirectoryName, fileName );
		} else throw new PlatformNotSupportedException( "Unhandled operating system" );
	}

	/// <summary>
	/// Loads the configuration from all sources.
	/// The priority is: System Configuration File -> User Configuration File -> Project Configuration File -> Environment Variables.
	/// </summary>
	/// <param name="fileName">The name of the configuration file.</param>
	/// <param name="windowsDirectoryName">The name of the directory on Windows.</param>
	/// <param name="linuxDirectoryName">The name of the directory on Linux.</param>
	/// <param name="environmentVariablePrefix">The prefix of the environment variables.</param>
	/// <exception cref="Exception"></exception>
	public static Configuration Load(
		string fileName = fileName,
		string windowsDirectoryName = windowsDirectoryName,
		string linuxDirectoryName = linuxDirectoryName,
		string environmentVariablePrefix = environmentVariablePrefix
	) {
		string systemPath = GetSystemPath( fileName, windowsDirectoryName, linuxDirectoryName );
		string userPath = GetUserPath( fileName, windowsDirectoryName, linuxDirectoryName );
		string developmentPath = Path.Combine( Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ) ?? Environment.CurrentDirectory, fileName );
		Program.Logger.LogInformation( "The system configuration file path is '{0}'.", systemPath );
		Program.Logger.LogInformation( "The user configuration file path is '{0}'.", userPath );
		Program.Logger.LogDebug( "The development configuration file path is '{0}'.", developmentPath );

		IConfigurationRoot root = new ConfigurationBuilder()
			.AddJsonFile( systemPath, true, false )
			.AddJsonFile( userPath, true, false )
			#if DEBUG
			.AddJsonFile( developmentPath, true, false ) // Useful for development
			#endif
			.AddEnvironmentVariables( environmentVariablePrefix )
			.Build();

		Configuration configuration = root.Get<Configuration>() ?? throw new LoadException( "Failed to load configuration, are there malformed/missing properties?" );
		configuration.IsLoaded = true;
		Program.Logger.LogInformation( "Loaded configuration from {0} provider(s).", root.Providers.Count() );

		Program.Logger.LogDebug( configuration.TwitchOAuthClientIdentifier );
		Program.Logger.LogDebug( configuration.TwitchOAuthClientSecret );

		return configuration;
	}

	/// <summary>
	/// Gets the default system directory where persistent data should be stored.
	/// On Windows, it is: C:\ProgramData\TwitchBot\data.
	/// On Linux, it is: /var/lib/twitch-bot.
	/// </summary>
	/// <returns>The path to a directory where persistent data should be stored.</returns>
	public static string GetSystemDataPath() {
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData ), windowsDirectoryName, "data" );
		} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
			return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), linuxDirectoryName, "data" );
		} else return Path.Combine( Environment.CurrentDirectory, "data" );
	}

	/// <summary>
	/// Gets the user's default directory where persistent data should be stored.
	/// On Windows, it is: %LOCALAPPDATA%\TwitchBot\data.
	/// On Linux, it is: ~/.local/twitch-bot.
	/// </summary>
	/// <returns>The path to a directory where persistent data should be stored.</returns>
	public static string GetUserDataPath() {
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), windowsDirectoryName, "data" );
		} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
			return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), ".local", linuxDirectoryName );
		} else return Path.Combine( Environment.CurrentDirectory, "data" );
	}

	/// <summary>
	/// Gets the default system directory where volatile cache should be stored.
	/// On Windows, it is: %TEMP%\TwitchBot.
	/// On Linux, it is: /tmp/twitch-bot.
	/// </summary>
	/// <returns>The path to a directory where volatile cache should be stored.</returns>
	public static string GetSystemCachePath() {
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			return Path.Combine( Path.GetTempPath(), windowsDirectoryName );
		} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
			return Path.Combine( "/tmp", linuxDirectoryName ); // No enumeration for /tmp
		} else return Path.Combine( Environment.CurrentDirectory, "cache" );
	}

	/// <summary>
	/// Gets the user's default directory where volatile cache should be stored.
	/// On Windows, it is: %TEMP%\TwitchBot.
	/// On Linux, it is: ~/.cache/twitch-bot.
	/// </summary>
	/// <returns>The path to a directory where volatile cache should be stored.</returns>
	public static string GetUserCachePath() {
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) {
			return Path.Combine( Path.GetTempPath(), windowsDirectoryName );
		} else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) {
			return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.UserProfile ), ".cache", linuxDirectoryName );
		} else return Path.Combine( Environment.CurrentDirectory, "cache" );
	}

	/// <summary>
	/// Thrown when loading the configuration fails.
	/// </summary>
	public class LoadException : Exception {
		public LoadException( string? message ) : base( message ) {}
		public LoadException( string? message, Exception? innerException ) : base( message, innerException ) {}
	}

	/****************************/
	/* Configuration Properties */
	/****************************/

	/// <summary>
	/// The directory where persistent data is stored, such as OAuth tokens.
	/// </summary>
	[ JsonPropertyName( "data-directory" ) ]
	public readonly string DataDirectory = GetUserDataPath();

	/// <summary>
	/// The directory where volatile cache is stored, such as bot state.
	/// </summary>
	[ JsonPropertyName( "cache-directory" ) ]
	public readonly string CacheDirectory = GetUserCachePath();

	/// <summary>
	/// The base URL of the Twitch OAuth API.
	/// https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/#authorization-code-grant-flow
	/// </summary>
	[ JsonPropertyName( "twitch-oauth-base-url" ) ]
	public readonly string TwitchOAuthBaseURL = "https://id.twitch.tv/oauth2";

	/// <summary>
	/// The client identifier of the Twitch OAuth application.
	/// https://dev.twitch.tv/docs/authentication/register-app/
	/// </summary>
	[ JsonPropertyName( "twitch-oauth-client-identifier" ) ]
	public readonly string TwitchOAuthClientIdentifier = "";

	/// <summary>
	/// The client secret of the Twitch OAuth application.
	/// https://dev.twitch.tv/docs/authentication/register-app/
	/// </summary>
	[ JsonPropertyName( "twitch-oauth-client-secret" ) ]
	public readonly string TwitchOAuthClientSecret = "";

	/// <summary>
	/// The redirect URL of the Twitch OAuth application.
	/// https://dev.twitch.tv/docs/authentication/register-app/
	/// </summary>
	[ JsonPropertyName( "twitch-oauth-redirect-url" ) ]
	public readonly string TwitchOAuthRedirectURL = "https://example.com/my-redirect-handler";

	/// <summary>
	/// The scopes to request on behalf of the Twitch OAuth application.
	/// https://dev.twitch.tv/docs/authentication/scopes/
	/// </summary>
	[ JsonPropertyName( "twitch-oauth-scopes" ) ]
	public readonly string[] TwitchOAuthScopes = new[] { "chat:read", "chat:edit" };

	/// <summary>
	/// The IP address of the Twitch chat IRC server.
	/// https://dev.twitch.tv/docs/irc/#connecting-to-the-twitch-irc-server
	/// </summary>
	[ JsonPropertyName( "twitch-chat-address" ) ]
	public readonly string TwitchChatAddress = "irc.chat.twitch.tv";

	/// <summary>
	/// The port number of the Twitch chat IRC server.
	/// https://dev.twitch.tv/docs/irc/#connecting-to-the-twitch-irc-server
	/// </summary>
	[ JsonPropertyName( "twitch-chat-port" ) ]
	public readonly int TwitchChatPort = 6697;

	/// <summary>
	/// The identifier of the primary Twitch channel.
	/// </summary>
	[ JsonPropertyName( "twitch-primary-channel-identifier" ) ]
	public readonly int TwitchPrimaryChannelIdentifier = 127154290;

	/// <summary>
	/// The base URL of the Twitch API.
	/// https://dev.twitch.tv/docs/api/
	/// </summary>
	[ JsonPropertyName( "twitch-api-base-url" ) ]
	public readonly string TwitchAPIBaseURL = "https://api.twitch.tv/helix";

	/// <summary>
	/// The URL of the Twitch EventSub WebSocket.
	/// https://dev.twitch.tv/docs/eventsub/handling-websocket-events/
	/// </summary>
	[ JsonPropertyName( "twitch-events-websocket-url" ) ]
	public readonly string TwitchEventsWebSocketURL = "wss://eventsub.wss.twitch.tv/ws";

	/// <summary>
	/// The IP address of the MongoDB server.
	/// </summary>
	[ JsonPropertyName( "mongodb-server-address" ) ]
	public readonly string MongoDBServerAddress = "127.0.0.1";

	/// <summary>
	/// The port number of the MongoDB server.
	/// </summary>
	[ JsonPropertyName( "mongodb-server-port" ) ]
	public readonly int MongoDBServerPort = 27017;

	/// <summary>
	/// The username of the MongoDB user.
	/// </summary>
	[ JsonPropertyName( "mongodb-user-name" ) ]
	public readonly string MongoDBUserName = "";

	/// <summary>
	/// The password of the MongoDB user.
	/// </summary>
	[ JsonPropertyName( "mongodb-user-password" ) ]
	public readonly string MongoDBUserPassword = "";

	/// <summary>
	/// The name of the MongoDB database.
	/// </summary>
	[ JsonPropertyName( "mongodb-database-name" ) ]
	public readonly string MongoDBDatabaseName = "twitch-bot";

}
