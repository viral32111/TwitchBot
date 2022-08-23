using Microsoft.Extensions.Configuration;

// https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets

namespace TwitchBot {
	public class UserSecrets {
		public string AppClientIdentifier = string.Empty;
		public string AppClientSecret = string.Empty;
		public string AccountName = string.Empty;

		public static UserSecrets Load() {
			ConfigurationBuilder configurationBuilder = new();
			configurationBuilder.AddUserSecrets<UserSecrets>();

			IConfigurationRoot configuration = configurationBuilder.Build();

			string appClientIdentifier = configuration[ "AppClientIdentifier" ];
			string appClientSecret = configuration[ "AppClientSecret" ];
			string accountName = configuration[ "AccountName" ];

			if ( string.IsNullOrEmpty( appClientIdentifier ) ) throw new Exception( "User secrets is missing application client identifier" );
			if ( string.IsNullOrEmpty( appClientSecret ) ) throw new Exception( "User secrets is missing application client secret" );
			if ( string.IsNullOrEmpty( accountName ) ) throw new Exception( "User secrets is missing account name" );

            return new UserSecrets() {
				AppClientIdentifier = appClientIdentifier,
				AppClientSecret = appClientSecret,
				AccountName = accountName
            };
		}
	}
}
