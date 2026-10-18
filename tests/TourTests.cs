using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Script.Serialization;
using FiveVibe;

internal static class TourTests
{
    private static int passed;
    private static void Check(bool value,string name) {if(!value)throw new Exception(name);passed++;}
    private static void Reject(Action action,string name) {try{action();}catch(ArgumentException){passed++;return;}throw new Exception(name);}
    public static int Main()
    {
        try
        {
            Check(TourCatalog.All.Length==2,"Exactly two supported themes");
            Check(string.Join(",",TourCatalog.ForAction("tour_coast").Stops)=="pacific_bluffs,chumash,paleto_forest,paleto_bay","Coastal itinerary order");
            Check(string.Join(",",TourCatalog.ForAction("tour_countryside").Stops)=="route_68,sandy_shores,grapeseed,alamo_sea","Countryside itinerary order");
            foreach(var profile in TourCatalog.All)
            {
                Check(profile.Stops.Length==4 && profile.Stops.Distinct().Count()==4,"Bounded distinct stops for "+profile.Id);
                Check(profile.Stops.All(id=>PlaceCatalog.Get(id)!=null && !id.StartsWith("fav_")),"Known stable outdoor catalog stops for "+profile.Id);
                var clone=profile.Stops;clone[0]="made_up";Check(profile.Stops[0]!="made_up","External callers cannot mutate routes");
                var session=new TourSession();session.Start("tour_"+profile.Id,1000);
                Check(session.Active && session.Index==0 && session.CurrentPlace==profile.Stops[0] && !session.WaitingForStop,"Start at first leg");
                Check(!session.Complete() && session.Active,"Starting a tour is not completion");
                for(int i=1;i<4;i++)
                {
                    Check(session.ReachStop() && session.Index==i && session.CurrentPlace==profile.Stops[i] && session.Active,"Advance one stop without ending drive: "+i);
                    Check(!session.Complete(),"Intermediate arrival cannot report success");
                }
                Check(!session.ReachStop() && session.WaitingForStop && session.Active && session.Result=="active","Last arrival waits for physical braking");
                Check(!session.ReachStop() && session.Index==3,"Repeated last arrival cannot run past the route");
                Check(session.Complete() && !session.Active && session.Result=="completed","Confirmed final stop completes itinerary");
                session.Cancel();Check(session.Result=="completed" && !session.Complete(),"Cleanup does not change a completed result or repeat completion");
                session.Start("tour_"+profile.Id,2000);session.ReachStop();session.Cancel();
                Check(!session.Active && session.Result=="cancelled" && !session.ReachStop() && !session.Complete(),"Cancellation cannot advance/restart or claim completion");
                session.Start("tour_"+profile.Id,3000);
                Check(!session.Expired(3000+TourSession.MaximumMilliseconds-1) && session.Expired(3000+TourSession.MaximumMilliseconds),"Bounded total duration across legs");
                session.ReachStop();Check(session.Started==3000 && session.Expired(3000+TourSession.MaximumMilliseconds),"Advancing a leg does not reset whole-tour timeout");
                session.Cancel();Check(!session.Expired(int.MaxValue),"Cancelled tours do not retain an active timer");
                session.Start("tour_"+profile.Id,int.MaxValue-1000);
                Check(!session.Expired(unchecked(int.MinValue+1000)),"Game timer wrap does not cause premature expiry");
            }
            var switched=new TourSession();switched.Start("tour_coast",10);switched.ReachStop();switched.Start("tour_countryside",20);
            Check(switched.Index==0 && switched.CurrentPlace=="route_68" && switched.Started==20,"Changing theme starts a new itinerary");
            Reject(()=>switched.Start("tour_mountain",30),"Invented theme rejected");
            Check(switched.Profile.Id=="countryside" && switched.Active,"Rejected theme does not partially replace session");
            string[] coastal={"Fammi un giro sulla costa","Facciamo un giro lungo la costa","Avvia il giro costiero","Start coastal drive","Take a coastal drive","Please take me for a coastal ride.","Drive along the coast","Coastal tour"};
            foreach(string input in coastal)Check(LocalPlanner.Parse(input,55).Action=="tour_coast","Coastal local request: "+input);
            string[] country={"Facciamo un giro in campagna","Giro in campagna","Portami a fare un giro in campagna","Start countryside drive","Take a rural drive","Take me for a drive through the countryside","Countryside tour"};
            foreach(string input in country)Check(LocalPlanner.Parse(input,55).Action=="tour_countryside","Countryside local request: "+input);
            foreach(string input in new[]{"Non fare un giro sulla costa","Don't start a coastal drive","Start coastal drive and countryside drive","Start coastal drive and turn off radio","Start mountain tour","Fammi un giro in montagna"})
                Check(LocalPlanner.Parse(input,45).Action=="say","Negated, combined or unknown theme is not movement: "+input);
            CommandPlan parsed;
            Check(!TourCommands.TryParse("Drive to Chumash on the coast",45,out parsed) && LocalPlanner.Parse("Drive to Chumash on the coast",45).Place=="chumash","A named destination on the coast is not a tour request");
            Check(LocalPlanner.Parse("Take a chill drive",45).Action=="cruise","Existing free-roam cruise preserved");
            Check(!TourCommands.TryParse("Play Radio Mirror Park",45,out parsed) && !TourCommands.TryParse("Stop",45,out parsed),"Radio and takeover are not tour starts");
            var plan=LocalPlanner.Parse("Start coastal drive",65);Check(plan.SpeedKmh==65 && plan.Place=="none" && plan.Actor=="current","Tour uses current driver and requested/default speed, not an arbitrary place");
            plan.Place="beach";Reject(()=>plan.Validate(),"Tour cannot smuggle a separate destination");
            plan=LocalPlanner.Parse("Start coastal drive",45);plan.Actor="trevor";Reject(()=>plan.Validate(),"Tour cannot silently switch character");
            plan=LocalPlanner.Parse("Start coastal drive",45);plan.Station="RADIO_02_POP";Reject(()=>plan.Validate(),"Tour cannot smuggle a radio change");
            plan=LocalPlanner.Parse("Start coastal drive",45);plan.WeatherType="rain";Reject(()=>plan.Validate(),"Tour cannot smuggle a weather change");
            plan=LocalPlanner.Parse("Start countryside drive",45);plan.VehicleType="suv";plan.Validate();Check(plan.VehicleType=="suv","Validated AI vehicle category can use ordinary vehicle acquisition");
            var json=new JavaScriptSerializer();
            string response=json.Serialize(new {status="completed",output=new[]{new{type="function_call",name="plan_game_action",arguments=json.Serialize(plan)}}});
            using(var handler=new TourHandler(response))using(var client=new HttpClient(handler))using(var planner=new OpenAiPlanner("test-only-token","test-model",client))
            {
                Check(planner.PlanAsync("A countryside drive with an SUV","synthetic in_vehicle=true",new string[0],CancellationToken.None).GetAwaiter().GetResult().Action=="tour_countryside","Structured AI tour plan accepted");
                Check(handler.Body.Contains("tour_coast") && handler.Body.Contains("tour_countryside") && handler.Body.Contains("without per-leg AI calls"),"AI schema and instructions constrain fixed tour actions");
            }
            Console.WriteLine(passed+" tour checks passed.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine("After "+passed+" tour checks: "+e.Message);return 1;}
    }
    private sealed class TourHandler:HttpMessageHandler
    {
        private readonly string response;public string Body;
        public TourHandler(string value){response=value;}
        protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {Body=await request.Content.ReadAsStringAsync();return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(response)};}
    }
}
