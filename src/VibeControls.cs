using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    public static class DrivingCommands
    {
        public const int MaximumSpeed=160;
        public static bool IsDriving(string action)
        {return action=="speed" || action=="cruise" || action=="place" || action=="waypoint" || action=="mission" || action=="timed_drive" || TourCatalog.IsStartAction(action);}
        public static bool TryParse(string input,int currentSpeed,out CommandPlan plan)
        {
            plan=null;string text=LocalPlanner.Normalize(input).Trim(' ','.','!');
            // Only these explicit negative traffic instructions are positive style requests.
            const string traffic=@"\b(?:non (?:seguire|rispettare) (?:il )?traffico|ignora (?:il )?traffico|non fermarti ai semafori|(?:do not|don't|dont) follow (?:the )?traffic|ignore (?:the )?traffic|ignore (?:the )?(?:traffic lights|red lights)|skip (?:the )?traffic)\b";
            const string rush=@"\b(?:arriva in fretta|vai (?:piu )?veloce|guida (?:piu )?veloce|sbrigati|fai in fretta|drive fast|go fast|get there quickly|hurry up|rush driving|fast driving)\b";
            const string normal=@"\b(?:guida normalmente|guida tranquillo|rispetta (?:il )?traffico|segui (?:il )?traffico|normal driving|drive normally|obey (?:the )?traffic|follow (?:the )?traffic)\b";
            string rest=Regex.Replace(text,traffic," ");bool urgent=rest!=text;
            if(Regex.IsMatch(rest,@"\b(?:non|not|never|don't|dont|do not|senza|without)\b"))return false;
            bool fast=Regex.IsMatch(rest,rush),calm=Regex.IsMatch(rest,normal);
            if(!urgent && !fast && !calm)return false;
            if(calm && (urgent || fast)) {plan=CommandPlan.Create("say","current",45,"none","Choose normal or rush driving, then send the other setting separately.");return true;}
            rest=Regex.Replace(Regex.Replace(rest,rush," "),normal," ");
            rest=Regex.Replace(rest,@"\b(?:e poi|and then|e|and|please|per favore)\b"," ");rest=Regex.Replace(rest,@"\s+"," ").Trim(' ', ',', ';');
            int speed=calm?45:fast?120:Math.Max(15,Math.Min(MaximumSpeed,currentSpeed));
            plan=rest.Length==0 || Regex.IsMatch(rest,@"^(?:vai|guida|drive|go)$") ? CommandPlan.Create("speed","current",speed) : LocalPlanner.Parse(rest,speed);
            if(!IsDriving(plan.Action))return false;
            plan.DrivingStyle=calm?"normal":"rush";plan.Validate();return true;
        }
    }
    public static class VibeCommands
    {
        public static bool TryParse(string input,out CommandPlan plan)
        {
            plan=null;string text=LocalPlanner.Normalize(input).Trim(' ','.','!');string action=null;
            if(Regex.IsMatch(text,@"^(?:(?:please|per favore) )?(?:random(?: mode| on)?|(?:start|enable|attiva|avvia)(?: la)? (?:modalita )?random(?: mode)?|fai cose (?:a caso|random)|do random things)$"))action="random_on";
            else if(Regex.IsMatch(text,@"^(?:(?:please|per favore) )?(?:random off|(?:stop|disable|disattiva|spegni|ferma)(?: la)? (?:modalita )?random(?: mode)?)$"))action="random_off";
            else if(Regex.IsMatch(text,@"\b(?:speedometer|tachimetro|hud (?:della )?velocita|vehicle speed hud|velocita (?:auto|veicolo|moto))\b") && !Regex.IsMatch(text,@"\b(?:non|not|never|don't|dont)\b"))
            {
                if(Regex.IsMatch(text,@"\b(?:hide|off|disable|nascondi|spegni|disattiva)\b"))action="speedometer_hide";
                else if(Regex.IsMatch(text,@"\b(?:show|on|enable|mostra|accendi|attiva)\b"))action="speedometer_show";
                else if(Regex.IsMatch(text,@"\b(?:toggle|alterna|cambia)\b"))action="speedometer_toggle";
            }
            if(action==null)return false;
            plan=CommandPlan.Create(action,"current",45);return true;
        }
    }
    // Bounded local activity scheduler, independent of AI requests and GTA frame rate.
    public sealed class RandomVibeSession
    {
        private readonly Random random;
        private int actor,started,finished,step,activityStarted;
        private bool waiting,dispatched;
        private string previous="none";
        public bool Active {get;private set;}
        public string Status {get;private set;}
        public RandomVibeSession() : this(new Random()) {}
        public RandomVibeSession(Random source) {random=source;Status="Random mode off.";}
        public void Start(int ped,int now) {actor=ped;started=finished=now;step=0;waiting=dispatched=false;Active=true;Status="Random mode / choosing an activity";}
        public void Stop() {Active=false;waiting=dispatched=false;Status="Random mode off.";}
        public bool Valid(int ped,int now) {return Active && ped==actor && unchecked((uint)(now-started))<60u*60*1000;}
        public bool EndShortActivity(int now) {return Active && dispatched && waiting && unchecked((uint)(now-activityStarted))>=25000;}
        public CommandPlan Next(int now,bool busy,bool inVehicle,float x,float y,int speed)
        {
            if(!Active)return null;
            if(dispatched)
            {
                if(busy)return null;
                dispatched=false;waiting=false;finished=now;return null;
            }
            if(busy || step>0 && unchecked((uint)(now-finished))<5000)return null;
            CommandPlan plan;
            int choice=step++%3;
            if(inVehicle && choice==0 || !inVehicle && choice==2)
            {
                string[] ids={"beach","mirror_park","observatory","rockford_hills","vinewood","chumash","sandy_shores","grapeseed","paleto_bay","route_68"};
                var available=ids.Select(PlaceCatalog.Get).Where(p=>p!=null && p.Id!=previous && Distance(p,x,y)>200).ToArray();
                var nearby=available.Where(p=>Distance(p,x,y)<5000).ToArray();if(nearby.Length>0)available=nearby;
                if(available.Length==0)return null;
                var place=available[random.Next(available.Length)];previous=place.Id;
                plan=CommandPlan.Create("place","current",speed,place.Id);Status="Random mode / visiting "+place.Name;
            }
            else
            {
                plan=CommandPlan.Create(choice==1?"greet_npc":"walk_wander","current",speed);
                waiting=true;Status=choice==1?"Random mode / greeting someone nearby":"Random mode / strolling nearby";
            }
            dispatched=true;activityStarted=now;return plan;
        }
        private static double Distance(PlaceDefinition p,float x,float y) {return Math.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y));}
    }
}
