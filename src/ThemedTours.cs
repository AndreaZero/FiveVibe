using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    public sealed class TourProfile
    {
        public string Id {get;private set;} public string Name {get;private set;}
        private readonly string[] stops;
        public string[] Stops {get{return (string[])stops.Clone();}}
        internal TourProfile(string id,string name,params string[] places) {Id=id;Name=name;stops=places;}
    }
    public static class TourCatalog
    {
        private static readonly TourProfile[] profiles={
            new TourProfile("coast","Coastal drive","pacific_bluffs","chumash","paleto_forest","paleto_bay"),
            new TourProfile("countryside","Countryside drive","route_68","sandy_shores","grapeseed","alamo_sea")
        };
        public static TourProfile[] All {get{return (TourProfile[])profiles.Clone();}}
        public static bool IsStartAction(string action) {return action=="tour_coast" || action=="tour_countryside";}
        public static TourProfile ForAction(string action) {return profiles.FirstOrDefault(p=>"tour_"+p.Id==action);}
    }
    // Pure itinerary state. GTA still owns road selection and physical driving on each leg.
    public sealed class TourSession
    {
        public const int MaximumMilliseconds=60*60*1000;
        public TourProfile Profile {get;private set;}
        public int Index {get;private set;} public int Started {get;private set;}
        public bool Active {get;private set;} public bool WaitingForStop {get;private set;}
        public string Result {get;private set;}
        public TourSession() {Result="idle";}
        public string CurrentPlace {get{return Profile==null?"none":Profile.Stops[Index];}}
        public string Status {get{return Profile==null?"Choose a coastal or countryside drive.":Profile.Name+
            (Active?" / "+(Index+1)+" of "+Profile.Stops.Length+" / "+PlaceCatalog.Get(CurrentPlace).Name:Result=="completed"?" completed.":" stopped.");}}
        public void Start(string action,int now)
        {
            var profile=TourCatalog.ForAction(action);
            if(profile==null || profile.Stops.Any(id=>PlaceCatalog.Get(id)==null))throw new ArgumentException("Unknown or unavailable themed drive.");
            Profile=profile;Index=0;Started=now;Active=true;WaitingForStop=false;Result="active";
        }
        // True means a next leg is ready. The final arrival still requires a physical stop.
        public bool ReachStop()
        {
            if(!Active || WaitingForStop)return false;
            if(Index+1<Profile.Stops.Length) {Index++;return true;}
            WaitingForStop=true;return false;
        }
        public bool Complete()
        {
            if(!Active || !WaitingForStop)return false;
            Active=false;WaitingForStop=false;Result="completed";return true;
        }
        public void Cancel() {if(!Active)return;Active=false;WaitingForStop=false;Result="cancelled";}
        public bool Expired(int now) {return Active && unchecked(now-Started)>=MaximumMilliseconds;}
    }
    public static class TourCommands
    {
        public static bool TryParse(string input,int speed,out CommandPlan plan)
        {
            plan=null;
            string text=Regex.Replace(LocalPlanner.Normalize(input),@"[^a-z0-9]+"," ").Trim();
            text=Regex.Replace(text,@"^(?:(?:please|per favore)\s+)+","");
            text=Regex.Replace(text,@"\s+(?:please|per favore)$","");
            string action=null;
            const string englishStart=@"(?:(?:start|begin|take|do)(?: me on| me for)?(?: a| the)? )?";
            const string italianStart=@"(?:(?:fammi|facciamo|fai|avvia|inizia|comincia|portami a fare)(?: un| il)? )?";
            bool coast=Regex.IsMatch(text,"^(?:"+englishStart+@"(?:coastal|coast|seaside) (?:drive|ride|tour)|(?:drive|ride) (?:along|on) (?:the )?coast|"+italianStart+@"(?:giro|tour)(?: panoramico)? (?:sulla|lungo la|lungo) costa|"+italianStart+@"giro costiero)$");
            bool country=Regex.IsMatch(text,"^(?:"+englishStart+@"(?:countryside|country|rural) (?:drive|ride|tour)|(?:(?:take me for a|take a|go for a) )?(?:drive|ride|tour) (?:in|through|around) (?:the )?countryside|"+italianStart+@"(?:giro|tour)(?: panoramico)? in campagna)$");
            if(coast)action="tour_coast";else if(country)action="tour_countryside";
            if(action!=null) {plan=CommandPlan.Create(action,"current",Math.Max(15,Math.Min(DrivingCommands.MaximumSpeed,speed)));return true;}
            // Do not replace a named destination merely because it mentions the coast/countryside.
            if(Regex.IsMatch(text,@"\b(?:giro|tour|drive|ride)\b") &&
                (Regex.IsMatch(text,@"\b(?:coast|coastal|costa|costiero|campagna|countryside|rural)\b.*\b(?:then|poi|and|e)\b") ||
                 Regex.IsMatch(text,@"\b(?:non|not|never|don t|do not)\b.*\b(?:coast|coastal|costa|costiero|campagna|countryside|rural)\b") ||
                 Regex.IsMatch(text,@"^(?:start|begin|avvia|inizia) (?:a |the |un |il )?(?:.+ )?tour$") ||
                 Regex.IsMatch(text,"^"+italianStart+@"(?:giro|tour)(?: panoramico)? (?:in|sulla|lungo) .+$")))
            {plan=CommandPlan.Create("say","current",45,"none","Choose one themed drive: 'Start coastal drive' or 'Start countryside drive'. Send other actions separately.");return true;}
            return false;
        }
    }
}
