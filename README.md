# Twitch Bot

This is my Twitch integration and chat bot, primarily made for [Rawreltv](https://www.twitch.tv/rawreltv).

Currently it is only able to manage user access tokens, connect to Twitch chat, request capabilities, and authenticate as the bot user. I am slowly working on implementing each part of the underlying protocol before working on any major features.

## Running

This project uses [.NET user secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets) to store sensitive configuration values.

Secrets can be set using the `dotnet user-secrets set` command. The following secrets are required:
 * `AppClientIdentifier` should be your Twitch application's client ID.
 * `AppClientSecret` should be your Twitch application's client secret.
 * `AccountName` should be your Twitch Bot's account name.

**NOTE: There is currently no system in place for providing secrets when running the project in a production environment.**

## Modules

* Front-end
  * Dashboard

* Back-end
  * OAuth Token Granter
  * IRC Chatbot
  * Event Listener (EventSub)
  
## License

Copyright (C) 2022 [viral32111](https://viral32111.com).

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program. If not, see https://www.gnu.org/licenses.
