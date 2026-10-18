using System;
using System.Collections.Generic;
using FiveVibe;

internal static class MissionTests
{
    private static int passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
    private static MissionObservation Frame(int time, string label = "")
    {
        return new MissionObservation { Time=time, Objective=label, ScriptRunning=true, CanAct=true, Franklin=true,
            PassedCount=40, FailedCount=8, LamarAvailable=true };
    }
    public static int Main()
    {
        try
        {
            var flow=new MissionFlow(); flow.Arm(0);
            Check(flow.Update(new MissionObservation { Time=100, PassedCount=39 })==MissionStep.Armed,"Free roam cannot start mission driving");
            var o=Frame(200,"AR1_WAIT");
            Check(flow.Update(o)==MissionStep.WalkWithLamar && flow.Joined,"Attach only to an observed supported script");
            o=Frame(1000,"AR1_CHOOSE"); Check(flow.Update(o)==MissionStep.ChooseCar,"Real choose-car objective");
            o=Frame(2000); o.IsDriver=o.InCar=o.LamarPlayback=true;
            Check(flow.Update(o)==MissionStep.FollowLamar,"Recording plus driver seat bridges the pre-subtitle chase");
            o=Frame(3000,"AR1_CHASE"); o.IsDriver=o.InCar=true; flow.Update(o);
            o=Frame(11000); o.IsDriver=o.InCar=true;
            Check(flow.Update(o)==MissionStep.FollowLamar,"An expired subtitle does not cancel the chase");
            o=Frame(12000); o.CanAct=false;
            Check(flow.Update(o)==MissionStep.Observe && flow.Goal==MissionStep.FollowLamar,"Cutscenes suspend without losing the mission goal");
            o=Frame(20000); o.IsDriver=o.InCar=true;
            Check(flow.Update(o)==MissionStep.FollowLamar,"Resume the same objective after the scene");
            o=Frame(21000,"AR1_PARK"); o.IsDriver=o.InCar=true;
            Check(flow.Update(o)==MissionStep.Park,"Parking needs an observed objective");
            o=Frame(22000,"LOSE_WANTED"); o.Wanted=2; o.IsDriver=o.InCar=true;
            Check(flow.Update(o)==MissionStep.EvadePolice,"Wanted objective enters police evasion");
            o=Frame(23000,"AR1_GOGARAGE"); o.Wanted=2; o.IsDriver=o.InCar=true;
            Check(flow.Update(o)==MissionStep.EvadePolice,"Do not bring police to Simeon");
            o=Frame(24000); o.IsDriver=o.InCar=true;
            Check(flow.Update(o)==MissionStep.Dealership,"Native wanted level zero releases the dealership leg");
            o=Frame(25000); o.CanAct=false; flow.Update(o);
            o=Frame(26000); o.PersonalCarAvailable=true;
            Check(flow.Update(o)==MissionStep.PersonalCar,"After dealership scene enter Buffalo");
            o=Frame(27000,"AR1_HOME"); o.InCar=o.IsDriver=true;
            Check(flow.Update(o)==MissionStep.WaitForLamar,"Home leg waits for Lamar to board");
            o=Frame(28000); o.InCar=o.IsDriver=o.LamarInPlayerCar=true;
            Check(flow.Update(o)==MissionStep.Home,"Drive home only after passenger confirmed");
            o=Frame(29000); o.InCar=o.IsDriver=o.NearHome=true;
            Check(flow.Update(o)==MissionStep.Home,"Passenger leaving at home must not restart boarding");
            o=Frame(30000); o.CanAct=false;
            Check(flow.Update(o)==MissionStep.Observe,"Yield to the final game-managed scene");
            o=Frame(40000); o.ScriptRunning=false; o.OtherMission=true;
            Check(flow.Update(o)==MissionStep.AwaitResult,"Thread exit and mission flag alone do not imply success or failure");
            o=Frame(41000); o.ScriptRunning=false; o.PassedCount=41;
            Check(flow.Update(o)==MissionStep.Passed && !flow.Active,"Delayed native pass counter confirms result");
            flow.Arm(0); flow.Update(Frame(1,"AR1_CHASE"));
            o=Frame(100); o.ScriptRunning=false; flow.Update(o);
            o=Frame(30101); o.ScriptRunning=false;
            Check(flow.Update(o)==MissionStep.Unconfirmed,"No counter delta never produces fabricated success");
            flow.Arm(0); flow.Update(Frame(1)); o=Frame(100); o.FailedCount=9;
            Check(flow.Update(o)==MissionStep.Failed && !flow.Active,"A replay failure stops without retrying the menu");
            flow.Arm(0); o=Frame(1); o.PassedCount=o.FailedCount=-1; flow.Update(o);
            o=Frame(2); o.PassedCount=500;
            Check(flow.Update(o)!=MissionStep.Passed,"Unavailable counter baseline is not zero");
            flow.Cancel(); Check(!flow.Active && flow.Step==MissionStep.Off,"F8 cancellation disarms future stages");
            Check(flow.Update(Frame(10,"AR1_CHASE"))==MissionStep.Off,"Late observations cannot resume a cancelled pilot");
            flow.Arm(0); flow.Update(new MissionObservation { Time=1, OtherMission=true, CanAct=true });
            Check(flow.Update(new MissionObservation { Time=10002, OtherMission=true, CanAct=true })==MissionStep.Unconfirmed,"Unsupported mission does not attach");
            flow.Arm(0); flow.Update(new MissionObservation { Time=1, OtherMission=true });
            Check(flow.Update(Frame(15000,"AR1_CHOOSE"))==MissionStep.ChooseCar,"Mission-loading transition does not disarm a pre-armed pilot");
            flow.Arm(0); Check(flow.Update(new MissionObservation { Time=600001 })==MissionStep.Unconfirmed,"An armed pilot expires");
            flow.Arm(0); flow.Update(Frame(1)); o=Frame(2); o.Dead=true;
            Check(flow.Update(o)==MissionStep.Failed,"Death returns control");
            flow.Arm(Int32.MaxValue-100); o=Frame(Int32.MinValue+100,"AR1_CHOOSE");
            Check(flow.Update(o)==MissionStep.ChooseCar,"Game timer wrap does not expire the mission");
            o=Frame(Int32.MinValue+200); o.ScriptRunning=false; flow.Update(o);
            o.Time+=30001; Check(flow.Update(o)==MissionStep.Unconfirmed,"Result timeout also works after signed game-timer wrap");
            var left=MissionSteering.Follow(0,1,-10,20,10,20);
            var right=MissionSteering.Follow(0,1,10,20,10,20);
            Check(left.Steering<0 && right.Steering>0,"Steering follows GTA's left/right axes");
            var north=MissionSteering.Follow(0,1,0,30,10,20);
            var east=MissionSteering.Follow(1,0,30,0,10,20);
            Check(Math.Abs(north.Steering)<.001 && Math.Abs(east.Steering)<.001,"Straight route follows orientation");
            Check(MissionSteering.Follow(0,1,0,20,25,10).Brake>0,"Overspeed brakes");
            Check(MissionSteering.Follow(0,1,0,20,0,20).Throttle>0,"Stationary car accelerates");
            Check(MissionSteering.Follow(0,1,0,20,7,0).Throttle==0,"Lamar stopping does not force a minimum forward speed");
            Check(MissionSteering.Follow(0,1,0,0,10,20).Brake==1,"Degenerate route brakes");
            Check(MissionSteering.Follow(0,1,Single.NaN,20,10,20).Brake==1,"Invalid geometry never injects NaN controls");
            Check(LocalPlanner.Parse("completa Franklin e Lamar",45).Action=="mission_auto","Italian mission request");
            Check(LocalPlanner.Parse("Start Mission Pilot",45).Action=="mission_auto","Mission Pilot works without an API call");
            Check(LocalPlanner.Parse("Switch to Franklin",45).Action=="switch_character","Existing protagonist command retained");
            Check(LocalPlanner.Parse("finisci tutta la storia",45).Action=="say","One profile does not imply whole-campaign autonomy");
            Check(LocalPlanner.Parse("Start Mission Pilot for Prologue",45).Action=="say","An unsupported named profile is never silently substituted");
            Check(CommandPlan.Create("mission_auto","franklin",45).Action=="mission_auto","Named supported protagonist needs no manual character switch");
            bool rejected=false;
            try { CommandPlan.Create("mission_auto","trevor",45); } catch(ArgumentException) { rejected=true; }
            Check(rejected,"Incompatible protagonist cannot start a different profile");
            var requestedPilot=CommandPlan.Create("mission_auto","current",45);
            Check(MissionRequests.Constrain("Complete Prologue",true,requestedPilot).Action=="say","Even valid AI output cannot substitute Franklin and Lamar for Prologue");
            Check(MissionRequests.Constrain("Play a mission called Unknown Mission",true,requestedPilot).Action=="say","Unknown named missions do not silently select the only profile");
            Check(MissionRequests.Constrain("Complete this mission",true,requestedPilot).Action=="mission_auto","A verified active profile permits a deictic request");
            Check(MissionRequests.Constrain("Complete this mission",false,requestedPilot).Action=="say","A deictic request cannot start a profile in unknown free roam");
            flow.Arm(0); o=Frame(1); o.MissionCarAvailable=true;
            Check(flow.Update(o)==MissionStep.ChooseCar,"Late join finds the marked mission cars after the subtitle expires");
            flow.Arm(0); o=Frame(1); o.PersonalCarAvailable=true;
            Check(flow.Update(o)==MissionStep.PersonalCar,"Late join after the dealership finds the marked Buffalo");
            flow.Arm(0); o=Frame(1); o.HomeRoute=o.InCar=o.IsDriver=o.LamarInPlayerCar=true;
            Check(flow.Update(o)==MissionStep.Home,"Late join on the way home reads the real mission route marker");
            Check(MissionSteering.Follow(0,1,0,0,0,0).Brake==0 && MissionSteering.Follow(0,1,0,0,0,0).Handbrake==1,"Stationary invalid target holds without reverse input");
            Check(SimulateSteering(),"Feedback steering completes a straight, right-angle curve and exit without diverging");
            Console.WriteLine(passed+" mission checks passed."); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine("After "+passed+" checks: "+error.Message); return 1; }
    }

    private static bool SimulateSteering()
    {
        // Independent bicycle-model replay: measures closed-loop behavior, not a formula copy.
        var path=new List<double[]>();
        for(int i=0;i<=50;i++) path.Add(new double[] {0,i});
        for(int i=1;i<=90;i++) { double a=i*Math.PI/180; path.Add(new double[] {30-30*Math.Cos(a),50+30*Math.Sin(a)}); }
        for(int i=1;i<=70;i++) path.Add(new double[] {30+i,80});
        double x=0,y=0,heading=0,speed=0; int progress=0;
        for(int frame=0;frame<1800;frame++)
        {
            double closest=Double.MaxValue;
            for(int i=progress;i<Math.Min(path.Count,progress+40);i++)
            {
                double d=Math.Pow(path[i][0]-x,2)+Math.Pow(path[i][1]-y,2);
                if(d<closest) { closest=d; progress=i; }
            }
            if(closest>144) return false;
            int target=progress;
            while(target+1<path.Count && Math.Pow(path[target][0]-x,2)+Math.Pow(path[target][1]-y,2)<225) target++;
            var command=MissionSteering.Follow((float)Math.Sin(heading),(float)Math.Cos(heading),(float)(path[target][0]-x),(float)(path[target][1]-y),(float)speed,15);
            if(Single.IsNaN(command.Steering) || command.Throttle<0 || command.Throttle>1 || command.Brake<0 || command.Brake>1) return false;
            speed=Math.Max(0,speed+(command.Throttle*3-command.Brake*6-.12*speed)*.05);
            heading+=speed/2.8*Math.Tan(command.Steering*.55)*.05;
            x+=Math.Sin(heading)*speed*.05; y+=Math.Cos(heading)*speed*.05;
            if(progress>=path.Count-5) return true;
        }
        return false;
    }
}
