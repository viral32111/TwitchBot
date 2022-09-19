using System.Collections.Generic;
using System.Threading.Tasks;

namespace TwitchBot.Twitch {
	public class Channel {
		public Dictionary<string, User> Users = new();

		public string Name;

		public int? Identifier = null;

		public bool? IsEmoteOnly = null;
		public bool? IsFollowersOnly = null;
		public bool? IsSubscribersOnly = null;
		public bool? IsR9K = null;
		public bool? IsRituals = null;
		public bool? IsSlowMode = null;

		public Channel( string channelName ) {
			Name = channelName;
		}

		public async Task Send( Client client, string message ) {
			await client.SendAsync( InternetRelayChat.Command.PrivateMessage, $"#{Name} :{message}" );
		}
	}
}
