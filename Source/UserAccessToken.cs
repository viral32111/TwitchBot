using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.WebUtilities;

namespace TwitchBot {
	public enum TokenType {
		Unknown = 0,
		Bearer = 1
	}

	public class UserAccessToken {
		private static readonly HttpListener redirectListener = new();
		private static readonly string saveFileName = "UserAccessToken.json";

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

		public static async Task<UserAccessToken> Fetch() {
			UserAccessToken userAccessToken;

			try {
				userAccessToken = await Load();
			} catch ( Exception exception ) {
				Console.WriteLine( $"Failed to load user access token: '{exception.Message}'.\nRequesting a fresh user access token...\n" );

				userAccessToken = await RequestUserAuthorization();
			}

			return userAccessToken;
		}

		private static async Task Save( JsonDocument document ) {
			if ( !Directory.Exists( Shared.ApplicationDataDirectory ) ) Directory.CreateDirectory( Shared.ApplicationDataDirectory );
			string documentPath = Path.Combine( Shared.ApplicationDataDirectory, saveFileName );

			string documentJson = JsonSerializer.Serialize( document, new JsonSerializerOptions() {
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				PropertyNameCaseInsensitive = false,
				WriteIndented = true
			} );

			await File.WriteAllTextAsync( documentPath, documentJson );

			Console.WriteLine( "Saved the user access token response." );
		}

		private static async Task<UserAccessToken> Load() {
			if ( !Directory.Exists( Shared.ApplicationDataDirectory ) ) throw new Exception( "Application data directory does not exist" );
			string documentPath = Path.Combine( Shared.ApplicationDataDirectory, saveFileName );

			if ( !File.Exists( documentPath ) ) throw new Exception( "User access token file does not exist" );
			string fileContent = await File.ReadAllTextAsync( documentPath );

			UserAccessToken userAccessToken;
			using ( FileStream fileStream = File.Open( documentPath, FileMode.Open, FileAccess.Read, FileShare.None ) ) {
				JsonDocument document = await JsonDocument.ParseAsync( fileStream );
				userAccessToken = ReadDocumentValues( document );
			}

			if ( userAccessToken.Expires >= DateTime.UtcNow ) throw new Exception( "Saved token has expired" );

			return userAccessToken;
		}

		private static async Task<UserAccessToken> RequestUserAuthorization() {
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
			if ( string.IsNullOrEmpty( authorizationCode ) ) throw new Exception( "Authorization code is null or empty" );

			Console.WriteLine( "The authorization has completed. Requesting user access token..." );
			UserAccessToken userAccessToken = await RequestUserAccessToken( authorizationCode );
			Console.WriteLine( "Received the user access token." );

			return userAccessToken;
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
			HttpResponseMessage tokenResponse = await Shared.httpClient.PostAsync( $"{Config.OAuthBaseURI}/token", new FormUrlEncodedContent( new Dictionary<string, string>() {
				{ "client_id", Shared.UserSecrets.AppClientIdentifier },
				{ "client_secret", Shared.UserSecrets.AppClientSecret },
				{ "code", authorizationCode },
				{ "grant_type", "authorization_code" },
				{ "redirect_uri", Config.OAuthRedirectURI },
			} ) );

			Stream responseStream = await tokenResponse.Content.ReadAsStreamAsync();
			JsonDocument responseDocument = await JsonDocument.ParseAsync( responseStream );

			UserAccessToken userAccessToken = ReadDocumentValues( responseDocument );

			await Save( responseDocument );

			return userAccessToken;
		}

		private static UserAccessToken ReadDocumentValues( JsonDocument document ) {
			string? accessToken = document.RootElement.GetProperty( "access_token" ).GetString();
			string? refreshToken = document.RootElement.GetProperty( "refresh_token" ).GetString();
			string? tokenType = document.RootElement.GetProperty( "token_type" ).GetString();
			int expiresIn = document.RootElement.GetProperty( "expires_in" ).GetInt32(); // Seconds

			if ( string.IsNullOrEmpty( accessToken ) ) throw new Exception( "No access token found in response" );
			if ( string.IsNullOrEmpty( refreshToken ) ) throw new Exception( "No refresh token found in response" );
			if ( string.IsNullOrEmpty( tokenType ) ) throw new Exception( "No token type found in response" );
			if ( expiresIn <= 0 ) throw new Exception( "Invalid expiry time found in response" );

			return new UserAccessToken( accessToken, refreshToken, tokenType, expiresIn );
		}
	}
}
