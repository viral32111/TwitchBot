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

			return new UserSecrets() {
				AppClientIdentifier = configuration[ "AppClientIdentifier" ],
				AppClientSecret = configuration[ "AppClientSecret" ],
				AccountName = configuration[ "AccountName" ]
			};
		}
	}
}
