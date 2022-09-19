// https://www.alien.net.au/irc/irc2numerics.html

namespace TwitchBot.InternetRelayChat {
	public static class Command {

		// Post-registration greeting
		public static readonly string Welcome = "001";
		public static readonly string YourHost = "002";
		public static readonly string Created = "003";
		public static readonly string MyInfo = "004";

		// Message of the day
		public static readonly string MoTD = "372";
		public static readonly string MoTDStart = "375";
		public static readonly string MoTDEnd = "376";

		// List users in the channel
		public static readonly string Names = "353";
		public static readonly string NamesEnd = "366";

		// Users joining & leaving - https://datatracker.ietf.org/doc/html/rfc1459#section-4.2.1
		public static readonly string Join = "JOIN";
		public static readonly string Leave = "PART";

		// Keeping connection alive - https://datatracker.ietf.org/doc/html/rfc1459#section-4.6.2
		public static readonly string Ping = "PING";
		public static readonly string Pong = "PONG";

		// Private message - https://datatracker.ietf.org/doc/html/rfc1459#section-4.4.1
		public static readonly string PrivateMessage = "PRIVMSG";

		// End a client session - https://datatracker.ietf.org/doc/html/rfc1459#section-4.1.6
		public static readonly string Quit = "QUIT";

		// Private message, except no automatic replies - https://datatracker.ietf.org/doc/html/rfc1459#section-4.4.2
		public static readonly string Notice = "NOTICE";

		// Requesting capabilities -- https://ircv3.net/specs/extensions/capability-negotiation.html
		public static readonly string RequestCapabilities = "CAP REQ";

		// User registration - https://datatracker.ietf.org/doc/html/rfc1459#section-4.1
		public static readonly string Username = "USER";
		public static readonly string Password = "PASS";

		// Change/set nickname - https://datatracker.ietf.org/doc/html/rfc1459#section-4.1.2
		public static readonly string Nickname = "NICK";

	}
}
