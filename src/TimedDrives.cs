using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    // Session clock uses monotonic real milliseconds, independent of GTA's world clock.
    // Position capture and all native tasks remain on the game thread.
    public sealed class TimedDriveSession
    {
        public const int MaximumMinutes=120;
        public const int ReturnLimitMilliseconds=20*60*1000;
        public string Stage {get;private set;}
        public int Minutes {get;private set;}
        public long ElapsedMilliseconds {get;private set;}
        public long ReturnMilliseconds {get;private set;}
        private int previous;
        private bool counting;
        public TimedDriveSession() {Stage="idle";}
        public bool Active {get{return Stage=="acquiring" || Stage=="cruising" || Stage=="returning" || Stage=="braking";}}
        public int RemainingSeconds {get{return (int)Math.Max(0,((long)Minutes*60000-ElapsedMilliseconds+999)/1000);}}
        public bool ReturnDue {get{return Stage=="cruising" && ElapsedMilliseconds>=(long)Minutes*60000;}}
        public bool ReturnExpired {get{return Stage=="returning" && ReturnMilliseconds>=ReturnLimitMilliseconds;}}
        public string Status {get{return Stage=="acquiring"?"Timed drive / finding a car / "+Minutes+" minutes before return":
            Stage=="cruising"?"Timed drive / "+(RemainingSeconds/60).ToString("00",CultureInfo.InvariantCulture)+":"+(RemainingSeconds%60).ToString("00",CultureInfo.InvariantCulture)+" until return":
            Stage=="returning"?"Timed drive / returning to the starting point":Stage=="braking"?"Timed drive / stopping at the starting point":
            Stage=="completed"?"Timed drive completed. Back near the starting point.":Stage=="cancelled"?"Timed drive cancelled.":"Choose how long to drive before returning.";}}
        public void Start(int minutes)
        {
            if(minutes<1 || minutes>MaximumMinutes)throw new ArgumentException("Choose 1 to 120 whole minutes.");
            Minutes=minutes;ElapsedMilliseconds=ReturnMilliseconds=0;counting=false;Stage="acquiring";
        }
        public void BeginCruise(int now)
        {
            if(Stage!="acquiring")return;
            Stage="cruising";previous=now;counting=true;
        }
        public void Sample(int now,bool playable)
        {
            uint delta=unchecked((uint)(now-previous));
            if(counting && playable)
            {
                if(Stage=="cruising")ElapsedMilliseconds=Math.Min((long)Minutes*60000,ElapsedMilliseconds+delta);
                else if(Stage=="returning")ReturnMilliseconds+=delta;
            }
            previous=now;counting=playable && (Stage=="cruising" || Stage=="returning");
        }
        public bool BeginReturn()
        {
            if(!ReturnDue)return false;
            Stage="returning";ReturnMilliseconds=0;return true;
        }
        public void ReachOrigin() {if(Stage=="returning")Stage="braking";}
        public bool Complete() {if(Stage!="braking")return false;Stage="completed";counting=false;return true;}
        public void Cancel() {if(!Active)return;Stage="cancelled";counting=false;}
    }
    public static class TimedDriveCommands
    {
        public static CommandPlan Create(int minutes,int speed)
        {
            var plan=new CommandPlan {Action="timed_drive",Actor="current",Place="none",SpeedKmh=Math.Max(15,Math.Min(DrivingCommands.MaximumSpeed,speed)),DurationMinutes=minutes};
            plan.Validate();return plan;
        }
        public static bool TryParse(string input,int speed,out CommandPlan plan,bool clarifyUnsupported=true)
        {
            plan=null;
            string text=LocalPlanner.Normalize(input).Trim(' ','.','!','?');
            text=Regex.Replace(text,@"^(?:please|per favore)\s+|\s+(?:please|per favore)$","");
            var match=Regex.Match(text,@"^(?:(?:fai|fammi|facciamo|avvia|inizia)(?: un)? giro (?:di|per)|(?:guida|gira) per) (\d+) minut[oi][,]? (?:e )?(?:poi )?(?:torna|ritorna) (?:al punto di partenza|alla partenza|qui)$");
            if(!match.Success)match=Regex.Match(text,@"^(?:(?:drive|cruise)(?: around)? for|(?:take|start|do)(?: me on| me for)? a drive for) (\d+) minutes?[,]? (?:and )?(?:then )?(?:return|come back|go back) (?:to (?:the )?(?:starting point|start)|here)$");
            int minutes;
            if(match.Success && int.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out minutes) && minutes>=1 && minutes<=TimedDriveSession.MaximumMinutes)
            {plan=Create(minutes,speed);return true;}
            // Never silently turn a malformed timed/combined request into an unlimited cruise.
            var number=Regex.Match(text,@"(?<!\w)([-+]?\d+(?:[.,]\d+)?)\s*(?:minut[oi]|minutes?)\b");
            bool invalidNumber=number.Success && (!int.TryParse(number.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out minutes) || minutes<1 || minutes>TimedDriveSession.MaximumMinutes);
            if((clarifyUnsupported || invalidNumber || match.Success) && Regex.IsMatch(text,@"\b(?:minut[oi]|minutes?)\b") && Regex.IsMatch(text,@"\b(?:giro|gira|guida|drive|cruise|ride|tour)\b"))
            {plan=CommandPlan.Create("say","current",45,"none","Choose 1 to 120 whole minutes: 'Drive for 20 minutes then return to the starting point'. Return travel takes extra time. Send other actions separately.");return true;}
            return false;
        }
    }
}
