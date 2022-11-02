using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TwitchBot.Twitch {
	public class Stream {
		private static readonly Regex DurationPattern = new( @"^(?:(\d+)h)*(?:(\d+)m)*(?:(\d+)s)*$" );

		public readonly int Identifier;
		public readonly DateTimeOffset StartedAt;
		public readonly TimeSpan Duration;

		public readonly Channel Channel;

		public Stream( int identifier, DateTimeOffset startedAt, int durationSeconds, Channel channel ) {
			Identifier = identifier;
			StartedAt = startedAt;
			Duration = new( 0, 0, durationSeconds );
			Channel = channel;
		}

		public Stream( JsonObject apiData, Channel channel ) {
			Match durationMatch = DurationPattern.Match( apiData[ "duration" ]!.GetValue<string>() );
			if ( !durationMatch.Success ) throw new Exception( "Failed to parse stream duration" );

			Identifier = int.Parse( apiData[ "id" ]!.GetValue<string>() );
			StartedAt = DateTimeOffset.ParseExact( apiData[ "created_at" ]!.GetValue<string>(), "yyyy-MM-dd\\THH:mm:ss\\Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal );
			Duration = new(
				int.Parse( durationMatch.Groups[ 1 ].Value.NullIfEmpty() ?? "0" ), // Hours
				int.Parse( durationMatch.Groups[ 2 ].Value.NullIfEmpty() ?? "0" ), // Minutes
				int.Parse( durationMatch.Groups[ 3 ].Value.NullIfEmpty() ?? "0" ) // Seconds
			);

			Channel = channel;
		}
	}
}
