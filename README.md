# FiveVibe

**Set the vibe. Let it ride.**

An open-source AI companion for GTA V Enhanced Story Mode. Give your character a little direction, then let Los Santos unfold while you work, browse, or take a break.

> “Take Michael on a chill drive to Paleto Bay.”
>
> “Walk around Mirror Park.” · “Say hello to the person ahead.” · “Switch to Franklin.”

FiveVibe includes a native in-game chat and a local browser companion. Requests can be written in English or Italian; the interface and assistant replies are in English.

**Status: experimental, v0.6.1.** Driving has been confirmed by the original user. New walking, phone, character switching and mission assistance still need broader in-game testing. The browser's HTTP interface is tested independently; background gameplay depends on GTA continuing to run while unfocused.

## What you can do

| Vibe | Example |
| --- | --- |
| Take a drive | “Find an empty car and cruise.” |
| Go somewhere | “Drive to Sandy Shores.” / “Take me to the Kortz Center.” |
| Stretch your legs | “Take a stroll.” / “Run forward 50 metres.” |
| Use a waypoint | “Walk to the waypoint.” / “Drive to the waypoint.” |
| Meet the world | “Approach the person ahead and say hello.” / “Follow that person.” |
| Change perspective | “Switch to Trevor.” |
| Use the phone | “Answer the phone.” / “Hang up.” |
| Get mission assistance | “What missions are available?” / “Go to Lester's mission marker.” |
| Take over | Press **F8**, click **Stop & take over**, or say “stop.” |

42 built-in places cover landmarks, neighbourhoods and countryside: Del Perro Pier, Mirror Park, Vinewood, Rockford Hills, Galileo Observatory, Sandy Shores, Grapeseed, Paleto Bay, Alamo Sea, Route 68, the protagonists' houses and more. The companion lists and searches the full catalog.

Coordinates are approximate outdoor approach points. A destination does not guarantee building entry, access to restricted areas or a mission trigger. Mount Chiliad means the cable-car base; Fort Zancudo and Humane Labs use approach points. Driving uses GTA's road AI.

### Add your own places

Copy config/FiveVibe.locations.example.json to your game's scripts/FiveVibe.locations.json, change its name, aliases and coordinates, then reload the scripts:

    [
      {
        "Id": "my_spot",
        "Name": "Our Quiet Spot",
        "Aliases": ["our spot", "il nostro posto"],
        "X": 195.0, "Y": -935.0, "Z": 30.0
      }
    ]

Custom places become available to both AI and local commands. Up to 100 custom locations are supported. IDs must be unique; invalid files cannot partially replace the catalog. Unknown locations prompt clarification or a map waypoint, rather than invented coordinates.

## Install

Windows, a legitimate GTA V Enhanced installation, .NET Framework 4.8, a compatible [Script Hook V](https://www.dev-c.enbdev.com/gtav/scripthookv/) and [ScriptHookVDotNet Enhanced v1.1.0.6](https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced/releases/tag/v1.1.0.6) are required.

1. Install the appropriate Script Hook V and ASI loader for your game build.
2. Install SHVDNE following its upstream instructions. FiveVibe uses its API 3.
3. With GTA closed, copy FiveVibe.dll and FiveVibe.ini from the release's scripts directory into your game's scripts directory.
4. Start **Story Mode** and press **F7**. Local commands work without an API key.
5. For natural language, configure OpenAI below.

Release ZIPs contain FiveVibe only. Proprietary game files and third-party runtimes are not bundled. Compatibility depends on your exact game and Script Hook versions. A local development setup loaded on Enhanced 1.0.1158.16; that observation is not a compatibility guarantee for other installations.

### Connect OpenAI

Copy .env.example to a private .env file and fill in:

    OPENAI_API_KEY=your_key_here
    OPENAI_MODEL=gpt-4.1-mini

Edit scripts/FiveVibe.ini:

    [AI]
    Provider=OpenAI
    Model=gpt-4.1-mini
    EnvFile=C:\Private\FiveVibe\.env

EnvFile may also be relative to the game's scripts directory. The file is re-read before every request. Missing credentials produce an error; OpenAI mode never silently falls back to local parsing.

API access and billing are separate from a ChatGPT subscription. Keep your key out of chat, screenshots and commits. See [data and security](SECURITY.md).

## Keep it running while you use your PC

1. In GTA, turn **Pause Game on Focus Loss** off. Windowed or borderless mode is convenient.
2. Start Story Mode with FiveVibe loaded.
3. Run Open-Companion.ps1 from the release folder, or scripts/Open-Companion.ps1 from the source checkout.
4. Use the browser panel to send requests, search places, read replies and stop the current action.

The companion listens only on 127.0.0.1:6791 and uses a fresh session token. Its launcher discovers the running session in %LOCALAPPDATA%\FiveVibe\companion.json. No account, cloud relay, extra Node server, or API key in the browser is needed.

The panel reports when GTA is paused or disconnected. Requests cannot progress while the game is paused, sleeping or closed. Paused requests are rejected with a message so they do not unexpectedly start later. Stop returns character control; a moving car retains momentum.

Optionally, with GTA closed, scripts/Enable-BackgroundPlay.ps1 changes only PauseOnFocusLoss in Enhanced's settings.xml and creates a backup. It does not change resolution or graphics settings.

    [Companion]
    Enabled=true
    Port=6791

Set Enabled=false to disable the browser listener or choose a free port. There is no automatic browser launch on every game start.

## In-game controls

| Key | Action |
| --- | --- |
| F7 | Open / close chat |
| Enter | Send |
| Esc | Close chat |
| F8 | Cancel pending AI and autonomy; take over |
| F9 | Show / hide the active task HUD |
| Tab / Shift+Tab | Fill a quick request |
| Up / Down | Recall requests |
| Page Up / Page Down / mouse wheel | Scroll conversation |
| Ctrl+V | Paste |
| Insert | SHVDNE reload, with its default binding |

The HUD uses GTA fonts, menu textures, protagonist colours, notification feed and instructional buttons. Native Trainer and the SHVDNE console have their own keys; the source installer assigns a new SHVDNE console configuration to F6 to avoid a common F4 conflict.

## Chat menus, world controls and a clean screen

The native chat has five tabs: **Chat, Places, Ideas, World, HUD**. Click a tab or use Ctrl+Left/Right. Places lists every built-in and custom location, with Drive/Walk selection; Ideas provides ready-to-edit requests. Choosing a place or idea fills the composer: press Enter to send. Page Up/Down or the mouse wheel browses pages. Esc first returns from a submenu to Chat.

The composer supports 420 characters. Long input follows the latest three lines; use Shift+Page Up/Down or the wheel over the composer to read earlier lines. Text drawing and width measurement use GTA's multi-component text command with UTF-8-safe components, fixing the former disappearance after roughly 32 characters.

World provides weather presets, 06:00 / 12:00 / 19:00 / 00:00 and Freeze/Resume Clock buttons. These controls apply immediately in free roam. You can also ask:

> “Set rain and set the time to 22:30.”
>
> “Metti il tramonto.” · “Freeze the clock.” · “Restore automatic weather and resume the clock.”

Weather/time changes leave an active drive or walk running. The story retains control during missions and cutscenes; FiveVibe releases its weather/clock overrides when a mission starts or the script unloads. Automatic weather releases the manual weather override rather than selecting a new preset.

**F9** shows/hides the on-screen task panel and its closed-chat hints, without stopping autonomy. The HUD tab, browser button and requests such as “Hide the task HUD” do the same. F8 still takes over. This preference is saved per Windows user in %LOCALAPPDATA%\FiveVibe\preferences.json. The INI supports [HUD] ShowTaskHud and ToggleKey as defaults; a saved preference takes precedence for visibility.

## What is still limited

- One action per request. A character switch may precede that action; general multi-step plans are not implemented.
- Driving uses native GTA pathfinding: traffic, collisions and inaccessible routes can still interrupt it. It looks for an empty, unlocked, parked car rather than spawning one or ejecting an NPC.
- Walking destinations are limited to 600 metres; forward movement to 5–100 metres. A free stroll lasts up to three minutes. Blocked paths stop without teleporting.
- NPC interactions are approach, follow and an attempted native greeting. Generated NPC conversations and contact calling are not implemented.
- Story switching respects the save's unlocked characters and GTA's current restrictions.
- Mission support means recognized map markers, status and explicit movement/phone/interaction help. FiveVibe cannot read every objective or finish the campaign autonomously.
- Cutscenes, death and story transitions can interrupt autonomy. F8 and browser Stop cancel late AI responses as well.

Use mods in Story Mode. Follow Rockstar's official BattlEye settings for modded single-player; restore the appropriate clean setup before GTA Online.

### How mission assistance works today

FiveVibe is currently a tool for explicit actions, not an autonomous story player. The AI plans one supported action per request; it does not continuously read and reason about the mission's next step.

1. **Before a mission:** “What missions are available?” checks the global mission-active flag and reports the nearest recognized available story marker for the current character. It is not a complete quest log.
2. **Reaching a marker:** “Go to Lester's mission marker” selects a matching known map marker. On foot it walks if the point is within 600 metres; in a car it uses the driving controller. A car stops near the point (about 22 metres); the player may still need to get out or enter the activation area. FiveVibe does not force-start missions.
3. **When GTA starts the mission:** existing autonomy and pending AI planning are cancelled. GTA's story tasks and cutscenes take control; FiveVibe does not clear those tasks. Owned weather/clock overrides are released.
4. **During the mission:** when GTA allows player control, explicit requests for walking, driving to a manually placed waypoint, phone or contextual interaction can help. The player supplies the objective or next action. FiveVibe does not automatically read objective subtitles, select the yellow mission target, handle combat, switch mission roles or decide the next stage.
5. **Afterward:** there is no automatic “complete this, collect the next mission, continue the campaign” loop.

A practical current session is: ask to approach the mission, enter its trigger yourself if necessary, let the cutscene play, set a waypoint for a driving segment and request driving, then handle remaining objectives or give another supported request.

Whole-mission delegation needs actual objective/progress detection, stage transitions, mission-specific actions and recovery from failure. The roadmap should start with a tested driving mission rather than promising every mission from marker navigation alone.

Character switches wait for the requested protagonist, returned player control and a stable finished transition. The character wheel/loading stage is not treated as a rejection after five seconds. A two-minute unconfirmed timeout discards follow-up actions without claiming GTA rejected the switch. F8 cancels FiveVibe's wait and queued follow-up; GTA can still finish its own camera transition.

## Build and checks

In Windows PowerShell:

    .\scripts\Get-Dependencies.ps1
    .\scripts\Build.ps1
    .\tests\Test-ChatUpdate.ps1

The build uses Windows' .NET Framework compiler and a hash-verified upstream SHVDNE SDK. No NuGet restore or Node toolchain is required. Output: dist\FiveVibe.

Tests cover local parsing, destination catalogs, custom-file validation, OpenAI schemas and errors, cancellation, HUD layout, the companion's HTTP interface and update/migration behavior. They do not substitute for in-game testing. Optional live API probes use synthetic state and never command GTA:

    .\build\AiConnectionCheck.exe .\.env
    .\build\AiConnectionCheck.exe .\.env --expanded
    .\build\AiConnectionCheck.exe .\.env --places

Close GTA before updates. The source updater preserves configuration and makes a backup:

    .\scripts\Update-Chat.ps1 -GamePath 'D:\Games\GTAV Enhanced' -WhatIf
    .\scripts\Update-Chat.ps1 -GamePath 'D:\Games\GTAV Enhanced'

It also migrates the earlier GTA AI prototype, preserving its env-file reference and removing the old DLL to prevent duplicate scripts. Use scripts/Uninstall.ps1 -BackupPath '...' to restore the recorded files.

## Roadmap

- More places and community-tested approach points.
- Remember a favourite spot from the current waypoint.
- Short action sequences: arrive, park, get out, take a walk.
- Better stuck detection and activity feedback for background sessions.
- Mission-specific assistance from actual objective state.
- Optional richer NPC interactions.

See [CONTRIBUTING.md](CONTRIBUTING.md), [architecture](docs/architecture.md), and [CHANGELOG.md](CHANGELOG.md).

MIT licensed. FiveVibe is an independent project, not affiliated with Rockstar Games, Take-Two, or OpenAI. GTA and other product names belong to their respective owners.
