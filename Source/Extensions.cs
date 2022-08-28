using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TwitchBot {
	public static class Extensions {

		public static JsonArray ToJsonArray( this string[] strings ) {
			List<JsonNode> nodes = new();

			foreach ( string value in strings ) {
				nodes.Add( JsonValue.Create( value )! );
			}

			return new JsonArray( nodes.ToArray() );
		}

		public static string[] ToArray( this JsonArray array ) {
			List<string> strings = new();

			foreach ( JsonNode? node in array ) {
				if ( node == null ) continue;
				strings.Add( node.ToString() );
			}

			return strings.ToArray();
		}

		public static async Task Respond( this HttpListenerResponse response, HttpStatusCode statusCode = HttpStatusCode.OK, string? textMessage = null ) {

			response.StatusCode = ( int ) statusCode;

			textMessage ??= response.StatusCode.ToString();

			byte[] responseBody = Encoding.UTF8.GetBytes( textMessage );
			response.ContentType = "text/plain; encoding=utf-8";
			response.ContentLength64 = responseBody.LongLength;

			await response.OutputStream.WriteAsync( responseBody );

			response.Close();

		}

	}
}
