using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

// Parses IRC-styled messages
// https://ircv3.net/specs/extensions/message-tags.html
// https://datatracker.ietf.org/doc/html/rfc1459.html#section-2.3.1

namespace TwitchBot.InternetRelayChat {
	public class Message {
		private static readonly Regex Pattern = new ( @"^(?>@(.+?) )?:([\w.]+) (\d{3}|[\w *]+)(?> :?(.+))?$" );

		public Dictionary<string, string?>? Tags = null;
		public string? ServerName = null;
		public string? Command = null;
		public string? Parameters = null;

		public Message( string tags, string serverName, string command, string parameters ) {
			if ( !string.IsNullOrEmpty( tags ) ) {
				Tags = new();

				foreach ( string tag in tags.Split( ';' ) ) {
					if ( string.IsNullOrEmpty( tag ) ) continue;

					string[] tagPair = tag.Split( '=', 2 );
					if ( string.IsNullOrEmpty( tagPair[ 0 ] ) ) continue;

					Tags.Add( tagPair[ 0 ], ( string.IsNullOrEmpty( tagPair[ 1 ] ) ? null : tagPair[ 1 ] ) );
				}
			}

			ServerName = ( string.IsNullOrEmpty( serverName ) ? null : serverName );
			Command = ( string.IsNullOrEmpty( command ) ? null : command );
			Parameters = ( string.IsNullOrEmpty( parameters ) ? null : parameters );
		}

		public override string ToString() {
			return $"{( Tags != null ? $"@{TagsToString()}" : "")} :{ServerName} {Command} {Parameters}";
		}

		public static Message[] Parse( string rawMessage ) {
			List<Message> messages = new();

			// Split the entire message every new line because sometimes responses can contain multiple messages
			foreach ( string singleMessage in rawMessage.Split( "\r\n" ) ) {
				if ( string.IsNullOrEmpty( singleMessage ) ) continue;

				Match ircMatch = Pattern.Match( singleMessage );
				if ( !ircMatch.Success ) continue;

				messages.Add( new Message(
					ircMatch.Groups[ 1 ].Value,
					ircMatch.Groups[ 2 ].Value,
					ircMatch.Groups[ 3 ].Value,
					ircMatch.Groups[ 4 ].Value
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
