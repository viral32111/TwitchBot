namespace TwitchBot {
	public static class Config {
		public static readonly string ApplicationDataDirectory = "TwitchBot";

		public static readonly string OAuthBaseURI = "https://id.twitch.tv/oauth2";
		public static readonly string OAuthRedirectURI = "http://localhost:3000"; // https://dev.twitch.tv/console/apps

		public static readonly string ChatServerAddress = "irc-ws.chat.twitch.tv";
		public static readonly int ChatServerPort = 443;

		public static readonly string ChannelName = "Rawreltv";
	}
}
