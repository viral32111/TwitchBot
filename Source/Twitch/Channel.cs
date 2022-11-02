using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TwitchBot.Twitch {
	public class Channel {
		public Dictionary<string, User> Users = new();

		public readonly int Identifier;
		public readonly string Name;

		public bool? IsEmoteOnly = null;
		public bool? IsFollowersOnly = null;
		public bool? IsSubscribersOnly = null;
		public bool? IsR9K = null;
		public bool? IsRituals = null;
		public bool? IsSlowMode = null;

		private readonly Client Client;

		public Channel( int identifier, string name, Client client ) {
			Identifier = identifier;
			Name = name;
			Client = client;
		}

		// Sends a message to this channel's chat, optionally as a reply
		public async Task SendMessage( string message, int? replyTo = null ) {
			if ( replyTo != null ) {
				await Client.SendAsync( InternetRelayChat.Command.PrivateMessage, middle: $"#{Name}", parameters: message, tags: new() {
					{ "reply-parent-msg-id", replyTo.ToString() }
				} );
			} else {
				await Client.SendAsync( InternetRelayChat.Command.PrivateMessage, middle: $"#{Name}", parameters: message );
			}
		}

		// Fetches a list of streams for this channel
		public async Task<Stream[]> FetchStreams( int limit = 100 ) {

			// Update the stream history in the database
			await UpdateStreamsInDatabase();

			// Fetch streams from the database
			DbDataReader reader = await Database.QueryWithResults( $"SELECT Identifier, UNIX_TIMESTAMP( Start ) AS StartedAt, Duration FROM StreamHistory WHERE Channel = {Identifier} ORDER BY Start DESC LIMIT {limit};" );

			// Populate the list of streams with the query results
			List<Stream> streams = new();
			while ( await reader.ReadAsync() ) streams.Add( new(
				reader.GetInt32( reader.GetOrdinal( "Identifier" ) ),
				DateTimeOffset.FromUnixTimeSeconds( reader.GetInt64( reader.GetOrdinal( "StartedAt" ) ) ),
				reader.GetInt32( reader.GetOrdinal( "Duration" ) ),
				this
			) );

			// We're finished reading query results
			await reader.CloseAsync();

			// Sort the list in order of when the streams started
			streams.Sort( ( Stream currentStream, Stream nextStream ) => nextStream.StartedAt.CompareTo( currentStream.StartedAt ) );

			// Convert the list to a fixed array so it cannot be modified before returning it
			return streams.ToArray();

		}

		// Updates this channel's stream history in the database
		public async Task UpdateStreamsInDatabase( bool traversePages = false ) {

			// Will contain the cursor to the next page, if we are traversing
			string? nextPageCursor = null;

			do {

				// Fetch a list of the latest streams for this channel from the Twitch API
				JsonObject streamsResponse = await API.Request( "videos", queryParameters: new() {
					{ "user_id", Identifier.ToString() },
					{ "type", "archive" },
					{ "sort", "time" },
					{ "first", "100" },
					{ "after", nextPageCursor ?? "" }
				} );

				// Update the cursor to the next page (is set to null if this is the last page)
				nextPageCursor = streamsResponse[ "pagination" ]![ "cursor" ]?.GetValue<string?>();

				// Loop through the list of streams, skipping invalid ones...
				foreach ( JsonNode? node in streamsResponse[ "data" ]!.AsArray() ) {
					if ( node == null ) continue;
					Stream stream = new( node.AsObject(), this );

					// Add this stream to the database, or update its duration if it already exists
					await Database.Query( $"INSERT INTO StreamHistory ( Identifier, Channel, Start, Duration ) VALUES ( {stream.Identifier}, {stream.Channel.Identifier}, FROM_UNIXTIME( {stream.StartedAt.ToUnixTimeSeconds()} ), {stream.Duration.TotalSeconds} ) ON DUPLICATE KEY UPDATE Duration = {stream.Duration.TotalSeconds};" );
				}

				// Repeat above until we are on the last page, if we are traversing
			} while ( nextPageCursor != null && traversePages == true );

		}
	}
}
