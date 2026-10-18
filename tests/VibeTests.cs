using System;
using System.IO;
using FiveVibe;

internal static class VibeTests
{
    private static int passed;
    private static void Check(bool value,string label) {if(!value)throw new Exception(label);passed++;}
    private static CommandPlan Parse(string text) {return LocalPlanner.Parse(text,60);}
    public static int Main()
    {
        try
        {
            foreach(string text in new[]{"vai veloce","arriva in fretta","drive fast","hurry up","get there quickly"})
            {var p=Parse(text);Check(p.Action=="speed" && p.SpeedKmh==120 && p.DrivingStyle=="rush","Rush intent: "+text);}
            foreach(string text in new[]{"non seguire il traffico","non rispettare il traffico","ignore traffic","don't follow traffic","non fermarti ai semafori"})
            {var p=Parse(text);Check(p.Action=="speed" && p.SpeedKmh==60 && p.DrivingStyle=="rush","Traffic intent: "+text);}
            foreach(string text in new[]{"guida normalmente","normal driving","rispetta il traffico"})
            {var p=Parse(text);Check(p.Action=="speed" && p.SpeedKmh==45 && p.DrivingStyle=="normal","Restore normal driving: "+text);}
            foreach(string text in new[]{"non andare veloce","don't drive fast","non ignorare il traffico","what is a speedometer?","non attivare random","non mostrare il tachimetro"})
                Check(Parse(text).Action=="say","No accidental activation: "+text);
            var combined=Parse("vai veloce e portami a Paleto Bay");
            Check(combined.Action=="place" && combined.Place=="paleto_bay" && combined.SpeedKmh==120 && combined.DrivingStyle=="rush","Rush destination is a single plan");
            combined=Parse("ignore traffic and drive to the waypoint at 140 km/h");
            Check(combined.Action=="waypoint" && combined.SpeedKmh==140 && combined.DrivingStyle=="rush","Explicit speed and style with waypoint");
            Check(Parse("set speed to 160 km/h").SpeedKmh==160,"Higher speed bound supported");
            Check(Parse("set speed to 161 km/h").Action=="say" && Parse("set speed to 14 km/h").Action=="say","Bounds rejected without movement");
            Check(Parse("set speed to 80 km/h").DrivingStyle=="unchanged","Numeric followup preserves the chosen style");
            Check(Parse("Start coastal drive and drive fast").Action=="tour_coast","Style works with themed drives");
            Check(Parse("Drive for 20 minutes then return to the starting point and drive fast").DurationMinutes==20,"Style works with timed return");
            Check(Parse("attiva random").Action=="random_on" && Parse("Start random mode").Action=="random_on","Random starts locally in both languages");
            Check(Parse("disattiva random").Action=="random_off" && Parse("Stop random mode").Action=="random_off","Random stops locally in both languages");
            Check(Parse("mostra tachimetro").Action=="speedometer_show" && Parse("Hide speedometer").Action=="speedometer_hide","Independent speed HUD controls");
            Check(Parse("Toggle speedometer").Action=="speedometer_toggle","Speed HUD toggle");
            bool rejected=false;try{var p=CommandPlan.Create("say","current",45);p.DrivingStyle="rush";p.Validate();}catch(ArgumentException){rejected=true;}
            Check(rejected,"Style cannot be attached to an unrelated action");
            var session=new RandomVibeSession(new Random(18));session.Start(7,1000);
            var next=session.Next(1000,false,true,0,0,60);
            Check(next.Action=="place" && next.Place!="none" && next.Actor=="current","Random drive selects a catalog destination");string first=next.Place;
            Check(session.Next(9000,true,true,0,0,60)==null,"An active drive is not overwritten");
            Check(!session.EndShortActivity(100000),"Drive is not cut short by the walking timer");
            Check(session.Next(10000,false,true,0,0,60)==null && session.Next(14999,false,true,0,0,60)==null,"Cooldown follows completion or failed dispatch");
            next=session.Next(15000,false,true,0,0,60);Check(next.Action=="greet_npc","Next activity can leave a stopped vehicle for a greeting");
            Check(!session.EndShortActivity(39999) && session.EndShortActivity(40000),"Foot activity has a bounded duration");
            Check(session.Valid(7,20000) && !session.Valid(8,20000),"Character change invalidates the session");
            Check(!session.Valid(7,3601000),"Random session ends after an hour");
            session.Stop();Check(!session.Active && session.Next(50000,false,false,0,0,60)==null,"Stop cannot restart random actions");
            session.Start(9,Int32.MaxValue-100);Check(session.Valid(9,unchecked(Int32.MaxValue+1000)),"Clock wrap preserves the activity limit");
            next=session.Next(Int32.MaxValue,false,false,0,0,60);Check(next.Action=="walk_wander","Random mode works without a vehicle");
            for(int i=1;i<25;i++)
            {
                session.Next(i*40000,false,false,0,0,60);next=session.Next(i*40000+5000,false,i%3==0,0,0,60);
                if(next!=null)Check(next.Action=="place" || next.Action=="walk_wander" || next.Action=="greet_npc","Only supported activities are scheduled");
            }
            string path=Path.Combine(Path.GetTempPath(),"fivevibe-speed-"+Guid.NewGuid().ToString("N"),"preferences.json");
            try
            {
                HudPreferences.Save(path,false,false);Check(!HudPreferences.Load(path,true) && !HudPreferences.LoadSpeed(path,true),"Both HUD choices persist independently");
                HudPreferences.Save(path,true);Check(HudPreferences.Load(path,false) && !HudPreferences.LoadSpeed(path,true),"Legacy task HUD save preserves speed choice");
                File.WriteAllText(path,"{\"ShowTaskHud\":false}");Check(!HudPreferences.Load(path,true) && HudPreferences.LoadSpeed(path,true),"Existing preferences retain the configured speed default");
            }
            finally {if(File.Exists(path))File.Delete(path);}
            Console.WriteLine(passed+" driving, speedometer and random-mode checks passed.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine("After "+passed+" checks: "+e);return 1;}
    }
}
