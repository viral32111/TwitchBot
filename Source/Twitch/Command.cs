// https://dev.twitch.tv/docs/irc/commands

namespace TwitchBot.Twitch {
	public static class Command {

		// Sent after the bot authenticates with the server.
		public static readonly string GlobalUserState = "GLOBALUSERSTATE";

		// Sent to indicate the outcome of an action like banning a user.
		public static readonly string Notice = "NOTICE";

	}
}
