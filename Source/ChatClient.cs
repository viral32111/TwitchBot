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

		// twitch.tv/commands twitch.tv/tags twitch.tv/membership
		public async Task<bool> RequestCapabilities( string[] requestedCapabilities ) {
			// automatically prepend twitch.tv/ to strings in requestedCapabilities array
	
			string capabilitiesResponse = await Send( $"CAP REQ :{ string.Join( ' ', requestedCapabilities ) }" );

			// Check response and see if it includes all the requested capabilities

			return true; // placeholder
		}

		public async Task<bool> Authenticate( string accountName, string accessToken ) {
			string passResponse = await Send( $"PASS oauth:{accessToken}" );
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
				Console.WriteLine( "Received ({0}, {1}, {2}, {3}): {4}", webSocketClient.State, result.MessageType, result.Count, result.CloseStatus, Encoding.UTF8.GetString( receiveBuffer ) );
				Array.Clear( receiveBuffer, 0, receiveBuffer.Length );

				// somehow make this tie in with the latest Send() call
			}
		}

	}
}
