namespace TwitchBot.Twitch {
	public class GlobalUser {
		public string Name;
		public int? Identifier;

		public string? Type = null;
		public string? Color = null;

		public string[]? Badges = null;
		public string? BadgeInformation = null;

		public string[]? EmoteSets = null;

		public GlobalUser( string name ) {
			Name = name;
		}
	}

	public class User {
		public GlobalUser Global;
		public Channel Channel;

		public bool? IsModerator = null;
		public bool? IsSubscriber = null;

		public User( GlobalUser user, Channel channel ) {
			Global = user;
			Channel = channel;
		}
	}
}
