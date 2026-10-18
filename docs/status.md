# Project status

Status snapshot for **FiveVibe 0.10.0**, prepared on **18 October 2026**. See [CHANGELOG.md](../CHANGELOG.md) for feature history. This remains an experimental, work-in-progress mod for GTA V Enhanced Story Mode.

## Distribution

- Public source: [AndreaZero/FiveVibe](https://github.com/AndreaZero/FiveVibe), MIT licensed.
- Release entry: [v0.10.0 pre-release](https://github.com/AndreaZero/FiveVibe/releases/tag/v0.10.0). Use the installable `FiveVibe-v0.10.0.zip` and its SHA-256 file, not GitHub's automatic source archives. See the release entry for publication state and assets.
- CI: [Build and checks](https://github.com/AndreaZero/FiveVibe/actions/workflows/build.yml). The workflow builds an artifact; it does not automatically create a GitHub Release. Check the run for the published commit.
- Website: [fivevibe.gta-vi.it](https://fivevibe.gta-vi.it), maintained in a separate repository. Live availability was not confirmed during this review.
- GTA5-Mods: no listing URL is configured in the website checkout. See [publishing.md](publishing.md) before submitting.

The release contains the mod DLL, public configuration, helpers, license and documentation. It excludes the website, private env files, third-party runtimes and game assets. The earlier v0.9.0 archive remains a separate release; new documentation does not retroactively change it.

## Implemented behavior and evidence

| Area | What exists | Validation limit |
| --- | --- | --- |
| Chat and driving | Native chat, local commands, optional OpenAI interpretation, nearby available vehicle acquisition and road-AI driving | Original user confirmed base chat/driving; not every later feature |
| Radio | 19 standard Story Mode station names, off commands and companion buttons; observed station status; audio-only dispatch preserves movement/camera/pilot | Automated logic and simulated browser checks passed; actual in-game audio still pending |
| Destinations | 47 built-in places, waypoints, custom JSON places and LS Customs branch selection | Approach coordinates and native routes need community testing |
| World actions | Walking, NPC approach/follow/greeting attempts, phone/context inputs, protagonist switching and mission-marker assistance | Availability depends on GTA; no generated NPC conversations |
| Companion | Loopback browser panel, places, requests, state and Stop | Background use requires GTA to remain unpaused |
| Cameras | First/third person, native cinema, six-shot director and automatic cinema during AI driving | Visual/native behavior still needs gameplay validation |
| Real-world sync | PC clock and optional Open-Meteo weather selected by city or explicit browser location | Integration checked; native overrides/transitions still need gameplay tests |
| Vehicle categories | Motorcycle, SUV, off-road, sports, supercar, sedan, muscle, van and pickup filters | Entry/driving behavior, especially motorcycles, still needs gameplay tests |
| Mission Pilot | One bounded Franklin and Lamar profile, stage feedback and cancellation | No observed successful autonomous end-to-end playthrough; no campaign solver |

The v0.10.0 validation run passed **406 C# checks** (128 planner, 52 mission, 69 camera, 71 ambient, 58 radio and 28 companion) plus **four dependency-free companion interaction tests**: **410 automated checks total**. Update/backup/migration/rollback checks passed separately. Five live AI interpretation probes passed using synthetic game state, with no commands sent to GTA.

A simulated backend and a real headless Chrome session checked all 19 station buttons, select/off requests, observed highlighting, unchanged route display, pause behavior, mobile layout and absence of JavaScript errors. These are interface checks, not proof that GTA audibly changed station. The radio controller only retunes once and does not unlock or force-enable radios; Self Radio needs the user's own tracks configured in GTA.

AI interprets text into validated actions. GTA natives and controls execute them. Local radio commands and the Mission Pilot control loop do not require continuous model calls. No image, model, texture, voice or NPC-dialogue generation is implemented. These runtime facts do not certify source or promotional-material authorship. The website's labelled AI-generated concept artwork is separate from the mod ZIP.

## Next gameplay acceptance work

1. Install the published ZIP with a recorded Enhanced / Script Hook V / SHVDNE combination. Check chat, a local request, an AI request and F8 takeover.
2. During an active drive, select Radio Mirror Park, select Non-Stop-Pop FM, turn the radio off and select another station. Listen for each change and confirm route, speed, camera and driving continue. Repeat using companion buttons and during Mission Pilot driving.
3. Check on-foot/no-radio rejection, Self Radio with/without configured tracks, manual radio changes, pause/disconnection, scenes and character switching. Confirm no unsolicited radio changes occur on re-entry or a later drive.
4. Test vehicle-category driving, cinema restoration, real-world overrides and background companion use. Capture actual gameplay screenshots/video.
5. Run Franklin and Lamar without assistance and retain its result screen/diagnostic evidence. Keep the unverified label until a full run succeeds.
6. Verify the website and resolve GTA5-Mods eligibility before submission.
