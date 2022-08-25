using System;
using Microsoft.Extensions.Configuration;

// https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets

namespace TwitchBot {
	public class UserSecrets {

		public readonly string TwitchOAuthSecret;

		public UserSecrets( IConfigurationRoot secrets ) {

			TwitchOAuthSecret = secrets.GetValue<string>( "AppClientSecret" );
			if ( string.IsNullOrEmpty( TwitchOAuthSecret ) ) throw new Exception( "User secrets is missing OAuth client secret" );

			PrintDeprecationNotice( secrets, "AppClientIdentifier" );
			PrintDeprecationNotice( secrets, "AccountName" );

		}

		public static void PrintDeprecationNotice( IConfigurationRoot secrets, string keyName ) {
			if ( !string.IsNullOrEmpty( secrets.GetValue<string>( keyName ) ) ) {
				Log.Warn( $"The user secret '{keyName}' is deprecated, it should be removed from user secrets." );
			}
		}

		public static UserSecrets Load() {
			ConfigurationBuilder configurationBuilder = new();
			configurationBuilder.AddUserSecrets<UserSecrets>();

			IConfigurationRoot secrets = configurationBuilder.Build();

			return new( secrets );
		}

		/*********************************/
		/***** DEPRECATED PROPERTIES *****/
		/*********************************/

		[Obsolete( "Use Config.TwitchOAuthIdentifier instead" )] public readonly string AppClientIdentifier = string.Empty;
		[Obsolete( "Use Config.TwitchOAuthSecret instead" )] public readonly string AppClientSecret = string.Empty;
		[Obsolete( "Do not use" )] public readonly string AccountName = string.Empty;

	}
}
