# Changelog

## 0.11.0 — Favourite places

- Save the protagonist's current game position under a named favorite from English/Italian chat or the companion. Save/list commands preserve active movement, camera, Mission Pilot and pending plans.
- Return by chat or companion Drive/Walk buttons using normal destination tasks, with the existing walking-distance limit. Explicit favorite names resolve built-in-name collisions; returning replaces current movement.
- Persist up to 50 named places per Windows user with stable generated IDs, atomic writes, validated coordinates/names and protection against invalid files or external edits. Reusing a name updates its position.
- Add favorites to native Places and the validated AI destination catalog, without accepting model/browser coordinates. Reserve the fav_ ID namespace for private saved places.
- Add authenticated structured companion save/go requests, observed list updates and pause/disconnection/read-error states. Consume bounded rejected-origin POST bodies before replying to avoid connection resets.
- Add persistence, parsing, catalog, HTTP and companion interaction regressions and an optional synthetic live AI probe. Actual in-game save/restart/return validation remains pending.

## 0.10.0 — Radio for the ride

- Select 19 standard Story Mode radio stations by name in English/Italian chat, or turn the vehicle radio off. Named commands bypass OpenAI in both provider modes.
- Add companion station buttons, Radio off, observed station highlighting and disabled states for pause, disconnection, on-foot play and unavailable radios.
- Dispatch radio actions before movement cancellation. Preserve driving, route, speed, camera, Mission Pilot and pending plans; distinguish Stop radio from takeover.
- Validate station IDs in local/AI plans; reject unknown, negated and combined local radio requests without silently changing stations. Keep native radio failures inside the audio boundary.
- Retune only the current vehicle once, without unlocking stations, forcing radios, persistent overrides or bundled music. Self Radio uses the player's GTA configuration.
- Add radio command/execution/schema tests, authenticated companion queue regressions, dependency-free companion interaction tests and an optional synthetic live AI probe. Native in-game audio validation remains pending.

## 0.9.0 — Automatic cinema and real-world vibes

- Enable the cinema director automatically during AI driving, including Mission Pilot, and restore gameplay on drive completion or F8/Stop. Respect manual camera choices for the current drive.
- Add PC clock synchronization and optional real weather through Open-Meteo. Choose city/country or explicitly grant browser location; keep rounded coordinates and preferences locally.
- Add real-world controls to the native World tab and companion, independent time/weather switches, mission suspension, manual-setting precedence and cached periodic refreshes.
- Add Los Santos Customs destinations for Hawick/Burton, La Mesa, the airport and Route 68. Resolve generic requests against available Customs markers and prevent confusion with Simeon's dealership.
- Search nearby available motorcycles, SUVs, off-road vehicles/jeeps, sports cars, supercars, sedans, muscle cars, vans and pickups. A standalone vehicle request enters it and waits; driving requests preserve the selected type.
- Allow enough approach time for vehicles farther away in the search radius; report unavailable types without substitution or spawning.
- Add automatic-camera, location/privacy, weather and vehicle regressions, companion location endpoint checks and optional live AI/weather probes. Native gameplay validation remains pending.

## 0.8.0 — Spectator cameras

- Add native first-person/cockpit and third-person views, the original GTA cinematic camera and a FiveVibe cinema director.
- Cycle six director shots with slow drift, damped tracking, field-of-view adaptation, obstacle probes and optional letterbox.
- Add F10 camera cycling, Shift+F10 next shot, a Camera tab, companion controls and English/Italian requests.
- Preserve active drives, walks and Mission Pilot when changing the view; F8/Stop restores gameplay.
- Yield during story scenes, fades, pause, death, protagonist switches and external script cameras. Clean up only owned cameras on release/unload.
- Add framing, obstruction, timing and command regressions. Visual behavior remains unverified in-game.

## 0.7.0 — Experimental Franklin and Lamar Mission Pilot

- Add a bounded full-flow attempt for the first driving mission, selected through English/Italian chat or the Ideas menu.
- Read known native objective labels, mission entities, passenger occupancy and wanted state; preserve the goal between subtitles and across cutscenes.
- Follow loaded mission-route geometry with physical steering inputs; use road AI and precise arrival steering for dealership/home legs.
- Wait for Lamar, yield to scripted driving controls and confirm native pass/fail counter changes, including replay-controller support in the researched scripts.
- Add a bounded stuck recovery, F8 cancellation, explicit unconfirmed results and private local diagnostics.
- Show Mission Pilot state in the companion; document manual replay start and unsupported missions.
- Add mission-flow and steering regressions. Full autonomous completion remains unverified in-game.

## 0.6.1 — Reliable character-switch confirmation

- Remove the false rejection after five seconds during the character wheel / loading stage.
- Confirm the actual requested protagonist and returned control after a stable 750 ms interval, with no active switch, fade or cutscene.
- Wait up to two minutes for an unconfirmed switch; report an unconfirmed timeout rather than claiming rejection. Follow-up actions only run after confirmation.
- Add regression cases for delayed transitions, transient flags, cancellation and late success.
- Explain mission-marker navigation and explicit assistance versus autonomous story progression; clarify the in-game mission status reply.

## 0.6.0 — Chat, places and world controls

- Fix native input and text measurement truncating multi-component text around 32 characters.
- Keep 420-character requests readable with three visible lines and input scrolling.
- Add Chat / Places / Ideas / World / HUD tabs with paged destinations and request suggestions.
- Add free-roam weather, custom time, clock freeze/resume and combined ambience requests.
- Add persistent task HUD visibility, F9, and matching browser controls.
- Release owned weather/clock overrides for story transitions and script unload.

## 0.5.0 — FiveVibe

- Rebrand the project, native HUD and replies in English.
- Expand travel to 42 named locations with English/Italian aliases and custom JSON additions.
- Add a local browser companion with shared conversation, searchable places, live status and Stop.
- Add a background-play settings helper with backup.
- Add legacy migration, public-file checks, contributor documentation and MIT licensing.

New gameplay actions remain experimental until tested on the user's exact game/runtime versions.

## Earlier local prototype

The GTA AI prototype introduced native chat and driving (0.1), native HUD styling (0.2), OpenAI planning from a private env file (0.3), and walking/NPC/phone/character-switch/mission-marker assistance (0.4).
