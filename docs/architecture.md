# Architecture

FiveVibe is a .NET Framework / SHVDNE API 3 script for GTA V Enhanced Story Mode.

    In-game chat ─────────────┐
                             ├─> request planner ─> validated CommandPlan ─> script Tick ─> GTA natives
    Local browser ─> queue ──┘                              ↑
                    ↑                          places + custom locations
               published snapshot

- FiveVibeScript owns game-thread scheduling, driving, configuration and request cancellation.
- WorldActions owns walking, NPC approach/follow/greeting, phone/context input and real story character switching.
- MissionPilot reads native mission observations and executes the bounded Franklin and Lamar profile on the game thread. MissionFlow holds pure stage decisions and steering logic; it is independent of GTA for regression testing.
- ChatOverlay and HudLayout render the native HUD.
- PlaceCatalog provides 42 outdoor approach points and validated custom additions.
- OpenAiPlanner calls Responses API with one strict function schema; LocalPlanner provides offline English/Italian commands.
- CompanionServer serves an embedded page on loopback and queues text/Stop commands. It never reads game state directly.
- AiConfiguration parses env values without evaluating shell commands or exposing source lines on errors.

Every plan is validated again before execution. Locations are catalog IDs, not model-generated coordinates. F8/Stop increments a generation counter so a late HTTP response cannot regain control. External character changes also invalidate pending actions.

The native switch uses GTA's ordinary protagonist-selection controls and verifies the resulting character. The mod does not swap ped models, edit story globals, or unlock characters. Phone and contextual interactions use ordinary game inputs. Native tasks remain subject to GTA's own game state and pathfinding.

The companion page has no external assets or dependencies. Its HTML/CSS/JS is embedded in FiveVibe.dll at compile time. The private discovery file lives in the user's local application data directory, outside the source checkout.

Automated tests use pure logic and loopback HTTP. Actual native game behavior requires Story Mode testing.

Mission Pilot is selected as one validated `mission_auto` action. Explicit start phrases are also handled locally in OpenAI mode. Its continuous loop makes no API calls: the model interprets the request, while known native objective labels and game observations select each stage. Ordinary world/drive tasks are released when the pilot starts; new movement requests and F8 disarm it. Cutscenes suspend the pilot rather than disarming it. `mission_status`, chat replies and HUD toggles do not interrupt it.

The mission layer does not read/write script locals or story globals. It reads per-actor pass/failure stats with successful-baseline checks and waits briefly after mission-thread exit. A missing stat or ended script cannot produce a successful result. The profile's source evidence and gameplay limits are documented separately in [mission-pilot.md](mission-pilot.md).
