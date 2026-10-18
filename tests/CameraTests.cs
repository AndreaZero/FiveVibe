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
            Check(Action("Attiva la camera cinema")=="camera_native","Cinema uses GTA");
            Check(Action("Set cinematic camera")=="camera_native","English cinema");
            Check(Action("Voglio riprese cinematiche")=="camera_native","Casual cinematic request");
            Check(Action("Use GTA cinematic camera")=="camera_native","Explicit native cinema");
            Check(Action("Metti cinema originale GTA")=="camera_native","Italian native cinema");
            Check(Action("Ripristina camera normale")=="camera_gameplay","Restore normal view");
            Check(Action("Restore normal camera")=="camera_gameplay","Companion restore works offline");
            Check(Action("Toggle camera letterbox")=="say","Companion letterbox works offline");
            Check(Action("Cambia visuale")=="camera_cycle","Cycle views");
            Check(Action("Next shot")=="say","Next shot");
            Check(Action("Prossima inquadratura")=="say","Italian next shot");
            Check(Action("Nascondi le bande nere")=="say","Hide is not a toggle");
            Check(Action("Show letterbox")=="say","Show is not a toggle");
            Check(Action("Non cambiare la camera in prima persona")=="say","Negated camera change does not execute");
            Check(Action("che bella questa camera")=="say","Casual camera mention does not execute");
            Check(Action("Drive to Paleto Bay")=="place","Camera parser preserves travel commands");
            Check(Action("stop")=="stop","Takeover retains priority");
            bool rejected=false;
            try { CommandPlan.Create("camera_cinema","trevor",45); } catch(ArgumentException) {rejected=true;}
            Check(rejected,"A camera request cannot switch protagonist");
            GTA.Native.Function.Reset();
            var camera=new SpectatorCamera(message=>{});
            camera.UpdateDriving(true);
            Check(camera.Mode==SpectatorView.NativeCinema,"Auto drive chooses GTA native cinema");
            camera.Tick(true,false);Check(GTA.Native.Function.Cinematic,"Native cinema is enabled");
            camera.Tick(true,true);Check(!GTA.Native.Function.Cinematic && camera.Mode==SpectatorView.NativeCinema,"Chat suspends native shots without changing the selected view");
            camera.Tick(true,false);Check(GTA.Native.Function.Cinematic,"Closing chat resumes GTA cinema");
            GTA.Game.IsPaused=true;camera.Tick(true,false);Check(!GTA.Native.Function.Cinematic,"Pause releases owned native cinema");
            GTA.Game.IsPaused=false;camera.Tick(true,false);Check(GTA.Native.Function.Cinematic,"Gameplay resume restores cinema");
            camera.Execute("camera_first");camera.Tick(true,false);
            Check(!GTA.Native.Function.Cinematic && GTA.Native.Function.PedView==4 && GTA.Native.Function.VehicleView==4,"First person restores native rendering and cockpit");
            camera.UpdateDriving(true);Check(camera.Mode==SpectatorView.FirstPerson,"Manual choice survives the current drive");
            camera.Reset();Check(camera.Mode==SpectatorView.Gameplay && GTA.Native.Function.PedView==2 && GTA.Native.Function.VehicleView==3,"Takeover restores prior follow views");
            for(int i=0;i<4;i++)camera.Execute("camera_cycle");
            Check(camera.Mode==SpectatorView.Gameplay,"Four native views cycle without a custom mode");
            GTA.Native.Function.Cinematic=true;camera.Execute("camera_native");camera.Tick(true,false);camera.Reset();
            Check(GTA.Native.Function.Cinematic,"An external cinematic camera is not owned or disabled");
            GTA.Native.Function.Cinematic=false;camera.Execute("camera_native");GTA.Game.Player.Character.InVehicle=false;camera.Tick(true,false);
            Check(!GTA.Native.Function.Cinematic && camera.Status.Contains("enter a vehicle"),"Native vehicle cinema waits on foot");
            camera.Reset();GTA.Game.Player.Character.InVehicle=true;
            Console.WriteLine(passed+" camera checks passed."); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine("After "+passed+" checks: "+error.Message); return 1; }
    }
}

// Native camera adapter: tests the real controller, without running GTA.
namespace GTA
{
    public static class Game {public static bool IsPaused;public static int GameTime=10000;public static Player Player=new Player();}
    public sealed class Player {public bool CanControlCharacter=true;public Ped Character=new Ped();}
    public sealed class Ped {public bool IsDead;public bool InVehicle=true;public bool IsInVehicle(){return InVehicle;}}
}
namespace GTA.Native
{
    public enum Hash {GET_FOLLOW_PED_CAM_VIEW_MODE,GET_FOLLOW_VEHICLE_CAM_VIEW_MODE,SET_FOLLOW_PED_CAM_VIEW_MODE,SET_FOLLOW_VEHICLE_CAM_VIEW_MODE,NETWORK_IS_SESSION_ACTIVE,IS_CUTSCENE_ACTIVE,IS_PLAYER_SWITCH_IN_PROGRESS,IS_SCREEN_FADED_OUT,IS_SCREEN_FADING_OUT,IS_SCREEN_FADING_IN,IS_GAMEPLAY_CAM_RENDERING,IS_CINEMATIC_CAM_RENDERING,SET_CINEMATIC_MODE_ACTIVE}
    public static class Function
    {
        public static bool Cinematic;public static int PedView=2,VehicleView=3;
        public static void Reset(){Cinematic=false;PedView=2;VehicleView=3;}
        public static T Call<T>(Hash hash,params object[] args)
        {
            object value=typeof(T)==typeof(int)?(object)0:false;
            if(hash==Hash.GET_FOLLOW_PED_CAM_VIEW_MODE)value=PedView;
            else if(hash==Hash.GET_FOLLOW_VEHICLE_CAM_VIEW_MODE)value=VehicleView;
            else if(hash==Hash.IS_GAMEPLAY_CAM_RENDERING)value=!Cinematic;
            else if(hash==Hash.IS_CINEMATIC_CAM_RENDERING)value=Cinematic;
            return (T)value;
        }
        public static void Call(Hash hash,params object[] args)
        {
            if(hash==Hash.SET_CINEMATIC_MODE_ACTIVE)Cinematic=(bool)args[0];
            else if(hash==Hash.SET_FOLLOW_PED_CAM_VIEW_MODE)PedView=(int)args[0];
            else if(hash==Hash.SET_FOLLOW_VEHICLE_CAM_VIEW_MODE)VehicleView=(int)args[0];
        }
    }
}
