using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
	public class Client {

		// A websocket client to use for the underlying connection
		// NOTE: Cannot inherit from this because it is sealed
		private readonly ClientWebSocket wsClient = new();

		// A completion source for responses to sent websocket messages
		// NOTE: Should be null whenever a response is not expected
		private TaskCompletionSource<InternetRelayChat.Message[]>? responseSource = null;

		// Regular expression for matching the "Your Host" (004) post-authentication message
		private readonly Regex HostPattern = new( @"^Your host is (.+)$" );

		// The IRC-style Twitch host name that messages originate from
		// NOTE: This changes later on once authentication completes and we are told what our host is
		private string ExpectedHost = "tmi.twitch.tv";

		// The connection state
		public bool Connected { get; private set; } = false;

		// An event that is ran whenever an error occurs
		// TODO: For some reason throwing an exception like normal just does nothing...? I think it's something to do with the receive message background task?
		public delegate Task OnErrorHandler( object sender, OnErrorEventArgs e );
		public event OnErrorHandler? OnError;

		// An event that is ran whenever a connection is established
		public delegate Task OnConnectHandler( object sender, EventArgs e );
		public event OnConnectHandler? OnConnect;

		// An event that is ran whenever the client is ready
		public delegate Task OnReadyHandler( object sender, OnReadyEventArgs e );
		public event OnReadyHandler? OnReady;

		// An event that is ran whenever a user joins a channel
		public delegate Task OnChannelJoinHandler( object sender, OnChannelJoinLeaveEventArgs e );
		public event OnChannelJoinHandler? OnChannelJoin;

		// An event that is ran whenever a user leaves a channel
		public delegate Task OnChannelLeaveHandler( object sender, OnChannelJoinLeaveEventArgs e );
		public event OnChannelLeaveHandler? OnChannelLeave;

		// An event that is ran whenever a chat message is received
		public delegate Task OnChatMessageHandler( object sender, OnChatMessageEventArgs e );
		public event OnChatMessageHandler? OnChatMessage;

		// An event that is ran whenever a user is updated in a channel
		public delegate Task OnUserUpdateHandler( object sender, OnUserUpdateEventArgs e );
		public event OnUserUpdateHandler? OnUserUpdate;

		// An event that is ran whenever a channel is updated
		public delegate Task OnChannelUpdateHandler( object sender, OnChannelUpdateEventArgs e );
		public event OnChannelUpdateHandler? OnChannelUpdate;

		// Synchronous function to connect to the websocket server, or timeout after a specified period
		// NOTE: This blocks until the connection is over, which is intended behavior to keep the application running
		public void Connect( string serverAddress, int serverPort = 80, bool connectInsecurely = false, int timeoutSeconds = 10 ) {
			if ( wsClient.State != WebSocketState.None ) {
				OnError?.Invoke( this, new OnErrorEventArgs( "Attempt to connect while already connected" ) );
				return;
			}

			Uri serverUri = new( $"{( connectInsecurely ? "ws" : "wss" )}://{serverAddress}:{serverPort}" );
			Task connectTask = wsClient.ConnectAsync( serverUri, CancellationToken.None );

			Task<Task> raceTask = Task.WhenAny( connectTask, Task.Delay( timeoutSeconds * 1000 ) );
			if ( raceTask.Result != connectTask ) {
				OnError?.Invoke( this, new OnErrorEventArgs( "Timed out while connecting to websocket server" ) );
				return;
			}

			// We are now connected
			Connected = true;

			// Start receiving messages in the background
			Task receiveTask = ReceiveMessages();

			// Run the connection established event handlers, if there are any
			OnConnect?.Invoke( this, new EventArgs() );

			// Wait for the message receive task to finish
			receiveTask.Wait();
		}

		public async Task Disconnect( string reason = "Goodbye." ) {
			if ( wsClient.State != WebSocketState.Open ) {
				OnError?.Invoke( this, new OnErrorEventArgs( "Attempt to disconnect while not connected" ) );
				return;
			}

			await wsClient.CloseAsync( WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None );

			// We are no longer connected
			Connected = false;
		}

		// Asynchronous function to send a capabilities request to the websocket server
		public async Task RequestCapabilities( string[] capabilitiesRequested ) {
			InternetRelayChat.Message[]? capabilitiesResponses = await SendMessage( $"CAP REQ :{string.Join( ' ', capabilitiesRequested )}" );
			if ( capabilitiesResponses == null ) {
				OnError?.Invoke( this, new OnErrorEventArgs( "Never received response for capabilities request" ) );
				return;
			}

			string[]? capabilitiesResponse = capabilitiesResponses[ 0 ].Parameters?.Split( ' ' );
			if ( capabilitiesResponse == null ) {
				OnError?.Invoke( this, new OnErrorEventArgs( "Received invalid IRC-styled message for capabilities request" ) );
				return;
			}

			if ( !capabilitiesResponse.SequenceEqual( capabilitiesRequested ) ) {
				OnError?.Invoke( this, new OnErrorEventArgs( "Not all capabilities were granted" ) );
				return;
			}
		}

		// Asynchronous function to send account credentials to the websocket server
		public async Task Authenticate( string accountName, string accessToken ) {
			await SendMessage( $"PASS oauth:{accessToken}", expectResponse: false );
			InternetRelayChat.Message[]? authResponses = await SendMessage( $"NICK {accountName}" );
			if ( authResponses == null ) {
				OnError?.Invoke( this, new OnErrorEventArgs( "Never received response for authentication" ) );
				return;
			}

			// The Twitch authentication reply contains multiple messages
			foreach ( InternetRelayChat.Message message in authResponses ) {
				if ( message.Command == Command.Notice && message.Parameters == "Login authentication failed" ) {
					OnError?.Invoke( this, new OnErrorEventArgs( "Failed to authenticate" ) );
					return;
				}

				if ( !string.IsNullOrEmpty( message.Parameters ) ) {

					// Remove the account name from the start of the parameters value
					string parameters = ( message.Parameters.StartsWith( $"{accountName.ToLower()} :" ) ? message.Parameters[ ( accountName.Length + 2 ).. ] : message.Parameters );

					if ( message.Command == InternetRelayChat.Command.Welcome ) Log.Write( "The server welcomes us." );

					if ( message.Command == InternetRelayChat.Command.YourHost ) {
						Match hostMatch = HostPattern.Match( parameters );
						if ( hostMatch.Success ) {
							ExpectedHost = hostMatch.Groups[ 1 ].Value;
							Log.Write( "The expected host is now: '{0}'", ExpectedHost );
						}
					}

					if ( message.Command == InternetRelayChat.Command.MoTD ) Log.Write( "MoTD: '{0}'", parameters );

				} else {
					if ( message.Command == Command.GlobalUserState ) {
						if ( message.Tags == null ) {
							OnError?.Invoke( this, new OnErrorEventArgs( "Tags missing for user state command" ) );
							return;
						}

						GlobalUser user = State.UpdateGlobalUser( message.Tags );
						OnReady?.Invoke( this, new OnReadyEventArgs( user ) );
					}
				}
			}
		}

		public async Task JoinChannel( string channelName ) {
			await SendMessage( $"JOIN #{channelName.ToLower()}", expectResponse: false );

			// TODO: Check ':peeksabot!peeksabot@peeksabot.tmi.twitch.tv JOIN #rawreltv' response here & fire channel join event...?
		}


		// Asynchronous function to send messages to the websocket server
		// NOTE: This is NOT to send messages to Twitch chat, see Channel.Send() for that
		public async Task<InternetRelayChat.Message[]?> SendMessage( string messageToSend, bool expectResponse = true ) {
			if ( expectResponse == true ) {
				if ( responseSource != null ) {
					OnError?.Invoke( this, new OnErrorEventArgs( "Response source was never cleaned up" ) );
					return null;
				}

				responseSource = new();
			}

			await Task.Delay( 10 ); // TODO: Why is responseSource still null in the ReceiveMessages() task despite that this happens before SendAsync()?

			await wsClient.SendAsync( Encoding.UTF8.GetBytes( messageToSend ), WebSocketMessageType.Text, true, CancellationToken.None );

			if ( responseSource != null ) {
				InternetRelayChat.Message[] responseMessages = await responseSource.Task;
				responseSource = null;

				return responseMessages;
			}

			return null;
		}

		// Asynchronous function to constantly receive websocket messages from the server
		// NOTE: This is intended to be ran in the background (i.e, not awaited)
		private async Task ReceiveMessages( int bufferSize = 4096 ) {
			byte[] receiveBuffer = new byte[ bufferSize ];

			// Run forever while the underlying websocket connection is open
			// NOTE: This will not run for the final close received message
			while ( wsClient.State == WebSocketState.Open ) {
				WebSocketReceiveResult receiveResult = await wsClient.ReceiveAsync( receiveBuffer, CancellationToken.None );

				if ( receiveResult.MessageType == WebSocketMessageType.Text ) {
					string receivedMessage = Encoding.UTF8.GetString( receiveBuffer );

					List<InternetRelayChat.Message> ourMessages = new();
					foreach ( InternetRelayChat.Message message in InternetRelayChat.Message.Parse( receivedMessage ) ) {

						ConsoleColor currentColor = Console.ForegroundColor;
						Console.ForegroundColor = ConsoleColor.DarkRed;
						Console.WriteLine( message.ToString() );
						Console.ForegroundColor = currentColor;

						// https://dev.twitch.tv/docs/irc#keepalive-messages
						if ( message.Command == InternetRelayChat.Command.Ping ) { // message.Host == null && 
							await SendMessage( $"PONG :{message.Parameters}" );
							continue;
						}

						if ( message.Host == null || !message.Host.EndsWith( ExpectedHost ) ) {
							OnError?.Invoke( this, new OnErrorEventArgs( "Received message from foreign server" ) );
							return;
						}

						if ( responseSource == null ) {
							HandleUnexpectedMessage( message );
						} else {
							ourMessages.Add( message );
						}

					}

					if ( responseSource != null ) {
						responseSource.SetResult( ourMessages.ToArray() );
						//responseSource = null; // TODO: Is this needed? - It is done in the SendMessage function
					}

				} else if ( receiveResult.MessageType == WebSocketMessageType.Close ) {
					Console.WriteLine( "Connection closed." );

				} else {
					OnError?.Invoke( this, new OnErrorEventArgs( $"Received unknown message type {receiveResult.MessageType} of {receiveResult.Count} bytes" ) );
				}

				Array.Clear( receiveBuffer );
			}
		}

		private void HandleUnexpectedMessage( InternetRelayChat.Message message ) {

			// Server
			if ( message.IsServer( ExpectedHost ) ) {
				if ( message.Command == Command.UserState && message.Parameters != null && message.Tags != null ) {
					Channel channel = State.GetOrCreateChannel( message.Parameters[ 1.. ] );
					User user = State.UpdateUser( channel, message.Tags );
					OnUserUpdate?.Invoke( this, new OnUserUpdateEventArgs( user ) );

				} else if ( message.Command == Command.RoomState && message.Parameters != null && message.Tags != null ) {
					Channel channel = State.UpdateChannel( message.Parameters[ 1.. ], message.Tags );
					OnChannelUpdate?.Invoke( this, new OnChannelUpdateEventArgs( channel ) );

				} else {
					Console.WriteLine( "Unexpected Server Message: '{0}'", message.ToString() );
				}

			} else {

				// Command
				if ( message.IsFor( Shared.UserSecrets.AccountName, ExpectedHost ) == true ) {
					if ( message.Command == InternetRelayChat.Command.Join && message.Parameters != null ) {
						Channel channel = State.GetOrCreateChannel( message.Parameters[ 1.. ] );
						User user = State.GetOrCreateUser( channel, Shared.UserSecrets.AccountName );
						OnChannelJoin?.Invoke( this, new OnChannelJoinLeaveEventArgs( user, true ) );

					} else if ( message.Command == InternetRelayChat.Command.Names && message.Parameters != null ) {
						List<string> userNames = new( message.Parameters[ ( message.Parameters.IndexOf( ':' ) + 1 ).. ].Split( ' ' ) );
						userNames.Remove( Shared.UserSecrets.AccountName );
						userNames.Remove( Shared.UserSecrets.AccountName.ToLower() );

						if ( userNames.Count == 0 ) {
							Log.Write( "No users are in the channel with us." );
						} else {
							Log.Write( "Users '{0}' are in the channel with us.", string.Join( ", ", userNames ) );
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

						OnChatMessage?.Invoke( this, new OnChatMessageEventArgs( theMessage ) );

					} else if ( message.Command == InternetRelayChat.Command.Join && message.Parameters != null ) {
						Channel channel = State.GetOrCreateChannel( message.Parameters[ 1.. ] );
						User user = State.GetOrCreateUser( channel, message.User );
						OnChannelJoin?.Invoke( this, new OnChannelJoinLeaveEventArgs( user ) );

					} else if ( message.Command == InternetRelayChat.Command.Leave && message.Parameters != null ) {
						Channel channel = State.GetOrCreateChannel( message.Parameters[ 1.. ] );
						User user = State.GetOrCreateUser( channel, message.User );
						OnChannelLeave?.Invoke( this, new OnChannelJoinLeaveEventArgs( user ) );

					} else {
						Console.WriteLine( "Unexpected User Message: '{0}'", message.ToString() );
					}
				}
			}

		}

	}

	public class OnChannelJoinEventArgs : EventArgs {
		public Channel Channel { get; init; }

		public OnChannelJoinEventArgs( Channel channel ) => Channel = channel;
	}

	public class OnErrorEventArgs : EventArgs {
		public string Message { get; init; }

		public OnErrorEventArgs( string message ) => Message = message;
	}

	public class OnChatMessageEventArgs : EventArgs {
		public Message Message { get; init; }

		public OnChatMessageEventArgs( Message message ) => Message = message;
	}

	public class OnReadyEventArgs : EventArgs {
		public GlobalUser User { get; init; }
		public OnReadyEventArgs( GlobalUser user ) => User = user;
	}

	public class OnUserUpdateEventArgs : EventArgs {
		public User User { get; init; }

		public OnUserUpdateEventArgs( User user ) => User = user;
	}

	public class OnChannelUpdateEventArgs : EventArgs {
		public Channel Channel { get; init; }

		public OnChannelUpdateEventArgs( Channel channel ) => Channel = channel;
	}

	public class OnChannelJoinLeaveEventArgs : EventArgs {
		public User User { get; init; }
		public bool IsMe { get; init; }

		public OnChannelJoinLeaveEventArgs( User user, bool isMe = false ) => ( User, IsMe ) = ( user, isMe );
	}
}
