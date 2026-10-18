using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FiveVibe
{
    public sealed class HudPreferences
    {
        public bool ShowTaskHud { get; set; }
        public bool? ShowVehicleSpeed {get;set;}
        public static bool LoadSpeed(string path,bool fallback)
        {
            if(!File.Exists(path) || new FileInfo(path).Length>4096)return fallback;
            var settings=new JavaScriptSerializer().Deserialize<HudPreferences>(File.ReadAllText(path));
            return settings==null?fallback:settings.ShowVehicleSpeed??fallback;
        }
        public static bool Load(string path, bool fallback)
        {
            if (!File.Exists(path)) return fallback;
            if(new FileInfo(path).Length>4096) return fallback;
            var settings=new JavaScriptSerializer().Deserialize<HudPreferences>(File.ReadAllText(path));
            return settings==null ? fallback : settings.ShowTaskHud;
        }
        public static void Save(string path, bool visible)
        {Save(path,visible,LoadSpeed(path,true));}
        public static void Save(string path,bool visible,bool speedVisible)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string stage=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(stage,new JavaScriptSerializer().Serialize(new HudPreferences { ShowTaskHud=visible,ShowVehicleSpeed=speedVisible }),new UTF8Encoding(false));
                if(File.Exists(path)) File.Replace(stage,path,null); else File.Move(stage,path);
            }
            finally { if(File.Exists(stage)) File.Delete(stage); }
        }
    }
    public static class WorldControlParser
    {
        public static bool TryParse(string input,out CommandPlan plan)
        {
            string text=LocalPlanner.Normalize(input);
            plan=null;
            if(Regex.IsMatch(text,@"\b(?:non|not|never|don't|dont|senza|without)\b")) return false;
            if(Regex.IsMatch(text,@"\b(?:hud|overlay|task display|task a schermo)\b"))
            {
                string action=Regex.IsMatch(text,@"\b(?:hide|nascondi|nascondere|spegni)\b") ? "hud_hide" :
                    Regex.IsMatch(text,@"\b(?:show|mostra|mostrare|accendi)\b") ? "hud_show" :
                    Regex.IsMatch(text,@"\b(?:toggle|alterna|inverti)\b") ? "hud_toggle" : null;
                if(action!=null) { plan=CommandPlan.Create(action,"current",45); return true; }
            }
            string weather="none", clock="unchanged"; int hour=-1,minute=0;
            bool request=Regex.IsMatch(text,@"\b(?:set|make|change|metti|imposta|cambia|voglio|vorrei|want)\b");
            string[] keys={"auto","thunder","blizzard","christmas","overcast","clearing","sunny","clouds","fog","rain","snow","smog","clear"};
            string[] aliases={"automatic weather|restore weather|normal weather|meteo automatico|meteo normale|ripristina meteo","thunder|storm|temporale|tempesta","blizzard|bufera","christmas|natale","overcast|coperto","clearing|schiarite","sunny|sole|soleggiato","clouds|cloudy|nuvoloso|nuvole","fog|foggy|nebbia","rain|raining|pioggia|piovoso","snow|snowing|neve","smog","clear|sereno"};
            for(int i=0;i<keys.Length;i++)
                if(Regex.IsMatch(text,@"\b(?:"+aliases[i]+@")\b") && (request || Regex.IsMatch(text,@"^(?:"+aliases[i]+@")[.! ]*$"))) { weather=keys[i]; break; }
            bool timeRequest=Regex.IsMatch(text,@"\b(?:set (?:the )?(?:time|clock)|change (?:the )?time|make it|imposta (?:l[' ]?)?(?:ora|orario)|cambia (?:l[' ]?)?(?:ora|orario)|metti (?:le|l[' ]?ora)|metti (?:il )?(?:tramonto|giorno|notte|mezzogiorno|alba))\b");
            if(timeRequest || weather!="none")
            {
                var time=Regex.Match(text,@"\b(?:at|alle|le|ore|time|clock|ora|orario|it)\s*(?:to\s*)?(\d{1,2})(?::(\d{2}))?\b");
                if(time.Success) { hour=int.Parse(time.Groups[1].Value); minute=time.Groups[2].Success?int.Parse(time.Groups[2].Value):0; }
                else if(timeRequest) hour=Regex.IsMatch(text,@"\b(?:sunset|tramonto|golden hour)\b")?19:Regex.IsMatch(text,@"\b(?:dawn|sunrise|alba)\b")?6:
                    Regex.IsMatch(text,@"\b(?:noon|mezzogiorno|giorno|daytime)\b")?12:Regex.IsMatch(text,@"\b(?:night|notte|midnight|mezzanotte)\b")?0:-1;
            }
            if(Regex.IsMatch(text,@"\b(?:resume|unfreeze|riprendi|sblocca|riavvia)\b.*\b(?:time|clock|tempo|orologio)\b")) clock="resume";
            else if(Regex.IsMatch(text,@"\b(?:freeze|pause|ferma|blocca|congela)\b.*\b(?:time|clock|tempo|orologio)\b")) clock="pause";
            if(hour>23 || minute>59) { plan=CommandPlan.Create("say","current",45,"none","Use a time from 00:00 to 23:59."); return true; }
            if(weather=="none" && hour<0 && clock=="unchanged") return false;
            plan=CommandPlan.World(weather,hour,minute,clock); return true;
        }
        public static string WeatherCode(string weather)
        {
            switch(weather) {
                case "sunny":return "EXTRASUNNY"; case "clear":return "CLEAR"; case "clouds":return "CLOUDS";
                case "fog":return "FOGGY"; case "overcast":return "OVERCAST"; case "rain":return "RAIN";
                case "thunder":return "THUNDER"; case "smog":return "SMOG"; case "clearing":return "CLEARING";
                case "snow":return "SNOW"; case "blizzard":return "BLIZZARD"; case "christmas":return "XMAS";
                default:throw new ArgumentException("Unsupported weather preset.");
            }
        }
    }
}
