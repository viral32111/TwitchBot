using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TwitchBot.Twitch {
	public static class API {
		public async static Task<JsonObject> Request( string endpoint, HttpMethod? method = null, Dictionary<string, string>? queryParameters = null ) {
			
			// Construct the URL
			Uri targetUrl = new( $"https://{Config.TwitchAPIBaseURL}/{endpoint}?{queryParameters?.ToQueryString()}" );

			// Create the request, defaulting to GET
			HttpRequestMessage httpRequest = new( method ?? HttpMethod.Get, targetUrl.ToString() );

			// Add the OAuth credentials as headers
			httpRequest.Headers.Add( "Client-Id", Config.TwitchOAuthIdentifier );
			httpRequest.Headers.Authorization = Shared.UserAccessToken!.GetAuthorizationHeader();

			// Send the request & make sure to retry when it fails due to token expiry
			HttpResponseMessage httpResponse = await Shared.httpClient.SendAsync( httpRequest );
			if ( httpResponse.StatusCode == HttpStatusCode.Unauthorized ) {
				Log.Warn( "User access token has expired, refreshing & saving..." );
				await Shared.UserAccessToken.DoRefresh();
				Shared.UserAccessToken.Save( Shared.UserAccessTokenFilePath );
				
				Log.Info( "Retrying API request: '{0}' '{1}'...", method, endpoint );
				return await Request( endpoint, method, queryParameters );
			}
			
			// We do not want to continue if the response is not successful
			httpResponse.EnsureSuccessStatusCode();

			// Read the response content as JSON
			Stream responseStream = await httpResponse.Content.ReadAsStreamAsync();
			JsonNode? responseJson = JsonNode.Parse( responseStream );
			if ( responseJson == null ) throw new Exception( $"Failed to parse JSON response from API request: '{endpoint}'" );

			// Convert to a JSON object before returning
			return responseJson.AsObject();

		}
	}
}
