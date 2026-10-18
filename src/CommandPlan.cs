using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    public sealed class CommandPlan
    {
        public string Action { get; set; }
        public string Actor { get; set; }
        public string Place { get; set; }
        public int SpeedKmh { get; set; }
        public string Message { get; set; }
        public string Pace { get; set; }
        public string Target { get; set; }
        public int DistanceMeters { get; set; }
        public string WeatherType { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string Clock { get; set; }
        public string VehicleType { get; set; }
        public string Station { get; set; }

        public CommandPlan() { Pace = "walk"; Target = "nearest"; DistanceMeters = 25; WeatherType="none"; Hour=-1; Clock="unchanged"; VehicleType="any"; Station="none"; }

        public static readonly string[] Actions = { "cruise", "waypoint", "place", "speed", "stop", "leave", "help", "say",
            "walk_wander", "walk_forward", "walk_waypoint", "walk_place", "approach_npc", "greet_npc", "follow_npc",
            "switch_character", "phone_open", "phone_answer", "phone_hangup", "interact", "mission", "mission_status",
            "world_settings", "hud_show", "hud_hide", "hud_toggle", "mission_auto", "vehicle", "radio_set", "radio_off",
            "realtime_clock", "realtime_weather", "realtime_both", "realtime_off", "realtime_clock_off", "realtime_weather_off", "realtime_status", "camera_auto_on", "camera_auto_off",
            "camera_gameplay", "camera_first", "camera_third", "camera_cinema", "camera_native", "camera_cycle", "camera_next", "camera_bars", "camera_bars_on", "camera_bars_off" };
        public static readonly string[] WeatherTypes={"none","auto","clear","sunny","clouds","fog","overcast","rain","thunder","smog","clearing","snow","blizzard","christmas"};
        public static readonly string[] ClockModes={"unchanged","pause","resume"};
        public static readonly string[] Actors = { "current", "michael", "franklin", "trevor" };
        public static string[] Places { get { return PlaceCatalog.Ids; } }
        public static readonly string[] Paces = { "walk", "jog", "run" };
        public static readonly string[] Targets = { "nearest", "ahead", "current", "lester", "lamar", "simeon", "solomon" };

        public void Validate()
        {
            if (!Actions.Contains(Action) || !Actors.Contains(Actor) || !Places.Contains(Place))
                throw new ArgumentException("The plan contains an unsupported action.");
            if (SpeedKmh < 15 || SpeedKmh > 90)
                throw new ArgumentException("Speed must be between 15 and 90 km/h.");
            if(!VehicleSelection.Types.Contains(VehicleType) || (VehicleType!="any" && Action!="vehicle" && Action!="cruise" && Action!="place" && Action!="waypoint" && Action!="mission"))
                throw new ArgumentException("Vehicle selection does not match the action.");
            if ((Action == "place" || Action == "walk_place") != (Place != "none"))
                throw new ArgumentException("The destination does not match this action.");
            if (!Paces.Contains(Pace) || !Targets.Contains(Target) || DistanceMeters < 5 || DistanceMeters > 100)
                throw new ArgumentException("Unsupported movement parameters.");
            if (Action == "switch_character" && Actor == "current")
                throw new ArgumentException("Choose Michael, Franklin or Trevor to switch characters.");
            if (Action == "mission_auto" && Actor != "current" && Actor != "franklin")
                throw new ArgumentException("Mission Pilot uses the mission's own protagonist.");
            if (CameraCommands.IsCamera(Action) && Actor != "current") throw new ArgumentException("Camera controls use the current protagonist.");
            if (!RadioCatalog.Ids.Contains(Station) || ((Action=="radio_set") != (Station!="none")) || (RadioCommands.IsRadio(Action) && Actor!="current"))
                throw new ArgumentException("Radio station does not match the action or current protagonist.");
            if (Action.EndsWith("_npc", StringComparison.Ordinal) && Target != "nearest" && Target != "ahead" && Target != "current")
                throw new ArgumentException("Invalid NPC selection.");
            if (Action != "mission" && !Action.EndsWith("_npc", StringComparison.Ordinal) && Target != "nearest")
                throw new ArgumentException("This action does not use an NPC or mission target.");
            if (Action == "mission" && (Target == "ahead" || Target == "current"))
                throw new ArgumentException("Invalid mission selection.");
            if (!WeatherTypes.Contains(WeatherType) || !ClockModes.Contains(Clock) || Hour < -1 || Hour > 23 || Minute < 0 || Minute > 59 || (Hour==-1 && Minute!=0))
                throw new ArgumentException("Invalid weather or clock settings.");
            bool changesWorld=WeatherType!="none" || Hour!=-1 || Clock!="unchanged";
            if ((Action=="world_settings") != changesWorld) throw new ArgumentException("World settings do not match the action.");
            Message = (Message ?? "").Trim();
            if (Message.Length > 240) Message = Message.Substring(0, 240);
        }

        public static CommandPlan Create(string action, string actor, int speed, string place = "none", string message = "", string pace = "walk", string target = "nearest", int distance = 25)
        {
            var plan = new CommandPlan { Action = action, Actor = actor, SpeedKmh = speed, Place = place, Message = message, Pace = pace, Target = target, DistanceMeters = distance };
            plan.Validate();
            return plan;
        }
        public static CommandPlan World(string weather="none", int hour=-1, int minute=0, string clock="unchanged")
        {
            var plan=new CommandPlan { Action="world_settings", Actor="current", SpeedKmh=45, Place="none", WeatherType=weather, Hour=hour, Minute=minute, Clock=clock };
            plan.Validate(); return plan;
        }
    }

    public static class LocalPlanner
    {
        public static string Normalize(string input)
        {
            string decomposed = (input ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var text = new StringBuilder();
            foreach (char c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) text.Append(c);
            return Regex.Replace(text.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
        }

        public static bool IsImmediateStop(string input)
        {
            string text = Normalize(input);
            return Regex.IsMatch(text, @"^(?:ok[ ,]+)?(?:stop|cancel|take over|give me control|basta|annulla|fermati|ferma(?:ti)?(?: l[' ]?auto| la macchina)?|smetti(?: di guidare)?|riprendo (?:io|il controllo))[.! ]*$");
        }

        public static CommandPlan Parse(string input, int currentSpeed)
        {
            var plan=ParseCore(input,currentSpeed);
            if(plan.Action=="vehicle" || plan.Action=="cruise" || plan.Action=="place" || plan.Action=="waypoint" || plan.Action=="mission")
                plan.VehicleType=VehicleSelection.FromText(input);
            plan.Validate(); return plan;
        }

        private static CommandPlan ParseCore(string input, int currentSpeed)
        {
            string text = Normalize(input);
            int speed = Math.Max(15, Math.Min(90, currentSpeed));
            string actor = Has(text, "michael|micheal") ? "michael" : Has(text, "franklin") ? "franklin" : Has(text, "trevor") ? "trevor" : "current";
            if (IsImmediateStop(text)) return CommandPlan.Create("stop", "current", speed);
            CommandPlan radioPlan;
            if (RadioCommands.TryParse(text,out radioPlan)) return radioPlan;
            CommandPlan cameraPlan;
            if (CameraCommands.TryParse(text,out cameraPlan)) return cameraPlan;
            CommandPlan realPlan;
            if(RealWorldCommands.TryPlan(text,out realPlan)) return realPlan;
            CommandPlan worldPlan;
            if (WorldControlParser.TryParse(text,out worldPlan)) return worldPlan;
            if (Has(text, "non|mai|senza|poi|oppure|not|never|then|or") || Has(text, "spara|uccidi|compra|vola|elicottero|aereo|teletrasporta|spawn|insegui|shoot|kill|teleport"))
                return Say("In local mode, try a simple request: cruise, drive to Paleto Bay, walk to the waypoint, or stop.");
            if (Has(text, "aiuto|help|comandi|ciao")) return CommandPlan.Create("help", actor, speed);
            bool namedPilot = Regex.IsMatch(text, @"\bfranklin (?:and|e|&) lamar\b");
            bool pilotRequest = (Has(text, "mission pilot|autopilot|pilota missione") && Has(text, "avvia|inizia|attiva|start|enable|begin|play")) ||
                (namedPilot && Has(text, "avvia|inizia|gioca|completa|finisci|start|play|complete|finish|drive|guida|segui"));
            if (pilotRequest && !MissionRequests.AllowsStart(text, false)) return Say("Mission Pilot currently supports Franklin and Lamar only. Say 'Start Mission Pilot for Franklin and Lamar'.");
            if (pilotRequest) return CommandPlan.Create("mission_auto", "current", speed);
            if (Has(text, "passa|cambia|switch|usa") && actor != "current") return CommandPlan.Create("switch_character", actor, speed);
            if (Has(text, "telefono|chiamata|telefonata|phone|call"))
                return CommandPlan.Create(Has(text, "rispondi|accetta|answer|accept") ? "phone_answer" : Has(text, "chiudi|riaggancia|termina|close|hang up|end") ? "phone_hangup" : "phone_open", actor, speed);
            if (Has(text, "missione|missioni|storia|mission|missions|story"))
            {
                if (Has(text, "finisci|completa|tutta|finish|complete|entire")) return Say("I can reach a mission marker and assist on request. Autonomous story completion is not available.");
                string contact = Has(text, "lester") ? "lester" : Has(text, "lamar") ? "lamar" : Has(text, "simeon") ? "simeon" : Has(text, "solomon") ? "solomon" : "nearest";
                bool status = Has(text, "stato|info|obiettivo|storia|status|story|which|what");
                return CommandPlan.Create(status ? "mission_status" : "mission", actor, speed, "none", "", "walk", status ? "nearest" : contact);
            }
            string npcTarget = Has(text, "davanti|fronte|ahead|front") ? "ahead" : Has(text, "quello|seguilo|salutalo|them again") ? "current" : "nearest";
            if (Has(text, "saluta|salutalo|parla|greet|say hi|say hello|talk") && Has(text, "npc|persona|uomo|donna|pedone|tizio|quello|salutalo|person|pedestrian|man|woman"))
                return CommandPlan.Create("greet_npc", actor, speed, "none", "", "walk", npcTarget);
            if (Has(text, "segui|seguilo|follow") && Has(text, "npc|persona|uomo|donna|pedone|tizio|quello|seguilo|person|pedestrian|them"))
                return CommandPlan.Create("follow_npc", actor, speed, "none", "", "walk", npcTarget);
            if (Has(text, "avvicinati|raggiungi|approach") && Has(text, "npc|persona|uomo|donna|pedone|tizio|quello|person|pedestrian"))
                return CommandPlan.Create("approach_npc", actor, speed, "none", "", "walk", npcTarget);
            if (Has(text, "interagisci|interact")) return CommandPlan.Create("interact", actor, speed);

            var number = Regex.Match(text, @"\b(\d{1,3})\s*(?:km\s*/?\s*h|kmh|chilometri(?: orari)?)\b");
            if (number.Success)
            {
                speed = int.Parse(number.Groups[1].Value, CultureInfo.InvariantCulture);
                if (speed < 15 || speed > 90) return Say("Choose a speed between 15 and 90 km/h.");
            }
            else if (Has(text, "rallenta|piano|lentamente|slower|slow down")) speed = Math.Max(15, speed - 10);
            else if (Has(text, "accelera|veloce|faster|speed up")) speed = Math.Min(90, speed + 10);

            var mentioned = PlaceCatalog.Match(text);
            int destinations = mentioned.Length + (Has(text, "segnaposto|waypoint|indicatore|marker") ? 1 : 0);
            if (destinations > 1) return Say("Choose one destination at a time.");
            string place = mentioned.FirstOrDefault() ?? "none";
            if (place.EndsWith("_home", StringComparison.Ordinal) && !Regex.IsMatch(text,@"^(?:michael|micheal|franklin|trevor)\s*[,!:]")) actor="current";
            // A destination is actionable only with a travel verb, not a casual mention.
            bool travel = Has(text, "vai|andiamo|andare|portami|porta|raggiungi|guidaci|guida|accompagna|torna|go|drive|take|head|walk|run|jog");
            bool onFoot = Has(text, "piedi|cammina|camminare|passeggia|passeggiata|corri|correre|walk|walking|stroll|on foot|run|jog");
            string pace = Has(text, "corri|correre|run") ? "run" : Has(text,"jog") ? "jog" : "walk";
            if (onFoot && Has(text, "segnaposto|waypoint|indicatore|marker")) return CommandPlan.Create("walk_waypoint", actor, speed, "none", "", pace);
            if (onFoot && place != "none") return CommandPlan.Create("walk_place", actor, speed, place, "", pace);
            if (onFoot)
            {
                var meters = Regex.Match(text, @"\b(\d{1,3})\s*(?:metri|meters|metres|m)\b");
                int distance = meters.Success ? int.Parse(meters.Groups[1].Value, CultureInfo.InvariantCulture) : 25;
                if (distance < 5 || distance > 100) return Say("Choose between 5 and 100 metres for a forward movement.");
                return CommandPlan.Create(Has(text, "avanti|corri|correre|forward|run|jog") || meters.Success ? "walk_forward" : "walk_wander", actor, speed, "none", "", pace, "nearest", distance);
            }
            if (travel && Has(text, "segnaposto|waypoint|indicatore|marker")) return CommandPlan.Create("waypoint", actor, speed);
            if (travel && place != "none") return CommandPlan.Create("place", actor, speed, place);
            if (Regex.IsMatch(text, @"\b(?:scendi|esci|uscire|get out|leave|exit)\b.*\b(?:auto|macchina|veicolo|car|vehicle)\b") || text == "scendi")
                return CommandPlan.Create("leave", actor, speed);
            if (number.Success || Has(text, "rallenta|accelera|slower|faster|slow down|speed up")) return CommandPlan.Create("speed", actor, speed);
            if (Has(text, "giro|chill|gira|giretto|cruise|drive around"))
                return CommandPlan.Create("cruise", actor, speed);
            if(Has(text,"prendi|cerca|trova|find|get|take") && VehicleSelection.FromText(text)!="any") return CommandPlan.Create("vehicle",actor,speed);
            return Say("Request not recognized in local mode. Try: find a car and take a chill drive.");
        }

        private static bool Has(string text, string words) { return Regex.IsMatch(text, @"\b(?:" + words + @")\b"); }
        private static CommandPlan Say(string message) { return CommandPlan.Create("say", "current", 45, "none", message); }
    }

    public static class MissionRequests
    {
        // Model output cannot silently substitute this profile for another named mission.
        public static bool AllowsStart(string request, bool supportedMissionRunning)
        {
            string text = LocalPlanner.Normalize(request);
            if (Regex.IsMatch(text, @"\b(?:prologo|prologue|repossession|complications|entire story|whole campaign|tutta la storia)\b")) return false;
            if (Regex.IsMatch(text, @"\bfranklin\s+(?:and|e|&)\s+lamar\b")) return true;
            if (Regex.IsMatch(text, @"^(?:please\s+)?(?:start|enable|begin|avvia|attiva|inizia)\s+(?:(?:the|il)\s+)?(?:mission pilot|autopilot|pilota missione)[.! ]*$")) return true;
            return supportedMissionRunning && Regex.IsMatch(text, @"\b(?:questa missione|missione attiva|this mission|current mission)\b");
        }
        public static CommandPlan Constrain(string request, bool supportedMissionRunning, CommandPlan plan)
        {
            if (plan.Action != "mission_auto" || AllowsStart(request, supportedMissionRunning)) return plan;
            return CommandPlan.Create("say", "current", 45, "none", "Mission Pilot currently supports Franklin and Lamar only. Say 'Start Mission Pilot for Franklin and Lamar' to select that profile.");
        }
    }

    public static class GameplayRules
    {
        public static int SwitchControl(string actor)
        { return actor == "michael" ? 166 : actor == "franklin" ? 167 : actor == "trevor" ? 168 : -1; }
        public static float WalkSpeed(string pace) { return pace == "run" ? 3f : pace == "jog" ? 2f : 1f; }
        public static bool IsWorldAction(string action)
        {
            return action.StartsWith("walk_", StringComparison.Ordinal) || action.EndsWith("_npc", StringComparison.Ordinal) ||
                action.StartsWith("phone_", StringComparison.Ordinal) || action == "switch_character" || action == "interact";
        }
    }

    // A cancelled HTTP request can still finish. The generation prevents a late response
    // from taking control after F8 or after a newer command.
    public sealed class RequestGate
    {
        private int generation;
        public int Begin() { return ++generation; }
        public void Cancel() { ++generation; }
        public bool IsCurrent(int request) { return request == generation; }
    }
}
