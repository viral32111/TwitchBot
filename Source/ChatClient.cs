﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot {
	public class ChatClient {
		private readonly ClientWebSocket webSocketClient = new();
		private readonly CancellationTokenSource cancellationTokenSource = new();

		private Task? receiveTask;

		public async Task<bool> Connect( string serverUri ) {
			if ( webSocketClient.State != WebSocketState.None ) throw new Exception( "Cannot connect when already connected" );

			await webSocketClient.ConnectAsync( new Uri( serverUri ), cancellationTokenSource.Token );

			receiveTask = Receive();

			return webSocketClient.State == WebSocketState.Open;
		}

		public async Task<bool> Disconnect() {
			if ( webSocketClient.State != WebSocketState.Open ) throw new Exception( "Cannot disconnect client that never connected" );

			await webSocketClient.CloseAsync( WebSocketCloseStatus.NormalClosure, "Goodbye.", cancellationTokenSource.Token );

			if ( receiveTask != null ) receiveTask.Wait(); // await .WaitAsync()?

			return webSocketClient.State == WebSocketState.Closed;
		}

		// twitch.tv/commands twitch.tv/tags twitch.tv/membership
		public async Task RequestCapabilities( string[] capabilities ) {
			await Send( $"CAP REQ :{ string.Join( ' ', capabilities ) }" );
		}

		public async Task Authenticate( string accountName, string accessToken ) {
			await Send( $"PASS oauth:{accessToken}" );
			await Send( $"NICK {accountName}" );
		}

		private async Task Send( string message ) {
			await webSocketClient.SendAsync( Encoding.UTF8.GetBytes( message ), WebSocketMessageType.Text, true, cancellationTokenSource.Token );
			
			// I guess this should wait for a Receive() then return it
		}

		private async Task Receive() {
			byte[] receiveBuffer = new byte[ 4096 ];

			while ( webSocketClient.State != WebSocketState.Closed ) {
				WebSocketReceiveResult result = await webSocketClient.ReceiveAsync( receiveBuffer, cancellationTokenSource.Token );
				Console.WriteLine( "Received ({0}, {1}, {2}, {3}): {4}", webSocketClient.State, result.MessageType, result.Count, result.CloseStatus, Encoding.UTF8.GetString( receiveBuffer ) );
				Array.Clear( receiveBuffer, 0, receiveBuffer.Length );
			}
		}

	}
}
