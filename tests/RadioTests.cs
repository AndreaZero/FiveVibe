using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Script.Serialization;
using FiveVibe;

internal static class RadioTests
{
    private static int passed;
    private static void Check(bool value,string description) { if(!value)throw new Exception(description); passed++; }
    private static void Reject(Action action,string description)
    { try { action(); } catch(ArgumentException) { passed++; return; } throw new Exception(description); }
    public static int Main()
    {
        try
        {
            foreach(var station in RadioCatalog.All)
            {
                var plan=LocalPlanner.Parse("Play "+station.Name,45);
                Check(plan.Action=="radio_set" && plan.Station==station.Id && plan.Actor=="current","Named station: "+station.Name);
            }
            Check(RadioCatalog.All.Select(s=>s.Id).Distinct().Count()==RadioCatalog.All.Length,"Unique station identifiers");
            string[] mirror={"Metti Radio Mirror Park","Sintonizza la radio su Radio Mirror Park","Switch the radio to Radio Mirror Park","Please play Radio Mirror Park, keep driving.","Metti Radio Mirror Park senza interrompere il giro","Radio Mirror Park"};
            foreach(string text in mirror) Check(LocalPlanner.Parse(text,70).Station=="RADIO_16_SILVERLAKE","English/Italian radio request: "+text);
            Check(LocalPlanner.Parse("Metti Non Stop Pop FM",45).Station=="RADIO_02_POP","Non Stop is a station, not a negation/Stop");
            Check(LocalPlanner.Parse("Play The Lowdown 91.1",45).Station=="RADIO_15_MOTOWN","Station frequency punctuation");
            Check(LocalPlanner.Parse("metti WCTR",45).Station=="RADIO_05_TALK_01","Station alias");
            foreach(string text in new[]{"Spegni la radio","Radio off","Turn the radio off","Turn off the radio","Mute the radio","Stop radio","Spegni la radio e continua a guidare"})
                Check(LocalPlanner.Parse(text,45).Action=="radio_off" && !LocalPlanner.IsImmediateStop(text),"Radio off does not stop driving: "+text);
            CommandPlan parsed;
            foreach(string text in new[]{"Metti Radio Atlantis","Non mettere Radio Mirror Park","Don't change the radio","Metti Radio Mirror Park e vai a Paleto Bay","Quali radio ci sono?"})
                Check(RadioCommands.TryParse(text,out parsed) && parsed.Action=="say","Unknown/negated/combined radio request makes no change: "+text);
            Check(!RadioCommands.TryParse("Drive to Mirror Park",out parsed) && LocalPlanner.Parse("Drive to Mirror Park",45).Action=="place","Place destination is not a station");
            Check(!RadioCommands.TryParse("Walk around Mirror Park",out parsed),"Walking not intercepted");
            Check(LocalPlanner.IsImmediateStop("Stop") && LocalPlanner.Parse("Stop",45).Action=="stop","Takeover still works");
            var selected=LocalPlanner.Parse("Play Rebel Radio",45);
            selected.Station="RADIO_FAKE"; Reject(()=>selected.Validate(),"Invented station rejected");
            selected=LocalPlanner.Parse("Play Rebel Radio",45); selected.Actor="trevor"; Reject(()=>selected.Validate(),"Radio cannot switch protagonists");
            selected=LocalPlanner.Parse("Play Rebel Radio",45); selected.Station="none"; Reject(()=>selected.Validate(),"Selection needs a station");
            selected=CommandPlan.Create("radio_off","current",45); selected.Station="RADIO_06_COUNTRY"; Reject(()=>selected.Validate(),"Off cannot carry a selection");
            selected=CommandPlan.Create("cruise","current",45); selected.Station="RADIO_06_COUNTRY"; Reject(()=>selected.Validate(),"Movement cannot smuggle radio changes");
            var radio=new FakeRadio();
            var tune=LocalPlanner.Parse("Play Radio Mirror Park",45);
            Check(RadioControl.Execute(tune,radio).Contains("Tuning to Radio Mirror Park") && radio.Selected==tune.Station && radio.Calls==1,"Retune happens once, without movement APIs");
            Check(RadioControl.Execute(CommandPlan.Create("radio_off","current",45),radio).Contains("Radio off requested") && radio.Selected=="OFF" && radio.Calls==2,"Off uses native OFF, no permanent radio disable");
            radio.InVehicle=false;
            Check(RadioControl.Execute(tune,radio).Contains("Get into") && radio.Calls==2,"On foot reports unavailable without changing anything");
            radio.InVehicle=true; radio.HasRadio=false;
            Check(RadioControl.Execute(tune,radio).Contains("no available radio") && radio.Calls==2,"Radio-less vehicle unchanged");
            radio.HasRadio=true; radio.Blocked=true;
            Check(RadioControl.Execute(tune,radio).Contains("GTA has control") && radio.Calls==2,"Story transition defers radio");
            radio.Blocked=false; radio.Throw=true;
            Check(RadioControl.Execute(tune,radio).Contains("active action is unchanged"),"Native radio error stays inside audio boundary");
            Reject(()=>RadioControl.Execute(CommandPlan.Create("stop","current",45),radio),"Audio boundary cannot execute Stop");
            var json=new JavaScriptSerializer();
            string response=json.Serialize(new {status="completed",output=new[]{new{type="function_call",name="plan_game_action",arguments=json.Serialize(tune)}}});
            using(var handler=new RadioHandler(response))
            using(var client=new HttpClient(handler))
            using(var planner=new OpenAiPlanner("test-only-token","test-model",client))
            {
                var result=planner.PlanAsync("A supported radio paraphrase","synthetic in_vehicle=true",new string[0],CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Action=="radio_set" && result.Station==tune.Station,"Structured AI radio response parsed");
                Check(handler.Body.Contains("\"Station\"") && handler.Body.Contains("RADIO_16_SILVERLAKE") && handler.Body.Contains("Station=none for ALL non-radio_set"),"AI schema and instructions constrain station selection");
            }
            Console.WriteLine(passed+" radio checks passed."); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine("After "+passed+" radio checks: "+e.Message); return 1; }
    }
    private sealed class FakeRadio : IVehicleRadio
    {
        public bool Blocked {get;set;} public bool InVehicle {get;set;} public bool HasRadio {get;set;}
        public bool Throw; public string Selected; public int Calls;
        public FakeRadio() { InVehicle=true; HasRadio=true; }
        public void Select(string station) { if(Throw)throw new InvalidOperationException(); Selected=station; Calls++; }
    }
    private sealed class RadioHandler : HttpMessageHandler
    {
        private readonly string response; public string Body;
        public RadioHandler(string value) { response=value; }
        protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        { Body=await request.Content.ReadAsStringAsync(); return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(response)}; }
    }
}
