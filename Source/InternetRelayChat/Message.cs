using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// https://ircv3.net/specs/extensions/message-tags.html
// https://datatracker.ietf.org/doc/html/rfc1459.html#section-2.3.1

namespace TwitchBot.InternetRelayChat {
	public class Message {

		// Regular expression to divide up components of a fully-formed IRC message
		private static readonly Regex parsePattern = new( @"^(?>@(?'tags'.+?) )?(?>:(?>(?'nick'[\w.]+))?(?>!(?'user'[\w.]+))?(?>@?(?'host'[\w.]+)) )?(?'command'\d{3}|[A-Z]+) ?(?>\* (?'subcommand'\d{3}|[A-Z]+))?(?'middle'.*?)(?> :(?'params'.+))?$" );

		// Components of an IRC message
		public Dictionary<string, string?> Tags = new();
		public readonly string? Nick; // Prefix
		public readonly string? User; // Prefix
		public readonly string? Host; // Prefix
		public readonly string Command;
		public readonly string? SubCommand; // Shit like 'CAP * ACK' where CAP is command and ACK is subcommand
		public readonly string? Middle; // Annoying data that is sometimes present whenever the server feels like it and for some reason isn't just a parameter
		public readonly string? Parameters;

		// Create a server message from optional components
		public Message( string? tags, string? nick, string? user, string? host, string command, string? subCommand, string? middle, string? parameters ) {

			// Loop through the tags, if any
			if ( tags != null ) foreach ( string entireTag in tags.Split( ';' ) ) {
					if ( string.IsNullOrWhiteSpace( entireTag ) ) continue;

					// Split the tag up into name and value
					string[] tag = entireTag.Split( '=', 2 );

					// Skip tags without a name
					if ( string.IsNullOrWhiteSpace( tag[ 0 ] ) ) continue;

					// Add the tag to the dictionary
					Tags.Add( tag[ 0 ], tag[ 1 ] );
				}

			// Set the remaining component properties
			Nick = nick;
			User = user;
			Host = host;
			Command = command;
			SubCommand = subCommand;
			Middle = middle;
			Parameters = parameters;

			//Console.WriteLine( "MESSAGE:\n\tHOST: '{0}'\n\tCOMMAND: '{1}'\n\tSUBCOMMAND: '{2}'\n\tPARAMS: '{3}'", Host, Command, SubCommand, Parameters ); // DEBUGGING
		}

		// Create a client message, for sending to a server
		public Message( string command, string? middle = null, string? parameters = null ) {
			Command = command;
			Middle = middle;
			Parameters = parameters;
		}

		// Parse a message sent by a server
		public static Message[] Parse( string rawMessages ) {

			// Holds the parsed messages
			List<Message> messages = new();

			// Loop over each message (servers sometimes send multiple messages at once)
			foreach ( string rawMessage in rawMessages.Split( "\r\n" ) ) {
				if ( string.IsNullOrWhiteSpace( rawMessage ) ) continue;

				// Skip if the message did not match the regular expression
				Match match = parsePattern.Match( rawMessage );
				if ( !match.Success ) continue;

				//Console.WriteLine( $"Parsing IRC message: '{rawMessage}'" ); // DEBUGGING

				// Create a new message from the groups in the match, and add it to the list
				messages.Add( new(
					match.Groups[ "tags" ].Value.NullIfWhiteSpace(),
					match.Groups[ "nick" ].Value.NullIfWhiteSpace(),
					match.Groups[ "user" ].Value.NullIfWhiteSpace(),
					match.Groups[ "host" ].Value.NullIfWhiteSpace(),
					match.Groups[ "command" ].Value.NullIfWhiteSpace() ?? throw new Exception( "No command found in IRC message" ), // Fail if there is no command
					match.Groups[ "subcommand" ].Value.NullIfWhiteSpace(),
					match.Groups[ "middle" ].Value.NullIfWhiteSpace(),
					match.Groups[ "params" ].Value.NullIfWhiteSpace()
				) );

			}

			// Return the parsed messages as a fixed-length array
			return messages.ToArray();

		}

		// Parse a message sent by a server, as an array of bytes
		public static Message[] Parse( byte[] bytes, int length ) => Parse( Encoding.UTF8.GetString( bytes, 0, length ) );

		// Convert the message back to the original string
		public override string ToString() => string.Concat(
			Tags.Count > 0 ? $"@{Tags.Join()} " : string.Empty,
			Host != null ? ":" + ( Nick != null && User != null ? $"{Nick}!{User}@{Host}" : Host ) + " " : string.Empty,
			Command == InternetRelayChat.Command.Notice ? Command + " *" : Command, // Why are NOTICE commands always followed by an asterisk
			SubCommand != null ? $" * {SubCommand}" : string.Empty,
			Middle != null ? $" {Middle}" : string.Empty,
			Parameters != null ? $" :{Parameters}" : string.Empty // PASS and NICK don't use : before params??
		);

		// Convert the message to an array of bytes for use when sending over a socket
		public byte[] GetBytes() => Encoding.UTF8.GetBytes( ToString() + "\r\n" ); // CRLF required to terminate message as per RFC1459 2.3.1

		// Checks if this message is a "server message" (i.e. not from a user)
		public bool IsServer() => Nick == null && User == null && Host != null;

		// Checks if this message is a "user message" (i.e. from a user)
		public bool IsForUser( string user ) => !IsServer() && ( Nick == user || User == user );

	}
}
