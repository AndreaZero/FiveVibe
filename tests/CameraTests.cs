using System;
using FiveVibe;

internal static class CameraTests
{
    private static int passed;
    private static void Check(bool result,string name) { if(!result) throw new Exception(name); passed++; }
    private static string Action(string request) { return LocalPlanner.Parse(request,45).Action; }
    public static int Main()
    {
        try
        {
            Check(Action("Metti la camera in prima persona")=="camera_first","Italian first person");
            Check(Action("Switch to first person")=="camera_first","English first person");
            Check(Action("Metti visuale 1 persona")=="camera_first","Numeric first person");
            Check(Action("Passa alla terza persona")=="camera_third","Third person");
            Check(Action("Attiva la camera cinema")=="camera_cinema","Custom cinema");
            Check(Action("Set cinematic camera")=="camera_cinema","English cinema");
            Check(Action("Voglio riprese cinematiche")=="camera_cinema","Casual cinematic request");
            Check(Action("Use GTA cinematic camera")=="camera_native","Native cinema remains distinct");
            Check(Action("Metti cinema originale GTA")=="camera_native","Italian native cinema");
            Check(Action("Ripristina camera normale")=="camera_gameplay","Restore normal view");
            Check(Action("Restore normal camera")=="camera_gameplay","Companion restore works offline");
            Check(Action("Toggle camera letterbox")=="camera_bars","Companion letterbox works offline");
            Check(Action("Cambia visuale")=="camera_cycle","Cycle views");
            Check(Action("Next shot")=="camera_next","Next shot");
            Check(Action("Prossima inquadratura")=="camera_next","Italian next shot");
            Check(Action("Nascondi le bande nere")=="camera_bars_off","Hide is not a toggle");
            Check(Action("Show letterbox")=="camera_bars_on","Show is not a toggle");
            Check(Action("Non cambiare la camera in prima persona")=="say","Negated camera change does not execute");
            Check(Action("che bella questa camera")=="say","Casual camera mention does not execute");
            Check(Action("Drive to Paleto Bay")=="place","Camera parser preserves travel commands");
            Check(Action("stop")=="stop","Takeover retains priority");
            bool rejected=false;
            try { CommandPlan.Create("camera_cinema","trevor",45); } catch(ArgumentException) {rejected=true;}
            Check(rejected,"A camera request cannot switch protagonist");
            var clock=new CameraShotClock(); clock.Reset(100);
            Check(!clock.Update(5000) && clock.Shot==0,"No rapid automatic cuts");
            Check(clock.Update(8600) && clock.Shot==1,"Timed cut advances once");
            clock.Next(9000); Check(clock.Shot==2 && clock.Progress(9000)==0,"Manual cut resets shot movement");
            for(int i=0;i<4;i++) clock.Next(9001+i);
            Check(clock.Shot==0,"Shot cycle wraps");
            clock.Reset(Int32.MaxValue-100);
            Check(clock.Update(unchecked(Int32.MaxValue-100+8500)),"Cut scheduler handles timer wrap");
            for(int shot=0;shot<6;shot++) for(int subject=0;subject<2;subject++)
            {
                var start=CinemaFraming.Sample(shot,0,0,subject==1,4.5f);
                var finish=CinemaFraming.Sample(shot,1,70,subject==1,4.5f);
                Check(start.Height>.5 && finish.Height>.5,"Shot remains above ground");
                Check(start.Fov>=38 && finish.Fov<=65,"Comfortable field of view");
                Check(Math.Abs(start.Right)+Math.Abs(start.Forward)>3,"Camera stays outside subject");
            }
            Check(CinemaFraming.Clearance(10,true,4)<.4,"Obstruction brings camera inside the hit point");
            Check(CinemaFraming.Clearance(10,true,.2f)==0,"No room for camera yields zero clearance");
            Check(CinemaFraming.Clearance(10,false,0)==1,"Clear path preserves framing");
            Check(CinemaFraming.Clearance(0,true,1)==0 && CinemaFraming.Clearance(Single.NaN,true,1)==0,"Invalid probe data cannot place a camera");
            Check(CinemaFraming.Blend(.016f)>0 && CinemaFraming.Blend(.1f)<1,"Damping has no overshoot");
            var invalid=CinemaFraming.Sample(-1,Single.NaN,Single.PositiveInfinity,true,Single.NaN);
            Check(!Single.IsNaN(invalid.Right) && !Single.IsNaN(invalid.Fov),"Bad frame parameters remain bounded");
            Console.WriteLine(passed+" camera checks passed."); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine("After "+passed+" checks: "+error.Message); return 1; }
    }
}
