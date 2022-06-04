using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Enumerations for Twitch-specific IRC capabilities
// https://dev.twitch.tv/docs/irc/capabilities

namespace TwitchBot.Twitch {
	public static class Capability {

		// Lets your bot send PRIVMSG messages that include Twitch chat commands and receive Twitch-specific IRC messages.
		public static readonly string Commands = "twitch.tv/commands";

		// Lets your bot receive JOIN and PART messages when users join and leave the chat room.
		public static readonly string Membership = "twitch.tv/membership";

		// Adds additional metadata to the command and membership messages.
		public static readonly string Tags = "twitch.tv/tags";

	}
}
