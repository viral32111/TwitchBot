namespace TwitchBot.Twitch {
	public class User {
		public int Identifier;
		public string Name;

		public string? Type = null;
		public string? Color = null;

		public string[]? Badges = null;
		public string? BadgeInformation = null;

		public string[]? EmoteSets = null;
		
		public User(
			string userId,
			string displayName,
			string? userType = null,
			string? color = null,
			string? badges = null,
			string? badgeInfo = null,
			string? emoteSets = null
			
		) {
			Identifier = int.Parse( userId );
			Name = displayName;

			if ( userType != null ) Type = userType;
			if ( color != null ) Color = color;
			if ( badges != null ) Badges = badges.Split( ',' );
			if ( badgeInfo != null ) BadgeInformation = badgeInfo;
			if ( emoteSets != null ) EmoteSets = emoteSets.Split( ',' );
		}
	}
}
