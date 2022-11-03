using System;

/* Channel User Tags:
 badges=moderator/1
 mod=1
 subscriber=0
 turbo=0
 user-type=mod
 first-msg=0
 returning-chatter=0
*/

namespace TwitchBot.Twitch {
	public class ChannelUser : GlobalUser {

		// Dynamic data from IRC message tags
		public bool IsModerator { get; private set; } // mod
		public bool IsSubscriber { get; private set; } // subscriber
		public bool IsTurbo { get; private set; } // turbo
		public bool IsFirstMessager { get; private set; } // first-msg
		public bool IsReturningChatter { get; private set; } // returning-chatter
		public string Badges { get; private set; } = null!; // badges
		public string Type { get; private set; } = null!; // user-type

		// Other relevant data
		public readonly Channel Channel;

		// Creates a channel user (and thus global user) from an IRC message
		public ChannelUser( InternetRelayChat.Message ircMessage, Channel channel ) : base( ircMessage ) {
			
			// Set dynamic data
			UpdateProperties( ircMessage );

			// Set relevant data
			Channel = channel;

		}

		// Updates the dynamic data from the IRC message tags
		public new void UpdateProperties( InternetRelayChat.Message ircMessage ) {
			base.UpdateProperties( ircMessage );

			if ( !ircMessage.Tags.TryGetValue( "mod", out string? isModerator ) || string.IsNullOrWhiteSpace( isModerator ) ) throw new Exception( "IRC message does not contain a moderator tag for this channel user" );
			IsModerator = bool.Parse( isModerator );

			if ( !ircMessage.Tags.TryGetValue( "subscriber", out string? isSubscriber ) || string.IsNullOrWhiteSpace( isSubscriber ) ) throw new Exception( "IRC message does not contain a subscriber tag for this channel user" );
			IsSubscriber = bool.Parse( isSubscriber );

			if ( !ircMessage.Tags.TryGetValue( "turbo", out string? isTurbo ) || string.IsNullOrWhiteSpace( isTurbo ) ) throw new Exception( "IRC message does not contain a turbo tag for this channel user" );
			IsTurbo = bool.Parse( isTurbo );

			if ( !ircMessage.Tags.TryGetValue( "first-msg", out string? isFirstMessager ) || string.IsNullOrWhiteSpace( isFirstMessager ) ) throw new Exception( "IRC message does not contain a first message tag for this channel user" );
			IsFirstMessager = bool.Parse( isFirstMessager );

			if ( !ircMessage.Tags.TryGetValue( "returning-chatter", out string? isReturningChatter ) || string.IsNullOrWhiteSpace( isReturningChatter ) ) throw new Exception( "IRC message does not contain a returning chatter tag for this channel user" );
			IsReturningChatter = bool.Parse( isReturningChatter );

			if ( !ircMessage.Tags.TryGetValue( "badges", out string? badges ) || badges == null ) throw new Exception( "IRC message does not contain a badges tag for this channel user" );
			Badges = badges;

			if ( !ircMessage.Tags.TryGetValue( "user-type", out string? type ) || type == null ) throw new Exception( "IRC message does not contain a type tag for this channel user" );
			Type = type;

		}

	}
}
