using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot {
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

		public delegate Task OnConnectHandler( object sender, OnConnectEventArgs eventArgs );
		public event OnConnectHandler? OnConnect;

		public delegate Task OnChannelMessageHandler( object sender, OnConnectEventArgs eventArgs );
		public event OnChannelMessageHandler? OnChannelMessage;

		// on stream start/stop (change ig)
		// on view count change
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

			string capabilitiesResponse = await capabilitiesAcknowledgement.Task;
			capabilitiesGranted.AddRange( capabilitiesResponse.Split( ' ' ) );

			if ( !capabilitiesGranted.SequenceEqual( capabilitiesRequested ) ) throw new Exception( "Granted capabilities does not match requested capabilities." );

			return capabilities;
		}

		public async Task<bool> Authenticate( string accountName, string accessToken ) {
			Console.WriteLine( "{0}, {1}", accountName, accessToken );

			// :tmi.twitch.tv NOTICE * :Login authentication failed
			//string passResponse = await Send( $"PASS oauth:{accessToken}" );
			string passResponse = await Send( $"PASS oauth:{accessToken}" );
			//string nickResponse = await Send( $"NICK {accountName}" );
			string nickResponse = await Send( $"NICK {accountName}" );

			// Check response and see if its successful

			return true; // placeholder
		}

		private async Task<string> Send( string message ) {
			await webSocketClient.SendAsync( Encoding.UTF8.GetBytes( message ), WebSocketMessageType.Text, true, cancellationTokenSource.Token );

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
							capabilitiesAcknowledgement.SetResult( message[ ":tmi.twitch.tv CAP * ACK ".Length.. ] );

						} else {
							Console.WriteLine( "Got unknown response: {0}", message );
						}
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
