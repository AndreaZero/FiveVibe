# Project status

Status snapshot for **FiveVibe 0.12.0**, prepared on **18 October 2026**. See [CHANGELOG.md](../CHANGELOG.md) for feature history. This remains an experimental, work-in-progress mod for GTA V Enhanced Story Mode.

## Distribution

- Public source: [AndreaZero/FiveVibe](https://github.com/AndreaZero/FiveVibe), MIT licensed.
- Release entry: [v0.12.0 pre-release](https://github.com/AndreaZero/FiveVibe/releases/tag/v0.12.0). Use the installable `FiveVibe-v0.12.0.zip` and its SHA-256 file, not GitHub's automatic source archives. See the release entry for publication state and assets.
- CI: [Build and checks](https://github.com/AndreaZero/FiveVibe/actions/workflows/build.yml). The workflow builds an artifact; it does not automatically create a GitHub Release. Check the run for the published commit.
- Website: [fivevibe.gta-vi.it](https://fivevibe.gta-vi.it), maintained in a separate repository. Live availability was not confirmed during this review.
- GTA5-Mods: no listing URL is configured in the website checkout. See [publishing.md](publishing.md) before submitting.

The release contains the mod DLL, public configuration, helpers, license and documentation. It excludes the website, private env files, third-party runtimes and game assets. Earlier archives remain separate releases; new documentation does not retroactively change them.

## Implemented behavior and evidence

| Area | What exists | Validation limit |
| --- | --- | --- |
| Chat and driving | Native chat, local commands, optional OpenAI interpretation, nearby available vehicle acquisition and road-AI driving | Original user confirmed base chat/driving; not every later feature |
| Themed drives | Coast and countryside four-stop itineraries, local/AI starts, companion buttons and HUD progress; continuous intermediate legs and confirmed final stop | State/command/UI checks passed; both full native journeys still unverified |
| Favorites | Named current-position saves, local persistence for 50 places, chat returns and companion Drive/Walk buttons; saved entries join native Places/AI catalog | Persistence, dispatch and simulated browser checks passed; actual in-game save/restart/return pending |
| Radio | 19 standard Story Mode station names, off commands and companion buttons; observed station status; audio-only dispatch preserves movement/camera/pilot | Automated logic and simulated browser checks passed; actual in-game audio still pending |
| Destinations | 47 built-in places, waypoints, custom JSON places and LS Customs branch selection | Approach coordinates and native routes need community testing |
| World actions | Walking, NPC approach/follow/greeting attempts, phone/context inputs, protagonist switching and mission-marker assistance | Availability depends on GTA; no generated NPC conversations |
| Companion | Loopback browser panel, places, requests, state and Stop | Background use requires GTA to remain unpaused |
| Cameras | First/third person, native cinema, six-shot director and automatic cinema during AI driving | Visual/native behavior still needs gameplay validation |
| Real-world sync | PC clock and optional Open-Meteo weather selected by city or explicit browser location | Integration checked; native overrides/transitions still need gameplay tests |
| Vehicle categories | Motorcycle, SUV, off-road, sports, supercar, sedan, muscle, van and pickup filters | Entry/driving behavior, especially motorcycles, still needs gameplay tests |
| Mission Pilot | One bounded Franklin and Lamar profile, stage feedback and cancellation | No observed successful autonomous end-to-end playthrough; no campaign solver |

The v0.12.0 validation run passed **568 C# checks** (128 planner, 52 mission, 69 camera, 71 ambient, 58 radio, 65 favorites, 78 tours and 47 companion) plus **11 dependency-free companion interaction tests**: **579 automated checks total**. Update/backup/migration/rollback checks passed separately. Four live themed-drive interpretation probes passed with synthetic game state, without sending commands to GTA.

A simulated backend and a real headless Chrome session checked both tour buttons, waiting for observed progress/completion, radio/favorite requests during an active tour, pause behavior, mobile layout and absence of JavaScript errors. These are interface checks, not evidence of a completed native journey. Existing radio and favorite validation remains recorded in their respective releases.

AI interprets text into validated actions. GTA natives and controls execute them. Local radio commands and the Mission Pilot control loop do not require continuous model calls. No image, model, texture, voice or NPC-dialogue generation is implemented. These runtime facts do not certify source or promotional-material authorship. The website's labelled AI-generated concept artwork is separate from the mod ZIP.

## Next gameplay acceptance work

1. Install the published ZIP with a recorded Enhanced / Script Hook V / SHVDNE combination. Check chat, a local request, an AI request and F8 takeover.
2. Complete both themed drives without assistance: check first-stop approach, each intermediate transition, continuous camera/radio, final braking and completion. Repeat F8/Stop, a new destination, favorite saving, a mission transition and a failed/stuck attempt.
3. Save a named favorite during a drive, confirm the ride continues, restart scripts/game, and return by chat and companion Drive/Walk. Check same-name updates, unknown favorites, the walking-distance limit and an inaccessible saved point.
4. During an active drive, select Radio Mirror Park, select Non-Stop-Pop FM, turn the radio off and select another station. Listen for each change and confirm route, speed, camera and driving continue. Repeat using companion buttons and during Mission Pilot driving.
5. Check on-foot/no-radio rejection, Self Radio with/without configured tracks, manual radio changes, pause/disconnection, scenes and character switching. Confirm no unsolicited radio changes occur on re-entry or a later drive.
6. Test vehicle-category driving, cinema restoration, real-world overrides and background companion use. Capture actual gameplay screenshots/video.
7. Run Franklin and Lamar without assistance and retain its result screen/diagnostic evidence. Keep the unverified label until a full run succeeds.
8. Verify the website and resolve GTA5-Mods eligibility before submission.
