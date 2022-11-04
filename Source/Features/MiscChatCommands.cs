using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TwitchBot.Twitch;

namespace TwitchBot.Features {
	public class MiscChatCommands {

		private static readonly Random randomGenerator = new();

		[ModuleInitializer]
		public static void Setup() {

			// Register the chat commands
			ChatCommand.Register( HelloWorldCommand );

		}

		[ChatCommand( "hello" )]
		public static async Task HelloWorldCommand( Message message ) {
			await message.Reply( "Hello World!" );
		}

		[ChatCommand( "random" )]
		public static async Task RandomNumberCommand( Message message ) {
			await message.Reply( $"Your random number is {randomGenerator.Next( 100 )}" );
		}

		[ChatCommand( "cake" )]
		public static async Task ApertureScienceCommand( Message message ) {
			await message.Reply( "This was a triumph!\nI'm making a note here: Huge success!\nIt's hard to overstate my satisfaction.\n\nWe do what we must because we can. For the good of all of us. Except the ones who are dead.\n\nBut there's no sense crying over every mistake.\nYou just keep on trying 'til you run out of cake." );
		}

		[ChatCommand( "socials", new string[] { "twitter" } )]
		public static async Task SocialMediasCommand( Message message ) {
			await message.Reply( "You can find me on Twitter! https://twitter.com/RawrelTV" );
		}

		[ChatCommand( "whoami" )]
		public static async Task WhoAmICommand( Message message ) {
			await message.Reply( $"You are {message.Author.DisplayName}, your name color is {message.Author.NameColor}, your account identifier is {message.Author.Identifier}, you are {( message.Author.IsSubscriber == true ? "subscribed" : "not subscribed" )}, you are {( message.Author.IsModerator == true ? "a moderator" : "not a moderator" )}." );
		}

	}
}
