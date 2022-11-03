using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;

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

		public static T[] ToArrayOf<T>( this JsonArray array ) {
			List<T> values = new();

			foreach ( JsonNode? node in array ) {
				if ( node == null ) continue;
				//if ( node.GetType() != typeof( T ) ) continue;
				values.Add( node.GetValue<T>() );
			}

			return values.ToArray();
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

		public static string? NullIfEmpty( this string str ) => string.IsNullOrEmpty( str ) ? null : str;
		public static string? NullIfWhiteSpace( this string str ) => string.IsNullOrWhiteSpace( str ) ? null : str;

		public static string? Join( this Dictionary<string, string?> dictionary, char separator = ';', char pairSeparator = '=' ) {
			if ( dictionary.Count == 0 ) return null;

			List<string> pairs = new();

			foreach ( KeyValuePair<string, string?> pair in dictionary ) pairs.Add( string.Concat( pair.Key, pairSeparator, pair.Value ) );

			return string.Join( separator, pairs );
		}

		// Creates query string from a dictionary to use in URLs
		public static string ToQueryString( this Dictionary<string, string> dictionary ) {
			NameValueCollection queryString = HttpUtility.ParseQueryString( string.Empty );
			foreach ( KeyValuePair<string, string> entry in dictionary ) queryString.Add( entry.Key, entry.Value );
			return queryString.ToString() ?? "";
		}

		// https://stackoverflow.com/a/4335913
		public static string TrimEnd( this string target, string trimString ) {
			if ( string.IsNullOrEmpty( trimString ) ) return target;

			string result = target;
			while ( result.EndsWith( trimString ) ) {
				result = result[ ..^trimString.Length ];
			}

			return result;
		}

	}
}
