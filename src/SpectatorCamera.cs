using System;
using GTA;
using GTA.Native;

namespace FiveVibe
{
    // GTA owns every camera and cinematic shot. No scripted camera is created.
    public sealed class SpectatorCamera : IDisposable
    {
        private int savedPedView,savedVehicleView,lastView,nativeToggleAt;
        private bool savedViews,ownsNative;
        private readonly Action<string> say;
        private readonly AutoCinemaPolicy automatic=new AutoCinemaPolicy();
        public bool AutoCinema {get{return automatic.Enabled;}}
        public SpectatorView Mode {get;private set;}
        public string Status {get;private set;}
        public string ModeName {get{return CameraCommands.Label(Mode);}}
        public SpectatorCamera(Action<string> report,bool autoCinema=true)
        {say=report;automatic.Enabled=autoCinema;Status="Gameplay";}
        public void UpdateDriving(bool driving)
        {
            var change=automatic.Update(driving);
            if(change==AutoCameraChange.StartCinema)SetMode(SpectatorView.NativeCinema,false);
            else if(change==AutoCameraChange.RestoreGameplay)RestoreGameplay();
        }
        public void Execute(string action)
        {
            if(action=="camera_auto_on" || action=="camera_auto_off")
            {
                automatic.Enabled=action=="camera_auto_on";
                if(!AutoCinema && automatic.OwnsView) {RestoreGameplay();automatic.OwnsView=false;}
                if(AutoCinema)automatic.ManualChoice=false;
                if(AutoCinema && automatic.Driving) {SetMode(SpectatorView.NativeCinema,false);automatic.OwnsView=true;}
                say(AutoCinema?"Automatic GTA cinema on.":"Automatic GTA cinema off.");return;
            }
            automatic.ChooseManually();
            SetMode(action=="camera_cycle"?(SpectatorView)(((int)Mode+1)%4):action=="camera_first"?SpectatorView.FirstPerson:
                action=="camera_third"?SpectatorView.ThirdPerson:action=="camera_native"?SpectatorView.NativeCinema:SpectatorView.Gameplay,true);
        }
        private void SetMode(SpectatorView mode,bool announce)
        {
            if(mode==SpectatorView.Gameplay)RestoreGameplay();
            else
            {
                if(!savedViews)
                {
                    savedPedView=Function.Call<int>(Hash.GET_FOLLOW_PED_CAM_VIEW_MODE);
                    savedVehicleView=Function.Call<int>(Hash.GET_FOLLOW_VEHICLE_CAM_VIEW_MODE);savedViews=true;
                }
                Suspend();Mode=mode;lastView=Game.GameTime-1000;Status=ModeName;
            }
            if(announce)say(ModeName+" selected. Your active action continues.");
        }
        public void Tick(bool autonomous,bool chatOpen)
        {
            if(Mode==SpectatorView.Gameplay)return;
            if(Game.IsPaused || Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE) || Game.Player.Character.IsDead ||
                !Game.Player.CanControlCharacter || Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) ||
                Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS) || Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT) ||
                Function.Call<bool>(Hash.IS_SCREEN_FADING_OUT) || Function.Call<bool>(Hash.IS_SCREEN_FADING_IN))
            {Suspend();return;}
            if(Mode==SpectatorView.NativeCinema)
            {
                if(chatOpen || !Game.Player.Character.IsInVehicle())
                {Suspend();Status=chatOpen?"GTA cinema / chat open":"GTA cinema / enter a vehicle";return;}
                if(Function.Call<bool>(Hash.IS_GAMEPLAY_CAM_RENDERING) && (!ownsNative ||
                    !Function.Call<bool>(Hash.IS_CINEMATIC_CAM_RENDERING) && unchecked(Game.GameTime-nativeToggleAt)>1000))
                {Function.Call(Hash.SET_CINEMATIC_MODE_ACTIVE,true);ownsNative=true;nativeToggleAt=Game.GameTime;}
            }
            else if(unchecked(Game.GameTime-lastView)>300 && Function.Call<bool>(Hash.IS_GAMEPLAY_CAM_RENDERING))
            {
                int view=Mode==SpectatorView.FirstPerson?4:1;
                Function.Call(Hash.SET_FOLLOW_PED_CAM_VIEW_MODE,view);
                Function.Call(Hash.SET_FOLLOW_VEHICLE_CAM_VIEW_MODE,view);lastView=Game.GameTime;
            }
            Status=ModeName;
        }
        public void Suspend()
        {
            if(ownsNative){Function.Call(Hash.SET_CINEMATIC_MODE_ACTIVE,false);ownsNative=false;}
            Status=ModeName+" / waiting for gameplay";
        }
        private void RestoreGameplay()
        {
            Suspend();Mode=SpectatorView.Gameplay;Status="Gameplay";
            if(savedViews)
            {
                Function.Call(Hash.SET_FOLLOW_PED_CAM_VIEW_MODE,savedPedView);
                Function.Call(Hash.SET_FOLLOW_VEHICLE_CAM_VIEW_MODE,savedVehicleView);savedViews=false;
            }
        }
        public void Reset() {RestoreGameplay();automatic.Reset();}
        public void ReleaseForManualView() {RestoreGameplay();automatic.ChooseManually();}
        public void Dispose() {Reset();}
    }
}
