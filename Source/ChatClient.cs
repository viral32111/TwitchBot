using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot {
	public static class MessageTemplate {
		public static readonly string RequestCapabilities = "CAP REQ :{0}";

		public static readonly string AuthenticateName = "NICK {0}";
		public static readonly string AuthenticateToken = "PASS oauth:{0}";

		public static readonly string JoinChannel = "JOIN {0}";
		public static readonly string SendMessage = "PRIVMSG {0} :{1}";
		//public static readonly string SendReply = "@reply-parent-msg-id={0} PRIVMSG {1} :{2}";
	}

	public class OnConnectEventArgs {
		//public WebSocketState State;
	}

	public class OnChannelMessageEventArgs {
		// probably going to need custom classes for these, especially user cus they have badges, mod status, and other public acc information
		public string Channel;
		public string Message;
		public string User;
	}

	public class ChatClient {
		private readonly ClientWebSocket webSocketClient = new();
		private readonly CancellationTokenSource cancellationTokenSource = new();

		//private Task? receiveTask;
		private TaskCompletionSource<string> capabilitiesAcknowledgement = new();
		private TaskCompletionSource<string> authenticationAcknowledgement = new();

		public delegate Task OnConnectHandler( object sender, OnConnectEventArgs eventArgs );
		public event OnConnectHandler? OnConnect;

		public delegate Task OnChannelMessageHandler( object sender, OnConnectEventArgs eventArgs );
		public event OnChannelMessageHandler? OnChannelMessage;

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

		// This should block until the websocket connection closes
		public async Task<bool> Connect( string serverUri ) {
			if ( webSocketClient.State != WebSocketState.None ) throw new Exception( "Cannot connect when already connected" );

			await webSocketClient.ConnectAsync( new Uri( serverUri ), cancellationTokenSource.Token );

			Task receiveTask = Receive();

			OnConnect?.Invoke( this, new OnConnectEventArgs() );

			receiveTask.Wait();

			return webSocketClient.State == WebSocketState.Closed;
		}

		public async Task<bool> Disconnect() {
			if ( webSocketClient.State != WebSocketState.Open ) throw new Exception( "Cannot disconnect client that never connected" );

			await webSocketClient.CloseAsync( WebSocketCloseStatus.NormalClosure, "Goodbye.", cancellationTokenSource.Token );

			//if ( receiveTask != null ) receiveTask.Wait(); // await .WaitAsync()?

			return webSocketClient.State == WebSocketState.Closed;
		}

		public async Task<Twitch.Capability[]> RequestCapabilities( Twitch.Capability[] capabilities ) {
			List<string> capabilitiesRequested = new(), capabilitiesGranted = new();

			foreach ( Twitch.Capability capability in capabilities ) capabilitiesRequested.Add( capability switch {
				Twitch.Capability.Commands => "twitch.tv/commands",
				Twitch.Capability.Membership => "twitch.tv/membership",
				Twitch.Capability.Tags => "twitch.tv/tags",
				_ => string.Empty
			} );

			await Send( $"CAP REQ :{ string.Join( ' ', capabilitiesRequested ) }" );

			Console.WriteLine( "Waiting for capabilities acknowledgement..." );
			string capabilitiesResponse = await capabilitiesAcknowledgement.Task;
			Console.WriteLine( "Got capabilities acknowledgement: {0}", capabilitiesResponse );

			capabilitiesGranted.AddRange( capabilitiesResponse.Split( ' ' ) );
			Console.WriteLine( "Split capabilities string into list!" );

			//if ( !capabilitiesGranted.SequenceEqual( capabilitiesRequested ) ) throw new Exception( "Granted capabilities does not match requested capabilities." );
			//Console.WriteLine( "Capabilites match!" );

			return capabilities;
		}

		public async Task<bool> Authenticate( string accountName, string accessToken ) {
			Console.WriteLine( "{0}, {1}", accountName, accessToken );

			// :tmi.twitch.tv NOTICE * :Login authentication failed
			//string passResponse = await Send( $"PASS oauth:{accessToken}" );
			string passResponse = await Send( $"PASS oauth:{accessToken}" );
			//string nickResponse = await Send( $"NICK {accountName}" );
			string nickResponse = await Send( $"NICK {accountName}" );

			Console.WriteLine( "Waiting for authentication response..." );
			string authenticationResponse = await authenticationAcknowledgement.Task;
			Console.WriteLine( "Got authentication response: '{0}'", authenticationResponse );

			// Check response and see if its successful

			return true; // placeholder
		}

		public async Task JoinChannel( string channelName ) {
			await Send( $"JOIN #{channelName}" );

			//await Send( $"PRIVMSG #{channelName} :@Nightbot u suck" );
		}

		private async Task<string> Send( string message ) {
			await webSocketClient.SendAsync( Encoding.UTF8.GetBytes( message ), WebSocketMessageType.Text, true, cancellationTokenSource.Token );
			Console.WriteLine( $"SENT: '{message}'" );

			// I guess this should wait for a Receive() then decode & return it

			return string.Empty; // placeholder
		}

		private async Task Receive() {
			byte[] receiveBuffer = new byte[ 4096 ];

			while ( webSocketClient.State != WebSocketState.Closed ) {
				WebSocketReceiveResult result = await webSocketClient.ReceiveAsync( receiveBuffer, cancellationTokenSource.Token );

				if ( result.MessageType == WebSocketMessageType.Text ) {
					string message = Encoding.UTF8.GetString( receiveBuffer );

					if ( message.StartsWith( ":tmi.twitch.tv" ) ) {
						Console.WriteLine( "Got twitch message" );

						if ( message.StartsWith( ":tmi.twitch.tv CAP * ACK" ) ) {
							Console.WriteLine( "Got capabilities response: {0}", message );

							// Fire capabilities response event

							//capabilitiesResponseTask.SetResult( message.Substring( ":tmi.twitch.tv CAP * ACK ".Length ).Split( ' ' ) );
							Console.WriteLine( "Firing capabilities acknowledgement..." );
							capabilitiesAcknowledgement.SetResult( message[ ":tmi.twitch.tv CAP * ACK ".Length.. ] );
							Console.WriteLine( "Fired capabilities acknowledgement." );

						// Login success
						} else if ( message.StartsWith( ":tmi.twitch.tv 001 peeksabot :Welcome, GLHF!" ) ) {
							authenticationAcknowledgement.SetResult( message );

						} else {
							Console.WriteLine( "Got unknown message: '{0}'", message );
						}
					} else {
						Console.WriteLine( "Got unknown response: '{0}'", message );
					}
				} else {
					Console.WriteLine( "Received ({0}): {1}, {2}, {3})", webSocketClient.State, result.MessageType, result.Count, result.CloseStatus );
				}

				Array.Clear( receiveBuffer, 0, receiveBuffer.Length );



				// somehow make this tie in with the latest Send() call
			}
		}

	}
}
