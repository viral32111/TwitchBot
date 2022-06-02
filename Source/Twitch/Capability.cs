using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// https://dev.twitch.tv/docs/irc/capabilities

namespace TwitchBot.Twitch {
	public enum Capability {
		Commands,
		Membership,
		Tags
	}
}
