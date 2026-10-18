using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace FiveVibe
{
    public sealed class SpectatorCamera : IDisposable
    {
        private Camera camera;
        private readonly CameraShotClock clock=new CameraShotClock();
        private int subjectHandle, lastFrame, lastView, probe, generation, probeGeneration, nativeToggleAt;
        private int? retryAt;
        private int savedPedView, savedVehicleView;
        private bool savedViews, ownsNative, cut=true;
        private float subjectLength=4.5f, clearance=1, probeClearance=1, probeDistance;
        private Vector3 smoothPosition, probeOrigin, previousSubjectPosition, subjectMin, subjectMax;
        private readonly Action<string> say;
        private readonly AutoCinemaPolicy automatic=new AutoCinemaPolicy();
        public bool AutoCinema { get { return automatic.Enabled; } }
        public SpectatorView Mode { get; private set; }
        public bool Letterbox { get; private set; }
        public string Status { get; private set; }
        public bool Rendering { get { return camera != null && camera.Exists() && Function.Call<int>(Hash.GET_RENDERING_CAM)==camera.Handle; } }
        public string ModeName { get { return CameraCommands.Label(Mode); } }

        public SpectatorCamera(Action<string> report,bool letterbox,bool autoCinema=true)
        { say=report; Letterbox=letterbox; automatic.Enabled=autoCinema; Status="Gameplay"; }

        public void UpdateDriving(bool driving)
        {
            var change=automatic.Update(driving);
            if(change==AutoCameraChange.StartCinema) SetMode(SpectatorView.Cinema,false);
            else if(change==AutoCameraChange.RestoreGameplay) RestoreGameplay();
        }

        public void Execute(string action)
        {
            if(action=="camera_auto_on" || action=="camera_auto_off")
            {
                automatic.Enabled=action=="camera_auto_on";
                if(AutoCinema) automatic.ManualChoice=false;
                if(!AutoCinema && automatic.OwnsView) { RestoreGameplay(); automatic.OwnsView=false; }
                if(AutoCinema && automatic.Driving) { SetMode(SpectatorView.Cinema,false); automatic.OwnsView=true; automatic.ManualChoice=false; }
                say(AutoCinema?"Automatic driving cinema on.":"Automatic driving cinema off."); return;
            }
            if(action=="camera_bars" || action=="camera_bars_on" || action=="camera_bars_off")
            { Letterbox=action=="camera_bars_on" || (action=="camera_bars" && !Letterbox); say("Cinema letterbox "+(Letterbox?"on.":"off.")); return; }
            automatic.ChooseManually();
            if(action=="camera_next")
            {
                if(Mode!=SpectatorView.Cinema) SetMode(SpectatorView.Cinema);
                else { clock.Next(Game.GameTime); cut=true; generation++; say("Cinema: next shot."); }
                return;
            }
            var mode=action=="camera_cycle" ? (SpectatorView)(((int)Mode+1)%5) : action=="camera_first" ? SpectatorView.FirstPerson :
                action=="camera_third" ? SpectatorView.ThirdPerson : action=="camera_cinema" ? SpectatorView.Cinema :
                action=="camera_native" ? SpectatorView.NativeCinema : SpectatorView.Gameplay;
            SetMode(mode);
        }

        private void SetMode(SpectatorView mode,bool announce=true)
        {
            if(mode==SpectatorView.Gameplay) { RestoreGameplay(); if(announce) say("Gameplay camera restored."); return; }
            if(!savedViews)
            {
                savedPedView=Function.Call<int>(Hash.GET_FOLLOW_PED_CAM_VIEW_MODE);
                savedVehicleView=Function.Call<int>(Hash.GET_FOLLOW_VEHICLE_CAM_VIEW_MODE);
                savedViews=true;
            }
            Suspend(); Mode=mode; subjectHandle=0; lastView=Game.GameTime-1000; retryAt=null;
            clock.Reset(Game.GameTime); cut=true;
            if(announce) say(ModeName+" selected. Camera changes keep your active action running.");
        }

        public void Tick(bool autonomous,bool chatOpen)
        {
            ReadProbe();
            if(Mode==SpectatorView.Gameplay) return;
            if(Game.IsPaused || Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE) || Game.Player.Character.IsDead ||
                !Game.Player.CanControlCharacter || Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) ||
                Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS) || Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT) ||
                Function.Call<bool>(Hash.IS_SCREEN_FADING_OUT) || Function.Call<bool>(Hash.IS_SCREEN_FADING_IN))
            { Suspend(); Status=ModeName+" / waiting for gameplay"; return; }
            Ped player=Game.Player.Character;
            Entity subject=player.IsInVehicle() ? (Entity)player.CurrentVehicle : player;
            if(subject==null || !subject.Exists()) { Suspend(); return; }
            if(subject.Handle!=subjectHandle)
            {
                Suspend(); subjectHandle=subject.Handle; subject.Model.GetDimensions(out subjectMin,out subjectMax);
                subjectLength=Math.Max(2,Math.Min(9,subjectMax.Y-subjectMin.Y)); clock.Reset(Game.GameTime); cut=true;
            }
            if(Mode==SpectatorView.FirstPerson || Mode==SpectatorView.ThirdPerson)
            {
                Status=ModeName;
                if(unchecked(Game.GameTime-lastView)>300 && Function.Call<bool>(Hash.IS_GAMEPLAY_CAM_RENDERING))
                {
                    int view=Mode==SpectatorView.FirstPerson?4:1;
                    Function.Call(Hash.SET_FOLLOW_PED_CAM_VIEW_MODE,view);
                    Function.Call(Hash.SET_FOLLOW_VEHICLE_CAM_VIEW_MODE,view); lastView=Game.GameTime;
                }
                return;
            }
            if(Mode==SpectatorView.NativeCinema)
            {
                if(!player.IsInVehicle()) { StopNative(); Status="GTA cinema / enter a vehicle"; return; }
                if(Function.Call<bool>(Hash.IS_GAMEPLAY_CAM_RENDERING) && (!ownsNative ||
                    (!Function.Call<bool>(Hash.IS_CINEMATIC_CAM_RENDERING) && unchecked(Game.GameTime-nativeToggleAt)>1000)))
                { Function.Call(Hash.SET_CINEMATIC_MODE_ACTIVE,true); ownsNative=true; nativeToggleAt=Game.GameTime; }
                Status=ModeName; return;
            }
            // A script camera changes movement orientation for manual walking, so the director is for watching autonomy.
            if(!autonomous) { Suspend(); Status="Cinema director / waiting for an AI action"; return; }
            if(camera!=null && camera.Exists() && !Rendering)
            { Suspend(); Status="Cinema director / game camera in use"; return; }
            if(camera==null && (!Function.Call<bool>(Hash.IS_GAMEPLAY_CAM_RENDERING) || (retryAt.HasValue && unchecked(Game.GameTime-retryAt.Value)<0)))
            { Status="Cinema director / waiting for gameplay camera"; return; }
            if(clock.Update(Game.GameTime)) { cut=true; generation++; }
            float dt=lastFrame==0 ? .016f : Math.Max(.001f,Math.Min(.1f,unchecked(Game.GameTime-lastFrame)/1000f)); lastFrame=Game.GameTime;
            bool inCar=subject is Vehicle;
            var framing=CinemaFraming.Sample(clock.Shot,clock.Progress(Game.GameTime),subject.Speed,inCar,subjectLength);
            Vector3 forward=subject.ForwardVector; forward.Z=0;
            if(forward.Length()<.01f) forward=new Vector3(0,1,0); else forward.Normalize();
            Vector3 right=new Vector3(forward.Y,-forward.X,0);
            Vector3 focus=subject.Position+new Vector3(0,0,inCar?.75f:1.1f)+forward*framing.Lead;
            Vector3 desired=subject.Position+right*framing.Right+forward*framing.Forward+new Vector3(0,0,framing.Height+(inCar?.3f:1.1f));
            if(cut)
            {
                // One synchronous check on a cut; continuous checks below are asynchronous.
                var hit=World.Raycast(focus,desired,(IntersectFlags)339,subject);
                clearance=probeClearance=CinemaFraming.Clearance(focus.DistanceTo(desired),hit.DidHit,focus.DistanceTo(hit.HitPosition));
                smoothPosition=desired;
            }
            else
            {
                smoothPosition+=subject.Position-previousSubjectPosition;
                smoothPosition+=(desired-smoothPosition)*CinemaFraming.Blend(dt);
            }
            previousSubjectPosition=subject.Position;
            StartProbe(focus,smoothPosition,subject);
            clearance=probeClearance<clearance ? probeClearance : Math.Min(probeClearance,clearance+dt*1.5f);
            Vector3 position=focus+(smoothPosition-focus)*clearance;
            Vector3 local=subject.GetPositionOffset(position);
            bool inside=local.X>subjectMin.X-.4f && local.X<subjectMax.X+.4f &&
                local.Y>subjectMin.Y-.4f && local.Y<subjectMax.Y+.4f && local.Z>subjectMin.Z-.4f && local.Z<subjectMax.Z+.4f;
            if(clearance<.22f || inside)
            {
                Suspend(); retryAt=Game.GameTime+600; clock.Next(Game.GameTime);
                Status="Cinema director / finding a clear shot"; return;
            }
            if(camera==null)
            {
                camera=Camera.Create(ScriptedCameraNameHash.DefaultScriptedCamera,position,Vector3.Zero,framing.Fov);
                if(camera==null) { Status="Cinema director / camera unavailable"; return; }
                camera.PointAt(focus); camera.IsActive=true;
                Function.Call(Hash.RENDER_SCRIPT_CAMS,true,false,0,true,false,0);
            }
            camera.Position=position; camera.PointAt(focus);
            camera.FieldOfView=cut ? framing.Fov : camera.FieldOfView+(framing.Fov-camera.FieldOfView)*CinemaFraming.Blend(dt);
            cut=false; Status="Cinema / "+CinemaFraming.ShotNames[clock.Shot];
            if(Letterbox && !chatOpen)
            {
                Function.Call(Hash.DRAW_RECT,.5f,.0375f,1f,.075f,0,0,0,255,false);
                Function.Call(Hash.DRAW_RECT,.5f,.9625f,1f,.075f,0,0,0,255,false);
            }
        }

        private void ReadProbe()
        {
            if(probe!=0)
            {
                var hit=new OutputArgument(); var end=new OutputArgument(); var normal=new OutputArgument(); var entity=new OutputArgument();
                int result=Function.Call<int>(Hash.GET_SHAPE_TEST_RESULT,probe,hit,end,normal,entity);
                if(result==2)
                {
                    if(probeGeneration==generation) probeClearance=CinemaFraming.Clearance(probeDistance,hit.GetResult<bool>(),probeOrigin.DistanceTo(end.GetResult<Vector3>()));
                    probe=0;
                }
                else if(result==0) probe=0;
            }
        }
        private void StartProbe(Vector3 origin,Vector3 target,Entity subject)
        {
            if(probe==0)
            {
                probeOrigin=origin; probeDistance=origin.DistanceTo(target); probeGeneration=generation;
                probe=Function.Call<int>(Hash.START_SHAPE_TEST_SWEPT_SPHERE,origin.X,origin.Y,origin.Z,target.X,target.Y,target.Z,.35f,339,subject.Handle,7);
            }
        }
        private void StopNative()
        { if(ownsNative) { Function.Call(Hash.SET_CINEMATIC_MODE_ACTIVE,false); ownsNative=false; } }
        public void Suspend()
        {
            StopNative();
            if(camera!=null && camera.Exists())
            {
                if(Rendering) Function.Call(Hash.RENDER_SCRIPT_CAMS,false,false,0,true,false,0);
                camera.Delete();
            }
            camera=null; generation++; cut=true; lastFrame=0;
            Status=ModeName+" / waiting for gameplay";
        }
        public void Reset()
        { RestoreGameplay(); automatic.Reset(); }
        public void ReleaseForManualView() { RestoreGameplay(); automatic.ChooseManually(); }
        private void RestoreGameplay()
        {
            Suspend(); Mode=SpectatorView.Gameplay; Status="Gameplay";
            if(savedViews)
            {
                Function.Call(Hash.SET_FOLLOW_PED_CAM_VIEW_MODE,savedPedView);
                Function.Call(Hash.SET_FOLLOW_VEHICLE_CAM_VIEW_MODE,savedVehicleView);
                savedViews=false;
            }
        }
        public void Dispose() { Reset(); }
    }
}
