using Org.BouncyCastle.Utilities.IO;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TwitchBot.Features {
	public class Streak {

		// Holds a list of all streams in the streak, ordered from newest to oldest
		public readonly Stream[] Streams;

		// Constructor takes the progressive list of streams
		public Streak( List<Stream> streams ) {

			// Convert the list into a fixed-length array
			Streams = streams.ToArray();

			// Sort the streams from newest to oldest
			Array.Sort( Streams, ( Stream a, Stream b ) => {
				return b.StartedAt.CompareTo( a.StartedAt );
			} );

		}

		// Gets when the streak started (when the earliest stream started)
		public DateTimeOffset GetStartDate() {
			return Streams[ ^ 1 ].StartedAt;
		}

		// Gets the rounded-up duration (in days) of the streak (days elapsed since earliest stream)
		public int GetDuration() {
			return ( int ) Math.Ceiling( ( DateTimeOffset.UtcNow - GetStartDate() ).TotalDays );
		}

		// Gets the number of streams in this streak
		public int GetStreamCount() {
			return Streams.Length;
		}

		// Gets the total duration (in seconds) of all streams in the streak
		public int GetStreamDuration() {
			return Streams.Aggregate( 0, ( totalSeconds, stream ) => totalSeconds + stream.Duration );
		}

		/*****************************************************************/

		// The regular expression for parsing Twitch stream duration strings
		private static readonly Regex streamDurationPattern = new( @"^(?:(\d+)h)*(?:(\d+)m)*(?:(\d+)s)*$" );

		// Gets the current streaming streak 
		public static async Task<Streak?> FetchCurrentStreak( int channelIdentifier ) {

			// Update database with the latest stream data
			await UpdateStreamHistory( channelIdentifier );

			// Create a list to hold the streams that are in the streak
			List<Stream> streams = new();

			// When the previous stream finished, starts with the current date & time
			DateTimeOffset? previousStreamFinishedAt = DateTimeOffset.UtcNow;

			// Fetch all stream history from the database
			DbDataReader reader = await Database.QueryWithResults( $"SELECT Identifier, Channel, UNIX_TIMESTAMP( Start ) AS Start, Duration FROM StreamHistory WHERE Channel = {channelIdentifier} ORDER BY Start DESC;" );

			// Loop through each stream record...
			while ( await reader.ReadAsync() ) {

				// Parse the stream data
				Stream stream = new(
					reader.GetInt32( reader.GetOrdinal( "Identifier" ) ),
					reader.GetInt32( reader.GetOrdinal( "Channel" ) ),
					DateTimeOffset.FromUnixTimeSeconds( reader.GetInt64( reader.GetOrdinal( "Start" ) ) ),
					reader.GetInt32( reader.GetOrdinal( "Duration" ) )
				);

				// If the time between the last stream finishing and this stream starting is greater than 24 hours, end the loop
				if ( previousStreamFinishedAt.Value.Subtract( stream.StartedAt ).TotalSeconds > ( 86400 * 1.5 ) ) {
					Console.WriteLine( "IGNORE: {0} ({1}, {2}s)", stream.Identifier, stream.StartedAt, stream.Duration );
					//break;
				} else {
					Console.WriteLine( "STREAK: {0} {1} ({2}s)", stream.Identifier, stream.StartedAt, stream.Duration );

					// Add the stream to the list
					streams.Add( stream );

					// Update when the previous stream finished for the next iteration to use
					previousStreamFinishedAt = stream.StartedAt.AddSeconds( stream.Duration );

				}

			}

			// Close the record reader
			await reader.CloseAsync();

			// Return null if there is no streams
			if ( streams.Count < 1 ) return null;

			// Create a streak from these streams
			return new( streams );

		}

		// Updates the history of streams for a channel in the database
		// TODO: Implement initialUpdate boolean parameter which will go through all pages and populate the database - https://dev.twitch.tv/docs/api/guide#pagination
		public static async Task UpdateStreamHistory( int channelIdentifier ) {

			// Fetch the latest streams for this channel
			JsonObject streamsResponse = await Twitch.API.Request( "videos", queryString: new() {
				{ "first", "100" },
				{ "sort", "time" },
				{ "type", "archive" },
				{ "user_id", channelIdentifier.ToString() }
			} );

			// Loop through those fetched streams...
			foreach ( JsonNode? streamEntry in streamsResponse[ "data" ]!.AsArray() ) {

				// Skip entries that are null
				if ( streamEntry == null ) continue;
				JsonObject streamData = streamEntry.AsObject();

				// Get the unique identifier for this stream
				int streamIdentifier = int.Parse( streamData[ "id" ]!.GetValue<string>() );

				// Parse the date & time of when this stream started
				DateTimeOffset streamStartedAt = DateTimeOffset.ParseExact( streamData[ "created_at" ]!.GetValue<string>(), "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture );

				// Parse the stream duration as seconds
				Match durationMatch = streamDurationPattern.Match( streamData[ "duration" ]!.GetValue<string>() );
				if ( !durationMatch.Success ) throw new Exception( $"Failed to parse stream duration string: '{streamData[ "duration" ]}'" );
				_ = int.TryParse( durationMatch.Groups[ 1 ].Value, out int durationHours );
				_ = int.TryParse( durationMatch.Groups[ 2 ].Value, out int durationMinutes );
				_ = int.TryParse( durationMatch.Groups[ 3 ].Value, out int durationSeconds );
				int streamDuration = durationSeconds + ( durationMinutes * 60 ) + ( durationHours * 60 * 60 );

				Console.WriteLine( "Updating {0} ({1}, {2}) ({3})...", streamIdentifier, streamStartedAt, streamDuration, streamData[ "title" ] );

				// Add the stream to the database, or update its duration (the only value that may change) if it already exists
				await Database.Query( $"INSERT INTO StreamHistory ( Identifier, Channel, Start, Duration ) VALUES ( {streamIdentifier}, {channelIdentifier}, FROM_UNIXTIME( {streamStartedAt.ToUnixTimeSeconds()} ), {streamDuration} ) ON DUPLICATE KEY UPDATE Duration = {streamDuration};" );

			}

		}

	}

	public class Stream {
		public readonly int Identifier;
		public readonly int ChannelIdentifier;
		public readonly DateTimeOffset StartedAt;
		public readonly int Duration; // Seconds

		public Stream( int identifier, int channelIdentifier, DateTimeOffset startedAt, int duration ) {
			Identifier = identifier;
			ChannelIdentifier = channelIdentifier;
			StartedAt = startedAt;
			Duration = duration;
		}
	}
}
