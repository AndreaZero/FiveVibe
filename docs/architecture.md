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
- SpectatorCamera owns view overrides and its single scripted director camera. CameraDirectorLogic provides pure framing, timing, automatic-driving ownership and English/Italian command parsing. Camera actions execute before movement dispatch and do not replace active tasks.
- ChatOverlay and HudLayout render the native HUD.
- PlaceCatalog provides 47 destinations and validated custom additions. Generic Customs requests use available shop markers; explicit branch aliases constrain AI output independently of model interpretation.
- VehicleSelection holds validated categories, native-class filters and bounded approach timeouts. The game thread searches actual available nearby vehicles; a missing category cannot silently fall back to a car.
- RealWorldService stores per-user opt-in sync settings and asynchronously fetches city lookup/current weather from fixed Open-Meteo hosts. It performs no GTA calls; the game thread maps fresh readings and PC time to native overrides.
- OpenAiPlanner calls Responses API with one strict function schema; LocalPlanner provides offline English/Italian commands.
- RadioCatalog and RadioCommands validate 19 standard Story Mode station IDs and English/Italian name/off requests. RadioControl has an audio-only IVehicleRadio boundary; VehicleRadio reads the current vehicle and invokes native retuning on the game thread.
- CompanionServer serves an embedded page on loopback and queues text/Stop/location commands. It never reads game state directly. Structured location commands bypass AI and ordinary conversation history.
- AiConfiguration parses env values without evaluating shell commands or exposing source lines on errors.

Every plan is validated again before execution. Locations are catalog IDs, not model-generated coordinates. F8/Stop increments a generation counter so a late HTTP response cannot regain control. External character changes also invalidate pending actions.

The native switch uses GTA's ordinary protagonist-selection controls and verifies the resulting character. The mod does not swap ped models, edit story globals, or unlock characters. Phone and contextual interactions use ordinary game inputs. Native tasks remain subject to GTA's own game state and pathfinding.

The companion page has no external assets or dependencies. Its HTML/CSS/JS is embedded in FiveVibe.dll at compile time. The private discovery file lives in the user's local application data directory, outside the source checkout.

Automated tests use pure logic and loopback HTTP. Actual native game behavior requires Story Mode testing.

Named radio requests are dispatched before CancelPlanning in Submit; both local and AI radio plans return from Execute before Mission Pilot cancellation or movement dispatch. Neither the audio controller nor its native adapter receives a driving task, camera or route controller. Radio-native exceptions are caught within that boundary, avoiding the general action-stop handler. Station selection issues SET_VEH_RADIO_STATION and SET_RADIO_TO_STATION_NAME once, using OFF for shutdown, without disabling the vehicle's radio component or forcing a station every frame. The companion snapshot reads GET_PLAYER_RADIO_STATION_NAME; queued requests do not optimistically replace the displayed selection. On-foot, blocked and radio-less contexts cannot retune. Online-only stations are not unlocked.

Native identifier reference: [SHVDN Game.RadioStation](https://github.com/scripthookvdotnet/scripthookvdotnet/blob/main/source/scripting_v3/GTA/Game.cs). Native interfaces: [vehicle station](https://github.com/citizenfx/natives/blob/master/AUDIO/SetVehRadioStation.md), [player station](https://github.com/citizenfx/natives/blob/master/AUDIO/SetRadioToStationName.md), [radio availability](https://github.com/citizenfx/natives/blob/master/AUDIO/DoesPlayerVehHaveRadio.md) and [observed station](https://github.com/citizenfx/natives/blob/master/AUDIO/GetPlayerRadioStationName.md).

The camera director follows an active AI-controlled subject with six scheduled shots. A synchronous cut check and bounded asynchronous swept-sphere probes constrain placement; stale probe results are ignored after transitions. A subject bounding-box check rejects shots inside the car/person. Camera smoothing compensates for subject translation so it does not trail a fast vehicle by several metres. Saved follow views are restored on F8/Stop or unload. The implementation does not change time scale, weather, actor tasks, entity coordinates or rendering cameras owned by other scripts.

Automatic cinema observes actual driving rather than vehicle acquisition. Its policy remembers whether it owns the selected view; a manual camera choice suppresses automatic changes for that drive. F8 resets the policy and restores saved native follow views. Native camera failures release the override and suppress repeated automatic activation for the current drive.

Real-world weather requests require an enabled switch and a configured location. Browser geolocation is a single user-triggered request; both browser and backend round coordinates. Settings and current readings stay separate from the OpenAI context. Location changes cancel older requests, and a late result cannot overwrite a newer choice. Successful readings refresh after 15 minutes, failures retry after two minutes, and readings expire after 90 minutes. Missions, scenes, switching and pause release applied real-world overrides without disabling the saved preference. Explicit manual weather/time requests disable the corresponding sync component.

Mission Pilot is selected as one validated `mission_auto` action. Explicit start phrases are also handled locally in OpenAI mode. Its continuous loop makes no API calls: the model interprets the request, while known native objective labels and game observations select each stage. Ordinary world/drive tasks are released when the pilot starts; new movement requests and F8 disarm it. Cutscenes suspend the pilot rather than disarming it. `mission_status`, chat replies and HUD toggles do not interrupt it.

The mission layer does not read/write script locals or story globals. It reads per-actor pass/failure stats with successful-baseline checks and waits briefly after mission-thread exit. A missing stat or ended script cannot produce a successful result. The profile's source evidence and gameplay limits are documented separately in [mission-pilot.md](mission-pilot.md).
