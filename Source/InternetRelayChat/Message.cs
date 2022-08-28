using System.Collections.Generic;
using System.Text.RegularExpressions;

// Parses IRC-styled messages
// https://ircv3.net/specs/extensions/message-tags.html
// https://datatracker.ietf.org/doc/html/rfc1459.html#section-2.3.1

namespace TwitchBot.InternetRelayChat {
	public class Message {
		private static readonly Regex Pattern = new( @"^(?>@(.+?) )?(?>:(?>([\w.]+))?(?>!([\w.]+))?(?>@?([\w.]+)) )?(\d{3}|[A-Z]+)(?> \* ([A-Z]*))?(?> :?(?>\* )?(.+))?$" );

		public Dictionary<string, string?>? Tags = null;

		public string? Nick = null;
		public string? User = null;
		public string? Host = null;

		public string? Command = null;
		public string? SubCommand = null;

		public string? Parameters = null;

		public Message( string tags, string nick, string user, string host, string command, string subCommand, string parameters ) {
			if ( !string.IsNullOrEmpty( tags ) ) {
				Tags = new();

				foreach ( string tag in tags.Split( ';' ) ) {
					if ( string.IsNullOrEmpty( tag ) ) continue;

					string[] tagPair = tag.Split( '=', 2 );
					if ( string.IsNullOrEmpty( tagPair[ 0 ] ) ) continue;

					Tags.Add( tagPair[ 0 ], ( string.IsNullOrEmpty( tagPair[ 1 ] ) ? null : tagPair[ 1 ] ) );
				}
			}

			if ( !string.IsNullOrEmpty( nick ) ) Nick = nick;
			if ( !string.IsNullOrEmpty( user ) ) User = user;
			if ( !string.IsNullOrEmpty( host ) ) Host = host;

			if ( !string.IsNullOrEmpty( command ) ) Command = command;
			if ( !string.IsNullOrEmpty( subCommand ) ) SubCommand = subCommand;

			if ( !string.IsNullOrEmpty( parameters ) ) Parameters = parameters;
		}

		public bool IsServer( string host ) {
			return Nick == null && User == null && Host == host;
		}

		public bool IsFor( string user, string host ) {
			return Host == $"{user.ToLower()}.{host}"; // Nick == user.ToLower() && User == user.ToLower() && 
		}

		public override string ToString() {
			return $"{( Tags != null ? $"@{TagsToString()} " : "" )}{( Host != null ? ":" + ( Nick != null && User != null ? $"{Nick}!{User}@{Host}" : Host ) + " " : "" )}{Command}{( Parameters != null ? " :" + Parameters : "" )}";
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

					ircMatch.Groups[ 2 ].Value, // Nick
					ircMatch.Groups[ 3 ].Value, // User
					ircMatch.Groups[ 4 ].Value, // Host

					ircMatch.Groups[ 5 ].Value, // Command
					ircMatch.Groups[ 6 ].Value, // Sub-Command

					ircMatch.Groups[ 7 ].Value // Parameters
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
