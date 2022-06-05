using System.Text.RegularExpressions;

// Parses IRC-styled messages
// https://ircv3.net/specs/extensions/message-tags.html
// https://datatracker.ietf.org/doc/html/rfc1459.html#section-2.3.1

namespace TwitchBot.InternetRelayChat {
	public class Message {
		private static readonly Regex Pattern = new ( @"^(?>@(.+?) )?:([\w.]+) (\d{3}|[A-Z]+)(?> \* ([A-Z]*))?(?> :?(?>\* )?(.+))?$" );

		public Dictionary<string, string?>? Tags = null;
		public string? ServerName = null;
		public string? Command = null;
		public string? SubCommand = null;
		public string? Parameters = null;

		public Message( string tags, string serverName, string command, string subCommand, string parameters ) {
			if ( !string.IsNullOrEmpty( tags ) ) {
				Tags = new();

				foreach ( string tag in tags.Split( ';' ) ) {
					if ( string.IsNullOrEmpty( tag ) ) continue;

					string[] tagPair = tag.Split( '=', 2 );
					if ( string.IsNullOrEmpty( tagPair[ 0 ] ) ) continue;

					Tags.Add( tagPair[ 0 ], ( string.IsNullOrEmpty( tagPair[ 1 ] ) ? null : tagPair[ 1 ] ) );
				}
			}

			if ( !string.IsNullOrEmpty( serverName ) ) ServerName = serverName;
			if ( !string.IsNullOrEmpty( command ) ) Command = command;
			if ( !string.IsNullOrEmpty( subCommand ) ) SubCommand = subCommand;
			if ( !string.IsNullOrEmpty( parameters ) ) Parameters = parameters;
		}

		public override string ToString() {
			return $"{( Tags != null ? $"@{TagsToString()} " : "")}:{ServerName} {Command} {Parameters}";
		}

		public static Message[] Parse( string rawMessage ) {
			List<Message> messages = new();

			// Split the entire message every new line because sometimes responses can contain multiple messages
			foreach ( string singleMessage in rawMessage.Split( "\r\n" ) ) {
				if ( string.IsNullOrEmpty( singleMessage ) ) continue;

				Match ircMatch = Pattern.Match( singleMessage );
				if ( !ircMatch.Success ) continue;

				messages.Add( new Message(
					ircMatch.Groups[ 1 ].Value, // Tags
					ircMatch.Groups[ 2 ].Value, // Server Name
					ircMatch.Groups[ 3 ].Value, // Command
					ircMatch.Groups[ 4 ].Value, // Sub-command
					ircMatch.Groups[ 5 ].Value // Parameters
				) );
			}

			return messages.ToArray();
		}

		private string? TagsToString() {
			if ( Tags == null ) return null;

			List<string> tags = new();

			foreach ( KeyValuePair<string, string?> tag in Tags ) tags.Add( $"{tag.Key}={tag.Value}" );

			return string.Join( ';', tags );
		}
	}
}
