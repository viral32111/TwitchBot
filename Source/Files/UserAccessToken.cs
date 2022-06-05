using System.Text.Json.Serialization;

namespace TwitchBot.Files {
	public class UserAccessToken {

		[JsonInclude]
		[JsonPropertyName( "type" )]
		public string Type = string.Empty;

		[JsonInclude]
		[JsonPropertyName( "access" )]
		public string Access = string.Empty;

		[JsonInclude]
		[JsonPropertyName( "refresh" )]
		public string Refresh = string.Empty;

		[JsonInclude]
		[JsonPropertyName( "expires" )]
		public long Expires = 0;

	}
}
