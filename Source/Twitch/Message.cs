using System.Threading.Tasks;

namespace TwitchBot.Twitch {
	public class Message {
		public Channel Channel { get; init; }
		public User User { get; init; }
		public string Content { get; init; }
		public Client Client { get; init; }

		public Message( Channel channel, User user, string content, Client client ) {
			Channel = channel;
			User = user;
			Content = content;
			Client = client;
		}

		public async Task Reply( string content ) {
			await Channel.Send( Client, content );
		}
	}
}
