# FiveVibe

**Set the vibe. Let it ride.**

An open-source AI companion for GTA V Enhanced Story Mode. Give your character a little direction, then let Los Santos unfold while you work, browse, or take a break.

> “Take Michael on a chill drive to Paleto Bay.”
>
> “Walk around Mirror Park.” · “Say hello to the person ahead.” · “Switch to Franklin.”

FiveVibe includes a native in-game chat and a local browser companion. Requests can be written in English or Italian; the interface and assistant replies are in English.

**Status: experimental, v0.9.0.** Free-roam driving has been confirmed by the original user. Automatic driving cinema, vehicle categories and real-world time/weather are implemented and logic-tested; their native behavior still needs in-game validation. Mission Pilot implements a first full-flow attempt for **Franklin and Lamar**; no successful autonomous playthrough has yet been observed. Background gameplay depends on GTA continuing to run while unfocused.

## What you can do

| Vibe | Example |
| --- | --- |
| Take a drive | “Find an empty car and cruise.” |
| Choose a vehicle | “Get a motorcycle.” / “Find an SUV and take a chill drive.” |
| Go somewhere | “Drive to Sandy Shores.” / “Take me to the Kortz Center.” |
| Find a tuning shop | “Drive to LS Customs.” / “Take me to LS Customs La Mesa in a sports car.” |
| Stretch your legs | “Take a stroll.” / “Run forward 50 metres.” |
| Use a waypoint | “Walk to the waypoint.” / “Drive to the waypoint.” |
| Meet the world | “Approach the person ahead and say hello.” / “Follow that person.” |
| Change perspective | “Switch to Trevor.” |
| Use the phone | “Answer the phone.” / “Hang up.” |
| Get mission assistance | “What missions are available?” / “Go to Lester's mission marker.” |
| Try Mission Pilot | “Start Mission Pilot for Franklin and Lamar.” |
| Watch the ride | “Enable cinema camera.” / “Switch to first person.” |
| Match real life | “Enable real time and weather.” |
| Take over | Press **F8**, click **Stop & take over**, or say “stop.” |

47 built-in destinations cover landmarks, neighbourhoods and countryside: Del Perro Pier, Mirror Park, Vinewood, Rockford Hills, Galileo Observatory, Sandy Shores, Grapeseed, Paleto Bay, Alamo Sea, Route 68, the protagonists' houses and more. The companion lists and searches the full catalog.

“LS Custom”, “LS Customs” and “Los Santos Customs” select the nearest detected available Customs map marker among Hawick/Burton, La Mesa, airport and Route 68. These destinations are separate from Simeon's dealership. You can also choose a specific branch in Places if the shop marker is unavailable. Arrival is outside the workshop; FiveVibe does not operate its modification menus or unlock story access.

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

### Choose your ride

Ask for a motorcycle, SUV, off-road vehicle/jeep, sports car, supercar, sedan, muscle car, van or pickup. English and Italian work: “Prendi una moto”, “Prendi una jeep e fai un giro”, or “Portami a LS Customs con un SUV”. “Get a motorcycle” finds and enters one, then waits; add a drive or destination to continue driving.

FiveVibe searches nearby parked, unlocked, available vehicles, normally within 100 metres. It uses GTA vehicle classes; “jeep” means the off-road class, rather than a specific branded model. If no matching vehicle is available, it reports that instead of substituting a different type. It does not spawn vehicles or eject NPCs. Stop your current vehicle before requesting a different type. Bikes use the same native driving tasks as cars and need gameplay testing.

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
| F10 | Cycle camera: gameplay → first person → third person → cinema director → GTA cinema |
| Shift+F10 | Next director shot; enables the director if needed |
| Tab / Shift+Tab | Fill a quick request |
| Up / Down | Recall requests |
| Page Up / Page Down / mouse wheel | Scroll conversation |
| Ctrl+V | Paste |
| Insert | SHVDNE reload, with its default binding |

The HUD uses GTA fonts, menu textures, protagonist colours, notification feed and instructional buttons. Native Trainer and the SHVDNE console have their own keys; the source installer assigns a new SHVDNE console configuration to F6 to avoid a common F4 conflict.

## Chat menus, world controls and a clean screen

The native chat has six tabs: **Chat, Places, Ideas, World, HUD, Camera**. Click a tab or use Ctrl+Left/Right. Places lists every built-in and custom location, with Drive/Walk selection; Ideas provides ready-to-edit requests. Choosing a place or idea fills the composer: press Enter to send. Page Up/Down or the mouse wheel browses pages. Esc first returns from a submenu to Chat.

The composer supports 420 characters. Long input follows the latest three lines; use Shift+Page Up/Down or the wheel over the composer to read earlier lines. Text drawing and width measurement use GTA's multi-component text command with UTF-8-safe components, fixing the former disappearance after roughly 32 characters.

World provides weather presets, 06:00 / 12:00 / 19:00 / 00:00 and Freeze/Resume Clock buttons. These controls apply immediately in free roam. You can also ask:

> “Set rain and set the time to 22:30.”
>
> “Metti il tramonto.” · “Freeze the clock.” · “Restore automatic weather and resume the clock.”

Weather/time changes leave an active drive or walk running. The story retains control during missions and cutscenes; FiveVibe releases its weather/clock overrides when a mission starts or the script unloads. Automatic weather releases the manual weather override rather than selecting a new preset.

### Real-world time and weather

Open the companion's **Real-world time & weather** section:

1. Enable **PC time on** to use Windows' local hour, minute and second, at real-life speed. This works offline.
2. Enter **city, country** and click **Use this city**, or click **Use my location** and grant the browser's location permission. Check the resolved city shown in the panel. Choosing a location enables real weather.
3. Use **Sync off**, or ask “Disable real weather” / “Disable real time” to turn off either part independently. The native World tab also has PC time, real weather and sync-off controls.

Chat examples: “Sincronizza meteo e ora con quelli reali”, “Meteo reale a Milano, Italia”, “Set weather location to Rome, Italy”. City configuration is handled locally before AI planning. Browser location is requested only when you click its button; there is no automatic IP lookup or continuous tracking. If permission is denied, enter a city instead.

The selected location and sync switches are saved per Windows user in `%LOCALAPPDATA%\FiveVibe\real-world.json`. Coordinates are rounded to two decimal places before storage and weather requests. City searches and approximate coordinates go to [Open-Meteo](https://open-meteo.com/), which supplies model-based current conditions. They are not added to the OpenAI game-state context. Open-Meteo's free endpoint needs no API key and is intended for non-commercial use; see its [terms](https://open-meteo.com/en/terms).

Weather refreshes every 15 minutes and maps conditions to GTA presets: clear, clouds, overcast, fog, rain, thunder or snow. This is an approximation, not a recreation of local skies or snow-covered terrain. Failed requests retry after two minutes; readings older than 90 minutes stop being applied. The clock uses the PC's timezone, independently of the weather city. Both overrides yield during missions/cutscenes and resume in free roam. Setting weather or time manually disables the corresponding real-world sync so your explicit choice takes effect.

**F9** shows/hides the on-screen task panel and its closed-chat hints, without stopping autonomy. The HUD tab, browser button and requests such as “Hide the task HUD” do the same. F8 still takes over. This preference is saved per Windows user in %LOCALAPPDATA%\FiveVibe\preferences.json. The INI supports [HUD] ShowTaskHud and ToggleKey as defaults; a saved preference takes precedence for visibility.

## Watch the ride

The cinema director starts automatically when AI driving begins, including Mission Pilot driving, and restores the previous gameplay view when that drive ends or you take over with **F8 / Stop**. Vehicle searching and entry keep the gameplay camera. Toggle **Auto cinema while AI drives** in **F7 > Camera** or the companion, or say “Disable automatic cinema camera”.

You can choose a view yourself through **F7 > Camera**, the companion's **Watch the ride** buttons, or requests such as “Metti la camera cinema”, “Switch to first person”, “Use GTA cinematic camera”, or “Restore normal camera”. A manual choice takes priority for that drive; the next drive uses automatic cinema again if enabled. Camera requests work locally without an API call and preserve the active action.

- **First person:** native player/cockpit view, on foot or in a vehicle.
- **Third person:** GTA's medium follow camera.
- **Cinema director:** six timed shots (chase, side tracking, front quarter, high wide, low tracking and passing portrait), with slow framing drift, damped movement and speed-adjusted field of view. It watches an active AI action; when idle it waits for the next one.
- **GTA cinema:** the game's original vehicle cinematic camera; it waits for a vehicle when on foot.
- **Gameplay:** restores the view settings saved when the first camera override was selected.

**F10** cycles modes; **Shift+F10** cuts to the next director shot. **F8 / Stop** returns control and restores the gameplay camera. Pressing the game's normal **V** camera key also releases FiveVibe's camera override. Hiding the task HUD with F9 leaves the camera running.

The director checks for obstructions and shrinks or abandons blocked shots. It yields during pause, death, character transitions, fades, cutscenes and another script's rendering camera, then resumes once gameplay is available. It creates and destroys only its own camera. Optional letterbox bars are hidden while chat is open; Camera menu and companion controls toggle them for the current session. This is live viewing, with no video recording or export.

New camera behavior is compiled and logic-tested but **has not yet been visually validated in GTA Enhanced**. Tight spaces, interiors and interactions with other camera mods need real-game testing.

Defaults are used even when an existing INI has no Camera section. To customize:

    [Camera]
    CycleKey=F10
    Letterbox=true
    AutoCinemaDriving=true

If the cycle key conflicts with FiveVibe's chat, takeover, HUD, trainer or console bindings, a free fallback is selected and shown in the Camera menu/companion. Each script load starts with the gameplay view; automatic cinema waits for AI driving. The auto-cinema menu toggle applies to the current session; the INI supplies its startup default.

## What is still limited

- One action per request. A character switch may precede that action. Mission Pilot has one explicit mission profile; arbitrary multi-step plans are not implemented.
- Driving uses native GTA pathfinding: traffic, collisions and inaccessible routes can still interrupt it. It looks for an empty, unlocked, parked car rather than spawning one or ejecting an NPC.
- Walking destinations are limited to 600 metres; forward movement to 5–100 metres. A free stroll lasts up to three minutes. Blocked paths stop without teleporting.
- NPC interactions are approach, follow and an attempted native greeting. Generated NPC conversations and contact calling are not implemented.
- Story switching respects the save's unlocked characters and GTA's current restrictions.
- General mission support means map markers, status and explicit movement/phone/interaction help. Mission Pilot additionally attempts Franklin and Lamar. FiveVibe cannot read every mission's objectives or finish the campaign autonomously.
- Mission Pilot waits through cutscenes; other autonomy can be interrupted by story transitions. F8 and browser Stop cancel the pilot and late AI responses.

Use mods in Story Mode. Follow Rockstar's official BattlEye settings for modded single-player; restore the appropriate clean setup before GTA Online.

### Mission Pilot: Franklin and Lamar

1. In Story Mode, open **Pause > Game > Replay Mission > Franklin and Lamar** (the menu names depend on GTA's language). The mission must already be unlocked. Alternatively use its normal story start.
2. At the first playable section press F7 and send **“Start Mission Pilot for Franklin and Lamar”**. The same command works offline and from the companion. You can arm it before starting the replay; it waits up to ten minutes for this specific mission.
3. The pilot attempts: walk with Lamar → choose a mission car → follow his chase route → park → evade the police → return to Simeon's dealership → enter Franklin's Buffalo → wait for Lamar → drive home → wait for the final cutscene and result. No manually placed waypoints are needed.
4. The native task HUD and companion show the current stage. F9 hides the task HUD without stopping the pilot. **F8 / Stop takes over immediately**; a new movement request replaces the pilot. Resume with the same start command after repositioning, or restart the replay yourself.

The profile detects the running `armenian1` script, queries its known objective text labels and observes mission entities, passenger occupancy and wanted level. Short objective messages are remembered between subtitle appearances. The chase reads the game's loaded vehicle recording as steering geometry; Franklin's car still moves through ordinary physics and inputs. Other driving legs use road AI with precise arrival steering. Scenes and script-disabled driving controls are yielded to GTA.

Success requires a positive change in Franklin's native mission-pass counter, tied to the observed mission session. A failure counter or death stops the attempt. If the script ends without a confirmed result within 30 seconds, the result remains **unconfirmed**. The pilot does not click retry menus, start the next mission or seek gold-medal conditions.

**This is an unverified gameplay prototype.** Shortcuts, jumps, traffic, police escape and Enhanced-version differences can defeat the controller. It tries one short reverse recovery before returning control when blocked. Automated state tests and compilation do not establish an end-to-end win; an actual replay is the next acceptance test. See [profile design and evidence](docs/mission-pilot.md).

A private local diagnostic log is replaced on each pilot start at `%LOCALAPPDATA%\FiveVibe\mission-session.jsonl`. It contains stage transitions, observed objective labels/text, game position, wanted level and result counters. It contains no credentials or screenshots, and is not automatically uploaded. Check it before sharing a bug report.

Other missions retain explicit assistance: approach a recognized marker, then request a particular movement, phone or interaction. FiveVibe does not force-start missions or provide a universal campaign player. The Prologue is outside this first profile.

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
    .\build\AiConnectionCheck.exe .\.env --mission
    .\build\AiConnectionCheck.exe .\.env --camera
    .\build\AiConnectionCheck.exe .\.env --ambient
    .\build\RealWeatherCheck.exe

The last probe queries the public reference city Rome, Italy, independently of player location or saved sync settings. Ambient checks also cover auto-camera ownership, vehicle filters, weather mapping, opt-in requests and stale location results.

Close GTA before updates. The source updater preserves configuration and makes a backup:

    .\scripts\Update-Chat.ps1 -GamePath 'D:\Games\GTAV Enhanced' -WhatIf
    .\scripts\Update-Chat.ps1 -GamePath 'D:\Games\GTAV Enhanced'

It also migrates the earlier GTA AI prototype, preserving its env-file reference and removing the old DLL to prevent duplicate scripts. Use scripts/Uninstall.ps1 -BackupPath '...' to restore the recorded files.

## Roadmap

- More places and community-tested approach points.
- Remember a favourite spot from the current waypoint.
- Short action sequences: arrive, park, get out, take a walk.
- Better stuck detection and activity feedback for background sessions.
- Validate Franklin and Lamar in-game, then add independently tested mission profiles.
- Optional richer NPC interactions.

See [CONTRIBUTING.md](CONTRIBUTING.md), [architecture](docs/architecture.md), and [CHANGELOG.md](CHANGELOG.md).

MIT licensed. FiveVibe is an independent project, not affiliated with Rockstar Games, Take-Two, or OpenAI. GTA and other product names belong to their respective owners.
