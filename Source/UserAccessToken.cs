using System.Text;
using System.Net;
using System.Web;
using System.Collections.Specialized;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;

namespace TwitchBot {
	public enum TokenType {
		Unknown = 0,
		Bearer = 1
	}

	[Serializable]
	public class UserAccessToken {
		[NonSerialized]
		private static readonly HttpListener redirectListener = new();

		public string AccessToken = string.Empty;
		public string RefreshToken = string.Empty;
		public TokenType TokenType = TokenType.Unknown;
		public DateTimeOffset Expires = DateTimeOffset.UnixEpoch;

		public UserAccessToken( string accessToken, string refreshToken, string tokenType, int expiresIn ) {
			AccessToken = accessToken;
			RefreshToken = refreshToken;

			TokenType = tokenType switch {
				"bearer" => TokenType.Bearer,
				_ => TokenType.Unknown
			};

			Expires = new DateTimeOffset( DateTime.UtcNow ).AddSeconds( expiresIn );
		}

		public async void Save() {
			if ( !Directory.Exists( Shared.ApplicationDataDirectory ) ) Directory.CreateDirectory( Shared.ApplicationDataDirectory );

			string userAccessTokenPath = Path.Combine( Shared.ApplicationDataDirectory, "UserAccessToken.json" );

			string userAccessTokenJSON = JsonSerializer.Serialize( this, new JsonSerializerOptions() {
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				WriteIndented = true,
				MaxDepth = 1
			} );

			await File.WriteAllTextAsync( userAccessTokenPath, userAccessTokenJSON );
		}

		/*public static async Task<UserAccessToken> Fetch() {
			UserAccessToken userAccessToken;

			try {

			}
		}*/

		public static async Task<UserAccessToken> GetSaved() {
			if ( !Directory.Exists( Shared.ApplicationDataDirectory ) ) throw new Exception( "Application data directory does not exist." );

			string userAccessTokenPath = Path.Combine( Shared.ApplicationDataDirectory, "UserAccessToken.json" );

			if ( !File.Exists( userAccessTokenPath ) ) throw new Exception( "User access token JSON file does not exist." );

			string fileContent = await File.ReadAllTextAsync( userAccessTokenPath );

			UserAccessToken? userAccessToken = JsonSerializer.Deserialize<UserAccessToken>( fileContent, new JsonSerializerOptions() {
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			} );

			if ( userAccessToken == null ) throw new Exception( "Failed to deseralize user access token." );

			return userAccessToken;
		}

		public static async Task<UserAccessToken> Request() {
			string stateSecret = Shared.GenerateRandomString( 16 );
			string[] scopes = { "chat:read", "chat:edit" };

			string authorizationUrl = QueryHelpers.AddQueryString( $"{Config.OAuthBaseURI}/authorize", new Dictionary<string, string?>() {
				{ "client_id", Shared.UserSecrets.AppClientIdentifier },
				{ "force_verify", "true" },
				{ "redirect_uri", Config.OAuthRedirectURI },
				{ "response_type", "code" },
				{ "scope", string.Join( ' ', scopes ) }, // https://dev.twitch.tv/docs/authentication/scopes
				{ "state", stateSecret }
			} );

			Console.WriteLine( $"Please open this URL in your browser to authorize this application to use your Twitch account:\n\n{authorizationUrl}\n" );

			redirectListener.Prefixes.Add( $"{Config.OAuthRedirectURI}/" );
			redirectListener.Start();

			Task<string?> authorizationTask = HandleAuthorizationRedirects( stateSecret, scopes );

			Console.WriteLine( "Waiting for the authorization to complete..." );
			authorizationTask.Wait(); // await .WaitAsync()?

			string? authorizationCode = authorizationTask.Result;
			if ( string.IsNullOrEmpty( authorizationCode ) ) throw new Exception( "Authorization code is null or empty." );

			Console.WriteLine( "The authorization has completed. Requesting user access token..." );

			return await RequestUserAccessToken( authorizationCode );
		}

		private static async Task<string?> HandleAuthorizationRedirects( string stateSecret, string[] requestedScopes ) {
			string? authorizationCode = null;

			while ( redirectListener.IsListening ) {
				HttpListenerContext context = await redirectListener.GetContextAsync();

				string? requestMethod = context.Request?.HttpMethod;
				string? requestPath = context.Request?.Url?.AbsolutePath;
				string? requestQuery = context.Request?.Url?.Query;

				if ( requestMethod != "GET" && requestPath != "/" ) {
					await CloseResponse( context.Response, "The specified path could not be found.", 404 );
					continue;
				}

				if ( string.IsNullOrEmpty( requestQuery ) ) {
					await CloseResponse( context.Response, "No query string was provided.", 400 );
					continue;
				}

				NameValueCollection collection = HttpUtility.ParseQueryString( requestQuery );

				string? state = collection.GetValues( "state" )?[ 0 ];

				string? code = collection.GetValues( "code" )?[ 0 ];
				string? scope = collection.GetValues( "scope" )?[ 0 ];

				string? errorType = collection.GetValues( "error" )?[ 0 ];
				string? errorDescription = collection.GetValues( "error_description" )?[ 0 ];

				if ( state != stateSecret ) {
					await CloseResponse( context.Response, "The state string does not match.", 401 );
					continue;
				}

				if ( errorType != null || errorDescription != null ) {
					await CloseResponse( context.Response, $"An error occured: {errorDescription} ({errorType})", 500 );
					continue;
				}

				if ( string.IsNullOrEmpty( scope ) ) {
					await CloseResponse( context.Response, "No scopes provided.", 400 );
					continue;
				}

				if ( !scope.Split( ' ' ).SequenceEqual( requestedScopes ) ) {
					await CloseResponse( context.Response, "The granted scopes does not match the requested scopes.", 400 );
					continue;
				}

				if ( string.IsNullOrEmpty( code ) ) {
					await CloseResponse( context.Response, "No code provided.", 400 );
					continue;
				}

				authorizationCode = code;

				await CloseResponse( context.Response, "You have successfully authorized the application to use your Twitch account.\n\nYou may now close this page.", 200 );
				redirectListener.Close();
			}

			return authorizationCode;
		}

		private static async Task CloseResponse( HttpListenerResponse response, string body, int statusCode = 200 ) {
			response.StatusCode = statusCode;

			byte[] responseBody = Encoding.UTF8.GetBytes( body );

			response.ContentType = "text/plain; encoding=utf-8";
			response.ContentLength64 = responseBody.LongLength;

			await response.OutputStream.WriteAsync( responseBody );

			response.Close();
		}

		private static async Task<UserAccessToken> RequestUserAccessToken( string authorizationCode ) {
			HttpResponseMessage response = await Shared.httpClient.PostAsync( $"{Config.OAuthBaseURI}/token", new FormUrlEncodedContent( new Dictionary<string, string>() {
				{ "client_id", Shared.UserSecrets.AppClientIdentifier },
				{ "client_secret", Shared.UserSecrets.AppClientSecret },
				{ "code", authorizationCode },
				{ "grant_type", "client_credentials" },
				{ "redirect_uri", Config.OAuthRedirectURI },
			} ) );

			Stream responseStream = await response.Content.ReadAsStreamAsync();
			JsonDocument responseDocument = await JsonDocument.ParseAsync( responseStream );

			string? accessToken = responseDocument.RootElement.GetProperty( "access_token" ).GetString();
			string? refreshToken = responseDocument.RootElement.GetProperty( "refresh_token" ).GetString();
			string? tokenType = responseDocument.RootElement.GetProperty( "token_type" ).GetString();
			int expiresIn = responseDocument.RootElement.GetProperty( "expires_in" ).GetInt32(); // Seconds

			if ( string.IsNullOrEmpty( accessToken ) ) throw new Exception( "No access token found in response." );
			if ( string.IsNullOrEmpty( refreshToken ) ) throw new Exception( "No refresh token found in response." );
			if ( string.IsNullOrEmpty( tokenType ) ) throw new Exception( "No token type found in response." );
			if ( expiresIn <= 0 ) throw new Exception( "Invalid expiry time found in response." );

			return new UserAccessToken( accessToken, refreshToken, tokenType, expiresIn );
		}
	}
}
