using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FiveVibe;

internal static class AmbientTests
{
    private static int passed;
    private static void Check(bool condition,string name) { if(!condition) throw new Exception(name); passed++; }
    private static void Reject(Action action,string name) { bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(FormatException){rejected=true;}Check(rejected,name); }
    private static string WeatherJson(DateTime time,int code)
    { return "{\"current\":{\"time\":"+((long)(time-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds).ToString(CultureInfo.InvariantCulture)+",\"weather_code\":"+code+",\"is_day\":1}}"; }
    private sealed class Handler : HttpMessageHandler
    {
        public readonly List<string> Requests=new List<string>();
        public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        { Requests.Add(request.RequestUri.AbsoluteUri); return Respond(request,token); }
    }
    private static Task<HttpResponseMessage> Json(string body) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(body)}); }
    public static int Main()
    {
        string folder=Path.Combine(Path.GetTempPath(),"fivevibe-ambient-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var auto=new AutoCinemaPolicy();
            Check(auto.Update(false)==AutoCameraChange.None,"Idle does not open a camera");
            Check(auto.Update(true)==AutoCameraChange.StartCinema,"AI driving starts cinema by default");
            Check(auto.Update(true)==AutoCameraChange.None,"An active drive does not restart shots each frame");
            auto.ChooseManually(); Check(auto.Update(true)==AutoCameraChange.None,"First person chosen during a drive is respected");
            Check(auto.Update(false)==AutoCameraChange.None,"Ending a manually chosen view does not override it");
            Check(auto.Update(true)==AutoCameraChange.StartCinema,"A later drive can use auto cinema again");
            auto.Reset(); Check(auto.Update(false)==AutoCameraChange.None && !auto.OwnsView,"F8 returns to normal and cannot immediately reactivate cinema");
            auto.Enabled=false; Check(auto.Update(true)==AutoCameraChange.None,"Disabled automatic cinema stays off");
            auto.Reset(); auto.Enabled=true; auto.Update(true); Check(auto.Update(false)==AutoCameraChange.RestoreGameplay,"Finishing an automatic drive restores gameplay");
            Check(LocalPlanner.Parse("Disattiva cinema automatico",45).Action=="camera_auto_off","Italian automatic-camera disable works locally");
            var bike=LocalPlanner.Parse("prendi una moto",45); Check(bike.Action=="vehicle" && bike.VehicleType=="motorcycle","Get a motorcycle without implicitly driving");
            var ride=LocalPlanner.Parse("prendi una moto e fai un giro",45); Check(ride.Action=="cruise" && ride.VehicleType=="motorcycle","Motorcycle plus chill ride");
            Check(LocalPlanner.Parse("trova una jeep",45).VehicleType=="offroad","Jeep means off-road category");
            Check(LocalPlanner.Parse("prendi un SUV",45).VehicleType=="suv","SUV category");
            Check(LocalPlanner.Parse("prendi un auto sportiva",45).VehicleType=="sports","Sports car category");
            Check(VehicleSelection.Matches("motorcycle",8,false,true,"bati",true),"Motorcycle accepted");
            Check(!VehicleSelection.Matches("motorcycle",6,true,false,"comet",true),"Motorcycle request never substitutes a sports car");
            Check(VehicleSelection.Matches("suv",2,true,false,"baller",true) && !VehicleSelection.Matches("suv",9,true,false,"mesa",true),"SUV and off-road classes stay distinct");
            Check(VehicleSelection.Matches("sports",6,true,false,"comet",true) && !VehicleSelection.Matches("sports",7,true,false,"adder",true),"Sports and supercars stay distinct");
            Check(VehicleSelection.Matches("pickup",9,true,false,"sandking",true) && !VehicleSelection.Matches("pickup",9,true,false,"mesa",true),"Pickups use a bounded model family");
            Check(!VehicleSelection.Matches("any",8,false,true,"bati",true) && VehicleSelection.Matches("any",8,false,true,"bati",false),"Default search remains cars while an existing bike can be reused");
            Check(!VehicleSelection.Matches("any",13,false,false,"bmx",true),"Bicycles do not become motorcycles");
            Check(VehicleSelection.EntryTimeout(100)>50000,"A distant available vehicle has time for the actual walk");
            Check(VehicleSelection.EntryTimeout(10000)==90000 && VehicleSelection.EntryTimeout(-5)==22000,"Entry wait stays bounded");
            Check(LocalPlanner.Parse("andiamo a ls custom",45).Place=="ls_customs","Original LS Custom report maps to modification shop");
            Check(LocalPlanner.Parse("Drive to LS Customs La Mesa",45).Place=="customs_la_mesa","Specific Customs branch beats district alias");
            Check(LocalPlanner.Parse("Take a motorcycle to LS Customs",45).VehicleType=="motorcycle","Vehicle choice and destination combine");
            var wrong=CommandPlan.Create("place","current",45,"simeon_dealership");
            Check(PlaceCatalog.ConstrainDestination("andiamo a ls custom",wrong).Place=="ls_customs","Valid model output cannot send Customs request to Simeon");
            Check(PlaceCatalog.ConstrainDestination("Go to Simeon's Dealership",wrong).Place=="simeon_dealership","Actual dealer request remains a dealer");
            Check(!PlaceCatalog.IsCustoms("simeon_dealership"),"Dealer is not a Customs branch");
            Reject(()=>{var p=CommandPlan.Create("say","current",45);p.VehicleType="motorcycle";p.Validate();},"Vehicle choice cannot decorate arbitrary actions");
            var location=new RealLocation {Name="Test city",Latitude=41.902782,Longitude=12.496366}; location.Validate();
            Check(location.Latitude==41.9 && location.Longitude==12.5,"Only approximate coordinates are persisted and fetched");
            Reject(()=>new RealLocation {Name="Bad",Latitude=91,Longitude=0}.Validate(),"Latitude range");
            Reject(()=>new RealLocation {Name="Bad",Latitude=Double.NaN,Longitude=0}.Validate(),"Finite location values");
            Check(RealWeather.PresetFor(0,true)=="sunny" && RealWeather.PresetFor(0,false)=="clear","Day and night clear conditions");
            foreach(int code in new[] {51,53,55,56,57,61,63,65,66,67,80,81,82}) Check(RealWeather.PresetFor(code,true)=="rain","Rain code "+code);
            foreach(int code in new[] {71,73,75,77,85,86}) Check(RealWeather.PresetFor(code,true)=="snow","Snow code "+code);
            Check(RealWeather.PresetFor(45,true)=="fog" && RealWeather.PresetFor(99,true)=="thunder","Fog and storm presets");
            Reject(()=>RealWeather.PresetFor(999,true),"Unknown condition is not silently clear weather");
            Check(RealWeather.Parse(WeatherJson(DateTime.UtcNow,63),DateTime.UtcNow).Preset=="rain","Current API response parsing");
            Reject(()=>RealWeather.Parse(WeatherJson(DateTime.UtcNow.AddHours(-3),0),DateTime.UtcNow),"Stale data is rejected");
            string city; Check(RealWorldCommands.TryCity("meteo reale a Milano, Italia",out city) && city=="Milano, Italia","Native city request");
            Check(LocalPlanner.Parse("Sincronizza ora reale e meteo reale",45).Action=="realtime_both","Combined real-world sync");
            Check(LocalPlanner.Parse("Disattiva meteo reale",45).Action=="realtime_weather_off","Weather can stop without disabling the clock");
            Check(LocalPlanner.Parse("Set the time to 18:45",45).Action=="world_settings","Manual clock request stays a manual setting");
            var handler=new Handler(); handler.Respond=(request,token)=>request.RequestUri.Host.StartsWith("geocoding",StringComparison.Ordinal)?
                Json("{\"results\":[{\"name\":\"Test city\",\"country\":\"Test country\",\"latitude\":41.902782,\"longitude\":12.496366}]}"):Json(WeatherJson(DateTime.UtcNow,63));
            var reports=new List<string>();
            using(var service=new RealWorldService(Path.Combine(folder,"settings.json"),s=>reports.Add(s),new HttpClient(handler)))
            {
                service.Tick(true); Check(handler.Requests.Count==0,"No location or network fetch on first load");
                service.SetClock(true); service.Tick(true); Check(handler.Requests.Count==0 && service.ClockEnabled,"PC time requires no external service");
                service.SetWeather(true); service.Tick(true); Check(handler.Requests.Count==0,"Weather does not guess the player's location");
                service.SetCity("Test city, Test country");
                for(int i=0;i<100 && service.Current==null;i++) {service.Tick(true);Thread.Sleep(2);}
                Check(service.Current!=null && service.Current.Preset=="rain","Lookup then weather complete asynchronously");
                Check(!String.Join(" ",reports).Contains("Test city") && !service.Status.Contains("Test city"),"City stays in local companion state rather than AI conversation history");
                Check(handler.Requests.Count==2 && handler.Requests[1].Contains("latitude=41.90") && handler.Requests[1].Contains("longitude=12.50"),"Only configured coarse location is sent to fixed weather host");
                for(int i=0;i<10;i++)service.Tick(true);
                Check(handler.Requests.Count==2,"Weather is cached instead of fetched each frame");
                service.SetWeather(false); Check(service.ClockEnabled && !service.WeatherEnabled,"Independent sync controls");
            }
            var pending=new TaskCompletionSource<HttpResponseMessage>(); var slow=new Handler(); slow.Respond=(request,token)=>pending.Task;
            using(var service=new RealWorldService(Path.Combine(folder,"pending.json"),s=>{},new HttpClient(slow)))
            {
                service.SetCity("Old city"); service.SetLocation(new RealLocation {Name="New city",Latitude=10,Longitude=20});
                pending.SetResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"results\":[{\"name\":\"Old city\",\"country\":\"Test\",\"latitude\":0,\"longitude\":0}]}")});
                Thread.Sleep(10);service.Tick(false); Check(service.LocationName=="New city","Late geocoding result cannot overwrite newer location");
            }
            Console.WriteLine(passed+" ambient checks passed."); return 0;
        }
        catch(Exception e) {Console.Error.WriteLine("After "+passed+" checks: "+e.Message);return 1;}
        finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
}
