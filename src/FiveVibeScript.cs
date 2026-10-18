using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.Chrono;

namespace FiveVibe
{
    public sealed class FiveVibeScript : Script
    {
        private readonly ChatOverlay chat = new ChatOverlay();
        private readonly RequestGate gate = new RequestGate();
        private readonly Keys openKey;
        private readonly Keys takeoverKey;
        private readonly float searchRadius;
        private OpenAiPlanner planner;
        private readonly bool useOpenAi;
        private readonly string envFilePath, defaultAiModel;
        private AiConfiguration activeAiConfiguration;
        private CancellationTokenSource cancellation;
        private Task<CommandPlan> planning;
        private int requestId;
        private int requestPed;
        private Vehicle vehicle;
        private Ped driver;
        private bool wasPersistent;
        private bool claimedCar;
        private CommandPlan drivingPlan;
        private string phase = "idle";
        private int phaseStarted;
        private int lastMoveTime;
        private Vector3 lastPosition;
        private Vector3 destination;
        private int speedKmh;
        private bool hasDestination;
        private string brakingMessage;
        private readonly WorldActions worldActions;
        private bool missionWasActive;
        private string destinationLabel = "Waypoint";
        private CompanionServer companion;
        private long lastCompanionUpdate;
        private readonly string preferencesPath;
        private readonly Keys hudKey;
        private bool ownsClockPause, ownsWeather;

        public FiveVibeScript()
        {
            worldActions = new WorldActions(message => chat.Add(false, message), () => chat.IsOpen = false);
            missionWasActive = Function.Call<bool>(Hash.GET_MISSION_FLAG);
            var settings = ScriptSettings.Load(Path.Combine(BaseDirectory, "FiveVibe.ini"));
            try { PlaceCatalog.LoadCustom(Path.Combine(BaseDirectory, "FiveVibe.locations.json")); }
            catch (Exception) { chat.Add(false, "Custom locations could not be loaded. Check FiveVibe.locations.json; built-in places remain available."); }
            openKey = settings.GetValue("Chat", "OpenKey", Keys.F7);
            takeoverKey = settings.GetValue("Chat", "TakeOverKey", Keys.F8);
            if (takeoverKey == openKey) takeoverKey = Keys.F8 == openKey ? Keys.F9 : Keys.F8;
            speedKmh = Math.Max(15, Math.Min(90, settings.GetValue("Driving", "CruiseSpeedKmh", 45)));
            searchRadius = Math.Max(20, Math.Min(150, settings.GetValue("Driving", "SearchRadiusMeters", 100)));
            preferencesPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiveVibe","preferences.json");
            bool hudDefault=settings.GetValue("HUD","ShowTaskHud",settings.GetValue("HUD", "CompactDrivingHud", true));
            try { chat.ShowDrivingHud=HudPreferences.Load(preferencesPath,hudDefault); }
            catch(Exception) { chat.ShowDrivingHud=hudDefault; }
            hudKey=settings.GetValue("HUD","ToggleKey",Keys.F9);
            if(hudKey==openKey || hudKey==takeoverKey) hudKey=Keys.F10;
            chat.HudToggleKeyName=hudKey.ToString();
            chat.InterfaceSounds = settings.GetValue("HUD", "InterfaceSounds", true);
            string provider = settings.GetValue("AI", "Provider", "Local");
            useOpenAi = provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase);
            string envSetting = settings.GetValue("AI", "EnvFile", "FiveVibe.env");
            envFilePath = Path.IsPathRooted(envSetting) ? envSetting : Path.Combine(BaseDirectory, envSetting);
            defaultAiModel = settings.GetValue("AI", "Model", "gpt-4.1-mini");
            if (useOpenAi) RefreshOpenAiConfiguration(false);
            if (settings.GetValue("Companion", "Enabled", true))
            {
                try { companion = new CompanionServer(settings.GetValue("Companion", "Port", 6791)); }
                catch (Exception) { chat.Add(false, "The browser companion could not start. Check its port is available; in-game chat still works."); }
            }
            chat.Add(false, "FiveVibe ready. " + openKey + " opens chat. Write a request or choose a suggestion. " + takeoverKey + " gives you control immediately.");
            Tick += OnTick;
            KeyDown += OnKey;
            Aborted += delegate { CancelPlanning(); worldActions.Cancel(); ReleaseControl(); ResetWorldOverrides(); chat.Dispose(); if (planner != null) planner.Dispose(); if (companion != null) companion.Dispose(); };
            Interval = 0;
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == takeoverKey) { chat.IsOpen = false; Stop("You have control."); return; }
                if (e.KeyCode == hudKey) { SetTaskHud(!chat.ShowDrivingHud); return; }
                if (Game.IsPaused || Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE)) return;
                if (e.KeyCode == Keys.F4 || e.KeyCode == Keys.F6) { chat.IsOpen = false; return; }
                if (e.KeyCode == openKey) { chat.IsOpen = !chat.IsOpen; return; }
                if (!chat.IsOpen) return;
                string prompt = chat.Key(e);
                if (prompt != null) Submit(prompt);
            }
            catch (Exception error) { Fail(error); }
        }

        private void Submit(string prompt)
        {
            chat.Add(true, prompt);
            if (LocalPlanner.IsImmediateStop(prompt)) { BrakeOrRelease(); return; }
            if (Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE)) { chat.Add(false, "FiveVibe runs in Story Mode."); return; }
            CancelPlanning();
            if (!useOpenAi) { Execute(LocalPlanner.Parse(prompt, speedKmh)); return; }
            if (!RefreshOpenAiConfiguration(true)) return;
            requestId = gate.Begin();
            requestPed = Game.Player.Character.Handle;
            cancellation = new CancellationTokenSource();
            var ped = Game.Player.Character;
            string context = string.Format(CultureInfo.InvariantCulture,
                "character={0}; driving={1}; target_speed_kmh={2}; in_vehicle={3}; position=({4:0},{5:0},{6:0})",
                ActorName(ped), phase, speedKmh, ped.IsInVehicle(), ped.Position.X, ped.Position.Y, ped.Position.Z);
            context += worldActions.Context();
            context+="; task_hud_visible="+chat.ShowDrivingHud+"; clock_paused="+GameClock.IsPaused+"; game_time="+ClockLabel()+"; weather="+World.Weather;
            string[] history = chat.Messages.Skip(Math.Max(0, chat.Messages.Count - 10)).Select(m => (m.User ? "utente: " : "mod: ") + m.Text).ToArray();
            planning = planner.PlanAsync(prompt, context, history, cancellation.Token);
            chat.Thinking = true;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                UpdateCompanion();
                if (Game.IsPaused) return;
                bool missionActive = Function.Call<bool>(Hash.GET_MISSION_FLAG);
                if(missionActive && !missionWasActive && (ownsClockPause || ownsWeather))
                { ResetWorldOverrides(); chat.Add(false,"World overrides released for the mission."); }
                if (missionActive && !missionWasActive && (phase != "idle" || worldActions.Active || planning != null))
                {
                    CancelPlanning();
                    worldActions.Cancel(false);
                    ReleaseControl(false);
                    chat.Add(false, "A mission has started. I can assist on request when the game returns control.");
                }
                missionWasActive = missionActive;
                bool blocked = Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE) || Game.Player.Character.IsDead ||
                    (!Game.Player.CanControlCharacter && !worldActions.Switching) ||
                    ((Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) || Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS)) && !worldActions.Switching);
                if (blocked)
                {
                    CancelPlanning();
                    worldActions.Cancel(false);
                    ReleaseControl(false);
                    chat.IsOpen = false;
                    return;
                }
                if (planning != null && planning.IsCompleted)
                {
                    Task<CommandPlan> finished = planning;
                    planning = null;
                    chat.Thinking = false;
                    if (gate.IsCurrent(requestId) && requestPed == Game.Player.Character.Handle)
                    {
                        if (finished.IsFaulted) chat.Add(false, finished.Exception.GetBaseException().Message);
                        else if (!finished.IsCanceled) Execute(finished.Result);
                    }
                    if (cancellation != null) { cancellation.Dispose(); cancellation = null; }
                }
                worldActions.Tick();
                var afterSwitch = worldActions.TakeAfterSwitch();
                if (afterSwitch != null) Execute(afterSwitch);
                UpdateDriving();
                if (chat.IsOpen) Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 0);
                else if (phase != "idle" || worldActions.BlocksMovement)
                {
                    // AI tasks drive the protagonist. F8 always remains available via KeyDown.
                    foreach (int control in new[] { 59, 60, 63, 64, 71, 72, 75, 76, 23, 24, 25, 30, 31, 32, 33, 34, 35 })
                        Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, control, true);
                }
                worldActions.ApplyInput();
                chat.Status = worldActions.Active ? worldActions.Status : phase == "idle" ? "Ready" : phase == "entering" ? "Approaching car" :
                    phase == "leaving" ? "Leaving vehicle" : phase == "braking" ? "Stopping" : "Chill drive  /  " + speedKmh + " km/h";
                string actor = ActorName(Game.Player.Character);
                chat.CharacterName = actor == "current character" ? "Player" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(actor);
                chat.HudColorId = actor == "franklin" ? 144 : actor == "trevor" ? 145 : 143;
                chat.Autonomous = phase != "idle" || worldActions.Active;
                chat.OnFoot = !Game.Player.Character.IsInVehicle();
                chat.WorldTime=ClockLabel();
                chat.WeatherName=World.Weather.ToString();
                chat.ClockFrozen=GameClock.IsPaused;
                chat.CurrentSpeedKmh = vehicle != null && vehicle.Exists() ? (int)Math.Round(vehicle.Speed * 3.6f) : 0;
                chat.Route = worldActions.Active ? worldActions.GoalLabel : hasDestination && drivingPlan != null ? "To " + destinationLabel : "Free roam";
                string clickedCommand = chat.Draw(openKey.ToString(), takeoverKey.ToString());
                if (clickedCommand != null) Submit(clickedCommand);
                var uiPlan=chat.TakeUiPlan();
                if(uiPlan!=null) { if(uiPlan.Message.Length>0) chat.Add(true,uiPlan.Message); Execute(uiPlan); }
            }
            catch (Exception error) { Fail(error); }
        }

        private void UpdateCompanion()
        {
            if (companion == null) return;
            CompanionCommand command;
            if (companion.TryTake(out command))
            {
                if (command.Stop) Stop("You have control.");
                else if (Game.IsPaused) chat.Add(false, "The game is paused. Resume GTA and disable Pause Game on Focus Loss to run in the background, then send your request again.");
                else Submit(command.Text);
            }
            long now = DateTime.UtcNow.Ticks;
            if (now - lastCompanionUpdate < TimeSpan.TicksPerMillisecond * 500) return;
            lastCompanionUpdate = now;
            companion.Publish(new JavaScriptSerializer().Serialize(new {
                actor=CultureInfo.InvariantCulture.TextInfo.ToTitleCase(ActorName(Game.Player.Character)),
                status=chat.Status, route=chat.Route, active=phase != "idle" || worldActions.Active,
                paused=Game.IsPaused, thinking=chat.Thinking, provider=chat.Provider,
                taskHudVisible=chat.ShowDrivingHud, clockFrozen=GameClock.IsPaused,
                gameTime=ClockLabel(), weather=World.Weather.ToString(),
                messages=chat.Messages.Select(m => new { user=m.User, text=m.Text }).ToArray(),
                places=PlaceCatalog.All.Select(p => new { id=p.Id, name=p.Name }).ToArray()
            }));
        }

        private bool RefreshOpenAiConfiguration(bool announceFailure)
        {
            try
            {
                string legacyKey = null;
                if (!File.Exists(envFilePath))
                    legacyKey = ScriptSettings.Load(Path.Combine(BaseDirectory, "FiveVibe.local.ini")).GetValue("AI", "ApiKey", "");
                var configuration = AiConfiguration.Load(envFilePath, defaultAiModel, legacyKey,
                    () => Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
                        Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User));
                if (!configuration.HasApiKey)
                {
                    if (planner != null) { planner.Dispose(); planner = null; }
                    activeAiConfiguration = null;
                    chat.Provider = "OPENAI_PENDING";
                    if (announceFailure) chat.Add(false, "Set OPENAI_API_KEY in your .env, save it, then try again. Do not paste your key into chat.");
                    return false;
                }
                if (activeAiConfiguration == null || activeAiConfiguration.ApiKey != configuration.ApiKey || activeAiConfiguration.Model != configuration.Model)
                {
                    if (planner != null) planner.Dispose();
                    planner = new OpenAiPlanner(configuration.ApiKey, configuration.Model);
                    activeAiConfiguration = configuration;
                }
                chat.Provider = "OPENAI";
                return true;
            }
            catch (Exception error)
            {
                if (planner != null) { planner.Dispose(); planner = null; }
                activeAiConfiguration = null;
                chat.Provider = "OPENAI_PENDING";
                if (announceFailure) chat.Add(false, error is FormatException ? error.Message : "Cannot read .env configuration. Check the file exists and is accessible.");
                return false;
            }
        }

        private void Execute(CommandPlan plan)
        {
            plan.Validate();
            if (plan.Action == "stop") { BrakeOrRelease(); return; }
            if (plan.Action == "say") { chat.Add(false, plan.Message); return; }
            if(plan.Action.StartsWith("hud_",StringComparison.Ordinal))
            { SetTaskHud(plan.Action=="hud_show" || (plan.Action=="hud_toggle" && !chat.ShowDrivingHud)); return; }
            if(plan.Action=="world_settings") { ApplyWorldSettings(plan); return; }
            if (plan.Action == "help")
            {
                chat.Add(false, "Walk, run, greet or follow nearby people, switch characters, use the phone, drive to named places or reach a mission marker. F8 takes over. " +
                    (planner == null ? "Local mode: basic commands only." : "OpenAI interprets your requests."));
                return;
            }
            if (plan.Action == "mission_status")
            {
                Vector3 missionPoint;
                string missionName;
                if (Function.Call<bool>(Hash.GET_MISSION_FLAG)) chat.Add(false, "A mission is active. I can help with movement, driving, phone and interactions on request. Autonomous mission completion is not available.");
                else if (WorldActions.TryMissionTarget("nearest", out missionPoint, out missionName)) chat.Add(false, "Detected mission marker: " + missionName + " at about " + Math.Round(missionPoint.DistanceTo(Game.Player.Character.Position)) + " metres. Ask me to go there.");
                else chat.Add(false, "No known mission marker is available for this character. You can set a waypoint instead.");
                return;
            }
            Ped ped = Game.Player.Character;
            if (Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) || !Game.Player.CanControlCharacter)
            { chat.Add(false, "Wait until the game returns character control."); return; }
            if (plan.Actor != "current" && ActorName(ped) != plan.Actor)
            { ReleaseControl(); worldActions.Switch(plan.Actor, plan.Action == "switch_character" ? null : plan); return; }
            if (plan.Action == "switch_character") { chat.Add(false, "You are already playing as " + plan.Actor + "."); return; }
            if (GameplayRules.IsWorldAction(plan.Action))
            {
                ReleaseControl();
                worldActions.Start(plan);
                return;
            }
            if (plan.Action == "speed")
            {
                if (phase != "driving") { chat.Add(false, "Start a drive or choose a destination first."); return; }
                speedKmh = plan.SpeedKmh;
                drivingPlan.SpeedKmh = speedKmh;
                Function.Call(Hash.SET_DRIVE_TASK_CRUISE_SPEED, driver.Handle, speedKmh / 3.6f);
                chat.Add(false, "Target speed updated to " + speedKmh + " km/h.");
                return;
            }
            if (plan.Action == "leave")
            {
                if (!ped.IsInVehicle()) { chat.Add(false, "You are already on foot."); return; }
                if (ped.CurrentVehicle.Speed > 1f) { chat.Add(false, "Take over with " + takeoverKey + " and brake, then ask me to get out."); return; }
                ReleaseControl();
                worldActions.Cancel();
                driver = ped;
                vehicle = ped.CurrentVehicle;
                phase = "leaving";
                phaseStarted = Game.GameTime;
                Function.Call(Hash.TASK_LEAVE_VEHICLE, ped.Handle, vehicle.Handle, 0);
                return;
            }
            Vector3 target = Vector3.Zero;
            bool useDestination = plan.Action == "waypoint" || plan.Action == "place" || plan.Action == "mission";
            if (plan.Action == "waypoint")
            {
                int blip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, 8);
                if (!Function.Call<bool>(Hash.DOES_BLIP_EXIST, blip)) { chat.Add(false, "Set a waypoint on the map first."); return; }
                target = Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD, blip);
                destinationLabel = "the waypoint";
            }
            else if (plan.Action == "place")
            {
                target = PlacePosition(plan.Place);
                destinationLabel = PlaceLabel(plan.Place);
                var roadPoint = World.GetNextPositionOnStreet(target);
                if (roadPoint != Vector3.Zero && roadPoint.DistanceTo(target) < 150f) target = roadPoint;
            }
            else if (plan.Action == "mission")
            {
                if (Function.Call<bool>(Hash.GET_MISSION_FLAG))
                { chat.Add(false, "A mission is already active. Set a waypoint or give a specific instruction."); return; }
                if (!WorldActions.TryMissionTarget(plan.Target, out target, out destinationLabel))
                { chat.Add(false, "That mission marker is unavailable for this character. Set a waypoint instead."); return; }
                if (!ped.IsInVehicle()) { ReleaseControl(); worldActions.WalkToMission(target, destinationLabel); return; }
            }

            ReleaseControl();
            worldActions.Cancel();
            driver = ped;
            speedKmh = plan.SpeedKmh;
            drivingPlan = plan;
            hasDestination = useDestination;
            destination = target;
            if (ped.IsInVehicle())
            {
                vehicle = ped.CurrentVehicle;
                if (!IsUsableCar(vehicle) || Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, vehicle.Handle, -1, false) != ped.Handle)
                { ReleaseControl(); chat.Add(false, "Sit in the driver's seat of a usable car and try again."); return; }
                ClaimCar();
                StartDriving();
                return;
            }
            vehicle = World.GetNearbyVehicles(ped.Position, searchRadius)
                .Where(v => IsUsableCar(v) && v.Speed < .5f && !v.IsPersistent &&
                    Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, v.Handle, -1, false) &&
                    Function.Call<int>(Hash.GET_VEHICLE_DOOR_LOCK_STATUS, v.Handle) <= 1)
                .OrderBy(v => v.Position.DistanceTo(ped.Position)).FirstOrDefault();
            if (vehicle == null)
            { ReleaseControl(); chat.Add(false, "No empty, unlocked car is nearby. Move toward a parking area or get in a car and try again."); return; }
            ClaimCar();
            phase = "entering";
            phaseStarted = Game.GameTime;
            Function.Call(Hash.TASK_ENTER_VEHICLE, ped.Handle, vehicle.Handle, 22000, -1, 1f, 1, 0);
            chat.Add(false, "Found an empty car. Approaching and trying to get in.");
        }

        private void ClaimCar() { wasPersistent = vehicle.IsPersistent; vehicle.IsPersistent = true; claimedCar = true; }

        private void SetTaskHud(bool visible)
        {
            chat.ShowDrivingHud=visible;
            try { HudPreferences.Save(preferencesPath,visible); }
            catch(Exception) { chat.Add(false,"HUD changed for this session; preferences could not be saved."); }
            chat.Add(false,visible ? "Task HUD shown. "+hudKey+" toggles it." : "Task HUD hidden. Autonomy continues; "+takeoverKey+" still takes over.");
        }
        private void ApplyWorldSettings(CommandPlan plan)
        {
            if(Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE)) { chat.Add(false,"World controls are available in Story Mode."); return; }
            if(Function.Call<bool>(Hash.GET_MISSION_FLAG) || Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) || !Game.Player.CanControlCharacter)
            { chat.Add(false,"Weather and time controls are available in free roam after the game returns control."); return; }
            if(plan.WeatherType=="auto")
            {
                Function.Call(Hash.CLEAR_OVERRIDE_WEATHER);
                Function.Call(Hash.CLEAR_WEATHER_TYPE_PERSIST);
                ownsWeather=false;
            }
            else if(plan.WeatherType!="none")
            { Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST,WorldControlParser.WeatherCode(plan.WeatherType)); ownsWeather=true; }
            if(plan.Hour>=0) Function.Call(Hash.SET_CLOCK_TIME,plan.Hour,plan.Minute,0);
            if(plan.Clock!="unchanged")
            { ownsClockPause=plan.Clock=="pause"; Function.Call(Hash.PAUSE_CLOCK,ownsClockPause); }
            chat.ClockFrozen=ownsClockPause;
            var changes=new System.Collections.Generic.List<string>();
            if(plan.WeatherType!="none") changes.Add(plan.WeatherType=="auto" ? "Automatic weather restored" : "Weather set to "+plan.WeatherType);
            if(plan.Hour>=0) changes.Add("Time set to "+ClockLabel());
            if(plan.Clock!="unchanged") changes.Add(ownsClockPause ? "Clock frozen" : "Clock resumed");
            chat.Add(false,string.Join(". ",changes)+".");
        }
        private void ResetWorldOverrides()
        {
            if(ownsClockPause) Function.Call(Hash.PAUSE_CLOCK,false);
            if(ownsWeather) { Function.Call(Hash.CLEAR_OVERRIDE_WEATHER); Function.Call(Hash.CLEAR_WEATHER_TYPE_PERSIST); }
            ownsClockPause=ownsWeather=false;
        }
        private static string ClockLabel() { return GameClock.Hour.ToString("00",CultureInfo.InvariantCulture)+":"+GameClock.Minute.ToString("00",CultureInfo.InvariantCulture); }

        private void StartDriving()
        {
            const int calmDriving = 786603;
            if (hasDestination)
                Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE, driver.Handle, vehicle.Handle,
                    destination.X, destination.Y, destination.Z, speedKmh / 3.6f, calmDriving, 12f);
            else Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver.Handle, vehicle.Handle, speedKmh / 3.6f, calmDriving);
            phase = "driving";
            phaseStarted = lastMoveTime = Game.GameTime;
            lastPosition = vehicle.Position;
            chat.Add(false, hasDestination ? "Driving to " + destinationLabel + "." : "Chill drive started. Close chat and enjoy the ride.");
        }

        private void UpdateDriving()
        {
            if (phase == "idle") return;
            if (driver == null || !driver.Exists() || driver.Handle != Game.Player.Character.Handle || driver.IsDead)
            { Stop("Character changed: autonomy stopped."); return; }
            if (vehicle == null || !vehicle.Exists() || !vehicle.IsDriveable)
            { Stop("The car is no longer available. You have control."); return; }
            if (phase == "leaving")
            {
                if (!driver.IsInVehicle()) { ReleaseControl(); chat.Add(false, "You have left the vehicle."); }
                else if (Game.GameTime - phaseStarted > 10000) Stop("Unable to get out. You have control.");
                return;
            }
            if (phase == "entering")
            {
                if (Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, vehicle.Handle, -1, false) == driver.Handle) StartDriving();
                else if (Game.GameTime - phaseStarted > 24000) Stop("Could not reach the driver's seat. Try another car.");
                return;
            }
            if (!driver.IsInVehicle(vehicle) || Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, vehicle.Handle, -1, false) != driver.Handle)
            { Stop("You are no longer driving: autonomy stopped."); return; }
            if (phase == "braking")
            {
                if (vehicle.Speed < .8f) { string message = brakingMessage; ReleaseControl(); chat.Add(false, message); }
                else if (Game.GameTime - phaseStarted > 9000) Stop("You are near the destination. You have control.");
                return;
            }
            if (hasDestination && vehicle.Position.DistanceTo(destination) < 22f)
            {
                Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, driver.Handle, vehicle.Handle, 27, 9000);
                phase = "braking";
                phaseStarted = Game.GameTime;
                brakingMessage = "Destination reached. You have control.";
                return;
            }
            if (vehicle.Position.DistanceTo(lastPosition) > 8f) { lastPosition = vehicle.Position; lastMoveTime = Game.GameTime; }
            // Permit ordinary red lights and traffic. Never teleport out of trouble.
            if (Game.GameTime - lastMoveTime > 90000) Stop("The car has been stopped for a while. You have control.");
            if (hasDestination && Game.GameTime - phaseStarted > 20 * 60 * 1000) Stop("Travel time limit reached. You have control.");
        }

        private void Stop(string message)
        {
            CancelPlanning();
            worldActions.Cancel();
            ReleaseControl();
            chat.Add(false, message);
        }

        private void BrakeOrRelease()
        {
            CancelPlanning();
            if (phase == "driving" && driver != null && driver.Exists() && vehicle != null && vehicle.Exists())
            {
                Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, driver.Handle, vehicle.Handle, 27, 9000);
                phase = "braking";
                phaseStarted = Game.GameTime;
                brakingMessage = "Car stopped. You have control.";
                chat.Add(false, "Stopping. " + takeoverKey + " gives you control immediately.");
            }
            else Stop("You have control.");
        }

        private void CancelPlanning()
        {
            gate.Cancel();
            if (cancellation != null) { cancellation.Cancel(); cancellation.Dispose(); cancellation = null; }
            if (planning != null)
                planning.ContinueWith(t => { var observed = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            planning = null;
            chat.Thinking = false;
        }

        private void ReleaseControl(bool clearOwnedTasks = true)
        {
            // Controls are disabled for one frame only, so even an unload cannot leave a
            // permanent SET_PLAYER_CONTROL(false) behind. Clear only tasks owned by this mod.
            if (clearOwnedTasks && phase != "idle" && driver != null && driver.Exists() && driver.Handle == Game.Player.Character.Handle)
                Function.Call(Hash.CLEAR_PED_TASKS, driver.Handle);
            if (claimedCar && vehicle != null && vehicle.Exists() && !wasPersistent) vehicle.MarkAsNoLongerNeeded();
            vehicle = null;
            driver = null;
            wasPersistent = false;
            claimedCar = false;
            phase = "idle";
            chat.Status = "Ready";
        }

        private void Fail(Exception error)
        {
            try { CancelPlanning(); worldActions.Cancel(); ReleaseControl(); chat.Add(false, "Action stopped: " + error.Message); }
            catch { phase = "idle"; }
        }

        private static bool IsUsableCar(Vehicle car) { return car != null && car.Exists() && car.Model.IsCar && car.IsDriveable; }
        private static string ActorName(Ped ped)
        {
            int hash = ped.Model.Hash;
            if (hash == unchecked((int)PedHash.Michael)) return "michael";
            if (hash == unchecked((int)PedHash.Franklin)) return "franklin";
            if (hash == unchecked((int)PedHash.Trevor)) return "trevor";
            return "current character";
        }
        internal static Vector3 PlacePosition(string place)
        {
            var location = PlaceCatalog.Get(place);
            if (location == null) throw new ArgumentException("Unknown destination.");
            return new Vector3(location.X, location.Y, location.Z);
        }
        internal static string PlaceLabel(string place)
        {
            var location = PlaceCatalog.Get(place);
            return location == null ? "Unknown destination" : location.Name;
        }
    }
}
