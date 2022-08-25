using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TwitchBot {
	public static class Config {

		// Will hold the loaded JSON configuration structure
		private static readonly JsonObject configuration;

		// Directories
		public static readonly string DataDirectory;
		public static readonly string CacheDirectory;

		// Twitch OAuth
		public static readonly string TwitchOAuthBaseURL;
		public static readonly string TwitchOAuthIdentifier;
		public static readonly string TwitchOAuthSecret;
		public static readonly string TwitchOAuthRedirectURL;

		// Twitch Chat IRC
		public static readonly string TwitchChatBaseURL;
		public static readonly string TwitchChatPrimaryChannelName;

		// Twitch API
		public static readonly string TwitchAPIBaseURL;

		// Cloudflare Tunnel
		public static readonly string CloudflareTunnelVersion;
		public static readonly string CloudflareTunnelChecksum;

		// Loads the configuration when the program is started
		static Config() {

			// Get the command-line arguments
			string[] arguments = Environment.GetCommandLineArgs();

			// Use the first argument as the configuration file path, or default to a file in the current working directory
			string configFilePath = arguments.Length >= 2 ? arguments[ 1 ] : "twitch-bot.json";

			// Load (or create) the configuration from the above file
			// TODO: Check for missing keys, add them if required, and save to file (may be missing due to user error, or older version of the configuration structure)
			try {
				configuration = LoadFromFile( configFilePath );
			} catch {
				configuration = CreateDefaultFile( configFilePath );
			}

			DataDirectory = Environment.ExpandEnvironmentVariables( GetString( "directory.data" ) );
			CacheDirectory = Environment.ExpandEnvironmentVariables( GetString( "directory.cache" ) );

			TwitchOAuthBaseURL = GetString( "twitch.oauth.url" );
			TwitchOAuthIdentifier = GetString( "twitch.oauth.identifier" );
			TwitchOAuthSecret = GetString( "twitch.oauth.secret" ); // This needs to fallback to retrieving from user secrets, or maybe it should try user secrets first
			TwitchOAuthRedirectURL = GetString( "twitch.oauth.redirect" );

			TwitchChatBaseURL = GetString( "twitch.chat.url" );
			TwitchChatPrimaryChannelName = GetString( "twitch.chat.channel" );

			TwitchAPIBaseURL = GetString( "twitch.api.url" );

			CloudflareTunnelVersion = GetString( "cloudflare.tunnel.version" );
			CloudflareTunnelChecksum = GetString( "cloudflare.tunnel.checksum" );

		}

		// Retrieves a nested property from the configuration
		// NOTE: This throws an error if the property does not exist, which is intentional behaviour
		private static JsonValue GetValue( string path ) {

			// Split the nested path up into individual property names
			List<string> propertyNames = path.Split( '.' ).ToList();

			// Contains the previously found JSON object, starts with the loaded configuration
			JsonObject previousJsonObject = configuration;

			// Repeat until there are no property names left...
			while ( propertyNames.Count > 0 ) {

				// Store the most recent property name
				string propertyName = propertyNames[ 0 ];

				// Attempt to retreive the property from the previous JSON object
				if ( !previousJsonObject.TryGetPropertyValue( propertyName, out JsonNode? propertyValue ) ) {
					throw new Exception( $"Property '{propertyName}' in '{path}' does not exist" );
				}

				// Error if the retreived property value is null
				if ( propertyValue == null ) {
					throw new Exception( $"Property '{propertyName}' in '{path}' is null" );
				}

				// Return this property as a value if this is the last iteration
				if ( propertyNames.Count == 1 ) {
					return propertyValue.AsValue();

					// Otherwise, store this property as a JSON object for the next iteration
				} else {
					previousJsonObject = propertyValue.AsObject();
				}

				// Remove this property name from the list
				propertyNames.RemoveAt( 0 );

			}

			// Error to catch any unexpected behaviour if there never was a return
			throw new Exception( $"Could not find property '{path}'" );

		}

		// Retrieves a string value from the configuration 
		public static string GetString( string path ) {
			return GetValue( path ).ToString();
		}

		// Loads a JSON structure from a file
		private static JsonObject LoadFromFile( string filePath ) {

			// Open the specified file for reading...
			using ( FileStream fileStream = File.Open( filePath, FileMode.Open, FileAccess.Read, FileShare.None ) ) {

				// Parse the JSON structure within the file
				JsonObject? jsonStructure = JsonSerializer.Deserialize<JsonObject>( fileStream, new JsonSerializerOptions() {
					PropertyNamingPolicy = null, // Keep property names as they are
					PropertyNameCaseInsensitive = false,
					ReadCommentHandling = JsonCommentHandling.Skip, // Ignore any human comments
					AllowTrailingCommas = true // Ignore minor human mistakes
				} );

				// Error if parsing the JSON structure failed
				if ( jsonStructure == null ) {
					throw new Exception( $"Failed to parse JSON structure from configuration file '{filePath}'" );
				}

				// Return the JSON structure so it can be used
				return jsonStructure;

			}

		}

		// Creates a file with the default configuration values
		private static JsonObject CreateDefaultFile( string filePath ) {

			// Define the default configuration values
			JsonObject defaultConfiguration = new() {
				[ "directory" ] = new JsonObject() {
					[ "data" ] = RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ? "%LOCALAPPDATA%\\TwitchBot" : "/var/lib/twitch-bot",
					[ "cache" ] = RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ? "%TEMP%\\TwitchBot" : "/var/cache/twitch-bot",
				},
				[ "twitch" ] = new JsonObject() {
					[ "oauth" ] = new JsonObject() {
						[ "url" ] = "id.twitch.tv/oauth2",
						[ "identifier" ] = "",
						[ "secret" ] = "",
						[ "redirect" ] = ""
					},
					[ "chat" ] = new JsonObject() {
						[ "url" ] = "irc-ws.chat.twitch.tv",
						[ "channel" ] = "",
					},
					[ "api" ] = new JsonObject() {
						[ "url" ] = "api.twitch.tv/helix"
					},
				},
				[ "cloudflare" ] = new JsonObject() {
					[ "tunnel" ] = new JsonObject() {
						[ "version" ] = "2022.8.0",
						[ "checksum" ] = "0aa0c6c576482399dfc098e6ff1969001ec924b3834b65ecb43ceac5bcd0a6c4"
					}
				}
			};

			// Save the configuration to the specified file
			SaveToFile( defaultConfiguration, filePath );

			// Return the default configuration structure so it can be used
			return defaultConfiguration;

		}

		// Saves a JSON structure to a file
		private static void SaveToFile( JsonObject jsonStructure, string filePath ) {

			// Create (or open) the specified file for writing...
			using ( FileStream fileStream = File.Open( filePath, FileMode.Create, FileAccess.Write, FileShare.None ) ) {

				// Write the provided configuration to the file
				JsonSerializer.Serialize( fileStream, jsonStructure, new JsonSerializerOptions() {
					PropertyNamingPolicy = null, // Keep property names as they are
					PropertyNameCaseInsensitive = false,
					WriteIndented = true // Make human editing easier
				} );

			}

		}

		/*********************************/
		/***** DEPRECATED PROPERTIES *****/
		/*********************************/

		[Obsolete( "Use Config.DataDirectory instead" )] public static readonly string ApplicationDataDirectory = "TwitchBot";
		[Obsolete( "Do not use" )] public static readonly string UserAccessTokenFileName = "UserAccessToken.json";

		[Obsolete( "Use Config.TwitchOAuthBaseURL instead" )] public static readonly string OAuthBaseURI = "https://id.twitch.tv/oauth2";
		[Obsolete( "Use Config.TwitchOAuthRedirectURL instead" )] public static readonly string OAuthRedirectURI = "http://localhost:3000"; // https://dev.twitch.tv/console/apps

		[Obsolete( "Use Config.TwitchChatBaseURL instead" )] public static readonly string ChatServerAddress = "irc-ws.chat.twitch.tv";
		[Obsolete( "Do not use" )] public static readonly int ChatServerPort = 443;

		[Obsolete( "Use Config.TwitchChatPrimaryChannelName instead" )] public static readonly string ChannelName = "Rawreltv";

		// https://github.com/cloudflare/cloudflared
		//[Obsolete] public static readonly string CloudflareTunnelVersion = "2022.8.0";
		//[Obsolete] public static readonly string CloudflareTunnelChecksum = "0aa0c6c576482399dfc098e6ff1969001ec924b3834b65ecb43ceac5bcd0a6c4";

		// https://dev.twitch.tv/docs/api/reference
		[Obsolete( "Deprecated, use Config.TwitchAPIBaseURL instead" )] public static readonly string ApiBaseURI = "https://api.twitch.tv/helix"; // Helix is v5

	}
}
