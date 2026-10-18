# Project status

Status snapshot for **FiveVibe 0.14.0**, prepared on **18 October 2026**. See [CHANGELOG.md](../CHANGELOG.md) for feature history. This remains an experimental, work-in-progress mod for GTA V Enhanced Story Mode.

## Distribution

- Public source: [AndreaZero/FiveVibe](https://github.com/AndreaZero/FiveVibe), MIT licensed.
- Release entry: [v0.14.0 pre-release](https://github.com/AndreaZero/FiveVibe/releases/tag/v0.14.0). Use the installable `FiveVibe-v0.14.0.zip` and its SHA-256 file, not GitHub's automatic source archives. See the release entry for publication state and assets.
- CI: [Build and checks](https://github.com/AndreaZero/FiveVibe/actions/workflows/build.yml). The workflow builds an artifact; it does not automatically create a GitHub Release. Check the run for the published commit.
- Website: [fivevibe.gta-vi.it](https://fivevibe.gta-vi.it), maintained in a separate repository. The public domain responded with HTTP 200 on 18 October 2026.
- GTA5-Mods: no listing URL is configured in the website checkout. See [publishing.md](publishing.md) before submitting.

The release contains the mod DLL, public configuration, helpers, license and documentation. It excludes the website, private env files, third-party runtimes and game assets. Earlier archives remain separate releases; new documentation does not retroactively change them.

## Implemented behavior and evidence

| Area | What exists | Validation limit |
| --- | --- | --- |
| Chat and driving | Native chat, local commands, optional OpenAI interpretation, nearby available vehicle acquisition and road-AI driving | Original user confirmed base chat/driving; not every later feature |
| Native menus | Six tabs, bounded World/Camera scrolling, fixed composer/quick requests, full key badges and a shortcut to the existing weather-city command | Actual drawing/input code passed 851 recording-native checks; author captures now show the World menu, chat and full footer labels. Camera-menu and complete interaction testing remain pending |
| Timed drives | 1–120 minutes of cruising, then return near the captured origin; local/AI commands, companion duration field, HUD countdown and observed stages | Clock/command/UI checks passed; full native duration/pause/return journey still unverified |
| Themed drives | Coast and countryside four-stop itineraries, local/AI starts, companion buttons and HUD progress; continuous intermediate legs and confirmed final stop | State/command/UI checks passed; both full native journeys still unverified |
| Favorites | Named current-position saves, local persistence for 50 places, chat returns and companion Drive/Walk buttons; saved entries join native Places/AI catalog | Persistence, dispatch and simulated browser checks passed; actual in-game save/restart/return pending |
| Radio | 19 standard Story Mode station names, off commands and companion buttons; observed station status; audio-only dispatch preserves movement/camera/pilot | Automated logic and simulated browser checks passed; actual in-game audio still pending |
| Destinations | 47 built-in places, waypoints, custom JSON places and LS Customs branch selection | Approach coordinates and native routes need community testing |
| World actions | Walking, NPC approach/follow/greeting attempts, phone/context inputs, protagonist switching and mission-marker assistance | Availability depends on GTA; no generated NPC conversations |
| Companion | Loopback browser panel, places, requests, state and Stop | Background use requires GTA to remain unpaused |
| Driving pace | Normal/rush style and 15–160 km/h target updates; active route/timer preserved | Command/state tests passed; overtaking, lights and actual speed need native playtesting |
| Speedometer | Independent saved HUD toggle; actual current-vehicle speed even in manual driving | Preference/draw/input checks passed; native car/motorcycle pass pending |
| Random mode | Sequential outdoor destinations, bounded strolls and native greeting attempts; stop/replacement/mission/switch cleanup | Pure scheduling and command tests passed; native sequence pending |
| Cameras | Four native views; automatic GTA vehicle cinema; custom director removed | Visual/native behavior still needs gameplay validation |
| Real-world sync | PC clock and optional Open-Meteo weather selected by city or explicit browser location | Integration checked; native overrides/transitions still need gameplay tests |
| Vehicle categories | Motorcycle, SUV, off-road, sports, supercar, sedan, muscle, van and pickup filters | Entry/driving behavior, especially motorcycles, still needs gameplay tests |
| Mission Pilot | One bounded Franklin and Lamar profile, stage feedback and cancellation | No observed successful autonomous end-to-end playthrough; no campaign solver |

The v0.14.0 validation run passed **1,549 checks** (1,529 C# and 20 companion script tests), plus the installer/rollback suite, five synthetic live-AI cases and desktop/mobile browser checks. It covers planner/API, driving styles, random scheduling, HUD preferences, real native-camera controller code against recording stubs, mission logic, ambient controls, radio, favorites, tours, timed drives, companion HTTP and actual overlay drawing/input. Browser interaction tests cover the new companion controls. These checks do not establish native gameplay success; the new behavior still requires a GTA Enhanced playtest.

Overlay tests compile the actual ChatOverlay drawing and input code against recording natives. They check all six tabs, World/Camera wheel/PgUp/PgDn and arrow scrolling, hidden-control click isolation, city command entry, Esc Back/Close and full key labels at four aspect ratios and three safe-zone settings. Generated previews use approximate Windows font metrics, not GTA screenshots. Native visual confirmation of this menu patch remains pending.

The four synthetic live AI timed-drive probes and headless Chrome companion checks recorded for v0.13.0 remain prior integration evidence; this menu patch does not change those paths. Those checks established interface/interpretation behavior, not completed native journeys. Radio, favorite and themed-drive validation remains recorded in the respective releases.

AI interprets text into validated actions. GTA natives and controls execute them. Local radio commands and the Mission Pilot control loop do not require continuous model calls. No image, model, texture, voice or NPC-dialogue generation is implemented. These runtime facts do not certify source or promotional-material authorship. The website now includes author-supplied gameplay captures and explicitly labelled AI-assisted presentation edits, with the unmodified PNGs available for comparison. Its separate interactive interface previews remain labelled concept artwork. These presentation assets are separate from the mod ZIP.

## Next gameplay acceptance work

1. Install the published ZIP with a recorded Enhanced / Script Hook V / SHVDNE combination. Check chat, a local request, an AI request and F8 takeover. Check all six tabs, scroll World/Camera to the bottom, set a weather city, verify Enter/Tab/Esc/takeover key labels and capture actual gameplay screenshots.
2. Run a 1-minute timed drive and the requested 20-minute drive from chat and companion. Check origin capture before acquisition, countdown start, pause/resume, return dispatch, near-origin braking and completion. Repeat radio/camera/speed/favorite saving, F8/Stop in each phase, replacement destination, mission transition and failed/stuck return. Arrival time includes the return journey.
3. Complete both themed drives without assistance: check first-stop approach, each intermediate transition, continuous camera/radio, final braking and completion. Repeat F8/Stop, a new destination, favorite saving, a mission transition and a failed/stuck attempt.
4. Save a named favorite during a drive, confirm the ride continues, restart scripts/game, and return by chat and companion Drive/Walk. Check same-name updates, unknown favorites, the walking-distance limit and an inaccessible saved point.
5. During an active drive, select Radio Mirror Park, select Non-Stop-Pop FM, turn the radio off and select another station. Listen for each change and confirm route, speed, camera and driving continue. Repeat using companion buttons and during Mission Pilot driving.
6. Check on-foot/no-radio rejection, Self Radio with/without configured tracks, manual radio changes, pause/disconnection, scenes and character switching. Confirm no unsolicited radio changes occur on re-entry or a later drive.
7. Test vehicle-category driving, cinema restoration, real-world overrides and background companion use. Capture actual gameplay screenshots/video.
8. Run Franklin and Lamar without assistance and retain its result screen/diagnostic evidence. Keep the unverified label until a full run succeeds.
9. Verify the website and resolve GTA5-Mods eligibility before submission.

## Author-supplied screenshot evidence

On 18 October 2026 the author supplied five captures: native chat near the pool, the scrolled World menu with the weather-city shortcut and complete key labels, a driving view with task HUD, chat showing vehicle acquisition and driving dispatch, and a second HUD view. Originals and the five highlighted presentation versions are in the [screenshot gallery](https://github.com/AndreaZero/FiveVibe/tree/main/docs/screenshots). The hero uses a supplied original as its source; highlighted images are disclosed as AI-assisted edits. These captures support limited visual/interface observations, without establishing successful timed returns, full itineraries or Mission Pilot completion.
