# https://hub.docker.com/_/microsoft-dotnet-aspnet
FROM mcr.microsoft.com/dotnet/aspnet:6.0

ARG USER_ID=1000 \
	USER_NAME=user \
	USER_HOME=/home/user \
	DIRECTORY_BIN=/usr/local/twitchbot \
	DIRECTORY_DATA=/var/lib/twitchbot \

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN mkdir --verbose --parents ${DIRECTORY_BIN} ${DIRECTORY_DATA} && \
	adduser --system --disabled-password --disabled-login --shell /usr/sbin/nologin --no-create-home --home ${USER_HOME} --gecos ${USER_NAME} --group --uid ${USER_ID} ${USER_NAME} && \
	chown --changes --recursive ${USER_ID}:${USER_ID} ${DIRECTORY_BIN} ${DIRECTORY_DATA}

COPY --chown=${USER_ID}:${USER_ID} ./TwitchBot.deps.json ${DIRECTORY_BIN}
COPY --chown=${USER_ID}:${USER_ID} ./TwitchBot.runtimeconfig.json ${DIRECTORY_BIN}
COPY --chown=${USER_ID}:${USER_ID} ./TwitchBot.dll ${DIRECTORY_BIN}
COPY --chown=${USER_ID}:${USER_ID} ./twitchbot.json /etc/twitchbot.json

USER ${USER_ID}:${USER_ID}

WORKDIR ${DIRECTORY_DATA}
VOLUME ${DIRECTORY_DATA}

EXPOSE 3000/tcp

ENTRYPOINT [ "dotnet", "/usr/local/twitchbot/TwitchBot.dll" ]
