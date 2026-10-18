using System;
using System.Linq;
using GTA;
using GTA.Native;

namespace FiveVibe
{
    internal sealed class VehicleRadio : IVehicleRadio
    {
        public bool Blocked { get { return Game.IsPaused || Game.Player.Character.IsDead || !Game.Player.CanControlCharacter ||
            Function.Call<bool>(Hash.NETWORK_IS_SESSION_ACTIVE) || Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE) ||
            Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS) || Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT); } }
        public bool InVehicle { get { return Game.Player.Character.IsInVehicle(); } }
        public bool HasRadio { get { return Function.Call<bool>(Hash.DOES_PLAYER_VEH_HAVE_RADIO); } }
        public void Select(string nativeStation)
        {
            // One-shot retune, on the game thread. Do not unlock, force-enable or reapply every frame.
            var current=Game.Player.Character.CurrentVehicle;
            if(current==null || !current.Exists()) throw new InvalidOperationException();
            Function.Call(Hash.SET_VEH_RADIO_STATION,current.Handle,nativeStation);
            Function.Call(Hash.SET_RADIO_TO_STATION_NAME,nativeStation);
        }
        public object Snapshot()
        {
            try
            {
                if(Blocked) return State(false,"none","Unavailable","Radio unavailable while GTA has control.");
                if(!InVehicle) return State(false,"none","On foot","Get into a vehicle with a radio.");
                if(!HasRadio) return State(false,"none","Unavailable","This vehicle has no available radio.");
                string id=Function.Call<string>(Hash.GET_PLAYER_RADIO_STATION_NAME);
                bool off=string.IsNullOrEmpty(id) || id=="OFF" || id=="RADIO_OFF";
                var station=RadioCatalog.Find(id);
                string name=off?"Radio off":station!=null?station.Name:Game.GetLocalizedString(id);
                if(string.IsNullOrEmpty(name) || name=="NULL") name="Other GTA station";
                return State(true,off?"off":id,name,off?"Radio off":name);
            }
            catch(Exception) { return State(false,"none","Unavailable","Radio status unavailable."); }
        }
        private static object State(bool available,string station,string name,string status)
        { return new {available=available,station=station,name=name,status=status,stations=RadioCatalog.All.Select(s=>new {id=s.Id,name=s.Name}).ToArray()}; }
    }
}
