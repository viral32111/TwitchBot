using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TwitchBot.Twitch;

namespace TwitchBot.Features {
	public static class TimeStreamedGoal {
		[ModuleInitializer]
		public static void Setup() {
			ChatCommand.Register( TimeStreamedGoalCommand );
		}

		[ChatCommand( "goal", new string[] { "100" } )]
		public static async Task TimeStreamedGoalCommand( Message message ) {
			Stream[] streamHistory = await message.Channel.FetchStreams();

			//Console.WriteLine( "STREAM HISTORY: {0}", streamHistory.Length );

			DateTime startingDate = new( 2022, 11, 1 ); // 1st November 2022

			int cumulativeDurationSeconds = 0;
			foreach ( Stream stream in streamHistory ) {
				//Console.WriteLine( $"{stream.Identifier}: {stream.Duration} @ {stream.StartedAt})" );
				if ( stream.StartedAt < startingDate ) continue;
				cumulativeDurationSeconds += stream.Duration;
				//Console.WriteLine( $"ADD {stream.Duration} TO {cumulativeDurationSeconds}" );
			}
			//Console.WriteLine( $"TOTAL (SECS): {cumulativeDurationSeconds}" );
			TimeSpan totalDuration = new( 0, 0, cumulativeDurationSeconds );
			//Console.WriteLine( $"TOTAL (TIMESPAN): {totalDuration}" );

			await message.Reply( $"I am trying to stream 100 hours throughout November, so far I have streamed for {Math.Floor( totalDuration.TotalHours )} hours!" );
		}
	}
}
