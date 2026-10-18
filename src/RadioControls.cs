using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    public sealed class RadioStationInfo
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        internal string[] Aliases { get; private set; }
        internal RadioStationInfo(string id, string name, params string[] aliases)
        { Id=id; Name=name; Aliases=new[] { name }.Concat(aliases).ToArray(); }
    }

    public static class RadioCatalog
    {
        // Standard Story Mode stations. Do not unlock online-only or scripted stations.
        // Native identifiers follow SHVDN's Game.RadioStation mapping.
        private static readonly RadioStationInfo[] stations = {
            new RadioStationInfo("RADIO_01_CLASS_ROCK","Los Santos Rock Radio","los santos rock","ls rock radio"),
            new RadioStationInfo("RADIO_02_POP","Non-Stop-Pop FM","non stop pop","nonstop pop","nonstop pop fm"),
            new RadioStationInfo("RADIO_03_HIPHOP_NEW","Radio Los Santos"),
            new RadioStationInfo("RADIO_04_PUNK","Channel X"),
            new RadioStationInfo("RADIO_05_TALK_01","West Coast Talk Radio","wctr"),
            new RadioStationInfo("RADIO_06_COUNTRY","Rebel Radio"),
            new RadioStationInfo("RADIO_07_DANCE_01","Soulwax FM","soulwax"),
            new RadioStationInfo("RADIO_08_MEXICAN","East Los FM","east los"),
            new RadioStationInfo("RADIO_09_HIPHOP_OLD","West Coast Classics"),
            new RadioStationInfo("RADIO_11_TALK_02","Blaine County Radio"),
            new RadioStationInfo("RADIO_12_REGGAE","Blue Ark","the blue ark"),
            new RadioStationInfo("RADIO_13_JAZZ","Worldwide FM","world wide fm","worldwide"),
            new RadioStationInfo("RADIO_14_DANCE_02","FlyLo FM","flylo","fly lo fm"),
            new RadioStationInfo("RADIO_15_MOTOWN","The Lowdown 91.1","the lowdown","lowdown"),
            new RadioStationInfo("RADIO_16_SILVERLAKE","Radio Mirror Park","mirror park radio"),
            new RadioStationInfo("RADIO_17_FUNK","Space 103.2","space 103 2"),
            new RadioStationInfo("RADIO_18_90S_ROCK","Vinewood Boulevard Radio","vinewood boulevard"),
            new RadioStationInfo("RADIO_19_USER","Self Radio","selfradio"),
            new RadioStationInfo("RADIO_20_THELAB","The Lab","lab radio")
        };
        public static RadioStationInfo[] All { get { return (RadioStationInfo[])stations.Clone(); } }
        public static string[] Ids { get { return new[] {"none"}.Concat(stations.Select(s=>s.Id)).ToArray(); } }
        public static RadioStationInfo Find(string id) { return stations.FirstOrDefault(s=>s.Id==id); }
        public static string Describe() { return string.Join("; ",stations.Select(s=>s.Id+"="+s.Name)); }
    }

    public static class RadioCommands
    {
        public static bool IsRadio(string action) { return action=="radio_set" || action=="radio_off"; }
        private static string Clean(string text)
        { return Regex.Replace(Regex.Replace(LocalPlanner.Normalize(text),@"[^a-z0-9]+"," "),@"\s+"," ").Trim(); }
        public static bool TryParse(string input, out CommandPlan plan)
        {
            plan=null;
            string text=Clean(input);
            text=Regex.Replace(text,@"^(?:(?:please|per favore|puoi|potresti|can you|could you)\s+)+","");
            text=Regex.Replace(text,@"\s+(?:please|per favore)$","");
            text=Regex.Replace(text,@"\s+(?:e\s+)?(?:continua(?: pure)? a guidare|senza (?:fermare|interrompere) (?:il giro|la guida)|keep driving|without (?:stopping|interrupting) (?:the drive|the ride))$","");
            if(Regex.IsMatch(text,@"^(?:radio off|(?:turn|switch) (?:the )?radio off|(?:turn off|switch off|stop|mute|disable) (?:the )?radio|(?:spegni|disattiva|silenzia) (?:la )?radio|metti (?:la )?radio off)$"))
            { plan=CommandPlan.Create("radio_off","current",45); return true; }
            const string prefix=@"^(?:(?:metti|seleziona|sintonizza|cambia|ascolta|attiva|play|select|set|switch|tune|change|listen|put)(?:\s+(?:la|the|radio|station|stazione|su|on|to|a|alla|sulla|in))*\s+)?";
            foreach(var station in RadioCatalog.All)
            foreach(string alias in station.Aliases)
                if(Regex.IsMatch(text,prefix+Regex.Escape(Clean(alias))+"$"))
                { plan=new CommandPlan {Action="radio_set",Actor="current",SpeedKmh=45,Place="none",Station=station.Id}; plan.Validate(); return true; }
            // Recognized radio requests fail locally instead of becoming a movement or an API request.
            if(Regex.IsMatch(text,@"\b(?:radio|station|stazione)\b") &&
                (Regex.IsMatch(text,@"^(?:radio|(?:non |don t |do not )?(?:metti|mettere|spegni|spegnere|seleziona|cambia|attiva|disattiva|play|select|set|switch|tune|change|turn|stop|mute|disable)|(?:quali|which|what)\b)") ||
                 Regex.IsMatch(text,@"^(?:la |the )?radio\b")))
            { plan=CommandPlan.Create("say","current",45,"none","Choose a named station from the companion, or say 'Radio off'. Radio requests do not change your route; send other actions separately."); return true; }
            return false;
        }
    }

    // Audio-only boundary: implementations receive no driving tasks, route or camera controller.
    public interface IVehicleRadio
    {
        bool Blocked { get; }
        bool InVehicle { get; }
        bool HasRadio { get; }
        void Select(string nativeStation);
    }

    public static class RadioControl
    {
        public static string Execute(CommandPlan plan, IVehicleRadio radio)
        {
            plan.Validate();
            if(!RadioCommands.IsRadio(plan.Action)) throw new ArgumentException("Not a radio action.");
            try
            {
                if(radio.Blocked) return "Radio unavailable while GTA has control. Your active action is unchanged.";
                if(!radio.InVehicle) return "Get into a vehicle with a radio first. Your active action is unchanged.";
                if(!radio.HasRadio) return "This vehicle has no available radio. Your active action is unchanged.";
                var station=RadioCatalog.Find(plan.Station);
                radio.Select(plan.Action=="radio_off"?"OFF":station.Id);
                return plan.Action=="radio_off" ? "Radio off requested. Your active action continues." :
                    "Tuning to "+station.Name+". Your active action continues.";
            }
            catch(Exception) { return "Radio unavailable. Your active action is unchanged."; }
        }
    }
}
