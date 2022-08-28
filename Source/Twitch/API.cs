using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TwitchBot.Twitch {
	public static class API {

		public async static Task<JsonObject> Request( string endpoint, string method = "GET" ) {

			if ( method != "GET" ) throw new NotImplementedException( "Methods other than GET are not implemented yet" );

			HttpRequestMessage httpRequest = new( HttpMethod.Get, $"https://{Config.TwitchAPIBaseURL}/{endpoint}" );
			httpRequest.Headers.Authorization = Shared.UserAccessToken!.GetAuthorizationHeader();
			httpRequest.Headers.Add( "Client-Id", Config.TwitchOAuthIdentifier );

			HttpResponseMessage httpResponse = await Shared.httpClient.SendAsync( httpRequest );

			if ( httpResponse.StatusCode != HttpStatusCode.OK ) throw new Exception( $"Unsuccessful HTTP response code {httpResponse.StatusCode} from API request: '{endpoint}'" );

			Stream responseStream = await httpResponse.Content.ReadAsStreamAsync();

			JsonNode? responseJson = JsonNode.Parse( responseStream );
			if ( responseJson == null ) throw new Exception( $"Failed to parse JSON response from API request: '{endpoint}'" );

			//Console.WriteLine( JsonSerializer.Serialize<JsonNode>( responseJson, Storage.serializerOptions ) );

			return responseJson.AsObject();

		}

	}
}
