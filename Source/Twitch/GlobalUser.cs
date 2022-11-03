using System;
using System.Drawing;

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

		// Static data from IRC message tags
		public readonly int Identifier; // user-id

		// Dynamic data from IRC message tags
		public string DisplayName { get; private set; } = null!; // display-name
		public Color NameColor { get; private set; } // color
		public string BadgeInformation { get; private set; } = null!; // badge-info
		public string Emotes { get; private set; } = null!; // emotes
		public string[] EmoteSets { get; private set; } = Array.Empty<string>(); // emote-sets

		// Creates a global user from an IRC message
		public GlobalUser( InternetRelayChat.Message ircMessage ) {

			// Set static data
			Identifier = ExtractIdentifier( ircMessage );

			// Set dynamic data
			UpdateProperties( ircMessage );

		}

		// Extracts the user identifier from the IRC message tags
		public static int ExtractIdentifier( InternetRelayChat.Message ircMessage ) {
			if ( !ircMessage.Tags.TryGetValue( "user-id", out string? userIdentifier ) || userIdentifier == null ) throw new Exception( "IRC message does not contain an identifier tag for this global user" );
			return int.Parse( userIdentifier );
		}

		// Updates the dynamic data from the IRC message tags
		public void UpdateProperties( InternetRelayChat.Message ircMessage ) {
			if ( !ircMessage.Tags.TryGetValue( "display-name", out string? displayName ) || string.IsNullOrWhiteSpace( displayName ) ) throw new Exception( "IRC message does not contain a display name tag for this global user" );
			DisplayName = displayName;

			if ( !ircMessage.Tags.TryGetValue( "color", out string? nameColor ) || string.IsNullOrWhiteSpace( nameColor ) ) throw new Exception( "IRC message does not contain a color tag for this global user" );
			NameColor = ColorTranslator.FromHtml( nameColor );

			if ( !ircMessage.Tags.TryGetValue( "badge-info", out string? badgeInformation ) || badgeInformation == null ) throw new Exception( "IRC message does not contain a badge information tag for this global user" );
			BadgeInformation = badgeInformation;

			if ( !ircMessage.Tags.TryGetValue( "emotes", out string? emotes ) || emotes == null ) throw new Exception( "IRC message does not contain an emotes tag for this global user" );
			Emotes = emotes;

			if ( !ircMessage.Tags.TryGetValue( "emote-sets", out string? emoteSets ) || emoteSets == null ) throw new Exception( "IRC message does not contain an emote sets tag for this global user" );
			EmoteSets = emoteSets.Split( "," );
		}

	}
}
