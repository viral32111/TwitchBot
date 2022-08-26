using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;

// https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets

namespace TwitchBot {
	public static class UserSecrets {

		public static readonly string TwitchOAuthSecret;

		static UserSecrets() {

			ConfigurationBuilder configurationBuilder = new();
			configurationBuilder.AddUserSecrets( Assembly.GetExecutingAssembly() );

			IConfigurationRoot secrets = configurationBuilder.Build();

			TwitchOAuthSecret = secrets.GetValue<string>( "AppClientSecret" );
			if ( string.IsNullOrEmpty( TwitchOAuthSecret ) ) throw new Exception( "User secrets is missing OAuth client secret" );

			PrintDeprecationNotice( secrets, "AppClientIdentifier" );
			PrintDeprecationNotice( secrets, "AccountName" );

		}

		private static void PrintDeprecationNotice( IConfigurationRoot secrets, string keyName ) {
			if ( !string.IsNullOrEmpty( secrets.GetValue<string>( keyName ) ) ) {
				Log.Warn( $"The user secret '{keyName}' is deprecated, it should be removed from user secrets." );
			}
		}

		/*********************************/
		/***** DEPRECATED PROPERTIES *****/
		/*********************************/

		[Obsolete( "Use Config.TwitchOAuthIdentifier instead" )] public static readonly string AppClientIdentifier = string.Empty;
		[Obsolete( "Use Config.TwitchOAuthSecret instead" )] public static readonly string AppClientSecret = string.Empty;
		[Obsolete( "Do not use" )] public static readonly string AccountName = string.Empty;

	}
}
