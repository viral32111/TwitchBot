using System.Text;
using System.Text.RegularExpressions;
using System.Net.WebSockets;

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

namespace TwitchBot.Twitch {
	public class Client {

		// A websocket client to use for the underlying connection
		// NOTE: Cannot inherit from this because it is sealed
		private readonly ClientWebSocket wsClient = new();

		// A completion source for responses to sent websocket messages
		// NOTE: Should be null whenever a response is not expected
		private TaskCompletionSource<string>? responseSource = null;

		// Regular expression for matching the "Your Host" (004) post-authentication message
		private readonly Regex HostPattern = new( @"^Your host is (.+)$" );

		// The IRC-style Twitch server name that messages originate from
		// NOTE: This changes later on once authentication completes and we are told what our host is
		private string ServerName = "tmi.twitch.tv";

		// An event that is ran whenever a connection is established
		public delegate Task OnConnectHandler( object sender, EventArgs e );
		public event OnConnectHandler? OnConnect;

		// Synchronous function to connect to the websocket server, or timeout after a specified period
		// NOTE: This blocks until the connection is over, which is intended behavior to keep the application running
		public void Connect( string serverAddress, int serverPort = 80, bool connectInsecurely = false, int timeoutSeconds = 10 ) {
			Uri serverUri = new( $"{( connectInsecurely ? "ws" : "wss" )}://{serverAddress}:{serverPort}" );
			Task connectTask = wsClient.ConnectAsync( serverUri, CancellationToken.None );

			Task<Task> raceTask = Task.WhenAny( connectTask, Task.Delay( timeoutSeconds * 1000 ) );
			if ( raceTask.Result != connectTask ) throw new Exception( "Timed out while connecting to websocket server" );

			// Start receiving messages in the background
			Task receiveTask = ReceiveMessages();

			// Run the connection established event handlers, if there are any
			OnConnect?.Invoke( this, new EventArgs() );

			// Wait for the message receive task to finish
			receiveTask.Wait();
		}

		// Asynchronous function to send a capabilities request to the websocket server
		public async Task RequestCapabilities( string[] capabilitiesRequested ) {
			InternetRelayChat.Message[]? capabilitiesResponses = await SendMessage( $"CAP REQ :{string.Join( ' ', capabilitiesRequested )}" );
			if ( capabilitiesResponses == null ) throw new Exception( "Never received response for capabilities request" );

			string[]? capabilitiesResponse = capabilitiesResponses[ 0 ].Parameters?.Split( ' ' );
			if ( capabilitiesResponse == null ) throw new Exception( "Received invalid IRC-styled message for capabilities request" );

			if ( !capabilitiesResponse.SequenceEqual( capabilitiesRequested ) ) throw new Exception( "Not all capabilities were granted" );
		}

		// Asynchronous function to send account credentials to the websocket server
		public async Task Authenticate( string accountName, string accessToken ) {
			await SendMessage( $"PASS oauth:{accessToken}", expectResponse: false );
			InternetRelayChat.Message[]? authResponses = await SendMessage( $"NICK {accountName}" );
			if ( authResponses == null ) throw new Exception( "Never received response for authentication" );

			// TODO: Check for authentication failure (e.g., expired token)

			// The Twitch authentication reply contains multiple messages
			foreach ( InternetRelayChat.Message message in authResponses ) {
				string? parameters = message.Parameters;

				if ( !string.IsNullOrEmpty( parameters ) ) {

					// Remove the account name from the start of the parameters value
					if ( parameters.StartsWith( $"{accountName.ToLower()} :" ) ) parameters = parameters[ ( accountName.Length + 2 ).. ];

					if ( message.Command == InternetRelayChat.Command.Welcome ) Log.Write( "The server welcomes us." );

					if ( message.Command == InternetRelayChat.Command.YourHost ) {
						Match hostMatch = HostPattern.Match( parameters );
						if ( hostMatch.Success ) {
							ServerName = hostMatch.Groups[ 1 ].Value;
							Log.Write( "The server name is: '{0}'", ServerName );
						}
					}

					if ( message.Command == InternetRelayChat.Command.MoTD ) Log.Write( "MoTD: '{0}'", parameters );

				} else {
					if ( message.Command == Command.GlobalUserState ) {
						if ( message.Tags == null ) throw new Exception( "Tags missing for user state command" );

						if ( !message.Tags.TryGetValue( "user-id", out string? userId ) || userId == null ) throw new Exception( "User identifier tag missing from user state command" );
						if ( !message.Tags.TryGetValue( "display-name", out string? displayName ) || displayName == null ) throw new Exception( "Display name tag missing from user state command" );

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

		// Asynchronous function to send messages to the websocket server
		// NOTE: This is NOT to send messages to Twitch chat, see Channel.SendMessage() for that
		private async Task<InternetRelayChat.Message[]?> SendMessage( string messageToSend, bool expectResponse = true ) {
			if ( expectResponse == true ) {
				if ( responseSource != null ) throw new Exception( "Response source was never cleaned up" );
				responseSource = new();
			}

			await Task.Delay( 10 ); // TODO: Why is responseSource still null in the ReceiveMessages() task despite that this happens before SendAsync()?

			await wsClient.SendAsync( Encoding.UTF8.GetBytes( messageToSend ), WebSocketMessageType.Text, true, CancellationToken.None );

			if ( responseSource != null ) {
				string responseMessage = await responseSource.Task;
				responseSource = null;

				InternetRelayChat.Message[] messages = InternetRelayChat.Message.Parse( responseMessage );
				foreach ( InternetRelayChat.Message message in messages ) {
					if ( message.ServerName != ServerName ) throw new Exception( "Received message from foreign server" );
				}

				return messages;
			}

			return null;
		}

		public async Task<Channel> JoinChannel( string channelName ) {
			InternetRelayChat.Message[]? joinResponse = await SendMessage( $"JOIN #{channelName.ToLower()}" );

			if ( joinResponse != null ) {
				Console.WriteLine( "We have join response" );
				foreach ( InternetRelayChat.Message message in joinResponse ) {
					Console.WriteLine( message );
				}
			} else {
				Console.WriteLine( "Join response is null :c" );
			}

			

			// TODO: Parse the response (channel name, current users, etc.) and construct a new Channel object from it

			return new Channel(); // Placeholder
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

					if ( responseSource != null ) {
						responseSource.SetResult( receivedMessage );
						responseSource = null; // Is this needed? - It is done in the SendMessage function
					} else {
						Console.WriteLine( "Got unexpected message: '{0}'", receivedMessage );
					}

				} else {
					throw new Exception( $"Received unknown message type {receiveResult.MessageType} of {receiveResult.Count} bytes" );
				}

				Array.Clear( receiveBuffer );
			}
		}

	}
}
