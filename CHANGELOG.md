# Changelog

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
