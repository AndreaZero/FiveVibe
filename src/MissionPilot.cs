using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using GTA;
using GTA.Math;
using GTA.Native;

namespace FiveVibe
{
    // A bounded profile for armenian1. All natives execute on the GTA script thread.
    // No script locals/globals, mission flags, stats, NPC tasks or entity positions are written.
    public sealed class MissionPilot
    {
        private const string Recording = "mattarmenian";
        private const int RouteNumber = 5;
        private static readonly Vector3 Dealer = new Vector3(-31.17f, -1090.754f, 25.4344f);
        private static readonly Vector3 Home = new Vector3(-25.4559f, -1426.998f, 29.656f);
        private static readonly string[] Objectives = { "AR1_CHOOSE", "AR1_CHASE", "AR1_PARK", "AR1_GOGARAGE", "AR1_HOME", "CMN_GENGETINY", "CMN_GENGETBCKY", "CMN_GENGETBCK", "LOSE_WANTED", "LOSE_WANTED_LEVEL", "AR1_WAIT" };
        private readonly MissionFlow flow = new MissionFlow();
        private readonly Action<string> say;
        private readonly string logPath;
        private Ped lamar, ownedPed;
        private Vehicle missionCar, personalCar, lamarCar;
        private MissionObservation observation = new MissionObservation();
        private MissionStep lastStep = MissionStep.Off;
        private int lastRead, lastTask, lastLog, lastMoved, lastRecovery, recoveryUntil, recoveryCount;
        private bool firstRead = true, ownsTask, inputDrive, suspended;
        private bool steeringAllowed, throttleAllowed, brakeAllowed, handbrakeAllowed;
        private string taskKey = "", lastObservedLabel = "", localizedObjective = "";
        private Vector3 taskTarget, lastPosition;
        private SteeringCommand controls;
        private readonly List<Vector3> route = new List<Vector3>();
        private int routeIndex;
        private bool routeJoined;

        public MissionPilot(Action<string> report, string dataDirectory)
        {
            say = report;
            logPath = Path.Combine(dataDirectory, "mission-session.jsonl");
        }
        public bool Active { get { return flow.Active; } }
        public bool Joined { get { return flow.Joined; } }
        public bool BlocksMovement { get { return Active && observation.CanAct && flow.Step != MissionStep.Armed && flow.Step != MissionStep.AwaitResult; } }
        public string Status { get { return MissionFlow.Label(flow.Step); } }
        public string Objective { get { return MissionFlow.Label(flow.Goal); } }
        public string ObjectiveKey { get { return flow.ObjectiveKey ?? ""; } }
        public string Result { get { return flow.Step == MissionStep.Passed ? "passed" : flow.Step == MissionStep.Failed ? "failed" : flow.Step == MissionStep.Unconfirmed ? "unconfirmed" : "pending"; } }

        public static bool IsSupportedMissionRunning()
        { return Function.Call<int>(Hash.GET_NUMBER_OF_THREADS_RUNNING_THE_SCRIPT_WITH_THIS_HASH, HashName("armenian1")) > 0; }

        public void Start()
        {
            if (Function.Call<bool>(Hash.GET_MISSION_FLAG) && !IsSupportedMissionRunning())
            { say("Mission Pilot supports Franklin and Lamar only. Start its replay from Pause > Game > Replay Mission."); return; }
            Cancel();
            flow.Arm(Game.GameTime); firstRead = true; lastStep = MissionStep.Off;
            lastMoved = Game.GameTime; lastPosition = Game.Player.Character.Position;
            recoveryCount = 0; route.Clear(); routeJoined = false;
            try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)); File.WriteAllText(logPath, ""); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            say("Experimental Mission Pilot armed for Franklin and Lamar. Start the mission or its replay; I will read objectives and attempt the full flow. F8 takes over.");
            Tick();
        }

        public void Cancel(bool clearOwnedTasks = true)
        {
            ReleaseTask(clearOwnedTasks && CanActNow());
            flow.Cancel(); controls = null; inputDrive = false;
            route.Clear(); routeJoined = false; lastObservedLabel = ""; localizedObjective = "";
            lamar = ownedPed = null; missionCar = personalCar = lamarCar = null;
            routeIndex = 0; recoveryUntil = lastRecovery = 0;
        }

        public string Context()
        {
            return "; mission_pilot=" + (Active ? "active" : Result) + "; supported_mission_running=" + IsSupportedMissionRunning() +
                "; mission_pilot_stage=" + Status + "; observed_objective_label=" + ObjectiveKey;
        }

        public void Tick()
        {
            if (!Active) return;
            controls = null;
            if (firstRead || MissionFlow.Elapsed(Game.GameTime, lastRead) >= 100)
            {
                firstRead = false; lastRead = Game.GameTime;
                observation = Read();
                flow.Update(observation);
                if (lastStep != flow.Step)
                {
                    if (observation.CanAct) ReleaseTask(true); else ReleaseTask(false);
                    if (flow.Step != MissionStep.Observe) say("Mission Pilot: " + Status + ".");
                    WriteLog("stage"); lastStep = flow.Step;
                    lastMoved = Game.GameTime; lastPosition = Game.Player.Character.Position;
                    recoveryCount = 0;
                }
                if (MissionFlow.Elapsed(Game.GameTime, lastLog) > 2000) { WriteLog("state"); lastLog = Game.GameTime; }
            }
            if (!Active) { ReleaseTask(CanActNow()); inputDrive = false; return; }
            if (!observation.CanAct || !CanActNow())
            {
                // Forget ownership during story scenes. Never clear a scene's scripted tasks.
                ReleaseTask(false); inputDrive = false; suspended = true; return;
            }
            if (suspended) { taskKey = ""; lastTask = 0; suspended = false; }
            Ped player = Game.Player.Character;
            Vehicle car = player.CurrentVehicle;
            inputDrive = false;
            steeringAllowed = Function.Call<bool>(Hash.IS_CONTROL_ENABLED, 0, 59);
            throttleAllowed = Function.Call<bool>(Hash.IS_CONTROL_ENABLED, 0, 71);
            brakeAllowed = Function.Call<bool>(Hash.IS_CONTROL_ENABLED, 0, 72);
            handbrakeAllowed = Function.Call<bool>(Hash.IS_CONTROL_ENABLED, 0, 76);
            if (IsDriver(player, car) && (!steeringAllowed || !throttleAllowed))
            { ReleaseTask(false); suspended = true; lastMoved = Game.GameTime; return; }
            switch (flow.Step)
            {
                case MissionStep.WalkWithLamar:
                    if (Valid(lamar))
                    {
                        if (lamar.Position.DistanceTo(player.Position) > 3f) Walk(player, lamar.Position, "walk_lamar");
                        else ReleaseTask(true);
                    }
                    break;
                case MissionStep.ChooseCar: Enter(player, missionCar, "mission_car"); break;
                case MissionStep.PersonalCar: Enter(player, personalCar, "personal_car"); break;
                case MissionStep.FollowLamar:
                    if (!IsDriver(player, car)) { Enter(player, missionCar, "mission_car"); break; }
                    if (!Valid(lamarCar)) break;
                    if (LoadRoute() && FollowRecordedRoute(player, car)) break;
                    if (MissionFlow.Elapsed(Game.GameTime, lastTask) > 2000 || taskKey != "follow_lamar")
                    {
                        SetTask(player, "follow_lamar", lamarCar.Position);
                        Function.Call(Hash.TASK_VEHICLE_FOLLOW, player.Handle, car.Handle, lamarCar.Handle, 24f, 786468, 8);
                    }
                    break;
                case MissionStep.Park:
                    if (Valid(lamarCar)) Drive(player, car, lamarCar.GetOffsetPosition(new Vector3(-6, 0, 0)), "park", true);
                    break;
                case MissionStep.EvadePolice: Evade(player, car); break;
                case MissionStep.Dealership:
                    if (!IsDriver(player, car)) Enter(player, missionCar, "mission_car");
                    else Drive(player, car, MissionTargetNear(Dealer), "dealer", false);
                    break;
                case MissionStep.Home:
                    if (IsDriver(player, car)) Drive(player, car, MissionTargetNear(Home), "home", false);
                    break;
                case MissionStep.WaitForLamar:
                    if (IsDriver(player, car)) Hold(player, car);
                    break;
            }
            WatchProgress(player, car);
        }

        private MissionObservation Read()
        {
            Ped player = Game.Player.Character;
            bool running = IsSupportedMissionRunning();
            var result = new MissionObservation {
                Time = Game.GameTime, ScriptRunning = running, OtherMission = Function.Call<bool>(Hash.GET_MISSION_FLAG),
                PassedCount = Counter("sp1_missions_passed"), FailedCount = Counter("sp1_missions_failed"),
                Franklin = player.Model.Hash == unchecked((int)PedHash.Franklin), Dead = player.IsDead,
                CanAct = CanActNow(), InCar = player.IsInVehicle(), IsDriver = IsDriver(player, player.CurrentVehicle),
                Wanted = Game.Player.Wanted.WantedLevel, NearHome = player.Position.DistanceTo(Home) < 18
            };
            if (!running) return result;
            lamar = World.GetNearbyPeds(player.Position, 2000f)
                .Where(p => Valid(p) && (p.Model.Hash == HashName("ig_lamardavis") || p.Model.Hash == HashName("cs_lamardavis")) && p.IsPersistent)
                .OrderBy(p => p.Position.DistanceTo(player.Position)).FirstOrDefault();
            result.LamarAvailable = Valid(lamar);
            if (Valid(lamar) && lamar.IsInVehicle()) lamarCar = lamar.CurrentVehicle;
            else lamarCar = null;
            var nearbyCars = World.GetNearbyVehicles(player.Position, 180f);
            if (!MissionVehicle(missionCar) || missionCar.Position.DistanceTo(player.Position) > 250 ||
                (missionCar.Model.Hash != HashName("ninef2") && missionCar.Model.Hash != HashName("rapidgt2"))) missionCar = null;
            if (!Valid(missionCar)) missionCar = nearbyCars.Where(v => MissionVehicle(v) &&
                (v.Model.Hash == HashName("ninef2") || v.Model.Hash == HashName("rapidgt2")) &&
                Function.Call<bool>(Hash.IS_VEHICLE_SEAT_FREE, v.Handle, -1, false))
                .OrderBy(v => v.Position.DistanceTo(player.Position)).FirstOrDefault();
            if (result.IsDriver && MissionVehicle(player.CurrentVehicle) &&
                (player.CurrentVehicle.Model.Hash == HashName("ninef2") || player.CurrentVehicle.Model.Hash == HashName("rapidgt2"))) missionCar = player.CurrentVehicle;
            personalCar = nearbyCars.Where(v => MissionVehicle(v) && v.Model.Hash == HashName("buffalo2") &&
                (v.Position.DistanceTo(Dealer) < 100 || v.Handle == (result.InCar ? player.CurrentVehicle.Handle : 0)))
                .OrderBy(v => v.Position.DistanceTo(player.Position)).FirstOrDefault();
            result.PersonalCarAvailable = Valid(personalCar) && HasEntityBlip(personalCar);
            result.MissionCarAvailable = Valid(missionCar) && HasEntityBlip(missionCar);
            result.HomeRoute = HasMissionRouteNear(Home);
            result.DealerRoute = HasMissionRouteNear(Dealer);
            result.LamarInPlayerCar = result.InCar && Valid(lamar) && lamar.IsInVehicle(player.CurrentVehicle);
            result.LamarPlayback = Valid(lamarCar) && Function.Call<bool>(Hash.IS_PLAYBACK_GOING_ON_FOR_VEHICLE, lamarCar.Handle);
            foreach (string label in Objectives)
            {
                Function.Call(Hash.BEGIN_TEXT_COMMAND_IS_MESSAGE_DISPLAYED, label);
                if (!Function.Call<bool>(Hash.END_TEXT_COMMAND_IS_MESSAGE_DISPLAYED)) continue;
                result.Objective = label;
                if (lastObservedLabel != label)
                {
                    lastObservedLabel = label;
                    localizedObjective = ChatOverlay.Clean(Game.GetLocalizedString(label));
                }
                break;
            }
            return result;
        }

        private static int Counter(string name)
        {
            var value = new OutputArgument();
            return Function.Call<bool>(Hash.STAT_GET_INT, HashName(name), value, -1) ? value.GetResult<int>() : -1;
        }
        private static int HashName(string name) { return unchecked((int)StringHash.AtStringHash(name)); }
        private static bool CanActNow()
        {
            return !Game.IsPaused && !Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE) && !Game.Player.Character.IsDead &&
                Game.Player.CanControlCharacter && !Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) &&
                !Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS) && !Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT) &&
                !Function.Call<bool>(Hash.IS_SCREEN_FADING_OUT);
        }
        private static bool Valid(Entity entity) { return entity != null && entity.Exists() && !entity.IsDead; }
        private static bool MissionVehicle(Vehicle car)
        { return Valid(car) && car.IsDriveable && Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, car.Handle); }
        private static bool IsDriver(Ped player, Vehicle car)
        { return Valid(car) && Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, car.Handle, -1, false) == player.Handle; }
        private static bool HasEntityBlip(Entity entity)
        {
            if (!Valid(entity)) return false;
            int blip = Function.Call<int>(Hash.GET_BLIP_FROM_ENTITY, entity.Handle);
            return Function.Call<bool>(Hash.DOES_BLIP_EXIST, blip) && Function.Call<int>(Hash.GET_BLIP_ALPHA, blip) > 0;
        }

        private void SetTask(Ped player, string key, Vector3 target)
        { ownedPed = player; ownsTask = true; taskKey = key; taskTarget = target; lastTask = Game.GameTime; }
        private bool NeedsTask(string key, Vector3 target, int interval)
        { return taskKey != key || (MissionFlow.Elapsed(Game.GameTime, lastTask) > interval && taskTarget.DistanceTo(target) > 3f); }
        private void ReleaseTask(bool clear)
        {
            if (clear && ownsTask && Valid(ownedPed) && ownedPed.Handle == Game.Player.Character.Handle)
                Function.Call(Hash.CLEAR_PED_TASKS, ownedPed.Handle);
            ownedPed = null; ownsTask = false; taskKey = ""; controls = null;
        }
        private void Walk(Ped player, Vector3 target, string key)
        {
            if (!NeedsTask(key, target, 1800)) return;
            SetTask(player, key, target);
            Function.Call(Hash.TASK_FOLLOW_NAV_MESH_TO_COORD, player.Handle, target.X, target.Y, target.Z, 1.5f, -1, 2f, 0, 0f);
        }
        private void Enter(Ped player, Vehicle car, string key)
        {
            if (!MissionVehicle(car) || car.Position.DistanceTo(player.Position) > 200 || player.IsInVehicle(car)) return;
            if (!NeedsTask(key, car.Position, 18000) && MissionFlow.Elapsed(Game.GameTime, lastTask) < 22000) return;
            SetTask(player, key, car.Position);
            Function.Call(Hash.TASK_ENTER_VEHICLE, player.Handle, car.Handle, 20000, -1, 1.5f, 1, 0);
        }

        private void Drive(Ped player, Vehicle car, Vector3 target, string key, bool direct)
        {
            if (!IsDriver(player, car)) return;
            float distance = car.Position.DistanceTo(target);
            // GTA takes control in the actual arrival trigger; do not stop 22 metres early.
            if (distance < (direct ? 3f : 2f)) { Hold(player, car); return; }
            if (distance < 35 || direct)
            {
                ReleaseTask(true); inputDrive = true;
                controls = MissionSteering.Follow(car.ForwardVector.X, car.ForwardVector.Y, target.X - car.Position.X, target.Y - car.Position.Y, car.Speed,
                    Math.Min(direct ? 7f : 10f, Math.Max(2f, distance * .35f)));
                return;
            }
            if (!NeedsTask(key, target, 4000)) return;
            SetTask(player, key, target);
            Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE, player.Handle, car.Handle, target.X, target.Y, target.Z, 20f, 786603, 3f);
        }
        private void Hold(Ped player, Vehicle car)
        {
            ReleaseTask(true); inputDrive = true; controls = new SteeringCommand { Brake = car.Speed > .5f ? 1 : 0, Handbrake = 1 };
        }

        private bool LoadRoute()
        {
            if (route.Count > 0) return true;
            if (!Function.Call<bool>(Hash.HAS_VEHICLE_RECORDING_BEEN_LOADED, RouteNumber, Recording)) return false;
            float duration = Function.Call<float>(Hash.GET_TOTAL_DURATION_OF_VEHICLE_RECORDING, RouteNumber, Recording);
            if (duration < 20000 || duration > 600000 || Single.IsNaN(duration)) return false;
            for (float time = 0; time <= duration; time += 400)
            {
                Vector3 point = Function.Call<Vector3>(Hash.GET_POSITION_OF_VEHICLE_RECORDING_AT_TIME, RouteNumber, time, Recording);
                if (point == Vector3.Zero || Single.IsNaN(point.X) || Single.IsNaN(point.Y)) { route.Clear(); return false; }
                route.Add(point);
            }
            WriteLog("route_loaded");
            return route.Count > 1;
        }

        private bool FollowRecordedRoute(Ped player, Vehicle car)
        {
            int nearest = routeIndex; float best = Single.MaxValue;
            int start = routeJoined ? Math.Max(0, routeIndex - 3) : 0;
            int end = routeJoined ? Math.Min(route.Count, routeIndex + 50) : route.Count;
            if (!routeJoined && Valid(lamarCar) && Function.Call<bool>(Hash.IS_PLAYBACK_GOING_ON_FOR_VEHICLE, lamarCar.Handle))
            {
                float time = Function.Call<float>(Hash.GET_TIME_POSITION_IN_RECORDING, lamarCar.Handle);
                end = Math.Min(end, Math.Max(2, (int)(time / 400f) + 8));
                start = Math.Max(0, end - 250);
            }
            for (int i = start; i < end; i++)
            {
                float distance = route[i].DistanceTo(car.Position);
                if (distance < best) { best = distance; nearest = i; }
            }
            // Never pull the car across an unrelated road or between parking levels.
            if (best > 35) return false;
            routeIndex = routeJoined ? Math.Max(routeIndex, nearest) : nearest; routeJoined = true;
            float lookahead = 7f + Math.Min(18f, car.Speed * .55f);
            int targetIndex = routeIndex;
            float along = route[routeIndex].DistanceTo(car.Position);
            while (targetIndex + 1 < route.Count && along < lookahead)
            { along += route[targetIndex].DistanceTo(route[targetIndex + 1]); targetIndex++; }
            Vector3 target = route[targetIndex];
            float lamarDistance = lamarCar.Position.DistanceTo(car.Position);
            float desired = Math.Max(9f, Math.Min(29f, lamarCar.Speed + (lamarDistance - 16) * .3f));
            if (lamarDistance < 9f) desired = Math.Max(0, lamarCar.Speed - 3);
            ReleaseTask(true); inputDrive = true;
            controls = MissionSteering.Follow(car.ForwardVector.X, car.ForwardVector.Y, target.X - car.Position.X, target.Y - car.Position.Y, car.Speed, desired);
            return true;
        }

        private void Evade(Ped player, Vehicle car)
        {
            if (!IsDriver(player, car)) { Enter(player, missionCar, "mission_car"); return; }
            if (Function.Call<bool>(Hash.ARE_PLAYER_STARS_GREYED_OUT, Game.Player.Handle)) { Hold(player, car); return; }
            if (taskKey == "evade" && MissionFlow.Elapsed(Game.GameTime, lastTask) < 10000) return;
            Ped cop = World.GetNearbyPeds(player.Position, 220f).Where(p => Valid(p) && Function.Call<int>(Hash.GET_PED_TYPE, p.Handle) == 6)
                .OrderBy(p => p.Position.DistanceTo(player.Position)).FirstOrDefault();
            Vector3 away = cop != null ? car.Position - cop.Position : car.ForwardVector;
            away.Z = 0;
            if (away.Length() < 1) away = car.ForwardVector;
            away.Normalize();
            Vector3 escape = World.GetNextPositionOnStreet(car.Position + away * 300f);
            if (escape == Vector3.Zero) return;
            SetTask(player, "evade", escape);
            Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE, player.Handle, car.Handle, escape.X, escape.Y, escape.Z, 24f, 786468, 8f);
        }

        private void WatchProgress(Ped player, Vehicle car)
        {
            if (player.Position.DistanceTo(lastPosition) > 3f)
            { lastMoved = Game.GameTime; lastPosition = player.Position; }
            if (flow.Step == MissionStep.Observe || flow.Step == MissionStep.Armed || flow.Step == MissionStep.AwaitResult ||
                flow.Step == MissionStep.WaitForLamar || (flow.Step == MissionStep.EvadePolice && Function.Call<bool>(Hash.ARE_PLAYER_STARS_GREYED_OUT, Game.Player.Handle)))
            { lastMoved = Game.GameTime; return; }
            if (inputDrive && recoveryUntil != 0 && MissionFlow.Elapsed(recoveryUntil, Game.GameTime) > 0)
            { controls = new SteeringCommand { Brake = .65f, Steering = -.5f }; return; }
            if (MissionFlow.Elapsed(Game.GameTime, lastMoved) < 25000) return;
            if (inputDrive && IsDriver(player, car) && recoveryCount == 0 && Function.Call<bool>(Hash.IS_VEHICLE_ON_ALL_WHEELS, car.Handle))
            {
                recoveryCount++; recoveryUntil = Game.GameTime + 1800; lastRecovery = Game.GameTime; lastMoved = Game.GameTime;
                controls = new SteeringCommand { Brake = .65f, Steering = -.5f }; WriteLog("reverse_recovery"); return;
            }
            if (MissionFlow.Elapsed(Game.GameTime, lastRecovery) < 4000) return;
            WriteLog("blocked"); Cancel(); say("Mission Pilot is blocked. You have control. The last observations were saved to the local mission log; restart the pilot after repositioning.");
        }

        public void ApplyInput()
        {
            if (!Active || !inputDrive || controls == null || !CanActNow()) return;
            // The mission may disable driving for a synchronized arrival before disabling player control.
            foreach (var value in new[] { new { Id = 59, Value = controls.Steering }, new { Id = 71, Value = controls.Throttle }, new { Id = 72, Value = controls.Brake }, new { Id = 76, Value = controls.Handbrake } })
            {
                bool allowed = value.Id == 59 ? steeringAllowed : value.Id == 71 ? throttleAllowed : value.Id == 72 ? brakeAllowed : handbrakeAllowed;
                if (!allowed) continue;
                Function.Call(Hash.ENABLE_CONTROL_ACTION, 0, value.Id, true);
                Function.Call<bool>(Hash.SET_CONTROL_VALUE_NEXT_FRAME, 0, value.Id, value.Value);
            }
        }

        private static Vector3 MissionTargetNear(Vector3 expected)
        {
            int blip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, 1);
            for (int i = 0; i < 128 && Function.Call<bool>(Hash.DOES_BLIP_EXIST, blip); i++)
            {
                int color = Function.Call<int>(Hash.GET_BLIP_COLOUR, blip);
                Vector3 position = Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD, blip);
                if ((color == 5 || color == 66 || color == 60) && position.DistanceTo(expected) < 30) return position;
                blip = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, 1);
            }
            return expected;
        }
        private static bool HasMissionRouteNear(Vector3 expected)
        {
            int blip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, 1);
            for (int i = 0; i < 128 && Function.Call<bool>(Hash.DOES_BLIP_EXIST, blip); i++)
            {
                Vector3 position = Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD, blip);
                int color = Function.Call<int>(Hash.GET_BLIP_COLOUR, blip);
                if ((color == 5 || color == 66 || color == 60) && position.DistanceTo(expected) < 30 &&
                    Function.Call<int>(Hash.GET_BLIP_ALPHA, blip) > 0 && Function.Call<bool>(Hash.DOES_BLIP_HAVE_GPS_ROUTE, blip)) return true;
                blip = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, 1);
            }
            return false;
        }

        private void WriteLog(string reason)
        {
            try
            {
                var player = Game.Player.Character;
                var position = player.Position;
                var record = new { utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), gameTime = Game.GameTime, reason = reason,
                    profile = "franklin_lamar", stage = flow.Step.ToString(), objective = flow.ObjectiveKey, observedText = localizedObjective,
                    scriptRunning = observation.ScriptRunning, canAct = observation.CanAct, passed = observation.PassedCount, failed = observation.FailedCount,
                    wanted = observation.Wanted, position = new { x = position.X, y = position.Y, z = position.Z },
                    lamar = Valid(lamar), lamarInCar = observation.LamarInPlayerCar, routePoint = routeIndex, routePoints = route.Count,
                    markedMissionCar = observation.MissionCarAvailable, markedPersonalCar = observation.PersonalCarAvailable,
                    homeRoute = observation.HomeRoute, dealerRoute = observation.DealerRoute, driver = observation.IsDriver,
                    inputs = new { steeringAllowed, throttleAllowed, brakeAllowed, handbrakeAllowed } };
                File.AppendAllText(logPath, new JavaScriptSerializer().Serialize(record) + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
