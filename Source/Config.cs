﻿using System;
using System.IO;
using System.Text.Json.Nodes;

namespace TwitchBot {
	public static class Config {

		// Directories
		public static readonly string DataDirectory;
		public static readonly string CacheDirectory;

		// Twitch OAuth
		public static readonly string TwitchOAuthBaseURL;
		public static readonly string TwitchOAuthIdentifier;
		public static readonly string TwitchOAuthSecret;
		public static readonly string TwitchOAuthRedirectURL;
		public static readonly string[] TwitchOAuthScopes;

		// Twitch Chat IRC
		public static readonly string TwitchChatBaseURL;
		public static readonly string TwitchChatPrimaryChannelName;

		// Twitch API
		public static readonly string TwitchAPIBaseURL;

		// Cloudflare Tunnel client
		public static readonly string CloudflareTunnelVersion;
		public static readonly string CloudflareTunnelChecksum;

		// Loads the configuration when the program is started
		static Config() {

			// Get the command-line arguments
			string[] arguments = Environment.GetCommandLineArgs();

			// Use the first argument as the configuration file path, or default to a file in the current working directory
			string configFilePath = arguments.Length >= 2 ? arguments[ 1 ] : Path.Combine( Directory.GetCurrentDirectory(), "twitch-bot.json" );

			// Define variable to hold storage once loaded or created below
			Storage storage;

			// Try to load the configuration from the above file
			try {
				storage = Storage.ReadFile( configFilePath );

				// TODO: Check for missing keys, add them if required, and save to file (may be missing due to user error, or older version of the configuration structure)

				Log.Info( "Loaded configuration from file: '{0}'.", configFilePath );

			// Otherwise, create it with default values in the above file
			} catch ( FileNotFoundException ) {
				storage = Storage.CreateFile( configFilePath, new() {
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
							[ "url" ] = "irc-ws.chat.twitch.tv",
							[ "channel" ] = "",
						},
						[ "api" ] = new JsonObject() {
							[ "url" ] = "api.twitch.tv/helix"
						},
					},
					[ "cloudflare" ] = new JsonObject() {
						[ "tunnel" ] = new JsonObject() {
							[ "version" ] = "2022.8.2",
							[ "checksum" ] = Shared.IsWindows() ? "61ed94712c1bfbf585c06de5fea82588662daeeb290727140cf2b199ca9f9c53" : "c971d24ae2f133b2579ac6fa3b1af34847e0f3e332766fbdc5f36521f410271a"
						}
					}
				} );

				Log.Info( "Created default configuration in file: '{0}'.", configFilePath );
			}

			// Populate directory configuration
			// NOTE: Environment variables such as %APPDATA% on Windows are parsed here
			DataDirectory = Environment.ExpandEnvironmentVariables( storage.Get<string>( "directory.data" ) );
			CacheDirectory = Environment.ExpandEnvironmentVariables( storage.Get<string>( "directory.cache" ) );

			// Populate Twitch OAuth configuration, except from the client secret
			TwitchOAuthBaseURL = storage.Get<string>( "twitch.oauth.url" );
			TwitchOAuthIdentifier = storage.Get<string>( "twitch.oauth.identifier" );
			TwitchOAuthSecret = storage.GetProperty( "twitch.oauth.secret" )?.ToString() ?? UserSecrets.TwitchOAuthSecret; // Fallback to the user secrets store
			TwitchOAuthRedirectURL = storage.Get<string>( "twitch.oauth.redirect" );
			TwitchOAuthScopes = storage.Get<string[]>( "twitch.oauth.scopes" );

			// Populate Twitch Chat IRC configuration
			TwitchChatBaseURL = storage.Get<string>( "twitch.chat.url" );
			TwitchChatPrimaryChannelName = storage.Get<string>( "twitch.chat.channel" );

			// Populate Twitch API configuration
			TwitchAPIBaseURL = storage.Get<string>( "twitch.api.url" );

			// Populate Cloudflare Tunnel client configuration
			CloudflareTunnelVersion = storage.Get<string>( "cloudflare.tunnel.version" );
			CloudflareTunnelChecksum = storage.Get<string>( "cloudflare.tunnel.checksum" );

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
