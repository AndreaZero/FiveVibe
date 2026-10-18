using System;
using System.Globalization;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace FiveVibe
{
    // Runs only on the SHVDN script thread. NPC tasks, models and story globals
    // are never overwritten: movement belongs to the player, inputs go to GTA.
    public sealed class WorldActions
    {
        private readonly Action<string> say;
        private readonly Action closeChat;
        private string state = "idle", switchActor, pulseAction;
        private Ped player, selectedNpc;
        private CommandPlan activePlan, afterSwitch;
        private Vector3 goal, lastPosition;
        private int started, lastMove, pulseControl = -1;
        private bool ownsTask, greetingPlayed;
        private CommandPlan completedSwitchPlan;
        private readonly CharacterSwitchMonitor switchMonitor=new CharacterSwitchMonitor();
        private bool switchWaitingAnnounced;
        public bool Active { get { return state != "idle"; } }
        public bool Switching { get { return state == "switching"; } }
        public bool BlocksMovement { get { return Active && state != "pulse" && state != "switching"; } }
        public string GoalLabel { get; private set; }
        public string Status
        {
            get
            {
                switch (state)
                {
                    case "switching": return "Switching character";
                    case "pulse": return pulseAction == "interact" ? "Interaction" : "Phone";
                    case "exit_brake": return "Stopping to get out";
                    case "exit_walk": return "Leaving vehicle";
                    case "wander": return "Taking a stroll";
                    case "approach": return "Approaching person";
                    case "follow": return "Following person";
                    case "greeting": return "Saying hello";
                    default: return "On foot";
                }
            }
        }

        public WorldActions(Action<string> reply, Action close) { say = reply; closeChat = close; }

        public static string ActorName(Ped ped)
        {
            int hash = ped.Model.Hash;
            return hash == unchecked((int)PedHash.Michael) ? "michael" : hash == unchecked((int)PedHash.Franklin) ? "franklin" :
                hash == unchecked((int)PedHash.Trevor) ? "trevor" : "current character";
        }

        public void Switch(string actor, CommandPlan deferred)
        {
            Cancel();
            if (Function.Call<bool>(Hash.GET_MISSION_FLAG) || !Game.Player.CanControlCharacter)
            { say("The game does not allow character switching right now."); return; }
            int control = GameplayRules.SwitchControl(actor);
            if (control < 0) { say("Choose Michael, Franklin or Trevor."); return; }
            switchActor = actor;
            afterSwitch = deferred;
            pulseControl = control;
            started = Game.GameTime;
            state = "switching";
            GoalLabel = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(actor);
            closeChat();
            say("Requesting a switch to " + GoalLabel + ".");
        }

        public CommandPlan TakeAfterSwitch()
        {
            var result = completedSwitchPlan;
            completedSwitchPlan = null;
            return result;
        }

        public void Start(CommandPlan plan)
        {
            Cancel();
            if (plan.Action == "switch_character") { Switch(plan.Actor, null); return; }
            player = Game.Player.Character;
            activePlan = plan;
            if (plan.Action.StartsWith("phone_", StringComparison.Ordinal) || plan.Action == "interact")
            {
                bool visible = Function.Call<bool>(Hash.CAN_PHONE_BE_SEEN_ON_SCREEN);
                if (plan.Action == "phone_open" && visible) { say("The phone is already open."); return; }
                pulseControl = plan.Action == "interact" ? 51 : plan.Action == "phone_hangup" ? 177 : 27;
                pulseAction = plan.Action;
                state = "pulse";
                started = Game.GameTime;
                GoalLabel = plan.Action == "interact" ? "Game interaction" : "Phone";
                closeChat();
                return;
            }
            if (player.IsInVehicle())
            {
                var car = player.CurrentVehicle;
                if (car.Speed > 1f)
                {
                    if (Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, car.Handle, -1, false) != player.Handle)
                    { say("Wait until the car stops before getting out."); return; }
                    Function.Call(Hash.TASK_VEHICLE_TEMP_ACTION, player.Handle, car.Handle, 27, 8000);
                    state = "exit_brake";
                }
                else { player.Task.LeaveVehicle(car, GTA.LeaveVehicleFlags.None); state = "exit_walk"; }
                ownsTask = true;
                started = Game.GameTime;
                GoalLabel = "Leaving vehicle";
                say("Stopping and getting out before continuing on foot.");
                return;
            }
            StartOnFoot(plan);
        }

        private void StartOnFoot(CommandPlan plan)
        {
            started = lastMove = Game.GameTime;
            lastPosition = player.Position;
            if (plan.Action == "walk_wander")
            {
                player.Task.WanderAround(player.Position, 35f, 0f, 4f);
                ownsTask = true;
                state = "wander";
                GoalLabel = "Strolling nearby";
                say("Stroll started. Ask me to stop whenever you like.");
                return;
            }
            if (plan.Action.EndsWith("_npc", StringComparison.Ordinal))
            {
                selectedNpc = FindNpc(plan.Target);
                if (!UsableNpc(selectedNpc)) { say("I cannot find that person nearby. Move closer or ask for the person ahead."); return; }
                goal = selectedNpc.Position;
                GoalLabel = "Person " + (plan.Target == "ahead" ? "ahead" : "nearby");
                if (plan.Action == "follow_npc")
                {
                    player.Task.FollowToOffsetFromEntity(selectedNpc, new Vector3(0f, -2f, 0f), GameplayRules.WalkSpeed(plan.Pace), -1, 2.5f, true);
                    ownsTask = true;
                    state = "follow";
                    say("Following the person at a comfortable distance.");
                }
                else { IssueWalk(goal, plan.Pace); state = "approach"; say("Approaching the person."); }
                return;
            }
            if (plan.Action == "walk_waypoint")
            {
                if (!TryWaypoint(out goal)) { say("Set a waypoint on the map first."); return; }
                // Map waypoints can have Z=0. Resolve loaded ground before
                // requesting a pedestrian route, without moving the player.
                float groundHeight;
                if (World.GetGroundHeight(new Vector3(goal.X, goal.Y, 1000f), out groundHeight, GetGroundHeightMode.Normal))
                    goal.Z = groundHeight;
                GoalLabel = "Waypoint";
            }
            else if (plan.Action == "walk_place") { goal = FiveVibeScript.PlacePosition(plan.Place); GoalLabel = FiveVibeScript.PlaceLabel(plan.Place); }
            else
            {
                goal = player.GetOffsetPosition(new Vector3(0f, plan.DistanceMeters, 0f));
                GoalLabel = plan.DistanceMeters + " metres forward";
            }
            StartWalking(goal, GoalLabel, plan.Pace);
        }

        public void WalkToMission(Vector3 destination, string label)
        {
            Cancel();
            player = Game.Player.Character;
            activePlan = CommandPlan.Create("mission", "current", 45);
            started = lastMove = Game.GameTime;
            lastPosition = player.Position;
            StartWalking(destination, "Mission marker: " + label, "walk");
        }

        private void StartWalking(Vector3 destination, string label, string pace)
        {
            goal = destination;
            GoalLabel = label;
            if (player.Position.DistanceTo(goal) > 600f)
            { say("That destination is over 600 metres away. Choose a closer point for walking, or drive there."); return; }
            IssueWalk(goal, pace);
            state = "walking";
            say("Walking to " + label + ".");
        }

        private void IssueWalk(Vector3 position, string pace)
        {
            // -1 disables the task's warp timer. An unreachable route is reported.
            player.Task.FollowNavMeshTo(position, new PedMoveBlendRatio(GameplayRules.WalkSpeed(pace)), -1, .75f,
                FollowNavMeshFlags.KeepToPavements | FollowNavMeshFlags.NeverEnterWater, 40000f);
            ownsTask = true;
        }

        public void Tick()
        {
            if (!Active) return;
            int elapsed = Game.GameTime - started;
            if (Switching)
            {
                bool inProgress = Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS);
                bool sceneReady=!Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT) && !Function.Call<bool>(Hash.IS_SCREEN_FADING_OUT) &&
                    !Function.Call<bool>(Hash.IS_SCREEN_FADING_IN) && !Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE);
                var result=switchMonitor.Observe(elapsed,ActorName(Game.Player.Character)==switchActor,inProgress,Game.Player.CanControlCharacter,sceneReady);
                if (result==CharacterSwitchResult.Confirmed)
                {
                    var deferred = afterSwitch;
                    string name = GoalLabel;
                    Cancel(false);
                    completedSwitchPlan = deferred;
                    say("You are now playing as " + name + ".");
                }
                else if(result==CharacterSwitchResult.Unconfirmed)
                    Finish("Could not confirm the character switch within two minutes. GTA may still finish it; no follow-up action will start.");
                else if(elapsed>=15000 && !switchWaitingAnnounced)
                { switchWaitingAnnounced=true; say("Still waiting for GTA to confirm the character switch. F8 cancels the wait and any follow-up action."); }
                return;
            }
            if (player == null || !player.Exists() || player.IsDead || player.Handle != Game.Player.Character.Handle)
            { Finish("Character changed: action stopped."); return; }
            if (state == "pulse")
            {
                if (elapsed < 1000) return;
                string action = pulseAction;
                Cancel(false);
                if (action == "interact") say("Requested the game's available interaction here.");
                else if (action == "phone_answer") say(Function.Call<bool>(Hash.IS_MOBILE_PHONE_CALL_ONGOING) ? "Call in progress." : "Requested the phone to answer. No active call is reported.");
                else if (action == "phone_hangup") say(!Function.Call<bool>(Hash.CAN_PHONE_BE_SEEN_ON_SCREEN) ? "Phone closed." : "Requested phone close / hang up.");
                else say(Function.Call<bool>(Hash.CAN_PHONE_BE_SEEN_ON_SCREEN) ? "Phone open." : "The phone is unavailable right now.");
                return;
            }
            if (state == "exit_brake")
            {
                if (!player.IsInVehicle()) ContinueAfterExit();
                else if (player.CurrentVehicle.Speed < .8f) { player.Task.LeaveVehicle(player.CurrentVehicle, GTA.LeaveVehicleFlags.None); state = "exit_walk"; started = Game.GameTime; }
                else if (elapsed > 9000) Finish("Unable to stop the car to get out. You have control.");
                return;
            }
            if (state == "exit_walk")
            {
                if (!player.IsInVehicle()) ContinueAfterExit();
                else if (elapsed > 10000) Finish("Unable to leave the vehicle. You have control.");
                return;
            }
            if (player.IsInVehicle()) { Finish("You entered a vehicle: walking stopped."); return; }
            if (state == "follow" || state == "approach" || state == "greeting")
            {
                if (!UsableNpc(selectedNpc) || selectedNpc.IsInVehicle() || selectedNpc.Position.DistanceTo(player.Position) > 80f)
                { Finish("The person is no longer reachable on foot."); return; }
                if (state == "approach" && player.Position.DistanceTo(selectedNpc.Position) < 3f)
                {
                    if (activePlan.Action == "greet_npc")
                    {
                        player.Task.TurnTo(selectedNpc, 900);
                        state = "greeting";
                        greetingPlayed = false;
                        started = Game.GameTime;
                    }
                    else Finish("You are near the person.");
                    return;
                }
                if (state == "approach" && goal.DistanceTo(selectedNpc.Position) > 3f)
                { goal = selectedNpc.Position; IssueWalk(goal, activePlan.Pace); }
                if (state == "greeting")
                {
                    if (!greetingPlayed && elapsed > 900) { player.PlayAmbientSpeech("GENERIC_HI", SpeechModifier.ForceNormal); greetingPlayed = true; }
                    if (elapsed > 4000) Finish("Reached the person and attempted a native greeting.");
                    return;
                }
            }
            if (state == "walking" && player.Position.DistanceTo(goal) < 2.5f) { Finish("Destination reached. You have control."); return; }
            if ((state == "walking" || state == "approach") && elapsed > 2000 && player.GetNavMeshRouteResult() == NavMeshRouteResult.RouteNotFound)
            { Finish("The game cannot find a walking route there. You have control."); return; }
            if (player.Position.DistanceTo(lastPosition) > 1.5f) { lastPosition = player.Position; lastMove = Game.GameTime; }
            bool followingNearby = state == "follow" && selectedNpc.Position.DistanceTo(player.Position) < 6f;
            if (!followingNearby && Game.GameTime - lastMove > 30000) { Finish("The route appears blocked. You have control."); return; }
            if (state != "follow" && elapsed > (state == "wander" ? 180000 : 600000)) Finish("Movement finished. You have control.");
        }

        private void ContinueAfterExit()
        {
            var plan = activePlan;
            Cancel();
            Start(plan);
        }

        public void ApplyInput()
        {
            int duration = Switching ? 850 : 180;
            if ((state != "switching" && state != "pulse") || pulseControl < 0 || Game.GameTime - started > duration) return;
            if (Switching && Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS)) return;
            foreach (int group in new[] { 0, 2 })
            {
                Function.Call(Hash.ENABLE_CONTROL_ACTION, group, pulseControl, true);
                Function.Call<bool>(Hash.SET_CONTROL_VALUE_NEXT_FRAME, group, pulseControl, 1f);
            }
        }

        public void Cancel(bool clearOwnedTask = true)
        {
            if (clearOwnedTask && ownsTask && player != null && player.Exists() && player.Handle == Game.Player.Character.Handle)
                Function.Call(Hash.CLEAR_PED_TASKS, player.Handle);
            state = "idle";
            ownsTask = false;
            player = null;
            activePlan = afterSwitch = completedSwitchPlan = null;
            switchMonitor.Reset();
            switchWaitingAnnounced=false;
            pulseControl = -1;
        }

        private void Finish(string message) { Cancel(); say(message); }
        private static bool UsableNpc(Ped npc)
        { return npc != null && npc.Exists() && npc.IsHuman && !npc.IsDead && npc.Handle != Game.Player.Character.Handle && !npc.IsInCombat; }
        private Ped FindNpc(string target)
        {
            if (target == "current") return UsableNpc(selectedNpc) && selectedNpc.Position.DistanceTo(Game.Player.Character.Position) < 80f ? selectedNpc : null;
            var ped = Game.Player.Character;
            return World.GetNearbyPeds(ped.Position, 30f).Where(npc => UsableNpc(npc) && !npc.IsInVehicle() &&
                (target != "ahead" || ped.GetPositionOffset(npc.Position).Y > 1f))
                .OrderBy(npc => npc.Position.DistanceTo(ped.Position)).FirstOrDefault();
        }

        public string Context()
        {
            var nearby = FindNpc("nearest");
            var ahead = FindNpc("ahead");
            Vector3 missionPosition;
            string missionLabel;
            bool mission = TryMissionTarget("nearest", out missionPosition, out missionLabel);
            return string.Format(CultureInfo.InvariantCulture,
                "; on_foot_action={0}; mission_active={1}; phone_visible={2}; call_ongoing={3}; nearest_npc={4}; npc_ahead={5}; previous_npc_available={6}; available_mission={7}",
                state, Function.Call<bool>(Hash.GET_MISSION_FLAG), Function.Call<bool>(Hash.CAN_PHONE_BE_SEEN_ON_SCREEN),
                Function.Call<bool>(Hash.IS_MOBILE_PHONE_CALL_ONGOING), nearby == null ? "none" : Math.Round(nearby.Position.DistanceTo(Game.Player.Character.Position)) + " metres",
                ahead == null ? "none" : Math.Round(ahead.Position.DistanceTo(Game.Player.Character.Position)) + " metres", UsableNpc(selectedNpc),
                mission ? missionLabel + " at " + Math.Round(missionPosition.DistanceTo(Game.Player.Character.Position)) + " metres" : "none detected");
        }

        public static bool TryWaypoint(out Vector3 position)
        {
            int blip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, 8);
            if (!Function.Call<bool>(Hash.DOES_BLIP_EXIST, blip)) { position = Vector3.Zero; return false; }
            position = Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD, blip);
            return true;
        }

        public static bool TryMissionTarget(string contact, out Vector3 position, out string label)
        {
            int[] sprites = { 76, 77, 78, 79, 80, 86, 88, 89, 104, 105, 106, 107, 118, 120 };
            string[] labels = { "Simeon", "Lester", "Michael", "Trevor", "Jewelry Store", "Lamar", "Franklin", "Cheng", "Port", "FIB", "FIB", "Big Score", "Paleto", "Solomon" };
            string actor = ActorName(Game.Player.Character);
            int expectedColor = actor == "michael" ? 42 : actor == "franklin" ? 43 : actor == "trevor" ? 44 : -1;
            float closest = float.MaxValue;
            position = Vector3.Zero;
            label = "";
            for (int i = 0; i < sprites.Length; i++)
            {
                if (contact != "nearest" && !labels[i].Equals(contact, StringComparison.OrdinalIgnoreCase)) continue;
                int handle = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, sprites[i]);
                for (int guard = 0; guard < 64 && Function.Call<bool>(Hash.DOES_BLIP_EXIST, handle); guard++)
                {
                    int color = Function.Call<int>(Hash.GET_BLIP_COLOUR, handle);
                    if (color != 39 && (!(color >= 42 && color <= 44) || color == expectedColor))
                    {
                        var candidate = Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD, handle);
                        float distance = candidate.DistanceTo(Game.Player.Character.Position);
                        if (distance < closest) { closest = distance; position = candidate; label = labels[i]; }
                    }
                    handle = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, sprites[i]);
                }
            }
            return closest < float.MaxValue;
        }
    }
}
