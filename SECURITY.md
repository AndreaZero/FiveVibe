# Data and security

FiveVibe reads an OpenAI API key from a local env file. Never put credentials in the public INI, a bug report, or a pull request. .env.example must keep OPENAI_API_KEY empty. scripts/Test-PublicFiles.ps1 checks staged public files before publication.

OpenAI receives the request, up to ten recent conversation messages and a compact game-state summary: character, position, vehicle, active action, phone state, nearby NPC availability, recognized mission marker availability, Mission Pilot stage/objective label, and selected camera mode/status. FiveVibe sets store=false. This does not replace the API provider's own data policies. No images, savegames, game files or arbitrary local files are sent. Mission Pilot itself makes no continuous API calls.

Mission Pilot replaces a private local `mission-session.jsonl` diagnostic log in the user's FiveVibe local application data folder at each start. It records in-game observations and result counters, with no credentials or screenshots. It is not uploaded automatically. The companion publishes the stage/objective label, never the log or API key. Review in-game positions and objective text before sharing a log publicly.

Real-world sync is optional and initially off. PC time uses the local Windows clock without a network request. Weather location is configured with a city or an explicit browser location permission after clicking **Use my location**. There is no automatic IP lookup or continuous tracking. Coordinates are rounded to two decimal places in the browser and validated/rounded again before local storage and weather requests. City searches go to `geocoding-api.open-meteo.com`; rounded coordinates go to `api.open-meteo.com`. HTTPS requests have fixed hosts, bounded responses and timeouts. These providers necessarily see the requesting IP address.

Location and sync switches are saved in `%LOCALAPPDATA%\FiveVibe\real-world.json`. The companion snapshot includes the location's display name, never coordinates. The structured city/location endpoint and recognized native city commands bypass OpenAI and conversation history; the game-state context excludes the real-world location. Arbitrary text typed into an ordinary AI chat request is still subject to the request-sharing behavior described above. Disable sync and remove this settings file with GTA closed to clear the saved location.

The browser companion:

- Binds to IPv4 loopback only; it is not designed for network or internet exposure.
- Uses a new random session token, explicit Host/Origin checks, no CORS, and authenticated state/action/location endpoints.
- Keeps the token in the launch URL fragment and browser session storage. The OpenAI key is never part of its state or page.
- Uses bounded headers, request bodies, connections, queues and timeouts.
- Runs no GTA calls on HTTP worker threads. A game-script tick consumes commands.
- Prioritizes Stop by discarding previously queued requests.

The local discovery file is a session capability for the current desktop user. Do not share it or the full launch URL. Other software running as the same Windows user is outside this isolation boundary. The browser server is a deliberately small HTTP/1.1 subset, not a general-purpose web host.

For a suspected vulnerability, avoid publishing secrets or an exploit against another user's setup. Contact the repository maintainer through their GitHub profile for a private reporting route; use public issues for non-sensitive problems only.
