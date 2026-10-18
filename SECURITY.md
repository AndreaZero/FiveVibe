# Data and security

FiveVibe reads an OpenAI API key from a local env file. Never put credentials in the public INI, a bug report, or a pull request. .env.example must keep OPENAI_API_KEY empty. scripts/Test-PublicFiles.ps1 checks staged public files before publication.

OpenAI receives the request, up to ten recent conversation messages and a compact game-state summary: character, position, vehicle, active action, phone state, nearby NPC availability, recognized mission marker availability, Mission Pilot stage/objective label, themed-drive progress, timed-drive stage/countdown, and selected camera mode/status. FiveVibe sets store=false. This does not replace the API provider's own data policies. No images, savegames, game files or arbitrary local files are sent. Mission Pilot itself makes no continuous API calls. Named themed-drive starts are handled locally, and itinerary progression makes no per-leg API calls; the two routes use predefined built-in places, not coordinates supplied by the model or browser.

Timed-drive origins are captured from the protagonist on the game thread and kept only in memory for that session. Canonical duration/return requests execute locally; natural paraphrases can use optional AI interpretation. The model selects a validated duration, never origin coordinates. The companion sends a text request and receives observed stage/status/duration/remaining seconds, not the origin. That origin is not added to the AI catalog, favorites file or release archive; ordinary AI context already includes the current protagonist position as described above. Stop, replacement movement or script reload cancels the session, with no deferred return after cancellation. There are no per-frame AI requests or external routing services.

Mission Pilot replaces a private local `mission-session.jsonl` diagnostic log in the user's FiveVibe local application data folder at each start. It records in-game observations and result counters, with no credentials or screenshots. It is not uploaded automatically. The companion publishes the stage/objective label, never the log or API key. Review in-game positions and objective text before sharing a log publicly.

Favorites are stored per Windows user in `%LOCALAPPDATA%\FiveVibe\favorites.json`, containing names, generated IDs and in-game coordinates. They are not real-world locations and are separate from custom-location configuration. The game thread captures the current protagonist position; the companion save endpoint accepts only a name, and its go endpoint accepts only a saved ID and drive/walk mode. The loopback snapshot contains favorite names/IDs, not coordinates. Save/list/explicit-return commands execute locally without an API call. Saved names and IDs join the destination catalog sent in subsequent OpenAI planning requests; names may also occur in recent chat history. Favorite coordinates are not included in that catalog, although the normal game-state context already includes the protagonist's current position. Review names and file contents before sharing. No favorites file is bundled or uploaded by the release workflow.

Favorite files are size/count bounded and fully validated before loading. Updates use a sibling staging file and atomic replacement; malformed or externally changed data is not silently reset. The mod does not support concurrent independent writers to the same file: reload scripts after editing it externally. To clear saved places, close GTA, back up and remove this file, then restart. The installer preserves it because it lives outside the game's scripts directory.

Real-world sync is optional and initially off. PC time uses the local Windows clock without a network request. Weather location is configured with a city or an explicit browser location permission after clicking **Use my location**. There is no automatic IP lookup or continuous tracking. Coordinates are rounded to two decimal places in the browser and validated/rounded again before local storage and weather requests. City searches go to `geocoding-api.open-meteo.com`; rounded coordinates go to `api.open-meteo.com`. HTTPS requests have fixed hosts, bounded responses and timeouts. These providers necessarily see the requesting IP address.

Location and sync switches are saved in `%LOCALAPPDATA%\FiveVibe\real-world.json`. The companion snapshot includes the location's display name, never coordinates. The structured city/location endpoint and recognized native city commands bypass OpenAI and conversation history; the game-state context excludes the real-world location. Arbitrary text typed into an ordinary AI chat request is still subject to the request-sharing behavior described above. Disable sync and remove this settings file with GTA closed to clear the saved location.

The browser companion:

- Binds to IPv4 loopback only; it is not designed for network or internet exposure.
- Uses a new random session token, explicit Host/Origin checks, no CORS, and authenticated state/action/location/favorites endpoints.
- Keeps the token in the launch URL fragment and browser session storage. The OpenAI key is never part of its state or page.
- Uses bounded headers, request bodies, connections, queues and timeouts.
- Runs no GTA calls on HTTP worker threads. A game-script tick consumes commands.
- Prioritizes Stop by discarding previously queued requests.

The local discovery file is a session capability for the current desktop user. Do not share it or the full launch URL. Other software running as the same Windows user is outside this isolation boundary. The browser server is a deliberately small HTTP/1.1 subset, not a general-purpose web host.

For a suspected vulnerability, avoid publishing secrets or an exploit against another user's setup. Contact the repository maintainer through their GitHub profile for a private reporting route; use public issues for non-sensitive problems only.
