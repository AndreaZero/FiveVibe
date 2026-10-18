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
        private int entryTimeout=22000;
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
        private readonly MissionPilot missionPilot;
        private readonly SpectatorCamera spectatorCamera;
        private readonly Keys cameraKey;
        private readonly RealWorldService realWorld;
        private readonly VehicleRadio vehicleRadio=new VehicleRadio();
        private readonly FavoriteStore favorites;
        private readonly TourSession tour=new TourSession();
        private readonly TimedDriveSession timedDrive=new TimedDriveSession();
        private Vector3 timedOrigin;
        private bool realClockApplied, realWeatherApplied;
        private string appliedRealWeather="";
        private long lastClockSecond=-1;

        public FiveVibeScript()
        {
            worldActions = new WorldActions(message => chat.Add(false, message), () => chat.IsOpen = false);
            missionPilot = new MissionPilot(message => chat.Add(false, message),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FiveVibe"));
            missionWasActive = Function.Call<bool>(Hash.GET_MISSION_FLAG);
            var settings = ScriptSettings.Load(Path.Combine(BaseDirectory, "FiveVibe.ini"));
            try { PlaceCatalog.LoadCustom(Path.Combine(BaseDirectory, "FiveVibe.locations.json")); }
            catch (Exception) { chat.Add(false, "Custom locations could not be loaded. Check FiveVibe.locations.json; built-in places remain available."); }
            favorites=new FavoriteStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiveVibe","favorites.json"));
            try { favorites.Load(); PlaceCatalog.SetFavorites(favorites.All); }
            catch(Exception) { PlaceCatalog.SetFavorites(null); chat.Add(false,"Favorites could not be loaded. Fix favorites.json and reload scripts; the file is preserved."); }
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
            spectatorCamera=new SpectatorCamera(message=>chat.Add(false,message),settings.GetValue("Camera","Letterbox",true),settings.GetValue("Camera","AutoCinemaDriving",true));
            realWorld=new RealWorldService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiveVibe","real-world.json"),message=>chat.Add(false,message));
            cameraKey=settings.GetValue("Camera","CycleKey",Keys.F10);
            if(cameraKey==openKey || cameraKey==takeoverKey || cameraKey==hudKey || cameraKey==Keys.F4 || cameraKey==Keys.F6)
                cameraKey=new[] {Keys.F10,Keys.F11,Keys.F12,Keys.F5}.First(k=>k!=openKey && k!=takeoverKey && k!=hudKey);
            chat.CameraKeyName=cameraKey.ToString();
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
            Aborted += delegate { CancelPlanning(); realWorld.Dispose(); spectatorCamera.Dispose(); missionPilot.Cancel(false); worldActions.Cancel(); ReleaseControl(); ResetWorldOverrides(); chat.Dispose(); if (planner != null) planner.Dispose(); if (companion != null) companion.Dispose(); };
            Interval = 0;
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode == takeoverKey) { chat.IsOpen = false; Stop("You have control."); return; }
                if (e.KeyCode == hudKey) { SetTaskHud(!chat.ShowDrivingHud); return; }
                if (Game.IsPaused || Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE)) return;
                if(e.KeyCode==cameraKey) { spectatorCamera.Execute(e.Shift?"camera_next":"camera_cycle"); return; }
                if(e.KeyCode==Keys.V && !chat.IsOpen) spectatorCamera.ReleaseForManualView();
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
            string city;
            if(RealWorldCommands.TryCity(prompt,out city))
            { chat.Add(true,"Set real-world weather location"); realWorld.SetCity(city); realWorld.SetWeather(true); return; }
            chat.Add(true, prompt);
            if (LocalPlanner.IsImmediateStop(prompt)) { BrakeOrRelease(); return; }
            if (Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE)) { chat.Add(false, "FiveVibe runs in Story Mode."); return; }
            FavoriteRequest favoriteRequest;
            if(FavoriteCommands.TryParse(prompt,favorites,out favoriteRequest)) { HandleFavorite(favoriteRequest,false); return; }
            CommandPlan timedPlan;
            if(TimedDriveCommands.TryParse(prompt,speedKmh,out timedPlan,false)) {Execute(timedPlan);return;}
            CommandPlan tourPlan;
            if(TourCommands.TryParse(prompt,speedKmh,out tourPlan)) {Execute(tourPlan);return;}
            CommandPlan radioPlan;
            if(RadioCommands.TryParse(prompt,out radioPlan)) { Execute(radioPlan); return; }
            var directPilot = LocalPlanner.Parse(prompt, speedKmh);
            if(CameraCommands.IsCamera(directPilot.Action) || directPilot.Action.StartsWith("realtime_",StringComparison.Ordinal)) { Execute(directPilot); return; }
            CancelPlanning();
            if (directPilot.Action == "mission_auto") { Execute(directPilot); return; }
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
            context += missionPilot.Context();
            context += "; themed_tour="+tour.Status;
            context += "; timed_drive="+timedDrive.Status;
            context += "; spectator_camera="+spectatorCamera.ModeName+"; camera_status="+spectatorCamera.Status;
            context+="; task_hud_visible="+chat.ShowDrivingHud+"; clock_paused="+GameClock.IsPaused+"; game_time="+ClockLabel()+"; weather="+World.Weather;
            string[] history = chat.Messages.Skip(Math.Max(0, chat.Messages.Count - 10)).Select(m => (m.User ? "utente: " : "mod: ") + m.Text).ToArray();
            planning = planner.PlanAsync(prompt, context, history, cancellation.Token);
            chat.Thinking = true;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                timedDrive.Sample(Environment.TickCount,!Game.IsPaused);
                UpdateCompanion();
                UpdateRealWorld();
                if (Game.IsPaused) { spectatorCamera.Suspend(); return; }
                missionPilot.Tick();
                if (missionPilot.Active || missionPilot.Result != "pending")
                {
                    chat.Status = missionPilot.Status;
                    chat.Route = "Franklin and Lamar / " + missionPilot.Objective;
                    chat.Autonomous = missionPilot.Active;
                }
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
                    spectatorCamera.Suspend();
                    CancelPlanning();
                    worldActions.Cancel(false);
                    ReleaseControl(false);
                    chat.IsOpen = false;
                    if (missionPilot.Active) { chat.Status = missionPilot.Status; chat.Route = "Franklin and Lamar / " + missionPilot.Objective; chat.Autonomous = true; }
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
                else if (phase != "idle" || worldActions.BlocksMovement || missionPilot.BlocksMovement)
                {
                    // AI tasks drive the protagonist. F8 always remains available via KeyDown.
                    foreach (int control in new[] { 59, 60, 63, 64, 71, 72, 75, 76, 23, 24, 25, 30, 31, 32, 33, 34, 35 })
                        Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, control, true);
                }
                worldActions.ApplyInput();
                missionPilot.ApplyInput();
                chat.Status = missionPilot.Active ? missionPilot.Status : worldActions.Active ? worldActions.Status : phase == "idle" ? "Ready" : phase == "entering" ? "Approaching car" :
                    phase == "leaving" ? "Leaving vehicle" : phase=="changing" ? "Changing vehicle" : phase == "braking" ? "Stopping" : "Chill drive  /  " + speedKmh + " km/h";
                string actor = ActorName(Game.Player.Character);
                chat.CharacterName = actor == "current character" ? "Player" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(actor);
                chat.HudColorId = actor == "franklin" ? 144 : actor == "trevor" ? 145 : 143;
                chat.Autonomous = phase != "idle" || worldActions.Active || missionPilot.Active;
                try { spectatorCamera.UpdateDriving(phase=="driving" || missionPilot.Driving); spectatorCamera.Tick(chat.Autonomous,chat.IsOpen); }
                catch(Exception) { spectatorCamera.ReleaseForManualView(); chat.Add(false,"Camera unavailable. Gameplay camera restored; your active action continues."); }
                chat.CameraMode=spectatorCamera.ModeName;
                chat.CameraState=spectatorCamera.Status;
                chat.CameraLetterbox=spectatorCamera.Letterbox;
                chat.AutoCinemaDriving=spectatorCamera.AutoCinema;
                chat.RealClock=realWorld.ClockEnabled; chat.RealWeather=realWorld.WeatherEnabled;
                chat.OnFoot = !Game.Player.Character.IsInVehicle();
                chat.WorldTime=ClockLabel();
                chat.WeatherName=World.Weather.ToString();
                chat.ClockFrozen=GameClock.IsPaused && !realClockApplied;
                var displayedCar = missionPilot.Active ? Game.Player.Character.CurrentVehicle : vehicle;
                chat.CurrentSpeedKmh = displayedCar != null && displayedCar.Exists() ? (int)Math.Round(displayedCar.Speed * 3.6f) : 0;
                chat.Route = missionPilot.Active ? "Franklin and Lamar / " + missionPilot.Objective : worldActions.Active ? worldActions.GoalLabel : timedDrive.Active ? timedDrive.Status : tour.Active ? tour.Status : hasDestination && drivingPlan != null ? "To " + destinationLabel : "Free roam";
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
                else if(command.FavoriteAction!=null)
                    HandleFavorite(new FavoriteRequest {Action=command.FavoriteAction,Name=command.FavoriteName,Id=command.FavoriteId,Walk=command.FavoriteMode=="walk"},true);
                else if(command.Location)
                {
                    if(command.City!=null) realWorld.SetCity(command.City);
                    else realWorld.SetLocation(new RealLocation {Name="Browser location",Latitude=command.Latitude,Longitude=command.Longitude});
                    realWorld.SetWeather(true);
                }
                else if (Game.IsPaused) chat.Add(false, "The game is paused. Resume GTA and disable Pause Game on Focus Loss to run in the background, then send your request again.");
                else Submit(command.Text);
            }
            long now = DateTime.UtcNow.Ticks;
            if (now - lastCompanionUpdate < TimeSpan.TicksPerMillisecond * 500) return;
            lastCompanionUpdate = now;
            companion.Publish(new JavaScriptSerializer().Serialize(new {
                actor=CultureInfo.InvariantCulture.TextInfo.ToTitleCase(ActorName(Game.Player.Character)),
                status=chat.Status, route=chat.Route, active=phase != "idle" || worldActions.Active || missionPilot.Active,
                paused=Game.IsPaused, thinking=chat.Thinking, provider=chat.Provider,
                taskHudVisible=chat.ShowDrivingHud, clockFrozen=GameClock.IsPaused && !realClockApplied,
                gameTime=ClockLabel(), weather=World.Weather.ToString(),
                camera=new { mode=spectatorCamera.ModeName, status=spectatorCamera.Status, letterbox=spectatorCamera.Letterbox, cycleKey=cameraKey.ToString(), autoDriving=spectatorCamera.AutoCinema },
                radio=vehicleRadio.Snapshot(),
                timedDrive=new {active=timedDrive.Active,stage=timedDrive.Stage,status=timedDrive.Status,minutes=timedDrive.Minutes,remainingSeconds=timedDrive.RemainingSeconds,canStart=CanUseFavorites() && !Function.Call<bool>(Hash.GET_MISSION_FLAG)},
                tour=new {active=tour.Active,id=tour.Profile==null?"none":tour.Profile.Id,status=tour.Status,result=tour.Result,step=tour.Active?tour.Index+1:0,total=tour.Profile==null?0:tour.Profile.Stops.Length,canStart=CanUseFavorites() && !Function.Call<bool>(Hash.GET_MISSION_FLAG)},
                favorites=new {ready=favorites.Ready,canUse=favorites.Ready && CanUseFavorites(),items=favorites.All.OrderBy(f=>f.Name).Select(f=>new {id=f.Id,name=f.Name}).ToArray()},
                realWorld=new { clock=realWorld.ClockEnabled, weather=realWorld.WeatherEnabled, location=realWorld.LocationName, status=realWorld.Status, paused=Function.Call<bool>(Hash.GET_MISSION_FLAG) },
                missionPilot = new { active=missionPilot.Active, joined=missionPilot.Joined, stage=missionPilot.Status, objective=missionPilot.Objective, label=missionPilot.ObjectiveKey, result=missionPilot.Result },
                messages=chat.Messages.Select(m => new { user=m.User, text=m.Text }).ToArray(),
                places=PlaceCatalog.All.Select(p => new { id=p.Id, name=p.Name }).ToArray()
            }));
        }

        private bool CanUseFavorites()
        {
            return !Game.IsPaused && !Game.Player.Character.IsDead && Game.Player.CanControlCharacter &&
                !Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE) && !Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) &&
                !Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS) && !Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT);
        }

        private void HandleFavorite(FavoriteRequest request,bool announce)
        {
            try
            {
                if(request.Error!=null) {chat.Add(false,request.Error);return;}
                if(!favorites.Ready) {chat.Add(false,"Favorites could not be loaded. Fix favorites.json and reload scripts; the file is preserved.");return;}
                if(request.Action=="list")
                {chat.Add(false,favorites.All.Length==0?"No favorites yet. Say 'Save this place as Home'.":"Favorites: "+string.Join(", ",favorites.All.Take(8).Select(f=>f.Name))+". The companion lists every saved place.");return;}
                if(!CanUseFavorites()) {chat.Add(false,"Resume normal Story Mode gameplay before saving or visiting a favorite. Your active action is unchanged.");return;}
                if(request.Action=="save")
                {
                    if(announce)chat.Add(true,"Save current location as "+request.Name);
                    var point=Game.Player.Character.Position;bool updated;
                    var saved=favorites.Save(request.Name,point.X,point.Y,point.Z,out updated);
                    PlaceCatalog.SetFavorites(favorites.All);
                    chat.Add(false,(updated?"Updated favorite: ":"Saved favorite: ")+saved.Name+". Your active action continues.");return;
                }
                if(request.Action!="go") {chat.Add(false,"Choose Save current location or a saved favorite.");return;}
                var destination=request.Id!=null?favorites.Get(request.Id):favorites.FindName(request.Name);
                if(destination==null) {chat.Add(false,"That favorite was not found. Check its exact name in the companion; your active action is unchanged.");return;}
                if(request.Walk && new Vector3(destination.X,destination.Y,destination.Z).DistanceTo(Game.Player.Character.Position)>600)
                {chat.Add(false,"That favorite is more than 600 metres away. Choose Drive instead; your active action is unchanged.");return;}
                var plan=CommandPlan.Create(request.Walk?"walk_place":"place","current",speedKmh,destination.Id);
                if(announce)chat.Add(true,(request.Walk?"Walk to favorite ":"Drive to favorite ")+destination.Name);
                CancelPlanning();Execute(plan);
            }
            catch(ArgumentException e) {chat.Add(false,e.Message);}
            catch(InvalidOperationException e) {chat.Add(false,e.Message);}
            catch(Exception) {if(request.Action=="go")throw;chat.Add(false,"Could not save favorites. Existing saved data was not reset; check file permissions and reload scripts.");}
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
            if(TourCatalog.IsStartAction(plan.Action) || plan.Action=="timed_drive")
            {
                if(!CanUseFavorites() || Function.Call<bool>(Hash.GET_MISSION_FLAG)) {chat.Add(false,"These drives start in free roam after GTA returns control. Your active action is unchanged.");return;}
                CancelPlanning();
            }
            if(RadioCommands.IsRadio(plan.Action)) { chat.Add(false,RadioControl.Execute(plan,vehicleRadio)); return; }
            if(CameraCommands.IsCamera(plan.Action)) { spectatorCamera.Execute(plan.Action); return; }
            if(plan.Action.StartsWith("realtime_",StringComparison.Ordinal))
            {
                if(plan.Action=="realtime_status") { chat.Add(false,realWorld.Status+". PC time sync: "+(realWorld.ClockEnabled?"on":"off")+"."); return; }
                if(plan.Action=="realtime_off") { realWorld.SetClock(false); realWorld.SetWeather(false); }
                else if(plan.Action=="realtime_clock_off") realWorld.SetClock(false);
                else if(plan.Action=="realtime_weather_off") realWorld.SetWeather(false);
                else { if(plan.Action=="realtime_clock" || plan.Action=="realtime_both") realWorld.SetClock(true); if(plan.Action=="realtime_weather" || plan.Action=="realtime_both") realWorld.SetWeather(true); }
                chat.Add(false,"Real-world sync: PC time "+(realWorld.ClockEnabled?"on":"off")+", weather "+(realWorld.WeatherEnabled?"on":"off")+". "+(realWorld.WeatherEnabled?realWorld.Status+".":"")); return;
            }
            if(plan.Action.StartsWith("hud_",StringComparison.Ordinal))
            { SetTaskHud(plan.Action=="hud_show" || (plan.Action=="hud_toggle" && !chat.ShowDrivingHud)); return; }
            if(plan.Action=="world_settings") { ApplyWorldSettings(plan); return; }
            if (plan.Action == "help")
            {
                chat.Add(false, "Walk, run, greet or follow nearby people, switch characters, use the phone, drive to named places or reach a mission marker. Say 'Play Radio Mirror Park' or 'Radio off' without stopping. Choose first person or cinema in CAMERA; "+cameraKey+" cycles views. F8 takes over. " +
                    (planner == null ? "Local mode: basic commands only." : "OpenAI interprets your requests."));
                return;
            }
            if (plan.Action == "mission_status")
            {
                if (missionPilot.Active) { chat.Add(false, "Mission Pilot: " + missionPilot.Status + ". Observed objective: " + missionPilot.ObjectiveKey + ". F8 takes over."); return; }
                if (MissionPilot.IsSupportedMissionRunning()) { chat.Add(false, "Franklin and Lamar is running. Say 'Start Mission Pilot' to attempt its full flow with objective detection."); return; }
                Vector3 missionPoint;
                string missionName;
                if (Function.Call<bool>(Hash.GET_MISSION_FLAG)) chat.Add(false, "A mission is active. I do not read its current objective or follow its steps automatically. Set a waypoint, then ask for movement, phone or interaction help.");
                else if (WorldActions.TryMissionTarget("nearest", out missionPoint, out missionName)) chat.Add(false, "Detected mission marker: " + missionName + " at about " + Math.Round(missionPoint.DistanceTo(Game.Player.Character.Position)) + " metres. Ask me to go there.");
                else chat.Add(false, "No known mission marker is available for this character. You can set a waypoint instead.");
                return;
            }
            if (plan.Action == "mission_auto")
            {
                if (Function.Call<bool>(Hash.GET_MISSION_FLAG) && !MissionPilot.IsSupportedMissionRunning())
                { chat.Add(false, "Mission Pilot supports Franklin and Lamar only. Start its replay from Pause > Game > Replay Mission."); return; }
                CancelPlanning(); worldActions.Cancel(); ReleaseControl(); ResetWorldOverrides();
                chat.IsOpen = false; missionPilot.Start(); return;
            }
            if (missionPilot.Active) missionPilot.Cancel();
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
            bool startingTour=TourCatalog.IsStartAction(plan.Action);
            bool useDestination = plan.Action == "waypoint" || plan.Action == "place" || plan.Action == "mission" || startingTour;
            if (plan.Action == "waypoint")
            {
                int blip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, 8);
                if (!Function.Call<bool>(Hash.DOES_BLIP_EXIST, blip)) { chat.Add(false, "Set a waypoint on the map first."); return; }
                target = Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD, blip);
                destinationLabel = "the waypoint";
            }
            else if (plan.Action == "place" || startingTour)
            {
                if(startingTour)
                {
                    string first=TourCatalog.ForAction(plan.Action).Stops[0];
                    target=PlacePosition(first);destinationLabel=PlaceLabel(first);
                }
                else if(plan.Place=="ls_customs")
                {
                    if(!TryCustoms(out target,out destinationLabel)) { chat.Add(false,"No available Los Santos Customs map marker was detected. Choose a specific Customs branch in Places or unlock the shop in the story."); return; }
                }
                else
                {
                target = PlacePosition(plan.Place);
                destinationLabel = PlaceLabel(plan.Place);
                }
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

            Vector3 origin=ped.Position;
            ReleaseControl();
            worldActions.Cancel();
            if(startingTour)tour.Start(plan.Action,Game.GameTime);
            if(plan.Action=="timed_drive") {timedOrigin=origin;timedDrive.Start(plan.DurationMinutes);}
            driver = ped;
            speedKmh = plan.SpeedKmh;
            drivingPlan = plan;
            hasDestination = useDestination;
            destination = target;
            if (ped.IsInVehicle())
            {
                vehicle = ped.CurrentVehicle;
                if(IsUsableCar(vehicle) && MatchesVehicle(vehicle,plan.VehicleType,false) && Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, vehicle.Handle, -1, false)==ped.Handle)
                {
                ClaimCar();
                if(plan.Action=="vehicle") { ReleaseControl(); chat.Add(false,"You are already in a suitable "+VehicleSelection.Label(plan.VehicleType)+"."); return; }
                StartDriving();
                return;
                }
                if(vehicle.Speed>.8f) { ReleaseControl(); chat.Add(false,"Stop the current vehicle before changing to a "+VehicleSelection.Label(plan.VehicleType)+"."); return; }
            }
            vehicle = World.GetNearbyVehicles(ped.Position, searchRadius)
                .Where(v => IsUsableCar(v) && MatchesVehicle(v,plan.VehicleType,true) && v.Speed < .5f && !v.IsPersistent &&
                    Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, v.Handle, -1, false) &&
                    Function.Call<int>(Hash.GET_VEHICLE_DOOR_LOCK_STATUS, v.Handle) <= 1)
                .OrderBy(v => v.Position.DistanceTo(ped.Position)).FirstOrDefault();
            if (vehicle == null)
            { ReleaseControl(); chat.Add(false, "No empty, unlocked "+VehicleSelection.Label(plan.VehicleType)+" is nearby within "+Math.Round(searchRadius)+" metres. Try another area; I will not substitute a different type."); return; }
            ClaimCar();
            entryTimeout=VehicleSelection.EntryTimeout(vehicle.Position.DistanceTo(ped.Position));
            if(ped.IsInVehicle())
            { phase="changing"; phaseStarted=Game.GameTime; Function.Call(Hash.TASK_LEAVE_VEHICLE,ped.Handle,ped.CurrentVehicle.Handle,0); chat.Add(false,"Leaving the current vehicle to reach the selected "+VehicleSelection.Label(plan.VehicleType)+"."); return; }
            phase = "entering";
            phaseStarted = Game.GameTime;
            Function.Call(Hash.TASK_ENTER_VEHICLE, ped.Handle, vehicle.Handle, entryTimeout, -1, 2f, 1, 0);
            chat.Add(false, "Found an empty "+VehicleSelection.Label(plan.VehicleType)+". Approaching and trying to get in.");
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
            if(plan.WeatherType!="none") { realWorld.SetWeather(false); realWeatherApplied=false; }
            if(plan.Hour>=0 || plan.Clock!="unchanged")
            {
                realWorld.SetClock(false);
                if(realClockApplied && plan.Clock=="unchanged") { Function.Call(Hash.PAUSE_CLOCK,false); ownsClockPause=false; }
                realClockApplied=false;
            }
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
            realClockApplied=realWeatherApplied=false; appliedRealWeather=""; lastClockSecond=-1;
        }
        private void UpdateRealWorld()
        {
            bool available=!Game.IsPaused && !Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE) && !Function.Call<bool>(Hash.GET_MISSION_FLAG) &&
                Game.Player.CanControlCharacter && !Game.Player.Character.IsDead && !Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) && !Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS);
            realWorld.Tick(available);
            if(available && realWorld.ClockEnabled)
            {
                long second=DateTime.Now.Ticks/TimeSpan.TicksPerSecond;
                if(second!=lastClockSecond)
                {
                    DateTime now=DateTime.Now;
                    Function.Call(Hash.PAUSE_CLOCK,true); Function.Call(Hash.SET_CLOCK_TIME,now.Hour,now.Minute,now.Second);
                    lastClockSecond=second;
                }
                realClockApplied=ownsClockPause=true;
            }
            else if(realClockApplied) { Function.Call(Hash.PAUSE_CLOCK,false); realClockApplied=ownsClockPause=false; lastClockSecond=-1; }
            var reading=realWorld.Current;
            if(available && realWorld.WeatherEnabled && reading!=null)
            {
                if(!realWeatherApplied || appliedRealWeather!=reading.Preset)
                { Function.Call(Hash.SET_WEATHER_TYPE_NOW_PERSIST,WorldControlParser.WeatherCode(reading.Preset)); appliedRealWeather=reading.Preset; }
                realWeatherApplied=ownsWeather=true;
            }
            else if(realWeatherApplied)
            { Function.Call(Hash.CLEAR_OVERRIDE_WEATHER); Function.Call(Hash.CLEAR_WEATHER_TYPE_PERSIST); realWeatherApplied=ownsWeather=false; appliedRealWeather=""; }
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
            timedDrive.BeginCruise(Environment.TickCount);
            chat.Add(false, timedDrive.Active ? timedDrive.Status+". Return travel takes extra time. F8 takes over." : tour.Active ? tour.Status+". F8 takes over." : hasDestination ? "Driving to " + destinationLabel + "." : "Chill drive started. Close chat and enjoy the ride.");
        }

        private void UpdateDriving()
        {
            if (phase == "idle") return;
            if(tour.Expired(Game.GameTime)) {Stop("Themed drive time limit reached. You have control.");return;}
            if(timedDrive.ReturnExpired) {Stop("Return travel time limit reached. You have control.");return;}
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
                if (Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, vehicle.Handle, -1, false) == driver.Handle)
                { if(drivingPlan.Action=="vehicle") { ReleaseControl(); chat.Add(false,"Vehicle ready. Ask for a destination or a chill ride."); } else StartDriving(); }
                else if (Game.GameTime - phaseStarted > entryTimeout+2000) Stop("Could not reach the driver's seat. Try another vehicle.");
                return;
            }
            if(phase=="changing")
            {
                if(!driver.IsInVehicle()) { phase="entering"; phaseStarted=Game.GameTime; Function.Call(Hash.TASK_ENTER_VEHICLE,driver.Handle,vehicle.Handle,entryTimeout,-1,2f,1,0); }
                else if(Game.GameTime-phaseStarted>12000) Stop("Could not leave the current vehicle. You have control.");
                return;
            }
            if (!driver.IsInVehicle(vehicle) || Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, vehicle.Handle, -1, false) != driver.Handle)
            { Stop("You are no longer driving: autonomy stopped."); return; }
            if (phase == "braking")
            {
                if (vehicle.Speed < .8f) { string message = brakingMessage; tour.Complete(); timedDrive.Complete(); ReleaseControl(); chat.Add(false, message); }
                else if (Game.GameTime - phaseStarted > 9000) Stop("You are near the destination. You have control.");
                return;
            }
            if(timedDrive.BeginReturn())
            {
                destination=timedOrigin;destinationLabel="the starting point";
                var roadPoint=World.GetNextPositionOnStreet(destination);
                if(roadPoint!=Vector3.Zero && roadPoint.DistanceTo(destination)<150f)destination=roadPoint;
                hasDestination=true;
                StartDriving();return;
            }
            if (hasDestination && vehicle.Position.DistanceTo(destination) < 22f)
            {
                if(tour.Active && tour.ReachStop())
                {
                    destination=PlacePosition(tour.CurrentPlace);destinationLabel=PlaceLabel(tour.CurrentPlace);
                    var roadPoint=World.GetNextPositionOnStreet(destination);
                    if(roadPoint!=Vector3.Zero && roadPoint.DistanceTo(destination)<150f)destination=roadPoint;
                    StartDriving();return;
                }
                Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, driver.Handle, vehicle.Handle, 27, 9000);
                phase = "braking";
                phaseStarted = Game.GameTime;
                timedDrive.ReachOrigin();
                brakingMessage = timedDrive.Active ? "Timed drive completed. Back near the starting point. You have control." : tour.Active ? tour.Profile.Name+" completed. You have control." : "Destination reached. You have control.";
                return;
            }
            if (vehicle.Position.DistanceTo(lastPosition) > 8f) { lastPosition = vehicle.Position; lastMoveTime = Game.GameTime; }
            // Permit ordinary red lights and traffic. Never teleport out of trouble.
            if (Game.GameTime - lastMoveTime > 90000) Stop("The car has been stopped for a while. You have control.");
            if (hasDestination && !timedDrive.Active && Game.GameTime - phaseStarted > 20 * 60 * 1000) Stop("Travel time limit reached. You have control.");
        }

        private void Stop(string message)
        {
            CancelPlanning();
            spectatorCamera.Reset();
            missionPilot.Cancel();
            worldActions.Cancel();
            ReleaseControl();
            chat.Add(false, message);
        }

        private void BrakeOrRelease()
        {
            tour.Cancel();
            timedDrive.Cancel();
            CancelPlanning();
            spectatorCamera.Reset();
            missionPilot.Cancel();
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
            tour.Cancel();
            timedDrive.Cancel();
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
            try { CancelPlanning(); spectatorCamera.Reset(); missionPilot.Cancel(); worldActions.Cancel(); ReleaseControl(); chat.Add(false, "Action stopped: " + error.Message); }
            catch { phase = "idle"; }
        }

        private static bool IsUsableCar(Vehicle car) { return car != null && car.Exists() && (car.Model.IsCar || (car.Model.IsBike && !car.Model.IsBicycle)) && car.IsDriveable; }
        private static bool MatchesVehicle(Vehicle car,string type,bool searching)
        { return VehicleSelection.Matches(type,(int)car.ClassType,car.Model.IsCar,car.Model.IsBike && !car.Model.IsBicycle,car.DisplayName,searching); }
        private static bool TryCustoms(out Vector3 position,out string label)
        {
            position=Vector3.Zero; label="Los Santos Customs"; float nearest=Single.MaxValue;
            int blip=Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID,72);
            for(int i=0;i<64 && Function.Call<bool>(Hash.DOES_BLIP_EXIST,blip);i++)
            {
                Vector3 point=Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD,blip);
                var branch=PlaceCatalog.All.Where(p=>p.Id.StartsWith("customs_",StringComparison.Ordinal)).OrderBy(p=>new Vector3(p.X,p.Y,p.Z).DistanceTo(point)).First();
                float distance=point.DistanceTo(Game.Player.Character.Position);
                if(new Vector3(branch.X,branch.Y,branch.Z).DistanceTo(point)<100 && distance<nearest && Function.Call<int>(Hash.GET_BLIP_COLOUR,blip)!=39)
                { nearest=distance; position=new Vector3(branch.X,branch.Y,branch.Z); label=branch.Name; }
                blip=Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID,72);
            }
            return nearest<Single.MaxValue;
        }
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
