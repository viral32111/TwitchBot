// https://www.alien.net.au/irc/irc2numerics.html

namespace TwitchBot.InternetRelayChat {
	public static class Command {
		public static readonly string Welcome = "001";
		public static readonly string YourHost = "002";
		public static readonly string Created = "003";
		public static readonly string MyInfo = "004";

		public static readonly string MoTD = "372";
		public static readonly string MoTDStart = "375";
		public static readonly string MoTDEnd = "376";

		public static readonly string Names = "353";
		public static readonly string NamesEnd = "366";

		public static readonly string Join = "JOIN";
		public static readonly string Leave = "PART";
	}
}
