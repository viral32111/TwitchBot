using System;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TwitchBot.Twitch;

namespace TwitchBot.Database.Documents {
	public class StreamDocument {
		[BsonId]
		public readonly int Identifier;

		[BsonElement]
		public readonly int Channel;

		[BsonElement]
		public readonly DateTimeOffset Began;

		[BsonElement]
		public readonly int Duration;

		[BsonConstructor]
		public StreamDocument( int identifier, int channel, DateTime began, int duration ) {
			Identifier = identifier;
			Channel = channel;
			Began = began;
			Duration = duration;
		}

		public StreamDocument( Stream stream ) {
			Identifier = stream.Identifier;
			Channel = stream.Channel.Identifier;
			Began = stream.StartedAt;
			Duration = ( int ) stream.Duration.TotalSeconds;
		}
	}
}
