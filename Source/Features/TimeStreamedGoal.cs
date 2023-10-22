using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using TwitchBot.Twitch;

namespace TwitchBot.Features;

public class TimeStreamedGoal {

	// Holds the data about goals for each channel
	private static readonly Dictionary<int, TimeStreamedGoal> channelGoals = new();

	// Metadata about this goal
	public readonly int TargetHours;
	public readonly DateTime StartDate;
	public readonly string ProgressMessageTemplate;
	public readonly string AchievedMessageTemplate;
	public readonly string CompletionMessageTemplate;

	// Initialize the above metadata
	public TimeStreamedGoal( int targetHours, DateTime startDate, string progressMessageTemplate, string achievedMessageTemplate, string completionMessageTemplate ) {
		TargetHours = targetHours;
		StartDate = startDate;
		ProgressMessageTemplate = progressMessageTemplate;
		AchievedMessageTemplate = achievedMessageTemplate;
		CompletionMessageTemplate = completionMessageTemplate;
	}

	[ ModuleInitializer ] // Makes this method run when the program starts
	public static void Setup() {

		// Setup the channel goal for RawrelTV
		// TODO: Move this to the configuration file!
		/*channelGoals.Add( 127154290, new(
			targetHours: 100,
			startDate: new( 2022, 11, 1 ), // 1st of November 2022
			progressMessageTemplate: "I am trying to stream {0} hours throughout November, so far I have streamed for {1} hours!",
			achievedMessageTemplate: "I have achieved my goal to stream {0} hours throughout November, as so far I have streamed for {1} hours!",
			completionMessageTemplate: "I have reached my goal of streaming {0} hours throughout November!"
		) );*/

		// Register the chat command
		ChatCommand.Register( GoalProgressCommand );

	}

	// Chat command to check goal progress
	[ ChatCommand( "goal", new string[] { "100" } ) ]
	public static async Task GoalProgressCommand( Message message ) {

		// Get this channel's goal, if they have one
		if ( !channelGoals.TryGetValue( message.Author.Channel.Identifier, out TimeStreamedGoal? goal ) ) {
			Log.Warn( "No time streamed goal exists for channel '{0}' ({1})", message.Author.Channel.Name, message.Author.Channel.Identifier );
			await message.Reply( "This channel has no time streamed goal." );
			return;
		}

		// Get the goal progress
		double totalHoursStreamed = await GetGoalProgress( message.Author.Channel, goal );

		// Has the channel completed their goal?
		if ( totalHoursStreamed >= goal.TargetHours ) {
			await message.Reply( string.Format( goal.AchievedMessageTemplate, goal.TargetHours, totalHoursStreamed ) );

		// The channel has not completed their goal yet
		} else {
			await message.Reply( string.Format( goal.ProgressMessageTemplate, goal.TargetHours, totalHoursStreamed ) );
		}

	}

	// Posts a message in the channel's chat when they hit their goal
	// NOTE: This method is meant to be called often at an interval (e.g. every 5 minutes) until the goal is completed
	public static async Task<bool> AnnounceGoalCompletion( Channel channel ) {

		// Fail if this channel does not have a goal
		if ( !channelGoals.TryGetValue( channel.Identifier, out TimeStreamedGoal? goal ) ) throw new Exception( "Channel has no time streamed goal" );

		// Get the goal progress
		double totalHoursStreamed = await GetGoalProgress( channel, goal );

		// Has the channel completed their goal?
		if ( totalHoursStreamed >= goal.TargetHours ) {
			await channel.SendMessage( string.Format( goal.CompletionMessageTemplate, goal.TargetHours ) );
			return true;

		// The channel has not completed their goal yet
		} else return false;

	}

	// Gets the progress of the current goal
	public static async Task<double> GetGoalProgress( Channel channel, TimeStreamedGoal goal ) {
		Stream[] streams = await channel.FetchStreams();
		return Math.Floor( TotalStreamDuration( streams, goal.StartDate ).TotalHours );
	}

	// Totals the duration of each stream after a goal's start date
	public static TimeSpan TotalStreamDuration( Stream[] streams, DateTimeOffset startDate ) {
		return streams.Aggregate( new TimeSpan( 0, 0, 0 ), ( cumulativeDuration, stream ) => {
			if ( stream.StartedAt >= startDate ) cumulativeDuration += stream.Duration;
			return cumulativeDuration;
		} );
	}

}
