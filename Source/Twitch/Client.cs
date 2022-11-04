#pragma warning disable CS1998

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using TwitchBot.InternetRelayChat;

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

namespace TwitchBot.Twitch {
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
		public delegate Task OnChannelJoinHandler( Client client, GlobalUser user, Channel channel, bool isMe );
		public event OnChannelJoinHandler? OnChannelJoin;

		// Event that runs after a user leaves a channel
		public delegate Task OnChannelLeaveHandler( Client client, GlobalUser user, Channel channel );
		public event OnChannelLeaveHandler? OnChannelLeave;

		// Event that runs after a chat message is received
		public delegate Task OnChatMessageHandler( Client client, Message message );
		public event OnChatMessageHandler? OnChatMessage;

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
		public async Task<Channel?> JoinChannel( int channelIdentifier ) {

			// Fetch information about this channel from the Twitch API
			JsonObject channelInfoResponse = await API.Request( "channels", queryParameters: new() {
				{ "broadcaster_id", channelIdentifier.ToString() }
			} );

			// Fetch chat settings for this channel from the Twitch API
			JsonObject chatSettingsResponse = await API.Request( "chat/settings", queryParameters: new() {
				{ "broadcaster_id", channelIdentifier.ToString() },
				{ "moderator_id", User!.Identifier.ToString() },
			} );

			// Use the above API responses to create the channel in state for the first time
			Channel channel = State.InsertChannel( new( channelInfoResponse[ "data" ]![ 0 ]!.AsObject(), chatSettingsResponse[ "data" ]![ 0 ]!.AsObject(), this ) );

			// IRC channels are prefixed with a hashtag
			string channelName = $"#{channel.Name}";

			// Request to join this channel & wait for a response
			InternetRelayChat.Message responseMessage = await SendExpectResponseAsync( InternetRelayChat.Command.Join, middle: channelName );

			// Checks if this message is for us, is about a join, and is for the channel we wanted to join
			if ( responseMessage.IsAboutUser( User!.DisplayName ) && responseMessage.Command == InternetRelayChat.Command.Join && responseMessage.Middle == channelName ) return channel;

			// Default to returning no channel
			return null;

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
				
				//OnChannelUserUpdate?.Invoke( this, State.UpdateChannelUser( ircMessage, channel ) );

			// We don't know what to do with this message
			} else {
				Log.Warn( "Ignored IRC message: '{0}'", ircMessage.ToString() );
			}


















		// Is this a server message?
		/*if ( message.IsServer() ) {

			// Are we being told a channel user's new state?
			if ( message.Command == Command.UserState && message.Middle != null && message.Tags != null ) {

				// Update the channel & user in state
				//Channel channel = State.UpdateChannel( message, this );
				//User user = State.UpdateUser( channel, message.Tags );

				// Fire the user update event
				//OnUserUpdate?.Invoke( this, user );

			// Are we being told a channel's new state?
			} else if ( message.Command == Command.RoomState && message.Middle != null && message.Tags != null ) {

				// Update the channel in state
				Channel channel = State.UpdateChannel( message, this );

				// Fire the channel update event
				OnChannelUpdate?.Invoke( this, channel );

			// Something else?
			} else Log.Warn( "Unexpected server message: '{0}'", message.ToString() );

			// Is this a user message for ourselves?
		} /*else if ( message.IsForUser( Shared.MyAccountName! ) ) {

			// Is this us joining a channel?
			if ( message.Command == InternetRelayChat.Command.Join && message.Parameters != null ) {

				// Get the channel from state
				int channelIdentifier = Channel.ExtractIdentifier( message );
				Channel? channel = State.GetChannel( channelIdentifier );
				if ( channel == null ) 

				// Update the channel and user in state

				GlobalUser user = State.GetOrCreateUser( channel, Shared.MyAccountName! );
				OnChannelJoin?.Invoke( this, user, channel, true );

			} else if ( message.Command == InternetRelayChat.Command.Names && message.Parameters != null ) {
				List<string> userNames = new( message.Parameters[ ( message.Parameters.IndexOf( ':' ) + 1 ).. ].Split( ' ' ) );
				userNames.Remove( Shared.MyAccountName! );
				userNames.Remove( Shared.MyAccountName!.ToLower() );

				if ( userNames.Count == 0 ) {
					Log.Info( "No users are in the channel with us." );
				} else {
					Log.Info( "Users '{0}' are in the channel with us.", string.Join( ", ", userNames ) );
				}

			// Ignore
			} else if ( message.Command == InternetRelayChat.Command.NamesEnd && message.Parameters != null ) {

			} else {
				Log.Warn( "Unexpected Command Message: '{0}'", message.ToString() );
			}

		// User
		} else if ( message.User != null ) {
			if ( message.Command == InternetRelayChat.Command.PrivateMessage && message.Middle != null && message.Parameters != null && message.Tags != null ) {

				if ( !message.Tags.TryGetValue( "room-id", out string? channelIdentifier ) || channelIdentifier == null ) throw new Exception( "Message contains no valid room identifier" );

				Channel channel = State.GetOrCreateChannel( int.Parse( channelIdentifier ), message.Middle[ 1.. ], this );
				GlobalUser user = State.GetOrCreateUser( channel, message.User );
				Message theMessage = new( message.Parameters, message.Tags, user, channel );

				State.UpdateUser( channel, message.Tags );

				OnChatMessage?.Invoke( this, theMessage );

			} else if ( message.Command == InternetRelayChat.Command.Join && message.Middle != null ) {

		// TODO: Get channel by name
		/*
		Channel channel = State.GetOrCreateChannel( message.Middle[ 1.. ] );
		User user = State.GetOrCreateUser( channel, message.User );
		OnChannelJoin?.Invoke( this, user, channel, false );
		*/

		/*} else if ( message.Command == InternetRelayChat.Command.Leave && message.Middle != null ) {

				// TODO: Get channel by name
				/*
				Channel channel = State.GetOrCreateChannel( message.Middle[ 1.. ] );
				User user = State.GetOrCreateUser( channel, message.User );
				OnChannelLeave?.Invoke( this, user, channel );
				*/

		/*} else {
			Log.Warn( "Unexpected User Message: '{0}'", message.ToString() );
		}

	} else {
		Log.Warn( "what is a '{0}' ?", message.ToString() );
	}
	*/
	}

	}
}
