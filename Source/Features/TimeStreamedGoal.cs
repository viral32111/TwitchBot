using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TwitchBot.Twitch;

namespace TwitchBot.Features {
	public class TimeStreamedGoal {

		// Holds the data about goals for each channel
		private static readonly Dictionary<int, TimeStreamedGoal> channelGoals = new();

		// Metadata about this goal
		public readonly int TargetHours;
		public readonly DateTime StartDate;
		public readonly string MessageTemplate;

		// Initialize the above metadata
		public TimeStreamedGoal( int targetHours, DateTime startDate, string messageTemplate ) {
			TargetHours = targetHours;
			StartDate = startDate;
			MessageTemplate = messageTemplate;
		}

		[ModuleInitializer] // Makes this method run when the program starts
		public static void Setup() {

			// Setup the channel goal for RawrelTV
			// TODO: Move this to the configuration file!
			channelGoals.Add( 127154290, new(
				targetHours: 100,
				startDate: new( 2022, 11, 1 ), // 1st of November 2022
				messageTemplate: "I am trying to stream {0} hours throughout November, so far I have streamed for {1} hours!"
			) );

			// Register the chat command
			ChatCommand.Register( GoalProgressCommand );

		}

		// Chat command to check goal progress
		[ChatCommand( "goal", new string[] { "100" } )]
		public static async Task GoalProgressCommand( Message message ) {

			// Get this channel's goal, if they have one
			if ( !channelGoals.TryGetValue( message.Channel.Identifier, out TimeStreamedGoal? goal ) ) {
				Log.Warn( "No time streamed goal exists for channel '{0}' ({1})", message.Channel.Name, message.Channel.Identifier );
				await message.Reply( "This channel has no time streamed goal." );
				return;
			}

			// Fetch a list of this channel's streams
			Stream[] streams = await message.Channel.FetchStreams();

			// Total the duration of each stream after the goal's start date
			TimeSpan totalDuration = streams.Aggregate( new TimeSpan( 0, 0, 0 ), ( cumulativeDuration, stream ) => {
				if ( stream.StartedAt >= goal.StartDate ) cumulativeDuration += stream.Duration;
				return cumulativeDuration;
			} );

			// Format the goal's message & send it back in Twitch chat
			await message.Reply( string.Format( goal.MessageTemplate, Math.Floor( totalDuration.TotalHours ) ) );

		}
	}
}
