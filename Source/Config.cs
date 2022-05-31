namespace TwitchBot {
	public static class Config {
		public static readonly string ApplicationDataDirectory = "TwitchIntegration";

		public static readonly string OAuthBaseURI = "https://id.twitch.tv/oauth2";
		public static readonly string OAuthRedirectURI = "http://localhost:3000"; // https://dev.twitch.tv/console/apps

		public static readonly string ChatServerURI = "wss://irc-ws.chat.twitch.tv:443";
	}
}
