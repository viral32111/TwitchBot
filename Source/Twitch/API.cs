using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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

			if ( httpResponse.StatusCode != HttpStatusCode.OK ) throw new Exception( $"Unsuccessful HTTP response code {httpResponse.StatusCode} from API request: '{endpoint}'" );

			Stream responseStream = await httpResponse.Content.ReadAsStreamAsync();

			JsonNode? responseJson = JsonNode.Parse( responseStream );
			if ( responseJson == null ) throw new Exception( $"Failed to parse JSON response from API request: '{endpoint}'" );

			//Console.WriteLine( "{0} {1} -> {2}", method, targetUrl, JsonSerializer.Serialize<JsonNode>( responseJson, Storage.serializerOptions ) );

			return responseJson.AsObject();

		}

	}
}
