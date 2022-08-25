using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.WebUtilities;

namespace TwitchBot {
	public static class TokenType {
		public static readonly string Bearer = "bearer";
	}

	public class UserAccessToken {
		private static readonly HttpListener redirectListener = new();
		private static readonly JsonSerializerOptions serializerOptions = new() {
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = false,
			WriteIndented = true
		};

		public string TokenType = string.Empty;
		public string AccessToken = string.Empty;
		public string RefreshToken = string.Empty;
		public DateTimeOffset Expires = DateTimeOffset.UnixEpoch;

		public UserAccessToken( string accessToken, string refreshToken, string tokenType, DateTimeOffset expires ) {
			TokenType = tokenType;
			AccessToken = accessToken;
			RefreshToken = refreshToken;
			Expires = expires;
		}

		// https://dev.twitch.tv/docs/authentication/validate-tokens
		public async Task<bool> IsValid() {
			Shared.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "OAuth", AccessToken );
			HttpResponseMessage validateResponse = await Shared.httpClient.GetAsync( $"{Config.OAuthBaseURI}/validate" );

			//Stream responseStream = await validateResponse.Content.ReadAsStreamAsync();
			//JsonDocument responseDocument = await JsonDocument.ParseAsync( responseStream );

			// TODO: Properly validate the response body
			return ( validateResponse.StatusCode == HttpStatusCode.OK );
		}

		// https://dev.twitch.tv/docs/authentication/refresh-tokens
		public async Task Refresh() {
			HttpResponseMessage refreshResponse = await Shared.httpClient.PostAsync( $"{Config.OAuthBaseURI}/token", new FormUrlEncodedContent( new Dictionary<string, string>() {
				{ "client_id", Shared.UserSecrets.AppClientIdentifier },
				{ "client_secret", Shared.UserSecrets.AppClientSecret },
				{ "grant_type", "refresh_token" },
				{ "refresh_token", RefreshToken },
			} ) );

			Stream responseStream = await refreshResponse.Content.ReadAsStreamAsync();
			JsonDocument refreshDocument = await JsonDocument.ParseAsync( responseStream );

			string? tokenType = refreshDocument.RootElement.GetProperty( "token_type" ).GetString();
			string? accessToken = refreshDocument.RootElement.GetProperty( "access_token" ).GetString();
			string? refreshToken = refreshDocument.RootElement.GetProperty( "refresh_token" ).GetString();

			if ( string.IsNullOrEmpty( tokenType ) ) throw new Exception( "No token type found in refresh response" );
			if ( string.IsNullOrEmpty( accessToken ) ) throw new Exception( "No access token found in refresh response" );
			if ( string.IsNullOrEmpty( refreshToken ) ) throw new Exception( "No refresh token found in refresh response" );

			TokenType = tokenType;
			AccessToken = accessToken;
			RefreshToken = refreshToken;
			Expires = new DateTimeOffset( DateTime.UtcNow ).AddHours( 12 ); // Refresh token provides no new expiry time? So default to 12 hours...
		}

		public async Task Save() {
			if ( !Directory.Exists( Shared.ApplicationDataDirectory ) ) Directory.CreateDirectory( Shared.ApplicationDataDirectory );
			string tokenPath = Path.Combine( Shared.ApplicationDataDirectory, Config.UserAccessTokenFileName );

			using ( FileStream fileStream = File.Open( tokenPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None ) ) {
				await JsonSerializer.SerializeAsync<Files.UserAccessToken>( fileStream, new() {
					Type = TokenType,
					Access = AccessToken,
					Refresh = RefreshToken,
					Expires = Expires.ToUnixTimeSeconds()
				}, serializerOptions, CancellationToken.None );
			}
		}

		public static async Task<UserAccessToken> Load() {
			if ( !Directory.Exists( Shared.ApplicationDataDirectory ) ) throw new DirectoryNotFoundException( "Application data directory does not exist" );
			string tokenPath = Path.Combine( Shared.ApplicationDataDirectory, Config.UserAccessTokenFileName );

			if ( !File.Exists( tokenPath ) ) throw new FileNotFoundException( "User access token file does not exist" );

			using ( FileStream fileStream = File.Open( tokenPath, FileMode.Open, FileAccess.Read, FileShare.None ) ) {
				Files.UserAccessToken? tokenFile = await JsonSerializer.DeserializeAsync<Files.UserAccessToken>( fileStream, serializerOptions, CancellationToken.None );

				if ( tokenFile == null ) throw new JsonException( "Failed to deserialize user access token file" );

				return new UserAccessToken(
					tokenFile.Access,
					tokenFile.Refresh,
					tokenFile.Type,
					DateTimeOffset.FromUnixTimeSeconds( tokenFile.Expires )
				);
			}
		}

		public static async Task<UserAccessToken> Request( string[] scopes ) {
			string stateSecret = Shared.GenerateRandomString( 16 );

			string authorizationUrl = QueryHelpers.AddQueryString( $"{Config.OAuthBaseURI}/authorize", new Dictionary<string, string?>() {
				{ "client_id", Shared.UserSecrets.AppClientIdentifier },
				{ "force_verify", "true" },
				{ "redirect_uri", Config.OAuthRedirectURI },
				{ "response_type", "code" },
				{ "scope", string.Join( ' ', scopes ) }, // https://dev.twitch.tv/docs/authentication/scopes
				{ "state", stateSecret }
			} );

			Console.WriteLine( $"Please open this URL in your browser to authorize this application to use your Twitch account: {authorizationUrl}" );

			redirectListener.Prefixes.Add( $"{Config.OAuthRedirectURI}/" );
			redirectListener.Start();

			Task<string?> authorizationTask = HandleAuthorizationRedirects( stateSecret, scopes );

			Log.Info( "Waiting for the authorization to complete..." );
			authorizationTask.Wait();

			string? authorizationCode = authorizationTask.Result;
			if ( string.IsNullOrEmpty( authorizationCode ) ) throw new Exception( "Authorization code is null or empty" );

			Log.Info( "The authorization has completed. Granting user access token..." );
			UserAccessToken userAccessToken = await Grant( authorizationCode );

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
					await CloseResponse( context.Response, $"An error occurred: {errorDescription} ({errorType})", 500 );
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

		private static async Task<UserAccessToken> Grant( string authorizationCode ) {
			HttpResponseMessage grantResponse = await Shared.httpClient.PostAsync( $"{Config.OAuthBaseURI}/token", new FormUrlEncodedContent( new Dictionary<string, string>() {
				{ "client_id", Shared.UserSecrets.AppClientIdentifier },
				{ "client_secret", Shared.UserSecrets.AppClientSecret },
				{ "code", authorizationCode },
				{ "grant_type", "authorization_code" },
				{ "redirect_uri", Config.OAuthRedirectURI },
			} ) );

			Stream responseStream = await grantResponse.Content.ReadAsStreamAsync();
			JsonDocument grantDocument = await JsonDocument.ParseAsync( responseStream );

			string? tokenType = grantDocument.RootElement.GetProperty( "token_type" ).GetString();
			string? accessToken = grantDocument.RootElement.GetProperty( "access_token" ).GetString();
			string? refreshToken = grantDocument.RootElement.GetProperty( "refresh_token" ).GetString();
			int expiresIn = grantDocument.RootElement.GetProperty( "expires_in" ).GetInt32(); // Seconds

			if ( string.IsNullOrEmpty( tokenType ) ) throw new Exception( "No token type found in grant response" );
			if ( string.IsNullOrEmpty( accessToken ) ) throw new Exception( "No access token found in grant response" );
			if ( string.IsNullOrEmpty( refreshToken ) ) throw new Exception( "No refresh token found in grant response" );
			if ( expiresIn <= 0 ) throw new Exception( "Invalid expiry time found in grant response" );

			return new UserAccessToken( accessToken, refreshToken, tokenType, new DateTimeOffset( DateTime.UtcNow ).AddSeconds( expiresIn ) );
		}
	}
}
