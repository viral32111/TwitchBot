using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;

// A client for connecting to Twitch chat
// https://dev.twitch.tv/docs/irc

// on stream start/stop (change ig)
// on view count change (on viewer/user JOIN and PART - membership capability)
// on mod action (on user ban, on user timeout)
// on chat message sent
// on chat message delete (more of a mod action ig)
// on user follow
// on user subscribe
// on user channel point used
// on user send bits
// on stream metadata change (on title change, on game change, in tags change, etc.)
// on user modded/unmodded

namespace TwitchBot.Twitch;

public class Client : InternetRelayChat.Client {

	// Regular expression for matching the "Your Host" command
	private readonly Regex hostPattern = new( @"^Your host is (?'host'.+)$" );

	// Ourselves
	public GlobalUser? User;

	// Event that runs after we have opened a connection to the server
	public new delegate Task OnOpenHandler( Client client );
	public new event OnOpenHandler? OnOpen;

	// Event that runs after the client is ready
	public delegate Task OnReadyHandler( Client client, GlobalUser user );
	public event OnReadyHandler? OnReady;

	// Event that runs after a user joins a channel
	public delegate Task OnGlobalUserJoinChannelHandler( Client client, GlobalUser user, Channel channel, bool isMe );
	public event OnGlobalUserJoinChannelHandler? OnGlobalUserJoinChannel;

	// Event that runs after a user leaves a channel
	public delegate Task OnGlobalUserLeaveChannelHandler( Client client, GlobalUser user, Channel channel );
	public event OnGlobalUserLeaveChannelHandler? OnGlobalUserLeaveChannel;

	// Event that runs after a chat message is received
	public delegate Task OnChannelChatMessageHandler( Client client, Message message );
	public event OnChannelChatMessageHandler? OnChannelChatMessage;

	// Event that runs after a user is updated in a channel
	public delegate Task OnChannelUserUpdateHandler( Client client, ChannelUser user );
	public event OnChannelUserUpdateHandler? OnChannelUserUpdate;

	// Event that runs after a channel is updated
	public delegate Task OnChannelUpdateHandler( Client client, Channel channel );
	public event OnChannelUpdateHandler? OnChannelUpdate;

	// Constructor to register event handlers
	public Client() {
		OnMessage += ProcessMessage;
		base.OnOpen += OnBaseOpen;
	}

	// Request capabilities for this connection
	public async Task RequestCapabilities( string[] desiredCapabilities ) {

		// Send the capabilities request, and wait for response message(s)
		InternetRelayChat.Message responseMessage = await SendExpectResponseAsync( InternetRelayChat.Command.RequestCapabilities, string.Join( ' ', desiredCapabilities ) );

		// Get a list of the granted capabilities from the response message
		string[]? grantedCapabilities = responseMessage.Parameters?.Split( ' ' );
		if ( grantedCapabilities == null ) throw new Exception( "No granted capabilities in capabilities response message" );

		// Fail if the desired capabilities do not match the granted capabilities
		if ( !desiredCapabilities.SequenceEqual( grantedCapabilities ) ) throw new Exception( "Not all desired capabilities were granted" );

	}

	// Authenticates using our OAuth credentials
	public async Task<bool> Authenticate( string accountName, string accessToken ) {

		// Send the access token as the password, the account name as the username, and wait for a response
		await SendAsync( InternetRelayChat.Command.Password, $"oauth:{accessToken}" );
		InternetRelayChat.Message? responseMessage = await SendExpectResponseAsync( InternetRelayChat.Command.Nickname, accountName );

		// Check the response to see if authentication was successful or failed
		if ( responseMessage.IsFromSystem() && responseMessage.Command == InternetRelayChat.Command.Welcome ) return true;
		else if ( responseMessage.IsFromSystem() && responseMessage.Command == Command.Notice && responseMessage.Parameters == "Login authentication failed" ) return false;
		else throw new Exception( "Authentication response IRC message was neither successful or a failure" );

	}

	// Joins a channel's chat
	public async Task<bool> JoinChannel( Channel channel ) {

		// IRC channels are prefixed with a hashtag
		string channelName = $"#{channel.Name}";

		// Request to join this channel & wait for a response
		InternetRelayChat.Message responseMessage = await SendExpectResponseAsync( InternetRelayChat.Command.Join, middle: channelName );

		// Fire the channel join event if this message is for us, is about a join, and is for the channel we wanted to join
		if ( responseMessage.IsAboutUser( User!.LoginName ) && responseMessage.Command == InternetRelayChat.Command.Join && responseMessage.Middle == channelName ) {
			// NOTE: Don't need to update global user or channel state here because this IRC message doesn't contain anything useful
			OnGlobalUserJoinChannel?.Invoke( this, User!, channel, true );
			return true;
		} else {
			return false;
		}

	}

	// Run the connect event when the IRC client connects to the server
	private async Task OnBaseOpen( InternetRelayChat.Client _ ) => OnOpen?.Invoke( this );

	// Processes received messages
	private async Task ProcessMessage( InternetRelayChat.Client _, InternetRelayChat.Message ircMessage ) {

		// Are we being told our host?
		if ( ircMessage.IsFromSystem() && ircMessage.Command == InternetRelayChat.Command.YourHost && ircMessage.Parameters != null ) {

			// Run regular expression match on parameters to extract the hostname
			Match hostMatch = hostPattern.Match( ircMessage.Parameters );
			if ( !hostMatch.Success ) throw new Exception( "Failed to extract hostname from expected host IRC message" );

			// Set the expected hostname to the match group
			ExpectedHost = hostMatch.Groups[ "host" ].Value;
			Log.Info( "The expected host is now: '{0}'", ExpectedHost );

		// One day this might change, so let's display it...
		} else if ( ircMessage.IsFromSystem() && ircMessage.Command == InternetRelayChat.Command.MoTD && ircMessage.Parameters != null ) {
			Log.Info( "MoTD: '{0}'", ircMessage.Parameters );

		// Update ourselves in state & fire the ready event, if we are being informed about our global self
		} else if ( ircMessage.IsFromSystem() && ircMessage.Command == Command.GlobalUserState && ircMessage.Tags.Count > 0 ) {
			OnReady?.Invoke( this, State.UpdateGlobalUser( ircMessage ) );

		// Update a channel in state & fire the channel update event, if we are being informed of a channel update
		} else if ( ircMessage.IsFromSystem() && ircMessage.Command == Command.RoomState && ircMessage.Middle != null && ircMessage.Tags.Count > 0 ) {
			OnChannelUpdate?.Invoke( this, State.UpdateChannel( ircMessage, this ) );

		// Update a channel user in state & fire the channel user update event, if we are being informed of a channel user update
		} else if ( ircMessage.IsFromSystem() && ircMessage.Command == Command.UserState && ircMessage.Middle != null && ircMessage.Tags.Count > 0 ) {
			Channel? channel = State.FindChannelByName( ircMessage.Middle[ 1.. ] );
			if ( channel == null ) throw new Exception( "Cannot update channel user because the channel is unknown" );

			OnChannelUserUpdate?.Invoke( this, State.UpdateChannelUser( ircMessage, channel ) );

		// Are we being informed about the users in a channel we just joined?
		} else if ( ircMessage.IsFromSystem() && ircMessage.Command == InternetRelayChat.Command.Names && ircMessage.Middle != null && ircMessage.Parameters != null ) {

			// Find the channel in state
			Channel? channel = State.FindChannelByName( ircMessage.Middle[ ( ircMessage.Middle.LastIndexOf( '#' ) + 1 ).. ] );
			if ( channel == null ) throw new Exception( "Received user list for an unknown channel" );

			// Get a list of user names, excluding ourselves
			string[] userNames = ircMessage.Parameters.Split( ' ' ).Where( userName => userName != User!.LoginName ).ToArray();

			// Find each user in state, or create them using the API if they don't exist
			List<GlobalUser> globalUsers = new();
			foreach ( string userName in userNames ) {
				GlobalUser? globalUser = State.FindGlobalUserByName( userName );
				globalUser ??= await GlobalUser.FetchFromAPI( userName ); // TODO: The API supports specifying multiple names to reduce request count, we should do that!
				globalUsers.Add( globalUser );
			}

			// Display the users
			if ( globalUsers.Count > 0 ) {
				Log.Info( "There are {0} global users in channel {1}: {2}", globalUsers.Count, channel.ToString(), string.Join( ", ", globalUsers.Select( globalUser => globalUser.ToString() ).ToArray() ) );
			} else {
				Log.Info( "There are no global users in channel {0}.", channel.ToString() );
			}

		// Did someone send a chat message in a channel?
		} else if ( !ircMessage.IsFromSystem() && ircMessage.Command == InternetRelayChat.Command.PrivateMessage && ircMessage.Middle != null && ircMessage.Parameters != null && ircMessage.Tags.Count > 0 ) {

			// Update state for channel, channel user & message
			Channel channel = State.UpdateChannel( ircMessage, this );
			ChannelUser channelUser = State.UpdateChannelUser( ircMessage, channel );
			Message message = State.InsertMessage( new( ircMessage, channelUser ) );

			// Fire the chat message event
			OnChannelChatMessage?.Invoke( this, message );

		// Has a user (that isn't us) joined a channel's chat?
		} else if ( !ircMessage.IsFromSystem() && ircMessage.User != null && ircMessage.Command == InternetRelayChat.Command.Join && ircMessage.Middle != null ) {

			// Find the channel in state
			Channel? channel = State.FindChannelByName( ircMessage.Middle[ 1.. ] );
			if ( channel == null ) throw new Exception( "Received user join for an unknown channel" );

			// Find the global user in state, or create using the API if they don't exist
			GlobalUser? globalUser = State.FindGlobalUserByName( ircMessage.User );
			globalUser ??= await GlobalUser.FetchFromAPI( ircMessage.User );

			// Fire the user join channel event
			OnGlobalUserJoinChannel?.Invoke( this, globalUser, channel, false );

		// Has a user left a channel's chat?
		} else if ( !ircMessage.IsFromSystem() && ircMessage.User != null && ircMessage.Command == InternetRelayChat.Command.Leave && ircMessage.Middle != null ) {

			// Find the channel in state
			Channel? channel = State.FindChannelByName( ircMessage.Middle[ 1.. ] );
			if ( channel == null ) throw new Exception( "Received user leave for an unknown channel" );

			// Find the global user in state, or create using the API if they don't exist
			GlobalUser? globalUser = State.FindGlobalUserByName( ircMessage.User );
			globalUser ??= await GlobalUser.FetchFromAPI( ircMessage.User );

			// Fire the user leave channel event
			OnGlobalUserLeaveChannel?.Invoke( this, globalUser, channel );

		// We don't know what to do with this message
		} else {
			Log.Warn( "Ignored IRC message: '{0}'", ircMessage.ToString() );
		}

	}

}
