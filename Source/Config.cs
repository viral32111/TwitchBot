namespace TwitchBot {
	public static class Config {
		public static readonly string ApplicationDataDirectory = "TwitchBot";
		public static readonly string UserAccessTokenFileName = "UserAccessToken.json";

		public static readonly string OAuthBaseURI = "https://id.twitch.tv/oauth2";
		public static readonly string OAuthRedirectURI = "http://localhost:3000"; // https://dev.twitch.tv/console/apps

		public static readonly string ChatServerAddress = "irc-ws.chat.twitch.tv";
		public static readonly int ChatServerPort = 443;

		public static readonly string ChannelName = "Rawreltv";

		// https://github.com/cloudflare/cloudflared
		public static readonly string CloudflareTunnelVersion = "2022.8.0";
		public static readonly string CloudflareTunnelChecksum = "0aa0c6c576482399dfc098e6ff1969001ec924b3834b65ecb43ceac5bcd0a6c4";

        // https://dev.twitch.tv/docs/api/reference
        public static readonly string ApiBaseURI = "https://api.twitch.tv/helix"; // Helix is v5
	}
}
