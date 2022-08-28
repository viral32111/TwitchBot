using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;
using TwitchBot.Files;

// Authorization code grant flow & implicit grant flow (unused)

namespace TwitchBot.Twitch.OAuth {
	public class UserAccessToken : AppAccessToken {

		public readonly string Refresh;
		public readonly string[] Scopes;

		public UserAccessToken( TokenType type, string access, double expiresIn, string refresh, string[] scopes ) : base( type, access, expiresIn ) {
			Refresh = refresh;
			Scopes = scopes;
		}

		public UserAccessToken( TokenType type, string access, DateTimeOffset expiresAt, string refresh, string[] scopes ) : base( type, access, expiresAt ) {
			Refresh = refresh;
			Scopes = scopes;
		}

		public static UserAccessToken Load( string filePath ) {

			Storage storage = Storage.ReadFile( filePath );

			TokenType tokenType = ( TokenType ) storage.Get<int>( "type" );
			string accessToken = storage.Get<string>( "access" );
			string refreshToken = storage.Get<string>( "refresh" );
			DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeSeconds( storage.Get<long>( "expires" ) );
			string[] scopes = storage.Get<string[]>( "scopes" );

			return new UserAccessToken( tokenType, accessToken, expiresAt, refreshToken, scopes );

		}

		public static async Task<UserAccessToken> RequestAuthorization( string redirectUri, string[] requestedScopes ) {

			if ( Shared.IsDocker() ) throw new Exception( "Cannot use self-hosted OAuth flow from within Docker environment" );

			string stateSecret = Shared.GenerateRandomString( 16 );

			string authorizationUrl = QueryHelpers.AddQueryString( $"https://{Config.TwitchOAuthBaseURL}/authorize", new Dictionary<string, string?>() {
				{ "client_id", Config.TwitchOAuthIdentifier },
				{ "force_verify", "true" },
				{ "redirect_uri", redirectUri },
				{ "response_type", "code" },
				{ "scope", string.Join( ' ', requestedScopes ) }, // https://dev.twitch.tv/docs/authentication/scopes
				{ "state", stateSecret }
			} );
			Log.Info( $"Authorization URL: {authorizationUrl}" );

			Log.Info( "Waiting for the authorization to complete..." );
			string? authorizationCode = null;
			await WebServer.ListenFor( Config.TwitchOAuthRedirectURL, async ( HttpListenerContext context ) => {

				NameValueCollection collection = HttpUtility.ParseQueryString( context.Request!.Url!.Query );

				// Always present
				string? qsState = collection.GetValues( "state" )?[ 0 ];

				// Only present on success
				string? qsCode = collection.GetValues( "code" )?[ 0 ];
				string? qsScope = collection.GetValues( "scope" )?[ 0 ];

				// Only present on errors
				string? qsErrorType = collection.GetValues( "error" )?[ 0 ];
				string? qsErrorDescription = collection.GetValues( "error_description" )?[ 0 ];

				if ( qsState != stateSecret ) {
					await context.Response.Respond( HttpStatusCode.Unauthorized, "The state string does not match." );
					return false;
				}

				if ( !string.IsNullOrEmpty( qsErrorType ) || !string.IsNullOrEmpty( qsErrorDescription ) ) {
					await context.Response.Respond( HttpStatusCode.InternalServerError, $"An error occurred: {qsErrorDescription} ({qsErrorType})" );
					return false;
				}

				if ( string.IsNullOrEmpty( qsScope ) ) {
					await context.Response.Respond( HttpStatusCode.BadRequest, $"No scopes provided." );
					return false;
				}

				if ( !qsScope.Split( ' ' ).SequenceEqual( requestedScopes ) ) {
					await context.Response.Respond( HttpStatusCode.BadRequest, $"The granted scopes does not match the requested scopes." );
					return false;
				}

				if ( string.IsNullOrEmpty( qsCode ) ) {
					await context.Response.Respond( HttpStatusCode.BadRequest, $"No code provided." );
					return false;
				}

				authorizationCode = qsCode;

				return true;
			}, "You have successfully authorized the application to use your Twitch account.\n\nYou may now close this page.", "GET", true );

			Log.Info( "Authorization complete, granting user access token..." );
			UserAccessToken userAccessToken = await GrantAuthorization( authorizationCode!, redirectUri );

			return userAccessToken;

		}

		private static async Task<UserAccessToken> GrantAuthorization( string authorizationCode, string redirectUri ) {

			HttpResponseMessage grantResponse = await Shared.httpClient.PostAsync( $"https://{Config.TwitchOAuthBaseURL}/token", new FormUrlEncodedContent( new Dictionary<string, string>() {
				{ "client_id", Config.TwitchOAuthIdentifier },
				{ "client_secret", Config.TwitchOAuthSecret },
				{ "code", authorizationCode },
				{ "grant_type", "authorization_code" },
				{ "redirect_uri", redirectUri },
			} ) );

			Stream responseStream = await grantResponse.Content.ReadAsStreamAsync();
			JsonDocument grantDocument = await JsonDocument.ParseAsync( responseStream );

			string? tokenType = grantDocument.RootElement.GetProperty( "token_type" ).GetString();
			string? accessToken = grantDocument.RootElement.GetProperty( "access_token" ).GetString();
			string? refreshToken = grantDocument.RootElement.GetProperty( "refresh_token" ).GetString();
			int expiresIn = grantDocument.RootElement.GetProperty( "expires_in" ).GetInt32(); // Seconds

			List<string> scopes = new();
			foreach ( JsonElement element in grantDocument.RootElement.GetProperty( "scope" ).EnumerateArray() ) {
				string? value = element.GetString();
				if ( value == null ) continue;
				scopes.Add( value );
			}

			if ( string.IsNullOrEmpty( tokenType ) ) throw new Exception( "No token type found in grant response" );
			if ( string.IsNullOrEmpty( accessToken ) ) throw new Exception( "No access token found in grant response" );
			if ( string.IsNullOrEmpty( refreshToken ) ) throw new Exception( "No refresh token found in grant response" );
			if ( expiresIn <= 0 ) throw new Exception( "Invalid expiry time found in grant response" );
			if ( scopes.Count == 0 ) throw new Exception( "No scopes found in grant response" );

			if ( tokenType.ToLower() == "bearer" ) {
				return new UserAccessToken( TokenType.Bearer, accessToken, expiresIn, refreshToken, scopes.ToArray() );
			} else {
				throw new Exception( $"Unknown user access token type: '{tokenType}'" );
			}

		}

		public async Task DoRefresh() {
			throw new NotImplementedException();
		}

		public void Save( string filePath ) {

			Storage.CreateFile( filePath, new JsonObject() {
				[ "type" ] = ( int ) Type,
				[ "access" ] = Access,
				[ "refresh" ] = Refresh,
				[ "expires" ] = ExpiresAt.ToUnixTimeSeconds(),
				[ "scopes" ] = Scopes.ToJsonArray()
			} );

		}

	}
}
