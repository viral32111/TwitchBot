using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TwitchBot {
	public static class Shared {
		private static readonly string randomCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

		public static readonly HttpClient httpClient = new();
		[Obsolete( "Use Config.DataDirectory instead" )] public static string ApplicationDataDirectory = string.Empty;

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
