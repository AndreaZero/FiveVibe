# Publication guide and description draft

Prepared on **18 October 2026** for FiveVibe **0.11.0**. See [status.md](status.md) for the evidence behind the claims below.

## GTA5-Mods eligibility first

The [official upload guidelines](https://github.com/5mods/tutorials/blob/master/5Mods%20Upload%20Guidelines.md), checked on 18 October 2026, include an update dated **18 May 2026** disallowing AI-generated descriptions, images and scripts in new uploads. Consequently, the draft below is **AI-assisted editorial material for GitHub/the website, not a compliant ready-to-paste GTA5-Mods submission** under that rule.

FiveVibe's use of AI during play is a separate fact: it interprets a player's text request into a validated action, and ordinary GTA tasks/controls move the character. The mod download contains no generated game art, models, textures, voices or NPC dialogue. The website's AI-generated concept artwork is separate and must not be represented as gameplay. Runtime behavior alone cannot establish the authorship of code or marketing copy, or guarantee moderation approval.

Before uploading, ask the moderators whether a text-command AI companion is eligible and explain the actual provenance of the materials you intend to submit. No contact has been made as part of this review. MIT licensing and a work-in-progress label do not establish platform eligibility.

If eligibility is confirmed, follow the site's current upload form and rules. Its guidelines require an English description, installation/dependency information and credits, and real in-game screenshots for a mod like this. Do not bundle separately updated dependencies. The upload form was not accessible during this review, so field limits and exact UI labels are not asserted here.

## Release preparation

The existing [v0.11.0 pre-release](https://github.com/AndreaZero/FiveVibe/releases/tag/v0.11.0) already has the installable ZIP and checksum. Users need **FiveVibe-v0.11.0.zip**, not GitHub's automatic “Source code” archives. The public pre-release is not a stable release.

Before advertising it broadly, complete the gameplay checks in [status.md](status.md) and record the exact versions tested. Do not advertise Legacy, FiveM, GTA Online or general autonomous mission completion as supported.

For a future release, on Windows:

```powershell
.\scripts\Get-Dependencies.ps1
.\scripts\Build.ps1
.\tests\Test-ChatUpdate.ps1
.\scripts\Test-PublicFiles.ps1
```

These are future release instructions; they were not executed during this documentation-only review. The public-file script checks Git-tracked files, so it does not substitute for checking new/untracked files and the final archive.

Package **only the contents of `dist/FiveVibe/`**, preserving the `scripts/` directory, into a versioned ZIP, then calculate its SHA-256. Do not zip the repository root, the entire `dist/` directory, `build/`, `vendor/`, local logs, backups, compatibility records or private env files. An older `dist/GtaAi/` prototype exists locally and is not part of FiveVibe's release. Default distributed configuration must remain `Provider=Local`, with no credentials or personal paths. Preserve a user's private INI/env configuration during upgrades.

For the existing v0.11.0 release, prefer the already published archive rather than repackaging it under the same version. New documentation in the checkout does not alter that archive. A future build copies the Markdown files under `docs/` into its distribution automatically.

## Listing fields to prepare

These are suggested editorial values, not a claim that the submission has been approved:

| Field | Value |
| --- | --- |
| Title | FiveVibe - Text-Controlled AI Companion [Enhanced / WIP] |
| Version | 0.11.0, experimental pre-release |
| Author | AndreaZero |
| Category | Scripts |
| Platform | GTA V Enhanced, PC, Story Mode |
| Source/license | [GitHub](https://github.com/AndreaZero/FiveVibe), MIT |
| Website | [fivevibe.gta-vi.it](https://fivevibe.gta-vi.it) — verify live availability first |
| Download | [v0.11.0 pre-release](https://github.com/AndreaZero/FiveVibe/releases/tag/v0.11.0) |
| Support | [GitHub Issues](https://github.com/AndreaZero/FiveVibe/issues) |

Capture actual gameplay showing the F7 chat, a drive with the active task, the director camera and a companion view alongside the game. A short video should demonstrate one request, the resulting movement and F8 takeover. Use only features you have actually demonstrated; do not present the website's concept scenes as mod screenshots.

## English description draft

Editorial draft for GitHub/the website. The GTA5-Mods restriction above applies to this AI-assisted text. Feature availability is not proof that every feature has passed gameplay testing.

```html
<b>FiveVibe — Set the vibe. Let it ride.</b>
<br><br>
FiveVibe is a free, open-source text-controlled companion for GTA V Enhanced Story Mode. Ask your character to take a relaxed drive, visit a place or go for a walk, then watch Los Santos unfold. Use the native in-game chat or a local browser companion.
<br><br>
<b>Work in progress — experimental v0.11.0</b>
<br>
This is an early pre-release. Base chat and free-roam driving have been confirmed by the original user. Favorite save/return behavior, vehicle radio controls, new camera behavior, real-world overrides and vehicle-category behavior still need in-game validation. Expect bugs and version-dependent behavior. Mission Pilot has not yet completed an observed autonomous end-to-end run.
<br><br>
<b>What “AI” means here</b>
<br>
AI interprets your text request into a validated game action; GTA's native tasks and controls move the character. The mod does not generate images, models, textures, voices or NPC dialogue, and the download contains no AI-generated game assets. Basic supported commands also work locally without AI or an API key. Optional OpenAI interpretation requires your own API key and separately billed API usage.
<br><br>
<b>Features</b>
<br>
- Text requests in English or Italian; interface and replies in English.<br>
- Native chat and a local browser companion with shared status and Stop.<br>
- Free-roam cruising, waypoint travel and 47 built-in destinations, plus custom JSON places.<br>
- Save the current game position as a named favorite and return through chat or companion Drive/Walk buttons. Up to 50 places are stored locally; saving preserves the ride, while returning starts a new route.<br>
- Nearby available vehicle selection, including motorcycles, SUVs and off-road vehicles. No vehicle spawning or forced NPC ejection.<br>
- Walking, strolling and basic NPC approach/follow/greeting attempts.<br>
- First-person, third-person, GTA cinematic camera and a six-shot director, with optional automatic cinema during AI driving.<br>
- Manual world controls, PC clock sync and optional approximate real weather through Open-Meteo.<br>
- Vehicle radio station selection by name, Radio off and companion buttons, preserving the active ride.<br>
- Immediate takeover with F8 or the companion's Stop button.<br>
- Experimental Mission Pilot for Franklin and Lamar only.
<br><br>
<b>Requirements</b>
<br>
Windows, a legitimate GTA V Enhanced installation, .NET Framework 4.8, a Script Hook V/ASI loader compatible with your game build, and ScriptHookVDotNet Enhanced API 3. The project currently targets SHVDNE v1.1.0.6. Install dependencies separately; they are not included.
<br>
Script Hook V by Alexander Blade: <a href="https://www.dev-c.enbdev.com/gtav/scripthookv/">official download</a><br>
ScriptHookVDotNet Enhanced by Chiheb-Bacha and contributors: <a href="https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced/releases/tag/v1.1.0.6">targeted release</a>
<br><br>
<b>Installation and first use</b>
<br>
1. Close GTA and install the compatible Script Hook V/ASI loader and SHVDNE following their instructions.<br>
2. Extract FiveVibe-v0.11.0.zip. Copy FiveVibe.dll and FiveVibe.ini from its scripts folder into your game's scripts folder. Keep your existing private configuration when updating.<br>
3. Start Story Mode and press F7. Local commands work immediately without an API key.<br>
4. Try “Drive to Paleto Bay”, “Walk to the waypoint” or “Find an empty car and cruise”. Press F8 to cancel and take over.<br>
5. For the optional browser panel, run Open-Companion.ps1 from the extracted release folder. Its Radio section selects stations or turns the radio off without stopping the ride.<br>
6. For optional OpenAI interpretation, follow the linked README to create a private env file and set Provider=OpenAI and EnvFile in FiveVibe.ini. Never publish your key.<br>
7. To keep playing while using another window, turn Pause Game on Focus Loss off in GTA. The game must remain running and unpaused.
<br><br>
<b>Controls</b>
<br>
F7: chat. Enter: send. Esc: close. F8: Stop/take over. F9: task HUD. F10: cycle camera. Shift+F10: next director shot.
<br><br>
<b>Known limitations</b>
<br>
Driving and walking depend on GTA pathfinding, traffic and accessible routes. Most requests perform one action. Character switching depends on the save and current story restrictions. This is not a general mission solver: Franklin and Lamar is an unverified prototype, not a promise of autonomous campaign completion. NPC dialogue generation, contact calling, video recording/export, Legacy and FiveM support are not implemented or claimed. Use in Story Mode only.
<br><br>
<b>Open source, links and feedback</b>
<br>
FiveVibe is free and MIT licensed. Source code, issues, contributions and the roadmap are on GitHub. Bug reports should include your game build, Script Hook V/SHVDNE versions, exact request and observed behavior, with credentials removed.<br>
Source and installation: <a href="https://github.com/AndreaZero/FiveVibe">GitHub</a><br>
Download: <a href="https://github.com/AndreaZero/FiveVibe/releases/tag/v0.11.0">v0.11.0 pre-release</a><br>
Website: <a href="https://fivevibe.gta-vi.it">FiveVibe website</a><br>
Support: <a href="https://github.com/AndreaZero/FiveVibe/issues">GitHub Issues</a><br>
Data and privacy: <a href="https://github.com/AndreaZero/FiveVibe/blob/main/SECURITY.md">SECURITY.md</a><br>
Weather provider: <a href="https://open-meteo.com/">Open-Meteo</a><br>
Thanks to the Script Hook V and ScriptHookVDotNet Enhanced authors and contributors. FiveVibe is an independent project, not affiliated with Rockstar Games, Take-Two or OpenAI.
```

## Practical publication sequence

1. Complete a clean-install gameplay check and record honest compatibility results.
2. Verify the website's production domain and download links. For pre-releases, use the explicit version URL; GitHub's `/releases/latest` may select a different stable release.
3. Use GitHub Releases and the project website as the existing distribution/reference points. Preserve the experimental label and list limitations beside features.
4. Resolve GTA5-Mods eligibility before submission. If eligible, use a human-authored description and eligible materials, then upload the installable ZIP under Scripts with real gameplay screenshots, dependency links and credits. Review the live form and await its moderation outcome; no approval time is promised.
5. Only after a listing exists, use its actual URL in the site's `GTA5_MODS_URL` build setting and rebuild/redeploy the website. This is a future action: the setting and website were not changed during this review.
