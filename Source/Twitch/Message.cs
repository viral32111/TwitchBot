using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/*
@badge-info=
badges=moderator/1
client-nonce=640a320bc852e4bc9034e93feac64b38
color=#FF0000
display-name=viral32111_
emotes=
first-msg=0
flags=
id=f93c6fec-e157-4224-8035-1b6f148a1ff8
mod=1
returning-chatter=0
room-id=127154290
subscriber=0
tmi-sent-ts=1667397167274
turbo=0;user-id=675961583
user-type=mod

:viral32111_!viral32111_@viral32111_.tmi.twitch.tv PRIVMSG #rawreltv :aa
*/

namespace TwitchBot.Twitch {
	public class Message {
		public readonly int Identifier;
		public readonly string Content;

		public readonly User User;
		public readonly Channel Channel;

		public Message( string content, Dictionary<string, string?> ircMessageTags, User user, Channel channel ) {
			if ( !ircMessageTags.TryGetValue( "id", out string? identifier ) || identifier == null ) throw new Exception( "Message does not contain a valid identifier tag" );

			Identifier = int.Parse( identifier );
			Content = content;

			User = user;
			Channel = channel;
		}

		public async Task Reply( string content ) {
			await Channel.SendMessage( content, replyTo: Identifier );
		}
	}
}
