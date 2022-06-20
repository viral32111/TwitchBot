// https://dev.twitch.tv/docs/irc/msg-id

// https://dev.twitch.tv/docs/irc/tags

// @badge-info=;badges=broadcaster/1;client-nonce=48fd35da166cdc8f3cf897f867e2b6fe;color=#FF0000;display-name=viral32111_;emotes=;first-msg=1;flags=;id=443d634f-ebf4-4cd7-8821-86fb8de658c5;mod=0;room-id=675961583;subscriber=0;tmi-sent-ts=1654201369060;turbo=0;user-id=675961583;user-type= :viral32111_!viral32111_@viral32111_.tmi.twitch.tv PRIVMSG #viral32111_ :test
// :viral32111_!viral32111_@viral32111_.tmi.twitch.tv PRIVMSG #rawreltv :test
// :rawreltv!rawreltv@rawreltv.tmi.twitch.tv PRIVMSG #rawreltv :This is a test message with many letters
// : NICK !USER @HOST COMMAND #CHANNEL :MESSAGE

// if all user IDs remain consistent across messages then I guess somewhere a state should be maintained and then updated every time a message is received
// State.UpdateUser( identifier, { name, color, mod, subscriber, ... } )
// Wanna avoid instansiating a new User() for every message received cus that might cause quite high memory usage?

// Channels -> Messages -> Users

namespace TwitchBot.Twitch {
	// @emote-only=0;followers-only=-1;r9k=0;room-id=127154290;slow=0;subs-only=0 :tmi.twitch.tv ROOMSTATE :#rawreltv'

	public class Channel {
		public Dictionary<string, User> Users = new();

		private readonly Client? client;

		public string Name;

		public int? Identifier = null; // room-id=675961583;

		public bool? IsEmoteOnly = null;
		public bool? IsFollowersOnly = null;
		public bool? IsSubscribersOnly = null;
		public bool? IsR9K = null;
		public bool? IsRituals = null;
		public bool? IsSlowMode = null;

		public Channel( Client theClient, string channelName ) {
			client = theClient;

			Name = channelName;
		}

		public Channel( string channelName ) {
			Name = channelName;
		}

		public async Task Send( string message ) {
			await client.SendMessage( $"PRIVMSG #{ Name } :{ message }", expectResponse: false );
		}
	}

	/*public class User {
		public string? Nick = null; // viral32111_
		public string? User_ = null; // !viral32111_

		// Requires TAGS capability
		public string? DisplayName = null; // display-name=viral32111_;
		public string? Color = null; // color=#FF0000;
		public int? Identifier = null; // user-id=675961583;
		public string? Type = null; // user-type=
		public bool? IsModerator = null; // mod=0;
		public bool? IsSubscriber = null; // subscriber=0;
		public bool? IsTurbo = null; // turbo=0;
		public string? BadgeInformation = null; // badge-info=;
		public string? Badges = null; // badges=broadcaster/1;
	}*/

	/*public class Message {
		public string? Server = null; // @viral32111_.tmi.twitch.tv

		//public string? Channel = null; // #rawreltv
		public string? Content = null; // test

		// Requires TAGS capability
		public string? Identifier = null; // id=443d634f-ebf4-4cd7-8821-86fb8de658c5;
		public bool? IsUsersFirstMessage = null; // first-msg=1;
		//public int? RoomIdentifier = null; 
		public int? SentAt = null; // tmi-sent-ts=1654201369060;
		public string? Emotes = null; // emotes=62835:0-10;
		public string? Flags = null; // flags=;

		public string? EmoteOnly = null; // emote-only=1;

		public string? ClientNonce = null; // client-nonce=48fd35da166cdc8f3cf897f867e2b6fe;

		// reply-parent-msg-id=uuid-here;
	}*/
}
