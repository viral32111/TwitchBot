using System;
using System.Drawing;
using System.Text.Json.Nodes;

/* Global User Tags:
 user-id=675961583
 display-name=viral32111_
 color=#FF0000
 badge-info=
 emotes=
 emote-sets=0,300374282
*/

namespace TwitchBot.Twitch {
	public class GlobalUser {

		// Static data from IRC message tags & API responses
		public readonly int Identifier;

		// Required dynamic data from IRC message tags & API responses
		public string DisplayName { get; private set; } = null!;

		// Optional dynamic data from IRC message tags
		public Color? NameColor { get; private set; } = null;
		public string? BadgeInformation { get; private set; } = null;
		public string? Emotes { get; private set; } = null;
		public string[] EmoteSets { get; private set; } = Array.Empty<string>();

		// Creates a global user from an IRC message
		public GlobalUser( InternetRelayChat.Message ircMessage ) {
			Identifier = ExtractIdentifier( ircMessage );
			UpdateProperties( ircMessage );
		}

		// Creates a global user from a users API response
		public GlobalUser( JsonObject userData ) {
			Identifier = int.Parse( userData[ "id" ]!.GetValue<string>() );
			DisplayName = userData[ "login" ]!.GetValue<string>();
		}

		// Extracts the user identifier from the IRC message tags
		public static int ExtractIdentifier( InternetRelayChat.Message ircMessage ) {
			if ( !ircMessage.Tags.TryGetValue( "user-id", out string? userIdentifier ) || userIdentifier == null ) throw new Exception( "IRC message does not contain an identifier tag for this global user" );
			return int.Parse( userIdentifier );
		}

		// Updates the dynamic data from the IRC message tags
		public void UpdateProperties( InternetRelayChat.Message ircMessage ) {
			if ( !ircMessage.Tags.TryGetValue( "display-name", out string? displayName ) || string.IsNullOrWhiteSpace( displayName ) ) throw new Exception( "IRC message does not contain a display name tag for this global user" );
			DisplayName = displayName.ToLower();

			if ( !ircMessage.Tags.TryGetValue( "color", out string? nameColor ) || nameColor == null ) throw new Exception( "IRC message does not contain a color tag for this global user" );
			NameColor = ColorTranslator.FromHtml( nameColor );

			if ( !ircMessage.Tags.TryGetValue( "badge-info", out string? badgeInformation ) || badgeInformation == null ) throw new Exception( "IRC message does not contain a badge information tag for this global user" );
			BadgeInformation = badgeInformation;

			// This one is not always there
			if ( ircMessage.Tags.TryGetValue( "emotes", out string? emotes ) && emotes == null ) Emotes = emotes;

			if ( !ircMessage.Tags.TryGetValue( "emote-sets", out string? emoteSets ) || emoteSets == null ) throw new Exception( "IRC message does not contain an emote sets tag for this global user" );
			EmoteSets = emoteSets.Split( "," );
		}

	}
}
