// https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types#subscription-types

namespace TwitchBot.Twitch.EventSubscription;

public static class SubscriptionType {
	public static readonly string ChannelUpdate = "channel.update"; // A broadcaster updates their channel properties (category, title, language, etc.)

	public static readonly string StreamStart = "stream.online"; // The specified broadcaster starts a stream.
	public static readonly string StreamFinish = "stream.offline"; // The specified broadcaster stops a stream.
}
