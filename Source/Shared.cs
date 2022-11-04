﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using TwitchBot.Twitch.OAuth;

namespace TwitchBot {
	public static class Shared {
		private static readonly string randomCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

		public static readonly HttpClient httpClient = new();

		[Obsolete( "Use Twitch.Client.User.DisplayName instead" )]
		public static string? MyAccountName;

		public static UserAccessToken? UserAccessToken;
		public static readonly string UserAccessTokenFilePath = Path.Combine( Config.DataDirectory, "UserAccessToken.json" );

		public static readonly Dictionary<SslProtocols, string> SslProtocolNames = new() {
			{ SslProtocols.Tls, "TLSv1.0" },
			{ SslProtocols.Tls11, "TLSv1.1" },
			{ SslProtocols.Tls12, "TLSv1.2" },
			{ SslProtocols.Tls13, "TLSv1.3" }
		};

		public static readonly Dictionary<CipherAlgorithmType, string> CipherAlgorithmNames = new() {
			{ CipherAlgorithmType.Aes, "AES" },
			{ CipherAlgorithmType.Aes128, "AES" },
			{ CipherAlgorithmType.Aes192, "AES" },
			{ CipherAlgorithmType.Aes256, "AES" },

			{ CipherAlgorithmType.Des, "DES" },
			{ CipherAlgorithmType.TripleDes, "Triple-DES" },

			{ CipherAlgorithmType.Rc2, "RC2" },
			{ CipherAlgorithmType.Rc4, "RC4" }
		};

		public static string GenerateRandomString( int length ) {
			StringBuilder builder = new( length );

			for ( int i = 0; i < length; i++ ) builder.Append( randomCharacters[ RandomNumberGenerator.GetInt32( 0, randomCharacters.Length ) ] );

			return builder.ToString();
		}

		// Checks if the current operating system is Windows
		public static bool IsWindows() {
			return RuntimeInformation.IsOSPlatform( OSPlatform.Windows );
		}

		// Creates required directories if they do not exist
		public static void CreateDirectories() {

			// Create the persistent data directory
			if ( !Directory.Exists( Config.DataDirectory ) ) {
				Directory.CreateDirectory( Config.DataDirectory );
				Log.Info( "Created data directory: '{0}'.", Config.DataDirectory );
			}

			// Create the cache directory
			if ( !Directory.Exists( Config.CacheDirectory ) ) {
				Directory.CreateDirectory( Config.CacheDirectory );
				Log.Info( "Created cache directory: '{0}'.", Config.CacheDirectory );
			}

		}

		public static bool IsDocker() {
			return RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) && File.Exists( "/.dockerenv" );
		}

	}
}
