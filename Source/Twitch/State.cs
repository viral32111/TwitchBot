using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchBot.Twitch {
	public static class State {

		private static readonly Dictionary<string, GlobalUser> globalUserState = new();
		private static readonly Dictionary<string, Channel> channelState = new();

		public static GlobalUser UpdateGlobalUser( Dictionary<string, string?> tags ) {
			GlobalUser? currentUser = null;

			// Identifiers
			tags.TryGetValue( "display-name", out string? displayName );
			tags.TryGetValue( "user-id", out string? userId ); // Included in GLOBALUSERSTATE

			if ( displayName == null && userId == null ) throw new Exception( "Cannot update user in state without any identifying tags" );

			// Properties
			tags.TryGetValue( "user-type", out string? userType );
			tags.TryGetValue( "color", out string? color );
			tags.TryGetValue( "badges", out string? badges );
			tags.TryGetValue( "badge-information", out string? badgeInformation );
			tags.TryGetValue( "emote-sets", out string? emoteSets );

			// Get user in state by name or identifier
			if ( displayName != null ) {
				globalUserState.TryGetValue( displayName.ToLower(), out currentUser );
			} else if ( userId != null ) {
				int userIdentifier = int.Parse( userId );

				foreach ( GlobalUser user in globalUserState.Values.ToArray() ) {
					if ( user.Identifier == userIdentifier ) {
						currentUser = user;
						break;
					}
				}
			}

			// User already exists in state
			if ( currentUser != null ) {
				if ( !string.IsNullOrEmpty( displayName ) ) currentUser.Name = displayName;
				if ( userId != null ) currentUser.Identifier = int.Parse( userId );

				if ( !string.IsNullOrEmpty( userType ) ) currentUser.Type = userType;
				if ( !string.IsNullOrEmpty( color ) ) currentUser.Color = color;
				if ( !string.IsNullOrEmpty( badges ) ) currentUser.Badges = badges?.Split( ',' );
				if ( !string.IsNullOrEmpty( badgeInformation ) ) currentUser.BadgeInformation = badgeInformation;
				if ( !string.IsNullOrEmpty( emoteSets ) ) currentUser.EmoteSets = emoteSets?.Split( ',' );

				return currentUser;

				// User does not exist in state yet...
			} else {
				if ( string.IsNullOrEmpty( displayName ) ) throw new Exception( "Cannot create new user in state without name" );

				GlobalUser newUser = new( displayName );

				if ( userId != null ) newUser.Identifier = int.Parse( userId );

				newUser.Type = userType;
				newUser.Color = color;
				newUser.Badges = badges?.Split( ',' );
				newUser.BadgeInformation = badgeInformation;
				newUser.EmoteSets = emoteSets?.Split( ',' );

				globalUserState.Add( displayName.ToLower(), newUser );

				return newUser;
			}
		}

		public static User UpdateUser( Channel channel, Dictionary<string, string?> tags ) {
			GlobalUser globalUser = UpdateGlobalUser( tags );

			channel.Users.TryGetValue( globalUser.Name.ToLower(), out User? currentUser );

			tags.TryGetValue( "mod", out string? mod );
			tags.TryGetValue( "subscriber", out string? subscriber );

			if ( currentUser != null ) {
				if ( !string.IsNullOrEmpty( mod ) ) currentUser.IsModerator = ( mod == "1" );
				if ( !string.IsNullOrEmpty( subscriber ) ) currentUser.IsSubscriber = ( subscriber == "1" );

				return currentUser;

			} else {
				User newUser = new( globalUser, channel ) {
					IsModerator = ( mod == "1" ),
					IsSubscriber = ( subscriber == "1" )
				};

				channel.Users.Add( globalUser.Name.ToLower(), newUser );

				return newUser;
			}
		}

		public static Channel UpdateChannel( string channelName, Dictionary<string, string?> tags, Client client ) {
			channelName = channelName.ToLower();

			channelState.TryGetValue( channelName, out Channel? currentChannel );

			if ( !tags.TryGetValue( "room-id", out string? roomId ) || roomId == null ) throw new Exception( "Message contains no valid room identifier" );
			int channelIdentifier = int.Parse( roomId );

			tags.TryGetValue( "emote-only", out string? emoteOnly );
			tags.TryGetValue( "followers-only", out string? followersOnly );
			tags.TryGetValue( "subs-only", out string? subsOnly );
			tags.TryGetValue( "r9k", out string? r9k );
			tags.TryGetValue( "rituals", out string? rituals );
			tags.TryGetValue( "slow", out string? slow );

			if ( currentChannel != null ) {
				if ( !string.IsNullOrEmpty( emoteOnly ) ) currentChannel.IsEmoteOnly = ( emoteOnly == "1" );
				if ( !string.IsNullOrEmpty( followersOnly ) ) currentChannel.IsFollowersOnly = ( followersOnly == "1" );
				if ( !string.IsNullOrEmpty( subsOnly ) ) currentChannel.IsSubscribersOnly = ( subsOnly == "1" );
				if ( !string.IsNullOrEmpty( r9k ) ) currentChannel.IsR9K = ( r9k == "1" );
				if ( !string.IsNullOrEmpty( rituals ) ) currentChannel.IsRituals = ( rituals == "1" );
				if ( !string.IsNullOrEmpty( slow ) ) currentChannel.IsSlowMode = ( slow == "1" );

				return currentChannel;

			} else {
				Channel newChannel = new( channelIdentifier, channelName, client ) {
					IsEmoteOnly = ( emoteOnly == "1" ),
					IsFollowersOnly = ( followersOnly == "1" ),
					IsSubscribersOnly = ( subsOnly == "1" ),
					IsR9K = ( r9k == "1" ),
					IsRituals = ( rituals == "1" ),
					IsSlowMode = ( slow == "1" )
				};

				channelState.Add( channelName, newChannel );

				return newChannel;
			}
		}

		public static User GetOrCreateUser( Channel channel, string userName ) {
			channel.Users.TryGetValue( userName.ToLower(), out User? existingUser );
			if ( existingUser != null ) return existingUser;

			globalUserState.TryGetValue( userName.ToLower(), out GlobalUser? globalUser );
			if ( globalUser == null ) {
				globalUser = new( userName );
				globalUserState.Add( userName.ToLower(), globalUser );
			}

			User newUser = new( globalUser, channel );
			channel.Users.Add( userName.ToLower(), newUser );

			return newUser;
		}

		public static Channel GetOrCreateChannel( int channelIdentifier, string channelName, Client client ) {
			channelName = channelName.ToLower();

			foreach ( Channel currentChannel in channelState.Values.ToArray() ) {
				if ( currentChannel.Identifier == channelIdentifier ) return currentChannel;
			}

			Channel newChannel = new( channelIdentifier, channelName, client );
			channelState.Add( channelName, newChannel );

			return newChannel;
		}

	}
}
