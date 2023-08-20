# syntax=docker/dockerfile:1

# Start from ASP.NET Core Runtime
FROM ghcr.io/viral32111/aspnetcore:7.0

# Configure directories & files
ARG TWITCHBOT_DIRECTORY=/opt/twitch-bot \
	TWITCHBOT_DATA_DIRECTORY=/var/lib/twitch-bot \
	TWITCHBOT_CACHE_DIRECTORY=/var/cache/twitch-bot \
	TWITCHBOT_CONFIG_FILE=/etc/twitch-bot.json

# Add artifacts from build
COPY --chown=${USER_ID}:${USER_ID} ./ ${TWITCHBOT_DIRECTORY}

# Setup required directories
RUN mkdir --verbose --parents ${TWITCHBOT_DATA_DIRECTORY} ${TWITCHBOT_CACHE_DIRECTORY} && \
	chown --changes --recursive ${USER_ID}:${USER_ID} ${TWITCHBOT_DATA_DIRECTORY} ${TWITCHBOT_CACHE_DIRECTORY}

# Initialize bot to create the configuration file
#RUN dotnet ${TWITCHBOT_DIRECTORY}/TwitchBot.dll --init ${TWITCHBOT_CONFIG_FILE} && \
#	chown --changes --recursive ${USER_ID}:${USER_ID} ${TWITCHBOT_CONFIG_FILE}

# Switch to the regular user
USER ${USER_ID}:${USER_ID}

# Switch to & persist the daa directory
WORKDIR ${TWITCHBOT_DATA_DIRECTORY}
VOLUME ${TWITCHBOT_DATA_DIRECTORY}

# Start the bot when launched
ENTRYPOINT [ "dotnet", "/opt/twitch-bot/TwitchBot.dll" ]
CMD [ "/etc/twitch-bot.json" ]
