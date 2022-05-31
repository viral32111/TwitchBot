using System.Security.Cryptography;
using System.Text;

namespace TwitchBot {
	public static class Shared {
		private static readonly string randomCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

		public static readonly HttpClient httpClient = new();
		public static string ApplicationDataDirectory = string.Empty;
		public static UserSecrets UserSecrets = new();
		
		public static string GenerateRandomString( int length ) {
			StringBuilder builder = new( length );

			for ( int i = 0; i < length; i++ ) builder.Append( randomCharacters[ RandomNumberGenerator.GetInt32( 0, randomCharacters.Length ) ] );

			return builder.ToString();
		}
	}
}
