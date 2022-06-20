using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot.Twitch {
	public static class State {

		//private static Client clientState;
		private static readonly Dictionary<string, GlobalUser> globalUserState = new();
		private static readonly Dictionary<string, Channel> channelState = new();
		//private static readonly Dictionary<string, Dictionary<string, User>> userState = new();

		/*public static void UpdateClient( Client client ) {
			throw new NotImplementedException();
		}*/

		/*public static User UpdateUser( int userIdentifier, Dictionary<string, string?> tags ) {
			userState.TryGetValue( userIdentifier, out User? currentUser );

			//tags.TryGetValue( "user-id", out string? userId ); // Included in GLOBALUSERSTATE
			tags.TryGetValue( "display-name", out string? displayName );
			tags.TryGetValue( "user-type", out string? userType );
			tags.TryGetValue( "color", out string? color );
			tags.TryGetValue( "badges", out string? badges );
			tags.TryGetValue( "badge-information", out string? badgeInformation );
			tags.TryGetValue( "emote-sets", out string? emoteSets );

			// These should really be for a "Member" of a channel...
			//tags.TryGetValue( "mod", out string? mod );
			//tags.TryGetValue( "subscriber", out string? subscriber );

			if ( currentUser != null ) {
				if ( displayName != null ) currentUser.Name = displayName;

				currentUser.Type = userType;
				currentUser.Color = color;
				currentUser.Badges = badges?.Split( ',' );
				currentUser.BadgeInformation = badgeInformation;
				currentUser.EmoteSets = emoteSets?.Split( ',' );

				return currentUser;

			} else {
				if ( displayName == null ) throw new Exception( "Cannot create new user in state with no name" );

				User newUser = new( userIdentifier, displayName );
				newUser.Type = userType;
				newUser.Color = color;
				newUser.Badges = badges?.Split( ',' );
				newUser.BadgeInformation = badgeInformation;
				newUser.EmoteSets = emoteSets?.Split( ',' );

				userState.Add( userIdentifier, newUser );

				return newUser;
			}
		}*/

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
				globalUserState.TryGetValue( displayName, out currentUser );
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

				globalUserState.Add( displayName, newUser );

				return newUser;
			}
		}

		public static User UpdateUser( Channel channel, Dictionary<string, string?> tags ) {
			GlobalUser globalUser = UpdateGlobalUser( tags );

			channel.Users.TryGetValue( globalUser.Name, out User? currentUser );

			tags.TryGetValue( "mod", out string? mod );
			tags.TryGetValue( "subscriber", out string? subscriber );

			if ( currentUser != null ) {
				if ( !string.IsNullOrEmpty( mod ) ) currentUser.IsModerator = ( mod == "1" );
				if ( !string.IsNullOrEmpty( subscriber ) ) currentUser.IsSubscriber = ( subscriber == "1" );

				return currentUser;

			} else {
				User newUser = new( globalUser, channel );

				newUser.IsModerator = ( mod == "1" );
				newUser.IsSubscriber = ( subscriber == "1" );

				channel.Users.Add( globalUser.Name, newUser );

				return newUser;
			}
		}

		/*public static User? UpdateUser( string userName, Dictionary<string, string?> tags ) {
			User? currentUser = null;
			
			foreach ( User user in userState.Values.ToArray() ) {
				if ( user.Name == userName ) {
					currentUser = user;
					break;
				}
			}

			tags.TryGetValue( "user-id", out string? userId ); // Included in GLOBALUSERSTATE
			tags.TryGetValue( "display-name", out string? displayName );
			tags.TryGetValue( "user-type", out string? userType );
			tags.TryGetValue( "color", out string? color );
			tags.TryGetValue( "badges", out string? badges );
			tags.TryGetValue( "badge-information", out string? badgeInformation );
			tags.TryGetValue( "emote-sets", out string? emoteSets );

			if ( currentUser != null ) {
				if ( displayName != null ) currentUser.Name = displayName;

				currentUser.Type = userType;
				currentUser.Color = color;
				currentUser.Badges = badges?.Split( ',' );
				currentUser.BadgeInformation = badgeInformation;
				currentUser.EmoteSets = emoteSets?.Split( ',' );

				return currentUser;

			} else {
				if ( userId == null ) throw new Exception( "Cannot create new user in state with no identifier" );
				if ( displayName == null ) throw new Exception( "Cannot create new user in state with no name" );

				int userIdentifier = int.Parse( userId );

				User newUser = new( userIdentifier, displayName );
				newUser.Type = userType;
				newUser.Color = color;
				newUser.Badges = badges?.Split( ',' );
				newUser.BadgeInformation = badgeInformation;
				newUser.EmoteSets = emoteSets?.Split( ',' );

				userState.Add( userIdentifier, newUser );

				return null;
			}
		}*/

		public static Channel UpdateChannel( string channelName, Dictionary<string, string?> tags ) {
			channelState.TryGetValue( channelName, out Channel? currentChannel );

			tags.TryGetValue( "room-id", out string? roomId );
			tags.TryGetValue( "emote-only", out string? emoteOnly );
			tags.TryGetValue( "followers-only", out string? followersOnly );
			tags.TryGetValue( "subs-only", out string? subsOnly );
			tags.TryGetValue( "r9k", out string? r9k );
			tags.TryGetValue( "rituals", out string? rituals );
			tags.TryGetValue( "slow", out string? slow );
			

			if ( currentChannel != null ) {
				if ( !string.IsNullOrEmpty( roomId ) ) currentChannel.Identifier = int.Parse( roomId );
				if ( !string.IsNullOrEmpty( emoteOnly ) ) currentChannel.IsEmoteOnly = ( emoteOnly == "1" );
				if ( !string.IsNullOrEmpty( followersOnly ) ) currentChannel.IsFollowersOnly = ( followersOnly == "1" );
				if ( !string.IsNullOrEmpty( subsOnly ) ) currentChannel.IsSubscribersOnly = ( subsOnly == "1" );
				if ( !string.IsNullOrEmpty( r9k ) ) currentChannel.IsR9K = ( r9k == "1" );
				if ( !string.IsNullOrEmpty( rituals ) ) currentChannel.IsRituals = ( rituals == "1" );
				if ( !string.IsNullOrEmpty( slow ) ) currentChannel.IsSlowMode = ( slow == "1" );

				return currentChannel;

			} else {
				Channel newChannel = new( channelName );

				if ( roomId != null ) newChannel.Identifier = int.Parse( roomId );
				newChannel.IsEmoteOnly = ( emoteOnly == "1" );
				newChannel.IsFollowersOnly = ( followersOnly == "1" );
				newChannel.IsSubscribersOnly = ( subsOnly == "1" );
				newChannel.IsR9K = ( r9k == "1" );
				newChannel.IsRituals = ( rituals == "1" );
				newChannel.IsSlowMode = ( slow == "1" );

				channelState.Add( channelName, newChannel );

				return newChannel;
			}
		}

		/*public static Client GetClient() {
			throw new NotImplementedException();
		}*/

		/*public static User? GetUser( int userIdentifier ) {
			userState.TryGetValue( userIdentifier, out User? user );
			return user;
		}

		public static User? GetUser( string userName ) {
			foreach ( User user in userState.Values.ToArray() ) {
				if ( user.Name == userName ) return user;
			}

			return null;
		}*/

		public static User GetOrCreateUser( Channel channel, string userName ) {
			channel.Users.TryGetValue( userName, out User? existingUser );
			if ( existingUser != null ) return existingUser;

			globalUserState.TryGetValue( userName, out GlobalUser? globalUser );
			if ( globalUser == null ) {
				globalUser = new( userName );
				globalUserState.Add( userName, globalUser );
			}

			User newUser = new( globalUser, channel );
			channel.Users.Add( userName, newUser );

			return newUser;
		}

		/*public static Channel? GetChannel( int channelIdentifier ) {
			channelState.TryGetValue( channelIdentifier, out Channel? channel );
			return channel;
		}*/

		public static Channel GetOrCreateChannel( string channelName ) {
			foreach ( Channel currentChannel in channelState.Values.ToArray() ) {
				if ( currentChannel.Name == channelName ) return currentChannel;
			}

			Channel newChannel = new( channelName );
			channelState.Add( channelName.ToLower(), newChannel );

			return newChannel;
		}

	}
}
