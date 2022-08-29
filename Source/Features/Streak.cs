using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TwitchBot.Features {
	public class Streak {

		// The persistent file that stream history is recorded in
		private static readonly string streamHistoryFilePath = Path.Combine( Config.DataDirectory, "stream-history.json" );

		// The regular expression for parsing Twitch stream duration strings
		private static readonly Regex streamDurationPattern = new( @"^(?:(\d+)h)*(?:(\d+)m)*(?:(\d+)s)*$" );

		// Information about the streak
		public int Duration = 0; // Duration of streak, in days
		public int StreamCount = 0; // Number of streams
		public int StreamDuration = 0; // Total time streamed, in seconds
		public DateTimeOffset StartedAt = DateTimeOffset.MinValue; // When it started

		// Fetches the latest streams for a channel & updates the history file
		public static async Task UpdateStreamHistory( int channelId ) {

			// Read the history file, or create a new one with empty history for this channel
			Storage historyStorage;
			try {
				historyStorage = Storage.ReadFile( streamHistoryFilePath );
			} catch ( FileNotFoundException ) {
				historyStorage = Storage.CreateFile( streamHistoryFilePath, new() {
					[ channelId.ToString() ] = new JsonObject()
				} );
			}

			// Get the history for this channel, or empty history if the channel is not in the history file
			JsonObject channelHistory;
			if ( historyStorage.HasProperty( channelId.ToString() ) ) {
				channelHistory = historyStorage.Get<JsonObject>( channelId.ToString() );
			} else {
				channelHistory = new();
			}

			// Fetch the last 100 streams for this channel
			JsonObject streamsResponse = await Twitch.API.Request( "videos", queryString: new() {
				{ "first", "100" },
				{ "sort", "time" },
				{ "type", "archive" },
				{ "user_id", channelId.ToString() }
			} );

			// Loop through those fetched streams...
			foreach ( JsonNode? responseEntry in streamsResponse[ "data" ]!.AsArray() ) {

				// Skip entries that are null
				if ( responseEntry == null ) continue;
				JsonObject stream = responseEntry.AsObject();

				// Store basic information about the stream
				int streamId = int.Parse( stream[ "id" ]!.GetValue<string>() );
				string streamTitle = stream[ "title" ]!.GetValue<string>();

				// Parse the date & time of when the stream happened
				DateTimeOffset streamCreatedAt = DateTimeOffset.ParseExact( stream[ "created_at" ]!.GetValue<string>(), "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture );

				// Parse the stream duration as seconds
				Match durationMatch = streamDurationPattern.Match( stream[ "duration" ]!.GetValue<string>() );
				if ( !durationMatch.Success ) throw new Exception( $"Failed to parse stream duration string: '{stream[ "duration" ]}'" );
				_ = int.TryParse( durationMatch.Groups[ 1 ].Value, out int durationHours );
				_ = int.TryParse( durationMatch.Groups[ 2 ].Value, out int durationMinutes );
				_ = int.TryParse( durationMatch.Groups[ 3 ].Value, out int durationSeconds );
				int streamDuration = durationSeconds + ( durationMinutes * 60 ) + ( durationHours * 60 * 60 );

				// Has this stream already been recorded in the history for this channel?
				if ( channelHistory.TryGetPropertyValue( streamId.ToString(), out JsonNode? historyEntry ) ) {

					// Skip entries that are null
					if ( historyEntry == null ) continue;
					JsonObject streamEntry = historyEntry.AsObject();

					//Console.WriteLine( "Stream {0} already exists in channel history! Updating...", streamId );

					// Update the information about this stream
					streamEntry[ "title" ] = streamTitle;
					streamEntry[ "created" ] = streamCreatedAt.ToUnixTimeSeconds();
					streamEntry[ "duration" ] = streamDuration;

					// Update the entry in the channel history
					channelHistory[ streamId.ToString() ] = streamEntry;

					// Is this the first time this stream has been seen?
				} else {

					//Console.WriteLine( "Stream {0} does not exist in channel history! Creating...", streamId );

					// Record the information about this stream in the channel history
					channelHistory[ streamId.ToString() ] = new JsonObject() {
						[ "title" ] = streamTitle,
						[ "created" ] = streamCreatedAt.ToUnixTimeSeconds(),
						[ "duration" ] = streamDuration
					};

				}

			}

			// Update the history file with the updated channel history
			historyStorage.SetProperty( channelId.ToString(), channelHistory );

			// Save the file to persist the changes
			historyStorage.Save();

		}

		public static async Task<Streak?> GetLatestStreak( int channelId ) {

			// Update with the latest stream information
			await UpdateStreamHistory( channelId );

			// Create blank streak object
			Streak streak = new();

			// Read the history file
			Storage historyStorage;
			try {
				historyStorage = Storage.ReadFile( streamHistoryFilePath );
			} catch ( FileNotFoundException ) {
				return null;
			}

			// Get the history for this channel
			JsonObject channelHistory;
			if ( historyStorage.HasProperty( channelId.ToString() ) ) {
				channelHistory = historyStorage.Get<JsonObject>( channelId.ToString() );
			} else {
				return null;
			}

			// Holds when the previous stream finished, starts with the current date & time
			DateTimeOffset? previousStreamFinishedAt = DateTimeOffset.UtcNow;

			// Loop through the stream history for this channel...
			foreach ( KeyValuePair<string, JsonNode?> historyEntry in channelHistory ) {

				// Skip entries that are null
				if ( historyEntry.Value == null ) continue;
				JsonObject streamInfo = historyEntry.Value.AsObject();

				// Store the stream ID
				int streamId = int.Parse( historyEntry.Key );

				// Store information about the stream
				string streamTitle = streamInfo[ "title" ]!.GetValue<string>();
				DateTimeOffset streamCreatedAt = DateTimeOffset.FromUnixTimeSeconds( streamInfo[ "created" ]!.GetValue<long>() );
				int streamDuration = streamInfo[ "duration" ]!.GetValue<int>();

				// If the time between the last stream finishing and this stream starting is greater than 24 hours, end the loop
				if ( previousStreamFinishedAt.Value.Subtract( streamCreatedAt ).TotalSeconds > ( 86400 * 1.5 ) ) break;

				// Update the streak statistics
				streak.StreamCount += 1;
				streak.StreamDuration += streamDuration;
				
				// Update when the streak started with when this stream started
				streak.StartedAt = streamCreatedAt;

				// Update when the previous stream finished for the next iteration to use
				previousStreamFinishedAt = streamCreatedAt.AddSeconds( streamDuration );

				Console.WriteLine( "[{0}] {1} ({2}s) '{3}'", streamId, streamCreatedAt, streamDuration, streamTitle );

			}

			// Was there even a streak?
			if ( streak.StreamCount < 1 ) return null;

			// Calculate the amount of days between the start of the streak and now
			streak.Duration = ( int ) ( DateTimeOffset.UtcNow - streak.StartedAt ).TotalDays;

			// Return the updated streak object
			return streak;

		}

	}
}
