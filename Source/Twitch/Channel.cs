using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using TwitchBot.Features;

namespace TwitchBot.Twitch {
	public class Channel {
		public Dictionary<string, User> Users = new();

		public string Name;

		public int? Identifier = null;

		public bool? IsEmoteOnly = null;
		public bool? IsFollowersOnly = null;
		public bool? IsSubscribersOnly = null;
		public bool? IsR9K = null;
		public bool? IsRituals = null;
		public bool? IsSlowMode = null;

		public Channel( string channelName ) {
			Name = channelName;
		}

		public async Task Send( Client client, string message ) {
			await client.SendAsync( InternetRelayChat.Command.PrivateMessage, middle: $"#{Name}", parameters: message );
		}

		public async Task<Stream[]> FetchStreams( int limit = 100 ) {
			if ( Identifier == null ) throw new Exception( "Cannot fetch streams of channel without channel identifier" );
			int channelIdentifier = ( int ) Identifier;

			// TODO: Move this method out of the streak feature into this class
			await Streak.UpdateStreamHistory( channelIdentifier );

			DbDataReader reader = await Database.QueryWithResults( $"SELECT Identifier, Channel, UNIX_TIMESTAMP( Start ) AS Start, Duration FROM StreamHistory WHERE Channel = {channelIdentifier} ORDER BY Start DESC LIMIT {limit};" );

			List<Stream> streams = new();
			while ( await reader.ReadAsync() ) streams.Add( new(
				reader.GetInt32( reader.GetOrdinal( "Identifier" ) ),
				reader.GetInt32( reader.GetOrdinal( "Channel" ) ),
				DateTimeOffset.FromUnixTimeSeconds( reader.GetInt64( reader.GetOrdinal( "Start" ) ) ),
				reader.GetInt32( reader.GetOrdinal( "Duration" ) )
			) );

			await reader.CloseAsync();

			return streams.ToArray();
		}
	}
}
