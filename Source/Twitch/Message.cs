namespace TwitchBot.Twitch {
	public class Message {
		public Channel Channel { get; init; }
		public User User { get; init; }
		public string Content { get; init; }

		public Message( Channel channel, User user, string content ) {
			Channel = channel;
			User = user;
			Content = content;
		}
	}
}
