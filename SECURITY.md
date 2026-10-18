# Data and security

FiveVibe reads an OpenAI API key from a local env file. Never put credentials in the public INI, a bug report, or a pull request. .env.example must keep OPENAI_API_KEY empty. scripts/Test-PublicFiles.ps1 checks staged public files before publication.

OpenAI receives the request, up to ten recent conversation messages and a compact game-state summary: character, position, vehicle, active action, phone state, nearby NPC availability, and recognized mission marker availability. FiveVibe sets store=false. This does not replace the API provider's own data policies. No images, savegames, game files or arbitrary local files are sent.

The browser companion:

- Binds to IPv4 loopback only; it is not designed for network or internet exposure.
- Uses a new random session token, explicit Host/Origin checks, no CORS, and authenticated state/action endpoints.
- Keeps the token in the launch URL fragment and browser session storage. The OpenAI key is never part of its state or page.
- Uses bounded headers, request bodies, connections, queues and timeouts.
- Runs no GTA calls on HTTP worker threads. A game-script tick consumes commands.
- Prioritizes Stop by discarding previously queued requests.

The local discovery file is a session capability for the current desktop user. Do not share it or the full launch URL. Other software running as the same Windows user is outside this isolation boundary. The browser server is a deliberately small HTTP/1.1 subset, not a general-purpose web host.

For a suspected vulnerability, avoid publishing secrets or an exploit against another user's setup. Contact the repository maintainer through their GitHub profile for a private reporting route; use public issues for non-sensitive problems only.
