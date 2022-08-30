using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TwitchBot.Twitch {
	public static class API {

		public async static Task<JsonObject> Request( string endpoint, string method = "GET", Dictionary<string, string?>? queryString = null ) {

			if ( method != "GET" ) throw new NotImplementedException( "Methods other than GET are not implemented yet" );

			string targetUrl = $"https://{Config.TwitchAPIBaseURL}/{endpoint}";

			if ( queryString != null ) targetUrl = QueryHelpers.AddQueryString( targetUrl, queryString );

			HttpRequestMessage httpRequest = new( HttpMethod.Get, targetUrl.ToString() );
			httpRequest.Headers.Authorization = Shared.UserAccessToken!.GetAuthorizationHeader();
			httpRequest.Headers.Add( "Client-Id", Config.TwitchOAuthIdentifier );

			HttpResponseMessage httpResponse = await Shared.httpClient.SendAsync( httpRequest );

			if ( httpResponse.StatusCode != HttpStatusCode.OK ) {
				Log.Warn( "API request: '{0}' '{1}' failed: '{2}'.", method, endpoint, httpResponse.StatusCode );

				if ( httpResponse.StatusCode == HttpStatusCode.Unauthorized ) {
					Log.Warn( "User access token has expired, refreshing & saving..." );
					await Shared.UserAccessToken.DoRefresh();
					Shared.UserAccessToken.Save( Shared.UserAccessTokenFilePath );

					Log.Info( "Retrying API request: '{0}' '{1}'...", method, endpoint );
					return await Request( endpoint, method, queryString );

				} else {
					throw new Exception( $"Unsuccessful HTTP response code '{httpResponse.StatusCode}' for API request: '{method}' '{endpoint}'" );
				}
			}

			Stream responseStream = await httpResponse.Content.ReadAsStreamAsync();

			JsonNode? responseJson = JsonNode.Parse( responseStream );
			if ( responseJson == null ) throw new Exception( $"Failed to parse JSON response from API request: '{endpoint}'" );

			//Console.WriteLine( "{0} {1} -> {2}", method, targetUrl, JsonSerializer.Serialize<JsonNode>( responseJson, Storage.serializerOptions ) );

			return responseJson.AsObject();

		}

	}
}
