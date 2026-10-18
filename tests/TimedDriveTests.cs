using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Script.Serialization;
using FiveVibe;

internal static class TimedDriveTests
{
    private static int passed;
    private static void Check(bool value,string name) {if(!value)throw new Exception(name);passed++;}
    private static void Reject(Action action,string name) {try{action();}catch(ArgumentException){passed++;return;}throw new Exception(name);}
    public static int Main()
    {
        try
        {
            string[] valid={"fai un giro di 20 minuti poi torna al punto di partenza","Fammi un giro per 20 minuti, e poi ritorna qui!","Facciamo un giro di 20 minuti e torna alla partenza","Guida per 20 minuti poi torna qui","Drive for 20 minutes then return to the starting point","Please cruise around for 20 minutes, and then come back here.","Take me for a drive for 20 minutes then go back to the start"};
            foreach(string text in valid)
            {
                var plan=LocalPlanner.Parse(text,55);
                Check(plan.Action=="timed_drive" && plan.DurationMinutes==20,"Timed request parsed: "+text);
                Check(plan.Actor=="current" && plan.Place=="none" && plan.SpeedKmh==55,"Duration does not become speed, destination or character");
            }
            foreach(int minutes in new[]{1,5,20,60,120})
                Check(LocalPlanner.Parse("Drive for "+minutes+" minutes then return to the starting point",45).DurationMinutes==minutes,"Supported duration "+minutes);
            Check(LocalPlanner.Parse("Fai un giro di 1 minuto poi torna qui",45).DurationMinutes==1,"Italian singular minute");
            foreach(string text in new[]{"Drive for 0 minutes then return here","Drive for -5 minutes then return here","Drive for 121 minutes then return here","Drive for 1.5 minutes then return here","Drive for 999999999999999 minutes then return here","Non fare un giro di 20 minuti poi torna qui","Don't drive for 20 minutes then return here","Drive for 20 minutes then return here and switch to Trevor","Fai un giro sulla costa di 20 minuti poi torna qui","Drive to Paleto Bay for 20 minutes then return here","Drive for 20 minutes","Fai un giro di 20 minuti poi spegni la radio"})
                Check(LocalPlanner.Parse(text,45).Action=="say","Invalid or incompatible request does not start unlimited movement: "+text);
            Check(LocalPlanner.Parse("Take a chill drive",45).Action=="cruise","Untimed cruise preserved");
            Check(LocalPlanner.Parse("Start coastal drive",45).Action=="tour_coast","Untimed tour preserved");
            Check(LocalPlanner.Parse("Stop",45).Action=="stop","Immediate takeover preserved");
            CommandPlan ignored;
            Check(!TimedDriveCommands.TryParse("Play Radio Mirror Park",45,out ignored),"Audio controls bypass timing parser");
            Check(!TimedDriveCommands.TryParse("I'd like to cruise for twenty minutes and return to where I started",45,out ignored,false),"Noncanonical natural language reaches AI in AI mode");
            Check(TimedDriveCommands.TryParse("Drive for -5 minutes then return here",45,out ignored,false) && ignored.Action=="say","Invalid numeric duration is clarified before AI too");
            Check(TimedDriveCommands.TryParse("Cruise for 1.5 minutes then return here",45,out ignored,false) && ignored.Action=="say","Fractional numeric duration cannot be rounded by AI");
            var p=TimedDriveCommands.Create(20,45);p.DurationMinutes=0;Reject(()=>p.Validate(),"Zero duration rejected on AI/native boundary");
            p.DurationMinutes=121;Reject(()=>p.Validate(),"Excessive duration rejected on AI/native boundary");
            p.DurationMinutes=20;p.Actor="trevor";Reject(()=>p.Validate(),"Timed drive cannot switch protagonist");
            p.Actor="current";p.Place="beach";Reject(()=>p.Validate(),"Timed drive cannot smuggle a destination");
            p.Place="none";p.Station="RADIO_02_POP";Reject(()=>p.Validate(),"Timed drive cannot smuggle radio change");
            p.Station="none";p.WeatherType="rain";Reject(()=>p.Validate(),"Timed drive cannot smuggle world changes");
            p.WeatherType="none";p.VehicleType="suv";p.Validate();Check(p.VehicleType=="suv","Ordinary acquisition supports validated AI vehicle choice");
            var other=CommandPlan.Create("cruise","current",45);other.DurationMinutes=20;Reject(()=>other.Validate(),"Other actions must use neutral duration");
            var session=new TimedDriveSession();
            Check(!session.Active && session.Stage=="idle" && !session.ReturnDue && !session.BeginReturn() && !session.Complete(),"Idle is not an active/completed drive");
            session.Start(20);session.Sample(500000,true);
            Check(session.Active && session.Stage=="acquiring" && session.RemainingSeconds==1200 && session.ElapsedMilliseconds==0,"Finding a car does not consume the cruise budget");
            session.BeginCruise(1000000);session.Sample(1059999,true);
            Check(session.RemainingSeconds==1141 && !session.ReturnDue && !session.Complete(),"Countdown uses real milliseconds and rounds up seconds");
            long beforePause=session.ElapsedMilliseconds;
            session.Sample(1060000,false);session.Sample(4660000,false);session.Sample(4660001,true);
            Check(session.ElapsedMilliseconds==beforePause,"Long pause and resume boundary do not consume duration");
            session.Sample(4661001,true);
            Check(session.ElapsedMilliseconds==beforePause+1000,"Countdown resumes after pause");
            session.BeginCruise(9999999);
            Check(session.ElapsedMilliseconds==beforePause+1000,"Repeated start cannot reset a running clock");
            session.Sample(4661001+1140001,true);
            Check(session.ReturnDue && session.RemainingSeconds==0 && session.Stage=="cruising","Full 20-minute cruise reaches return deadline");
            Check(session.BeginReturn() && session.Stage=="returning" && session.Active && !session.ReturnDue,"Return is a continuous active stage after the cruise");
            Check(!session.BeginReturn() && !session.Complete(),"Repeated ticks cannot reissue the return or report success while traveling");
            session.Sample(5801002+10000,true);
            Check(session.ReturnMilliseconds==10000 && !session.ReturnExpired,"Return travel has a separate budget");
            session.ReachOrigin();Check(session.Stage=="braking" && session.Active,"Arrival must still physically stop");
            Check(session.Complete() && session.Stage=="completed" && !session.Active,"Only a confirmed stop completes return");
            session.Cancel();Check(session.Stage=="completed" && !session.Complete(),"Cleanup preserves completed result without repeating success");
            foreach(string stage in new[]{"acquiring","cruising","returning","braking"})
            {
                session.Start(1);
                if(stage!="acquiring")session.BeginCruise(0);
                if(stage=="returning" || stage=="braking") {session.Sample(60000,true);session.BeginReturn();}
                if(stage=="braking")session.ReachOrigin();
                session.Cancel();session.Sample(999999999,true);session.ReachOrigin();session.BeginCruise(0);
                Check(!session.Active && session.Stage=="cancelled" && !session.ReturnDue && !session.ReturnExpired && !session.BeginReturn() && !session.Complete(),"Cancellation cannot return/restart/complete from "+stage);
            }
            session.Start(1);session.BeginCruise(int.MaxValue-30000);session.Sample(unchecked(int.MinValue+29998),true);
            Check(!session.ReturnDue && session.ElapsedMilliseconds==59999,"Monotonic tick wrap does not expire early");
            session.Sample(unchecked(int.MinValue+29999),true);Check(session.ReturnDue,"Deadline works across signed tick wrap");session.BeginReturn();
            int returnStart=unchecked(int.MinValue+29999);
            session.Sample(returnStart+TimedDriveSession.ReturnLimitMilliseconds-1,true);Check(!session.ReturnExpired,"Return limit does not expire early");
            session.Sample(returnStart+TimedDriveSession.ReturnLimitMilliseconds,false);session.Sample(returnStart+TimedDriveSession.ReturnLimitMilliseconds+100000,false);
            Check(!session.ReturnExpired,"Pauses also suspend return travel limit");
            session.Sample(0,true);session.Sample(1,true);Check(session.ReturnExpired,"Return limit resumes after pause");
            session.Cancel();session.Start(120);Check(session.Stage=="acquiring" && session.ElapsedMilliseconds==0 && session.ReturnMilliseconds==0 && session.Minutes==120,"New drive clears old timing and phase");
            Reject(()=>session.Start(0),"Invalid session start rejected");Check(session.Active && session.Minutes==120,"Rejected start does not mutate session");
            var json=new JavaScriptSerializer();p=TimedDriveCommands.Create(20,45);
            string response=json.Serialize(new{status="completed",output=new[]{new{type="function_call",name="plan_game_action",arguments=json.Serialize(p)}}});
            using(var handler=new Handler(response))using(var client=new HttpClient(handler))using(var planner=new OpenAiPlanner("test-only-token","test-model",client))
            {
                var result=planner.PlanAsync("Cruise twenty minutes and bring me back","synthetic state",new string[0],CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Action=="timed_drive" && result.DurationMinutes==20,"Structured AI plan retains duration");
                Check(handler.Body.Contains("DurationMinutes") && handler.Body.Contains("timed_drive") && handler.Body.Contains("Return travel takes additional time"),"AI schema and instructions describe duration and return semantics");
            }
            Console.WriteLine(passed+" timed-drive checks passed.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine("After "+passed+" timed-drive checks: "+e.Message);return 1;}
    }
    private sealed class Handler:HttpMessageHandler
    {
        private readonly string response;public string Body;
        public Handler(string value){response=value;}
        protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {Body=await request.Content.ReadAsStringAsync();return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(response)};}
    }
}
