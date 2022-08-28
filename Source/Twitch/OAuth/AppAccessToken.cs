using System;
using System.Threading.Tasks;

namespace TwitchBot.Twitch.OAuth {
	/*public static class Token {
		public static readonly UserAccessToken UserAccessToken;
		public static readonly AppAccessToken AppAccessToken;

		static Token() {
			//UserAccessToken = AuthorizationCode...
			//AppAccessToken = ClientCredentials...
		}
	}*/

	// TODO: String enum for scopes?

	// TODO: String enum for type so it can be used in the authorization header?
	public enum TokenType {
		Bearer = 0
	}

	// Client credentials grant flow
	public class AppAccessToken {
		public readonly TokenType Type;
		public readonly string Access;
		public readonly DateTimeOffset ExpiresAt;

		public AppAccessToken( TokenType type, string access, double expiresIn ) {
			Type = type;
			Access = access;
			ExpiresAt = DateTimeOffset.UtcNow.AddSeconds( expiresIn );
		}

		public AppAccessToken( TokenType type, string access, DateTimeOffset expiresAt ) {
			Type = type;
			Access = access;
			ExpiresAt = expiresAt;
		}

		public async Task<bool> Validate() {
			throw new NotImplementedException();
		}

		public async Task Revoke() {
			throw new NotImplementedException();
		}

		public string GetAuthorizationHeader() {
			return $"{Type} {Access}";
		}
	}
}
