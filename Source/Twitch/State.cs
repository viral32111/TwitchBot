using System;
using System.Linq;
using System.Collections.Generic;

/* Unknown Tags:
client-nonce=640a320bc852e4bc9034e93feac64b38
flags=
*/

namespace TwitchBot.Twitch;

public static class State {

	private static readonly Dictionary<Guid, Message> Messages = new();
	private static readonly Dictionary<int, Channel> Channels = new();
	private static readonly Dictionary<int, GlobalUser> GlobalUsers = new();
	private static readonly Dictionary<int, ChannelUser> ChannelUsers = new();

	/*********************************************************************************************/

	// This only has insert & no update because message's have nothing to update, they are purely static
	public static Message InsertMessage( Message message ) {
		Messages.Add( message.Identifier, message );
		Log.Debug( "Inserted message '{0}' in state.", message.Identifier );
		return message;
	}

	public static Message? GetMessage( Guid identifier ) => Messages[ identifier ];

	/*********************************************************************************************/

	// This is needed for channels created by the channel information & chat settings API responses
	public static Channel InsertChannel( Channel channel ) {
		Channels.Add( channel.Identifier, channel );
		Log.Debug( "Inserted channel '{0}' in state.", channel.Identifier );
		return channel;
	}

	public static Channel UpdateChannel( viral32111.InternetRelayChat.Message ircMessage, Client client ) {
		int identifier = Channel.ExtractIdentifier( ircMessage );

		if ( Channels.TryGetValue( identifier, out Channel? channel ) && channel != null ) {
			channel.UpdateProperties( ircMessage );
			Log.Debug( "Updated channel '{0}' in state.", identifier );
		} else {
			channel = new( ircMessage, client );
			Channels.Add( channel.Identifier, channel );
			Log.Debug( "Created channel '{0}' in state.", channel.Identifier );
		}

		return channel;
	}

	public static Channel? GetChannel( int identifier ) => Channels[ identifier ];
	public static Channel? FindChannelByName( string name ) => Channels.Values.Where( channel => channel.Name == name.ToLower() ).FirstOrDefault();

	public static bool TryGetChannel( int identifier, out Channel? channel ) => Channels.TryGetValue( identifier, out channel );

	/*********************************************************************************************/

	// This is needed for global users created by the users API response
	public static GlobalUser InsertGlobalUser( GlobalUser globalUser ) {
		GlobalUsers.Add( globalUser.Identifier, globalUser );
		Log.Debug( "Inserted global user '{0}' in state.", globalUser.Identifier );
		return globalUser;
	}

	public static GlobalUser UpdateGlobalUser( viral32111.InternetRelayChat.Message ircMessage ) {
		int identifier = GlobalUser.ExtractIdentifier( ircMessage );

		if ( GlobalUsers.TryGetValue( identifier, out GlobalUser? globalUser ) && globalUser != null ) {
			globalUser.UpdateProperties( ircMessage );
			Log.Debug( "Updated global user '{0}' in state.", identifier );
		} else {
			globalUser = new( ircMessage );
			GlobalUsers.Add( globalUser.Identifier, globalUser );
			Log.Debug( "Created global user '{0}' in state.", globalUser.Identifier );
		}

		return globalUser;
	}

	public static GlobalUser? GetGlobalUser( int identifier ) => GlobalUsers[ identifier ];
	public static GlobalUser? FindGlobalUserByName( string loginName ) => GlobalUsers.Values.Where( globalUser => globalUser.LoginName == loginName ).FirstOrDefault();

	/*********************************************************************************************/

	public static ChannelUser UpdateChannelUser( viral32111.InternetRelayChat.Message ircMessage, Channel channel ) {

		// USERSTATE updates seem to never contain a user-id IRC message tag, so we can't use GlobalUser.ExtractIdentifier()
		if ( !ircMessage.Tags.TryGetValue( "display-name", out string? displayName ) || string.IsNullOrWhiteSpace( displayName ) ) throw new Exception( "Cannot possibly update a channel user without their display name IRC message tag" );
		GlobalUser? globalUser = FindGlobalUserByName( displayName.ToLower() );
		globalUser ??= UpdateGlobalUser( ircMessage ); // for PRIVMSG
		Log.Debug( "Found global user '{0}' in state.", globalUser.Identifier );

		// TODO: This doesn't even get the user for a specific channel, so our channel users are glorified global users right now...
		if ( ChannelUsers.TryGetValue( globalUser.Identifier, out ChannelUser? channelUser ) && channelUser != null ) {
			channelUser.UpdateProperties( ircMessage );
			Log.Debug( "Updated channel user '{0}' in state.", globalUser.Identifier );
		} else {
			channelUser = new( ircMessage, globalUser, channel );
			ChannelUsers.Add( channelUser.Global.Identifier, channelUser );
			Log.Debug( "Created channel user '{0}' in state.", channelUser.Global.Identifier );
		}

		return channelUser;
	}

	public static ChannelUser? GetChannelUser( int identifier ) => ChannelUsers[ identifier ];

}
