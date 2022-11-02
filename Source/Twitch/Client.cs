using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

		// Event that runs after we connect to the server
		public new delegate Task OnConnectHandler( Client client );
		public new event OnConnectHandler? OnConnect;

		// Event that runs after the client is ready
		public delegate Task OnReadyHandler( Client client, GlobalUser user );
		public event OnReadyHandler? OnReady;

		// Event that runs after a user joins a channel
		public delegate Task OnChannelJoinHandler( Client client, User user, Channel channel, bool isMe );
		public event OnChannelJoinHandler? OnChannelJoin;

		// Event that runs after a user leaves a channel
		public delegate Task OnChannelLeaveHandler( Client client, User user, Channel channel );
		public event OnChannelLeaveHandler? OnChannelLeave;

		// Event that runs after a chat message is received
		public delegate Task OnChatMessageHandler( Client client, Message message );
		public event OnChatMessageHandler? OnChatMessage;

		// Event that runs after a user is updated in a channel
		public delegate Task OnUserUpdateHandler( Client client, User user );
		public event OnUserUpdateHandler? OnUserUpdate;

		// Event that runs after a channel is updated
		public delegate Task OnChannelUpdateHandler( Client client, Channel channel );
		public event OnChannelUpdateHandler? OnChannelUpdate;

		// Constructor to register event handlers
		public Client() {
			OnMessage += ProcessMessage;
			base.OnConnect += OnBaseConnect;
		}

		// Request capabilities for this connection
		public async Task RequestCapabilities( string[] desiredCapabilities ) {

			// Send the capabilities request, and wait for response message(s)
			InternetRelayChat.Message[] capabilitiesResponseMessages = await SendExpectResponseAsync( InternetRelayChat.Command.RequestCapabilities, string.Join( ' ', desiredCapabilities ) );

			// Get a list of the granted capabilities from the first response message
			string[]? grantedCapabilities = capabilitiesResponseMessages[ 0 ].Parameters?.Split( ' ' );

			// Fail if granted capabilities is invalid
			if ( grantedCapabilities == null ) throw new Exception( "No granted capabilities in capabilities response message" );

			// Fail if the desired capabilities do not match the granted capabilities
			if ( !desiredCapabilities.SequenceEqual( grantedCapabilities ) ) throw new Exception( "Not all desired capabilities were granted" );

		}

		// Authenticate using OAuth credentials
		public async Task Authenticate( string accountName, string accessToken ) {

			// Send the access token as the password
			await SendAsync( InternetRelayChat.Command.Password, $"oauth:{accessToken}" );

			// Send the account name as the username, and wait for response message(s)
			InternetRelayChat.Message[]? authenticationResponseMessages = await SendExpectResponseAsync( InternetRelayChat.Command.Username, accountName );

			// Loop through each message as the Twitch authentication reply contains multiple messages
			foreach ( InternetRelayChat.Message message in authenticationResponseMessages ) {

				// Fail if authentication failed
				if ( message.Command == Command.Notice && message.Parameters == "Login authentication failed" ) throw new Exception( "Failed to authenticate" );

				// Does the message have parameters?
				if ( !string.IsNullOrWhiteSpace( message.Parameters ) ) {

					// Remove the account name from the start of the parameters value
					string parameters = message.Parameters.StartsWith( $"{accountName.ToLower()} :" ) ? message.Parameters[ ( accountName.Length + 2 ).. ] : message.Parameters;

					// Is the welcome command?
					if ( message.Command == InternetRelayChat.Command.Welcome ) Log.Info( "The server welcomes us." );

					// Are we being told our host?
					else if ( message.Command == InternetRelayChat.Command.YourHost ) {

						// Run regular expression match on parameters to extract the hostname
						Match hostMatch = hostPattern.Match( parameters );

						// Set the expected hostname to the match group, if successful
						if ( hostMatch.Success ) {
							ExpectedHost = hostMatch.Groups[ "host" ].Value;
							Log.Info( "The expected host is now: '{0}'", ExpectedHost );
						}

					}

					// Is this the Message of The Day?
					else if ( message.Command == InternetRelayChat.Command.MoTD ) Log.Info( "MoTD: '{0}'", parameters );

				// The message does not have parameters
				} else {

					// Are we being informed about ourselves?
					if ( message.Command == Command.GlobalUserState && message.Tags.Count > 0 ) {

						// Update ourselves in state
						GlobalUser user = State.UpdateGlobalUser( message.Tags );

						// Run the ready event
						OnReady?.Invoke( this, user );

					}

				}
			}
		}

		// Joins a channel's chat
		public async Task JoinChannel( string channelName ) { // TODO: Use channel identifier instead?
			await SendAsync( InternetRelayChat.Command.Join, channelName.ToLower() );

			// TODO: Check ':peeksabot!peeksabot@peeksabot.tmi.twitch.tv JOIN #rawreltv' response here & run the channel join event...?
		}

		// Run the connect event when the IRC client connects to the server
		private async Task OnBaseConnect( InternetRelayChat.Client _ ) => OnConnect?.Invoke( this );

		// Processes received messages
		private async Task ProcessMessage( InternetRelayChat.Client client, InternetRelayChat.Message message ) {

			// Is this a server message?
			if ( message.IsServer() ) {

				// Are we being told a user's new state?
				if ( message.Command == Command.UserState && message.Parameters != null && message.Tags != null ) {
					
					// Update the channel and user in state
					Channel channel = State.GetOrCreateChannel( message.Parameters[ 1.. ] );
					User user = State.UpdateUser( channel, message.Tags );

					// Run the user update event
					OnUserUpdate?.Invoke( this, user );

				// Are we being told a channel's new state?
				} else if ( message.Command == Command.RoomState && message.Parameters != null && message.Tags != null ) {
					
					// Update the channel in state
					Channel channel = State.UpdateChannel( message.Parameters[ 1.. ], message.Tags );
					
					// Run the channel update event
					OnChannelUpdate?.Invoke( this, channel );

				// Something else?
				} else Console.WriteLine( "Unexpected Server Message: '{0}'", message.ToString() );

			// Is this a user message for ourselves?
			} else if ( message.IsForUser( Shared.MyAccountName! ) ) {

				// Is this a command?
				if ( message.Command == InternetRelayChat.Command.Join && message.Parameters != null ) {
					
					// Update the channel and user in state
					Channel channel = State.GetOrCreateChannel( message.Parameters[ 1.. ] );
					User user = State.GetOrCreateUser( channel, Shared.MyAccountName! );
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

				} else if ( message.Command == InternetRelayChat.Command.NamesEnd && message.Parameters != null ) {
					// Ignore

				} else {
					Console.WriteLine( "Unexpected Command Message: '{0}'", message.ToString() );
				}

			// User
			} else if ( message.User != null ) {
				if ( message.Command == InternetRelayChat.Command.PrivateMessage && message.Parameters != null && message.Tags != null ) {
					string[] parameters = message.Parameters.Split( ':', 2 );

					// @badge-info=;badges=moderator/1;client-nonce=08aa66b3ddb3914f22718e243edc0c37;color=#FF0000;display-name=viral32111_;emotes=;first-msg=0;flags=;id=9eb706b3-b7ea-4229-8d6f-b42f80f5108b;mod=1;returning-chatter=0;room-id=127154290;subscriber=0;tmi-sent-ts=1655820535330;turbo=0;user-id=675961583;user-type=mod :viral32111_!viral32111_@viral32111_.tmi.twitch.tv PRIVMSG :#rawreltv :test

					Channel channel = State.GetOrCreateChannel( parameters[ 0 ].Trim()[ 1.. ] );
					User user = State.GetOrCreateUser( channel, message.User );
					Message theMessage = new( channel, user, parameters[ 1 ].Trim() );

					State.UpdateUser( channel, message.Tags );

					OnChatMessage?.Invoke( this, theMessage );

				} else if ( message.Command == InternetRelayChat.Command.Join && message.Parameters != null ) {
					Channel channel = State.GetOrCreateChannel( message.Parameters[ 1.. ] );
					User user = State.GetOrCreateUser( channel, message.User );
					OnChannelJoin?.Invoke( this, user, channel, false );

				} else if ( message.Command == InternetRelayChat.Command.Leave && message.Parameters != null ) {
					Channel channel = State.GetOrCreateChannel( message.Parameters[ 1.. ] );
					User user = State.GetOrCreateUser( channel, message.User );
					OnChannelLeave?.Invoke( this, user, channel );

				} else {
					Console.WriteLine( "Unexpected User Message: '{0}'", message.ToString() );
				}

			} else {
				Console.WriteLine( "what is a '{0}' ?", message.ToString() );
			}
		}

	}
}
