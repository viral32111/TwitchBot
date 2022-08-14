using System;
using System.Security;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace TwitchBot {
	public static class Cloudflare {

		public static Uri StartTunnel( string clientVersion ) { // , short localPortNumber

			ProcessStartInfo startInfo = new() {
				FileName = GetClientPath( clientVersion ),
				Arguments = "tunnel --no-autoupdate --loglevel info --metrics 127.0.0.1: --hello-world", // --url http://127.0.0.1:3000
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = false,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			Process? tunnelClient = Process.Start( startInfo );

			if ( tunnelClient == null ) throw new Exception( "Failed to start the Cloudflare Tunnel client" );

			Uri? tunnelUrl = null;

			while ( tunnelUrl == null && tunnelClient.StandardError.EndOfStream == false ) {

				// Read the line
				string? line = tunnelClient.StandardError.ReadLine();
				if ( line == null ) continue;

				Match urlMatch = Regex.Match( line, @"^.*(https:\/\/.+\.trycloudflare\.com).*$" );
				if ( urlMatch.Success ) {
					tunnelUrl = new Uri( urlMatch.Groups[ 1 ].Value );
				}

			}

			return tunnelUrl!;

			/*Task readTask = Task.Run( () => {
				ReadStandardStream( tunnelClient.StandardError );
			} );

			readTask.Wait();*/

			//tunnelClient.Close();

			//Console.WriteLine( "closed" );

		}

		/*private static void ReadStandardStream( StreamReader standardStream ) {

			// Run until there is nothing left to read
			while ( standardStream.EndOfStream == false ) {

				// Read the line
				string? line = standardStream.ReadLine();
				if ( line == null ) continue;

				Match urlMatch = Regex.Match( line, @"^.*(https:\/\/.+\.trycloudflare\.com).*$" );
				if ( urlMatch.Success ) {
					string tunnelUrl = urlMatch.Groups[ 1 ].Value;
					Console.WriteLine( tunnelUrl );
				}

				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine( line );
				Console.ForegroundColor = ConsoleColor.White;

			}

		}*/

		// Gets the path to the executable file of the client
		public static string GetClientPath( string clientVersion ) {
			return Path.Combine( Shared.ApplicationDataDirectory, $"cloudflared-{clientVersion}-windows-amd64.exe" );
		}

		public async static Task DownloadClient( string clientVersion, string? clientChecksum ) {

			//Console.WriteLine( "downloadclient start" );

			// The file path to the downloaded executable file
			string executablePath = GetClientPath( clientVersion );

			// Create the application data directory if it does not exist
			if ( !Directory.Exists( Shared.ApplicationDataDirectory ) ) Directory.CreateDirectory( Shared.ApplicationDataDirectory );

			// Repeat forever...
			do {

				//Console.WriteLine( "downloadinggg" );

				// Download the specified version of the client from GitHub releases
				HttpResponseMessage downloadResponse = await Shared.httpClient.GetAsync( $"https://github.com/cloudflare/cloudflared/releases/download/{clientVersion}/cloudflared-windows-amd64.exe" );

				//Console.WriteLine( "savinggg" );

				// Save the downloaded client into an executable file in the data directory
				using ( FileStream fileStream = new( executablePath, FileMode.Create, FileAccess.Write ) ) {
					await downloadResponse.Content.CopyToAsync( fileStream );
				}

				//Console.WriteLine( "doneee", IsClientDownloaded( clientVersion, clientChecksum ) );

			// ...until the executable is downloaded
			} while ( IsClientDownloaded( clientVersion, clientChecksum ) == false );

			//Console.WriteLine( "downloadclient donee" );

		}

		public static bool IsClientDownloaded( string clientVersion, string? clientChecksum ) {

			// The file path to the downloaded executable file
			string executablePath = GetClientPath( clientVersion );

			// Does the executable file exist?
			if ( File.Exists( executablePath ) ) {

				// Validate the executable file checksum, if one was provided
				if ( clientChecksum != null ) {
					using ( SHA256 sha256 = SHA256.Create() ) {
						using ( FileStream fileStream = new( executablePath, FileMode.Open, FileAccess.Read ) ) {

							// Calculate the checksum of the executable file
							string executableChecksum = Convert.ToHexString( sha256.ComputeHash( fileStream ) );

							// Do the checksums match?
							return string.Equals( executableChecksum, clientChecksum, StringComparison.OrdinalIgnoreCase ); ;

						}
					}

					// The file exists, but we aren't validating the checksum
				} else {
					return true;
				}

				// The file does not exist
			} else {
				return false;
			}

		}

	}
}
