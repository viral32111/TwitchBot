using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using viral32111.JsonExtensions;

namespace TwitchBot {
	public static class Config {

		// Directories
		public static readonly string DataDirectory;
		public static readonly string CacheDirectory;

		// Twitch OAuth
		public static readonly string TwitchOAuthBaseURL;
		public static readonly string TwitchOAuthIdentifier;
		public static readonly string? TwitchOAuthSecret;
		public static readonly string TwitchOAuthRedirectURL;
		public static readonly string[] TwitchOAuthScopes;

		// Twitch Chat IRC
		public static readonly string TwitchChatBaseURL;
		public static readonly int TwitchChatPrimaryChannelIdentifier;

		// Twitch API
		public static readonly string TwitchAPIBaseURL;

		// Twitch EventSub
		public static readonly string TwitchEventSubWebSocketURL;

		// Cloudflare Tunnel client
		public static readonly string CloudflareTunnelVersion;
		public static readonly string CloudflareTunnelChecksum;

		// Database
		public static readonly string DatabaseName;
		public static readonly string DatabaseServerAddress;
		public static readonly int DatabaseServerPort;
		public static readonly string DatabaseUserName;
		public static readonly string DatabaseUserPassword;

		// Redis
		public static readonly int RedisDatabase;
		public static readonly string RedisKeyPrefix;
		public static readonly string RedisServerAddress;
		public static readonly int RedisServerPort;
		public static readonly string RedisUserName;
		public static readonly string RedisUserPassword;

		// Loads the configuration when the program is started
		static Config() {

			// Get the command-line arguments (excluding flags & executable path)
			string[] arguments = Environment.GetCommandLineArgs().ToList().FindAll( value => !value.StartsWith( "--" ) ).Skip( 1 ).ToArray();

			// Use the first argument as the configuration file path, or default to a file in the current working directory
			string configFilePath = arguments.Length > 0 ? arguments[ 0 ] : Path.Combine( Directory.GetCurrentDirectory(), "twitch-bot.json" );

			// Will hold the loaded (or newly created) configuration
			JsonObject? configuration;

			// Try to load the configuration from the above file path
			try {
				configuration = JsonExtensions.ReadFromFile( configFilePath );
				Log.Info( "Loaded configuration from file: '{0}'.", configFilePath );

				// Ensure that no properties are missing (this adds new properties to older configuration files)
				try {
					EnsurePropertiesExist( configuration, defaultConfiguration );
				} catch ( Exception exception ) {
					Log.Error( exception.Message );
					Environment.Exit( 1 );
					return;
				}

				// Resave the file to save any new properties that might have been added
				configuration.SaveToFile();

			// Otherwise, create it with default values in the above file path
			} catch ( FileNotFoundException ) {
				configuration = JsonExtensions.CreateNewFile( configFilePath, defaultConfiguration );
				Log.Info( "Created default configuration in file: '{0}'.", configFilePath );
			}

			// Fail if the configuration is invalid
			if ( configuration == null ) throw new Exception( "Configuration is invalid" );

			// Try to populate the configuration properties
			try {

				// Directories
				// NOTE: Environment variables such as %APPDATA% on Windows are parsed here
				DataDirectory = Environment.ExpandEnvironmentVariables( configuration.NestedGet<string>( "directory.data" ) );
				CacheDirectory = Environment.ExpandEnvironmentVariables( configuration.NestedGet<string>( "directory.cache" ) );

				// Twitch OAuth, except from the client secret
				TwitchOAuthBaseURL = configuration.NestedGet<string>( "twitch.oauth.url" );
				TwitchOAuthIdentifier = configuration.NestedGet<string>( "twitch.oauth.identifier" );
				TwitchOAuthRedirectURL = configuration.NestedGet<string>( "twitch.oauth.redirect" );
				TwitchOAuthScopes = configuration.NestedGet( "twitch.oauth.scopes" )!.AsArray<string>();

				// Twitch Chat IRC
				TwitchChatBaseURL = configuration.NestedGet<string>( "twitch.chat.url" );
				TwitchChatPrimaryChannelIdentifier = configuration.NestedGet<int>( "twitch.chat.channel" ); // This changed from a string (channel name) to an integer (channel id) in 0.5.0

				// Twitch API
				TwitchAPIBaseURL = configuration.NestedGet<string>( "twitch.api.url" );

				// Twitch EventSub
				TwitchEventSubWebSocketURL = configuration.NestedGet<string>( "twitch.eventsub.websocket.url" );

				// Cloudflare Tunnel client
				CloudflareTunnelVersion = configuration.NestedGet<string>( "cloudflare.tunnel.version" );
				CloudflareTunnelChecksum = configuration.NestedGet<string>( "cloudflare.tunnel.checksum" );

				// Database
				DatabaseName = configuration.NestedGet<string>( "database.name" );
				DatabaseServerAddress = configuration.NestedGet<string>( "database.server.address" );
				DatabaseServerPort = configuration.NestedGet<int>( "database.server.port" );
				DatabaseUserName = configuration.NestedGet<string>( "database.user.name" );
				DatabaseUserPassword = configuration.NestedGet<string>( "database.user.password" );

				// Redis
				RedisDatabase = configuration.NestedGet<int>( "redis.database" );
				RedisKeyPrefix = configuration.NestedGet<string>( "redis.prefix" );
				RedisServerAddress = configuration.NestedGet<string>( "redis.server.address" );
				RedisServerPort = configuration.NestedGet<int>( "redis.server.port" );
				RedisUserName = configuration.NestedGet<string>( "redis.user.name" );
				RedisUserPassword = configuration.NestedGet<string>( "redis.user.password" );

				// Fallback to the user secrets store if the Twitch OAuth secret is not in the configuration file
				string? twitchOAuthSecretConfig = configuration.NestedGet( "twitch.oauth.secret" )?.AsValue().ToString();
				TwitchOAuthSecret = !string.IsNullOrEmpty( twitchOAuthSecretConfig ) ? twitchOAuthSecretConfig : UserSecrets.TwitchOAuthSecret;

			// Fail if any errors happen while attempting to populate the configuration properties
			} catch ( Exception exception ) {
				Log.Error( exception.Message );
				Environment.Exit( 1 );
			}

		}

		// Ensures all properties in the second object exist in the first object
		private static void EnsurePropertiesExist( JsonObject badObject, JsonObject goodObject ) {

			// Loop through a list of all nested properties in the second object
			foreach ( string propertyPath in goodObject.NestedList() ) {

				// Skip properties which exist in the first object
				if ( badObject.NestedHas( propertyPath ) ) continue;

				// Add the property to the first object with the value from the second object
				badObject.NestedSet( propertyPath, goodObject.NestedGet( propertyPath ).Clone() );
				Log.Info( "Added missing property '{0}' to configuration file.", propertyPath );

			}

		}

		// The default configuration structure
		private static readonly JsonObject defaultConfiguration = new() {
			[ "directory" ] = new JsonObject() {
				[ "data" ] = Shared.IsWindows() ? "%LOCALAPPDATA%\\TwitchBot" : "/var/lib/twitch-bot",
				[ "cache" ] = Shared.IsWindows() ? "%TEMP%\\TwitchBot" : "/var/cache/twitch-bot",
			},
			[ "twitch" ] = new JsonObject() {
				[ "oauth" ] = new JsonObject() {
					[ "url" ] = "id.twitch.tv/oauth2",
					[ "identifier" ] = "",
					[ "secret" ] = "",
					[ "redirect" ] = "",
					[ "scopes" ] = new JsonArray( new JsonNode[] {
						JsonValue.Create( "chat:read" )!,
						JsonValue.Create( "chat:edit" )!
					} ),
				},
				[ "chat" ] = new JsonObject() {
					[ "url" ] = "irc.chat.twitch.tv",
					[ "channel" ] = 0,
				},
				[ "api" ] = new JsonObject() {
					[ "url" ] = "api.twitch.tv/helix"
				},
				[ "eventsub" ] = new JsonObject() {
					[ "websocket" ] = new JsonObject() {
						[ "url" ] = "eventsub-beta.wss.twitch.tv/ws"
					}
				}
			},
			[ "cloudflare" ] = new JsonObject() {
				[ "tunnel" ] = new JsonObject() {
					[ "version" ] = "2022.8.2",
					[ "checksum" ] = Shared.IsWindows() ? "61ed94712c1bfbf585c06de5fea82588662daeeb290727140cf2b199ca9f9c53" : "c971d24ae2f133b2579ac6fa3b1af34847e0f3e332766fbdc5f36521f410271a"
				}
			},
			[ "database" ] = new JsonObject() {
				[ "name" ] = "",
				[ "server" ] = new JsonObject() {
					[ "address" ] = "",
					[ "port" ] = 3306
				},
				[ "user" ] = new JsonObject() {
					[ "name" ] = "",
					[ "password" ] = ""
				}
			},
			[ "redis" ] = new JsonObject() {
				[ "database" ] = 0,
				[ "prefix" ] = "twitchbot:",
				[ "server" ] = new JsonObject() {
					[ "address" ] = "",
					[ "port" ] = 6379
				},
				[ "user" ] = new JsonObject() {
					[ "name" ] = "default",
					[ "password" ] = ""
				}
			}
		};

	}
}
