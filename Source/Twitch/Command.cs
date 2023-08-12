// https://dev.twitch.tv/docs/irc/commands

namespace TwitchBot.Twitch;

public static class Command {

	// Sent after the bot authenticates with the server.
	public static readonly string GlobalUserState = "GLOBALUSERSTATE";

	// Sent to indicate the outcome of an action like banning a user.
	public static readonly string Notice = "NOTICE";

	// Sent when the bot joins a channel or sends a PRIVMSG message.
	public static readonly string UserState = "USERSTATE";

	// Sent when the bot joins a channel or when the channel’s chat settings change.
	public static readonly string RoomState = "ROOMSTATE";

}
