# Start with ASP.NET Runtime v6 - https://hub.docker.com/_/microsoft-dotnet-aspnet
FROM mcr.microsoft.com/dotnet/aspnet:6.0

# Regular user configuration
ARG USER_ID=1000 \
	USER_NAME=user \
	USER_HOME=/home/user \

# Paths configuration
ARG DIRECTORY_BIN=/usr/local/twitch-bot \
	DIRECTORY_DATA=/var/lib/twitch-bot \
	DIRECTORY_CACHE=/var/cache/twitch-bot \
	FILE_CONFIG=/etc/twitch-bot.json

# Disable .NET telemetry
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Create directories & regular user
RUN mkdir --verbose --parents ${USER_HOME} ${DIRECTORY_BIN} ${DIRECTORY_DATA} ${DIRECTORY_CACHE} && \
	touch ${FILE_CONFIG} && \
	adduser --system --disabled-password --disabled-login --shell /usr/sbin/nologin --no-create-home --home ${USER_HOME} --gecos ${USER_NAME} --group --uid ${USER_ID} ${USER_NAME} && \
	chown --changes --recursive ${USER_ID}:${USER_ID} ${USER_HOME} ${DIRECTORY_BIN} ${DIRECTORY_DATA} ${DIRECTORY_CACHE} ${FILE_CONFIG}

# Add build artifacts
COPY --chown=${USER_ID}:${USER_ID} ./TwitchBot.deps.json ${DIRECTORY_BIN}/
COPY --chown=${USER_ID}:${USER_ID} ./TwitchBot.runtimeconfig.json ${DIRECTORY_BIN}/
COPY --chown=${USER_ID}:${USER_ID} ./TwitchBot.dll ${DIRECTORY_BIN}/

# Change to regular user & data directory
USER ${USER_ID}:${USER_ID}
WORKDIR ${DIRECTORY_DATA}

# Initialize to create configuration file
RUN dotnet ${DIRECTORY_BIN}/TwitchBot.dll --init ${FILE_CONFIG}

# Persist the data directory
VOLUME ${DIRECTORY_DATA}

# Start the bot when launched
ENTRYPOINT [ "dotnet", "/usr/local/twitchbot/TwitchBot.dll" ]
