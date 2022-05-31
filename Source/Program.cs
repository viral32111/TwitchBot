using System;
using System.Net.WebSockets;
using System.Text;

namespace TwitchBot {
	public class Program {
		public static async Task Main( string[] arguments ) {
			Shared.ApplicationDataDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), Config.ApplicationDataDirectory );
			Shared.UserSecrets = UserSecrets.Load();

			UserAccessToken userAccessToken = await UserAccessToken.Fetch();

			ClientWebSocket client = new();
			CancellationTokenSource cancellationTokenSource = new();

			Console.WriteLine( "Connecting to chat..." );
			await client.ConnectAsync( new Uri( Config.ChatServerURI ), cancellationTokenSource.Token );
			Console.WriteLine( " Connected to chat." );

			/*Task receiveTask = Task.Factory.StartNew( async () => {
				byte[] receiveBuffer = new byte[ 4096 ];
				
				Console.WriteLine( "Started recieving ({0})", client.State );
				
				while ( client.State != WebSocketState.Closed ) {
					WebSocketReceiveResult result = await client.ReceiveAsync( receiveBuffer, cancellationTokenSource.Token );
					Console.WriteLine( " Received ({0}): {1}", client.State, Encoding.UTF8.GetString( receiveBuffer ) );
					Array.Clear( receiveBuffer, 0, receiveBuffer.Length );
				}

				while ( true ) { }

				Console.WriteLine( " Finished receiving? ({0})", client.State );
			} );*/

			Console.WriteLine( "Starting receive task..." );
			Task receiveTask = WebSocketReceive( client, cancellationTokenSource );
			Console.WriteLine( " Started receive task.", receiveTask.ToString() );

			Console.WriteLine( "Sending capabilities request..." );
			await client.SendAsync( Encoding.UTF8.GetBytes( "CAP REQ :twitch.tv/commands twitch.tv/tags twitch.tv/membership" ), WebSocketMessageType.Text, true, cancellationTokenSource.Token );
			Console.WriteLine( " Sent capabilities request." );

			Console.WriteLine( "Sending authentication..." );
			await client.SendAsync( Encoding.UTF8.GetBytes( $"PASS oauth:{ userAccessToken.AccessToken }" ), WebSocketMessageType.Text, true, cancellationTokenSource.Token );
			await client.SendAsync( Encoding.UTF8.GetBytes( $"NICK { Shared.UserSecrets.AccountName }" ), WebSocketMessageType.Text, true, cancellationTokenSource.Token );
			Console.WriteLine( " Sent authentication." );

			/*Task sendTask = Task.Factory.StartNew( async () => {
				for ( int i = 1; i <= 5; i++ ) {
					Console.WriteLine( "Sending data... ({0})", client.State );
					await client.SendAsync( Encoding.UTF8.GetBytes( $"LOOP {i}/5" ), WebSocketMessageType.Text, true, cancellationTokenSource.Token );
					Console.WriteLine( " Sent data. ({0})", client.State );
					
					await Task.Delay( 1000 );
				}
				
				Console.WriteLine( "Closing connection... ({0})", client.State );
				await client.CloseAsync( WebSocketCloseStatus.NormalClosure, "Goodbye.", cancellationTokenSource.Token );
				Console.WriteLine( " Connection closed. ({0})", client.State );
			} );*/

			Console.WriteLine( "Waiting for all tasks to finish..." );
			receiveTask.Wait();
			//sendTask.Wait();
			Console.WriteLine( " All tasks have finished. ({1}, '{2}')", client.CloseStatus, client.CloseStatusDescription );
		}

		private static async Task WebSocketReceive( ClientWebSocket client, CancellationTokenSource cancellationTokenSource ) {
			byte[] receiveBuffer = new byte[ 4096 ];

			Console.WriteLine( "Started recieving..." );

			while ( client.State != WebSocketState.Closed ) {
				WebSocketReceiveResult result = await client.ReceiveAsync( receiveBuffer, cancellationTokenSource.Token );
				Console.WriteLine( " Received ({0}, {1}, {2}, {3}): {4}", client.State, result.MessageType, result.Count, result.CloseStatus, Encoding.UTF8.GetString( receiveBuffer ) );
				Array.Clear( receiveBuffer, 0, receiveBuffer.Length );
			}

			Console.WriteLine( " Finished receiving." );
		}
	}
}