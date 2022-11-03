using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/* Channel Tags:
 room-id=127154290
 emote-only=0
 followers-only=-1
 r9k=0
 rituals=0
 slow=10
 subs-only=0
*/

namespace TwitchBot.Twitch {
	public class Channel {

		// Static data from IRC message tags
		public readonly int Identifier; // room-id

		// Dynamic data from IRC message tags
		public bool IsEmoteOnly { get; private set; } // emote-only
		public int FollowersOnlyRequiredMinutes { get; private set; } // followers-only
		public bool IsSubscribersOnly { get; private set; } // subs-only
		public bool RequireUniqueMessages { get; private set; } // r9k
		public bool IsCelebrating { get; private set; } // rituals
		public int SlowModeCooldownSeconds { get; private set; } // slow

		// From IRC message parameter
		public string Name { get; private set; }

		// Other relevant objects
		//public readonly ChannelUser Broadcaster;
		private readonly Client Client;

		//public Dictionary<string, User> Users = new();

		// Creates a channel from an IRC message
		public Channel( InternetRelayChat.Message ircMessage, Client client ) { // User broadcaster, 

			// Set the static data
			Identifier = ExtractIdentifier( ircMessage );

			// Set the dynamic data
			UpdateProperties( ircMessage );

			// Set the name, forced to lowercase
			if ( ircMessage.Parameters == null ) throw new Exception( "IRC message does not contain a name for this channel" );
			Name = ircMessage.Parameters.ToLower();

			// Set relevant objects
			//Broadcaster = broadcaster;
			Client = client;

		}

		// Extracts the channel identifier from the IRC message tags
		public static int ExtractIdentifier( InternetRelayChat.Message ircMessage ) {
			if ( !ircMessage.Tags.TryGetValue( "room-id", out string? channelIdentifier ) || channelIdentifier == null ) throw new Exception( "IRC message does not contain an identifier tag for this channel" );
			return int.Parse( channelIdentifier );
		}

		// Updates the dynamic data from the IRC message tags
		public void UpdateProperties( InternetRelayChat.Message ircMessage ) {

			// Extract emote only mode as a boolean
			if ( !ircMessage.Tags.TryGetValue( "emote-only", out string? isEmoteOnly ) || isEmoteOnly == null ) throw new Exception( "IRC message does not contain an emote only tag for this channel" );
			IsEmoteOnly = bool.Parse( isEmoteOnly );

			// Extract followers only mode as an integer, forced to be zero or greater (zero means disabled)
			if ( !ircMessage.Tags.TryGetValue( "followers-only", out string? followersOnlyMinutes ) || followersOnlyMinutes == null ) throw new Exception( "IRC message does not contain a followers only tag for this channel" );
			FollowersOnlyRequiredMinutes = Math.Max( int.Parse( followersOnlyMinutes ), 0 );

			// Extract subscribers only mode as a boolean
			if ( !ircMessage.Tags.TryGetValue( "subs-only", out string? isSubscribersOnly ) || isSubscribersOnly == null ) throw new Exception( "IRC message does not contain a subscribers only tag for this channel" );
			IsSubscribersOnly = bool.Parse( isSubscribersOnly );

			// Extract require unique messages only mode as a boolean
			if ( !ircMessage.Tags.TryGetValue( "r9k", out string? requireUniqueMessages ) || requireUniqueMessages == null ) throw new Exception( "IRC message does not contain an r9k tag for this channel" );
			RequireUniqueMessages = bool.Parse( requireUniqueMessages );

			// Extract celebration in-progress as a boolean
			if ( !ircMessage.Tags.TryGetValue( "rituals", out string? isCelebrating ) || isCelebrating == null ) throw new Exception( "IRC message does not contain a rituals tag for this channel" );
			IsCelebrating = bool.Parse( isCelebrating );

			// Extract slow mode cooldown as an integer, forced to be zero or greater (zero means disabled)
			if ( !ircMessage.Tags.TryGetValue( "slow", out string? slowModeCooldownSeconds ) || slowModeCooldownSeconds == null ) throw new Exception( "IRC message does not contain a slow mode tag for this channel" );
			SlowModeCooldownSeconds = Math.Max( int.Parse( slowModeCooldownSeconds ), 0 );

		}

		// Sends a chat message, can be as a reply to another message
		public async Task SendMessage( string message, Message? replyTo = null ) => await Client.SendAsync( InternetRelayChat.Command.PrivateMessage,
			middle: $"#{Name}",
			parameters: message,
			tags: replyTo != null ? new() {
				{ "reply-parent-msg-id", replyTo.Identifier.ToString() }
			} : null
		);

		// Fetches a list of streams for this channel
		public async Task<Stream[]> FetchStreams( int limit = 100 ) {

			// Update the stream history in the database
			await UpdateStreamsInDatabase();

			// Fetch streams from the database
			DbDataReader reader = await Database.QueryWithResults( $"SELECT Identifier, UNIX_TIMESTAMP( Start ) AS StartedAt, Duration FROM StreamHistory WHERE Channel = ?identifier ORDER BY Start DESC LIMIT ?limit;", new() {
				{ "?identifier", Identifier },
				{ "?limit", limit }
			} );

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
					await Database.Query( $"INSERT INTO StreamHistory ( Identifier, Channel, Start, Duration ) VALUES ( ?streamIdentifier, ?channelIdentifier, FROM_UNIXTIME( ?startedAt ), ?duration ) ON DUPLICATE KEY UPDATE Duration = ?duration;", new() {
						{ "?streamIdentifier", stream.Identifier },
						{ "?channelIdentifier", stream.Channel.Identifier },
						{ "?startedAt", stream.StartedAt.ToUnixTimeSeconds() },
						{ "?duration", stream.Duration.TotalSeconds }
					} );
				}

				// Repeat above until we are on the last page, if we are traversing
			} while ( nextPageCursor != null && traversePages == true );

		}

	}
}
