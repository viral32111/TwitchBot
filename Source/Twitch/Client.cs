using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;

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

// await Send( $"PRIVMSG #{channelName} :@Nightbot u suck" );

/*
:peeksabot!peeksabot@peeksabot.tmi.twitch.tv JOIN #rawreltv
:peeksabot.tmi.twitch.tv 353 peeksabot = #rawreltv :peeksabot
:peeksabot.tmi.twitch.tv 366 peeksabot #rawreltv :End of /NAMES list
@badge-info=;badges=;color=;display-name=PeeksaBot;emote-sets=0,300374282;mod=0;subscriber=0;user-type= :tmi.twitch.tv USERSTATE #rawreltv
@emote-only=0;followers-only=-1;r9k=0;room-id=127154290;slow=0;subs-only=0 :tmi.twitch.tv ROOMSTATE #rawreltv

:alienconglomeration!alienconglomeration@alienconglomeration.tmi.twitch.tv JOIN #rawreltv
:0ax2!0ax2@0ax2.tmi.twitch.tv JOIN #rawreltv
:anotherttvviewer!anotherttvviewer@anotherttvviewer.tmi.twitch.tv JOIN #rawreltv
:metaviews!metaviews@metaviews.tmi.twitch.tv JOIN #rawreltv
:nightbot!nightbot@nightbot.tmi.twitch.tv JOIN #rawreltv
:mogulmail!mogulmail@mogulmail.tmi.twitch.tv JOIN #rawreltv
:streamelements!streamelements@streamelements.tmi.twitch.tv JOIN #rawreltv
:allroadsleadtothefarm!allroadsleadtothefarm@allroadsleadtothefarm.tmi.twitch.tv JOIN #rawreltv
:farminggurl!farminggurl@farminggurl.tmi.twitch.tv JOIN #rawreltv
*/

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

		// An event that is ran whenever a channel is joined
		public delegate Task OnChannelJoinHandler( object sender, OnChannelJoinEventArgs e );
		public event OnChannelJoinHandler? OnChannelJoin;

		// An event that is ran whenever a chat message is received
		public delegate Task OnChatMessageHandler( object sender, OnChatMessageEventArgs e );
		public event OnChatMessageHandler? OnChatMessage;

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

						if ( !message.Tags.TryGetValue( "user-id", out string? userId ) || userId == null ) {
							OnError?.Invoke( this, new OnErrorEventArgs( "User identifier tag missing from user state command" ) );
							return;
						}

						if ( !message.Tags.TryGetValue( "display-name", out string? displayName ) || displayName == null ) {
							OnError?.Invoke( this, new OnErrorEventArgs( "Display name tag missing from user state command" ) );
							return;
						}

						// This feels wrong...
						message.Tags.TryGetValue( "user-type", out string? userType );
						message.Tags.TryGetValue( "color", out string? color );
						message.Tags.TryGetValue( "badges", out string? badges );
						message.Tags.TryGetValue( "badge-information", out string? badgeInformation );
						message.Tags.TryGetValue( "emote-sets", out string? emoteSets );

						User user = new(
							userId,
							displayName,
							userType,
							color,
							badges,
							badgeInformation,
							emoteSets
						);

						Log.Write( "Logged in as '{0}' ({1}).", user.Name, user.Identifier );

						// TODO: Add this user to some sort of shared state
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

					InternetRelayChat.Message[] messages = InternetRelayChat.Message.Parse( receivedMessage );
					List<InternetRelayChat.Message> ourMessages = new();

					foreach ( InternetRelayChat.Message message in messages ) {

						// https://dev.twitch.tv/docs/irc#keepalive-messages
						if ( message.Command == InternetRelayChat.Command.Ping ) { // message.Host == null && 
							await SendMessage( $"PONG :{message.Parameters}" );							
							continue;
						}

						if ( message.Host == null || !message.Host.EndsWith( ExpectedHost ) ) {
							OnError?.Invoke( this, new OnErrorEventArgs( "Received message from foreign server" ) );
							return;
						}

						ourMessages.Add( message );

					}

					// @badge-info=;badges=moderator/1;client-nonce=e583d353bcaf5236bd267b87ca89690d;color=#FF0000;display-name=viral32111_;emotes=;first-msg=0;flags=;id=5587ba2a-e24a-46e6-9954-9881bc970dbb;mod=1;room-id=127154290;subscriber=0;tmi-sent-ts=1654544231319;turbo=0;user-id=675961583;user-type=mod :viral32111_!viral32111_@viral32111_.tmi.twitch.tv PRIVMSG :#rawreltv :wowe i ran the bot first time and it successfully logged in and joined this chat, gg me

					if ( responseSource != null ) {
						responseSource.SetResult( ourMessages.ToArray() );
						responseSource = null; // TODO: Is this needed? - It is done in the SendMessage function
					} else {
						foreach ( InternetRelayChat.Message message in ourMessages ) {
							if ( message.IsServer( ExpectedHost ) ) {
								Console.WriteLine( "Unhandled (server): '{0}'", message.ToString() );
							} else {
								if ( message.IsFor( Shared.UserSecrets.AccountName, ExpectedHost ) == true ) {
									if ( message.Command == InternetRelayChat.Command.Join && message.Parameters != null ) {
										OnChannelJoin?.Invoke( this, new OnChannelJoinEventArgs( this, message.Parameters[ 1.. ] ) );
									} else {
										Console.WriteLine( "Unhandled (command): '{0}'", message.ToString() );
									}
								} else {
									if ( message.Command == "PRIVMSG" && message.Parameters != null && message.User != null && message.Tags != null ) {
										string[] parameters = message.Parameters.Split( ':', 2 );
										string channelName = parameters[ 0 ].Trim()[ 1.. ];
										string messageContent = parameters[ 1 ].Trim();

										OnChatMessage?.Invoke( this, new OnChatMessageEventArgs( this, messageContent, message.User, channelName, message.Tags ) );
									} else {
										Console.WriteLine( "Unhandled (user): '{0}'", message.ToString() );
									}
								}
							}
						}
					}

				} else {
					throw new Exception( $"Received unknown message type {receiveResult.MessageType} of {receiveResult.Count} bytes" );
				}

				Array.Clear( receiveBuffer );
			}
		}

	}

	public class OnChannelJoinEventArgs : EventArgs {
		public Channel Channel { get; init; }

		public OnChannelJoinEventArgs( Client client, string channelName ) => Channel = new Channel( client, channelName );
	}

	public class OnErrorEventArgs : EventArgs {
		public string Message { get; init; }

		public OnErrorEventArgs( string message ) => Message = message;
	}

	public class OnChatMessageEventArgs : EventArgs {
		public Channel Channel { get; init; }
		public string Content { get; init; }
		public string User { get; init; }

		public Dictionary<string, string?> Tags { get; init; }

		public OnChatMessageEventArgs( Client client, string messageContent, string userName, string channelName, Dictionary<string, string?> _tags )
			=> ( Channel, Content, User, Tags ) = ( Channel = new Channel( client, channelName ), messageContent, userName, _tags );
	}
}
