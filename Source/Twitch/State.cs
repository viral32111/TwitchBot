using System.Collections.Generic;

/* Unknown Tags:
client-nonce=640a320bc852e4bc9034e93feac64b38
flags=
*/

namespace TwitchBot.Twitch {
	public static class State {

		private static readonly Dictionary<int, Message> Messages = new();
		private static readonly Dictionary<int, Channel> Channels = new();
		private static readonly Dictionary<int, GlobalUser> GlobalUsers = new();
		private static readonly Dictionary<int, ChannelUser> ChannelUsers = new();

		/*********************************************************************************************/

		// This is insert instead of update because message's have nothing to update, they are purely static
		public static Message InsertMessage( InternetRelayChat.Message ircMessage, ChannelUser author, Channel channel ) {
			Message message = new( ircMessage, author, channel );
			Messages.Add( message.Identifier, message );
			return message;
		}

		public static Message? GetMessage( int identifier ) => Messages[ identifier ];

		/*********************************************************************************************/

		public static Channel UpdateChannel( InternetRelayChat.Message ircMessage, Client client ) {
			int identifier = Channel.ExtractIdentifier( ircMessage );

			if ( Channels.TryGetValue( identifier, out Channel? channel ) && channel != null ) {
				channel.UpdateProperties( ircMessage );
			} else {
				channel = new( ircMessage, client );
				Channels.Add( channel.Identifier, channel );
			}

			return channel;
		}

		public static Channel? GetChannel( int identifier ) => Channels[ identifier ];

		/*********************************************************************************************/

		public static GlobalUser UpdateGlobalUser( InternetRelayChat.Message ircMessage ) {
			int identifier = GlobalUser.ExtractIdentifier( ircMessage );

			if ( GlobalUsers.TryGetValue( identifier, out GlobalUser? globalUser ) && globalUser != null ) {
				globalUser.UpdateProperties( ircMessage );
			} else {
				globalUser = new( ircMessage );
				GlobalUsers.Add( globalUser.Identifier, globalUser );
			}

			return globalUser;
		}

		public static GlobalUser? GetGlobalUser( int identifier ) => GlobalUsers[ identifier ];

		/*********************************************************************************************/

		public static ChannelUser UpdateChannelUser( InternetRelayChat.Message ircMessage, Channel channel ) {
			int identifier = GlobalUser.ExtractIdentifier( ircMessage );

			if ( ChannelUsers.TryGetValue( identifier, out ChannelUser? channelUser ) && channelUser != null ) {
				channelUser.UpdateProperties( ircMessage );
			} else {
				channelUser = new( ircMessage, channel );
				ChannelUsers.Add( channelUser.Identifier, channelUser );
			}

			return channelUser;
		}

		public static ChannelUser? GetChannelUser( int identifier ) => ChannelUsers[ identifier ];

	}
}
