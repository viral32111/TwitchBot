﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchBot.InternetRelayChat {

	// An IRC client that inherits from the TCP client class
	public class Client : TcpClient {

		// The size (in bytes) of the receive buffer, set in the constructor
		private readonly int receiveBufferSize;

		// The underlying stream for secure communication, set in the connect method
		private SslStream? secureStream;

		// The background task for receiving data, set in the connect method
		private Task? receiveTask;
		private CancellationTokenSource? receiveTaskCancellationSource;

		// Completion source for server responses to sent messages
		private TaskCompletionSource<Message[]>? responseCompletionSource = null;

		// The IRC server hostname that messages are expected to originate from
		public string? ExpectedHost;

		// Event that runs after we connect to the server
		public delegate Task OnConnectHandler( Client client );
		public event OnConnectHandler? OnConnect;

		// Event that runs after secure communication has started
		public delegate Task OnSecureCommunicationHandler( Client client, X509Certificate serverCertificate, SslProtocols protocol, CipherAlgorithmType cipherAlgorithm, int cipherStrength );
		public event OnSecureCommunicationHandler? OnSecureCommunication;

		// Event that runs after we're ready to send & receive messages
		public delegate Task OnOpenHandler( Client client );
		public event OnOpenHandler? OnOpen;

		// Event that runs after the connection has closed
		public delegate Task OnCloseHandler( Client client );
		public event OnCloseHandler? OnClose;

		// Event that runs when a message is received
		public delegate Task OnMessageHandler( Client client, Message message );
		public event OnMessageHandler? OnMessage;

		// Constructor sets private properties and initializes the base TCP client class
		public Client( AddressFamily addressFamily = AddressFamily.InterNetwork, int bufferSize = 4096 ) : base( addressFamily ) {
			receiveBufferSize = bufferSize;
		}

		// Connects to an IRC server
		public new async Task ConnectAsync( string hostname, int port = 6697 ) {
			if ( IsConnected() ) throw new Exception( "Connection to server is already established" );

			// Connect to the server using TCP
			await base.ConnectAsync( hostname, port );
			//Console.WriteLine( "TCP CONNECTED" ); // DEBUGGING
			OnConnect?.Invoke( this );

			// Start secure communication using TLS, throws an AuthenticationException if the server certificate is invalid
			secureStream = new( GetStream(), false );
			await secureStream.AuthenticateAsClientAsync( new SslClientAuthenticationOptions() {
				TargetHost = hostname,
				EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
				/*CipherSuitesPolicy = new( new[] {
					TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
					TlsCipherSuite.TLS_AES_256_GCM_SHA384,
					TlsCipherSuite.TLS_AES_128_GCM_SHA256
				} ),*/
				EncryptionPolicy = EncryptionPolicy.RequireEncryption,
				CertificateRevocationCheckMode = X509RevocationMode.Online,
				RemoteCertificateValidationCallback = ValidateServerCertificate
			} );
			//Console.WriteLine( "SSL STARTED" ); // DEBUGGING
			OnSecureCommunication?.Invoke( this, secureStream.RemoteCertificate!, secureStream.SslProtocol, secureStream.CipherAlgorithm, secureStream.CipherStrength );

			// Start receiving data in the background
			receiveTaskCancellationSource = new();
			receiveTask = ReceiveAsync( receiveTaskCancellationSource.Token );
			//Console.WriteLine( "RECEIVING" ); // DEBUGGING

			// We're now ready
			//Console.WriteLine( "OPEN" ); // DEBUGGING
			OnOpen?.Invoke( this );

		}

		// Closes the connection to the server
		public async Task CloseAsync() {

			// Send the IRC quit command, if connected to a server
			if ( IsConnected() ) await SendAsync( Command.Quit );

			// Stop receiving data in the background
			receiveTaskCancellationSource?.Cancel();

			// End the secure communication
			secureStream?.Close();

			// Close the TCP connection
			Close();

			// We're now closed
			OnClose?.Invoke( this );

		}

		// Waits for the receive data background task to finish, with an optional timeout in milliseconds
		public async Task WaitAsync( double timeout = -1 ) {
			if ( receiveTask == null ) throw new Exception( "Receive data background task has not been initialized" );

			await receiveTask.WaitAsync( TimeSpan.FromMilliseconds( timeout ) );
		}

		// Checks if the TCP socket is connected & secure communication is established
		public bool IsConnected() => Connected && secureStream != null && secureStream.IsAuthenticated;

		// Gets the IP address & port number of the server
		public IPEndPoint GetServerAddress() {
			IPEndPoint? serverEndPoint = ( IPEndPoint? ) Client.RemoteEndPoint;
			return serverEndPoint ?? throw new Exception( "Server endpoint is invalid" );
		}

		// Sends an IRC message to the server
		public async Task SendAsync( string command, string? parameters = null, string? middle = null, Dictionary<string, string?>? tags = null ) {
			if ( !IsConnected() ) throw new Exception( "Connection to server has not been established" );

			Message message = new( command, middle, parameters, tags );

			/*Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine( "SEND: '{0}'", message ); // DEBUGGING
			Console.ForegroundColor = ConsoleColor.Gray;*/

			await secureStream!.WriteAsync( message.GetBytes() );
			await secureStream!.FlushAsync();
		}

		// Sends an IRC message to the server, and waits for response message(s)
		public async Task<Message[]> SendExpectResponseAsync( string command, string? parameters = null ) {

			// Fail if the last completion source was not cleaned up
			if ( responseCompletionSource != null ) throw new Exception( "Response source was not cleaned up" );

			// Create a new completion source, if desired
			responseCompletionSource = new();

			// Send the message
			await SendAsync( command, parameters );

			// Wait for the response message(s)
			Message[] messages = await responseCompletionSource.Task;

			// Cleanup the completion source
			responseCompletionSource = null;

			// Return the response message(s)
			return messages;

		}

		// Receives data in the background from the server while connected
		private async Task ReceiveAsync( CancellationToken cancellationToken ) {

			// Create the buffer for receiving data
			byte[] receivedData = new byte[ receiveBufferSize ];

			// Repeat until we're no longer connected, or we're cancelled
			while ( IsConnected() || cancellationToken.IsCancellationRequested ) {

				// Clear the buffer
				Array.Clear( receivedData );

				// Receive data into the buffer
				int receivedBytes = await secureStream!.ReadAsync( receivedData, cancellationToken );

				/*Console.ForegroundColor = ConsoleColor.Cyan;
				Console.WriteLine( "RECEIVE: {0} '{1}'", receivedBytes, Encoding.UTF8.GetString( receivedData ) ); // DEBUGGING
				Console.ForegroundColor = ConsoleColor.Gray;*/

				// Parse the received data as IRC messages
				Message[] messages = Message.Parse( receivedData, receivedBytes );

				// Process these messages
				await ProcessReceivedMessages( messages );

			}

		}

		// Processes messages sent from the server
		private async Task ProcessReceivedMessages( Message[] messages ) {

			// Create a list for messages that are responses
			List<Message> responseMessages = new();

			// Loop through each message
			foreach ( Message message in messages ) {

				/*Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine( "PROCESS: '{0}'", message ); // DEBUGGING
				Console.ForegroundColor = ConsoleColor.Gray;*/

				// Respond to keep-alive pings - https://dev.twitch.tv/docs/irc#keepalive-messages
				if ( message.Command == Command.Ping ) await SendAsync( Command.Pong, message.Parameters );

				// Fail if the message originated from an unexpected server
				else if ( ExpectedHost != null && ( message.Host == null || !message.Host!.EndsWith( ExpectedHost ) ) ) throw new Exception( "Received message from foreign server" );

				// Add this message to the list if we are expecting response message(s)
				if ( responseCompletionSource != null ) responseMessages.Add( message );

				// Otherwise, run the event to further process this message
				else OnMessage?.Invoke( this, message );

			}

			// Complete the completion source if there are response message(s)
			if ( responseMessages.Count > 0 ) responseCompletionSource!.SetResult( responseMessages.ToArray() );

		}

		// Validates the server certificate to start secure communication
		private bool ValidateServerCertificate( object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors policyErrors ) {

			// Fail if the certificate or certificate chain is invalid
			if ( certificate == null || chain == null ) return false;

			// Fail if there are any policy violation errors
			if ( policyErrors != SslPolicyErrors.None ) return false;

			// Otherwise all is good
			return true;

		}

	}

}