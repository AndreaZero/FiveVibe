using System;

namespace FiveVibe
{
    public enum MissionStep { Off, Armed, Observe, WalkWithLamar, ChooseCar, FollowLamar, Park, EvadePolice, Dealership, PersonalCar, Home, WaitForLamar, AwaitResult, Passed, Failed, Unconfirmed }

    // No GTA references: decisions require observations, rather than elapsed route timers.
    public sealed class MissionObservation
    {
        public int Time, PassedCount = -1, FailedCount = -1, Wanted;
        public bool ScriptRunning, OtherMission, CanAct, Dead, Franklin, InCar, IsDriver;
        public bool LamarAvailable, LamarInPlayerCar, LamarPlayback, PersonalCarAvailable, MissionCarAvailable, NearHome, HomeRoute, DealerRoute;
        public string Objective = "";
    }

    public sealed class MissionFlow
    {
        public MissionStep Step { get; private set; }
        public MissionStep Goal { get; private set; }
        public bool Active { get { return Step >= MissionStep.Armed && Step <= MissionStep.AwaitResult; } }
        public string ObjectiveKey { get; private set; }
        public bool Joined { get; private set; }
        private int armedAt, joinedAt, baselinePassed = -1, baselineFailed = -1;
        private int? missingAt, unsupportedAt;
        private bool sawDealership, sawHome;

        public void Arm(int time)
        {
            Step = Goal = MissionStep.Armed; ObjectiveKey = ""; Joined = false;
            armedAt = time; missingAt = unsupportedAt = null; baselinePassed = baselineFailed = -1;
            sawDealership = sawHome = false;
        }
        public void Cancel() { Step = Goal = MissionStep.Off; ObjectiveKey = ""; Joined = false; }

        public MissionStep Update(MissionObservation o)
        {
            if (!Active) return Step;
            if (!Joined)
            {
                if (!o.ScriptRunning)
                {
                    if (o.OtherMission && o.CanAct)
                    {
                        if (!unsupportedAt.HasValue) unsupportedAt = o.Time;
                        if (Elapsed(o.Time, unsupportedAt.Value) > 10000) Step = MissionStep.Unconfirmed;
                    }
                    else unsupportedAt = null;
                    if (Elapsed(o.Time, armedAt) > 600000) Step = MissionStep.Unconfirmed;
                    return Step;
                }
                Joined = true; joinedAt = o.Time;
                baselinePassed = o.PassedCount; baselineFailed = o.FailedCount;
                Goal = MissionStep.Observe;
            }
            // Franklin's native counters also change on replays. A missing stat is not zero.
            if (baselineFailed >= 0 && o.FailedCount > baselineFailed) return Step = MissionStep.Failed;
            if (baselinePassed >= 0 && o.PassedCount > baselinePassed) return Step = MissionStep.Passed;
            if (o.Dead) return Step = MissionStep.Failed;
            if (!o.ScriptRunning)
            {
                if (!missingAt.HasValue) missingAt = o.Time;
                // The central controller may post the result after the mission script exits.
                Step = MissionStep.AwaitResult;
                if (Elapsed(o.Time, missingAt.Value) > 30000) Step = MissionStep.Unconfirmed;
                return Step;
            }
            missingAt = null;
            if (Elapsed(o.Time, joinedAt) > 45 * 60000) return Step = MissionStep.Unconfirmed;
            if (!o.CanAct || !o.Franklin) return Step = MissionStep.Observe;

            if (o.HomeRoute) { sawHome = true; Goal = MissionStep.Home; }
            else if (o.DealerRoute && !sawHome) { sawDealership = true; Goal = MissionStep.Dealership; }

            switch (o.Objective ?? "")
            {
                case "AR1_CHOOSE": Goal = MissionStep.ChooseCar; break;
                case "AR1_CHASE": Goal = MissionStep.FollowLamar; break;
                case "AR1_PARK": Goal = MissionStep.Park; break;
                case "LOSE_WANTED": case "LOSE_WANTED_LEVEL": Goal = MissionStep.EvadePolice; break;
                case "AR1_GOGARAGE": sawDealership = true; Goal = MissionStep.Dealership; break;
                case "AR1_HOME": sawHome = true; Goal = MissionStep.Home; break;
                case "CMN_GENGETINY": case "CMN_GENGETBCKY": sawHome = true; Goal = MissionStep.PersonalCar; break;
                case "CMN_GENGETBCK": if (!sawHome) Goal = MissionStep.ChooseCar; break;
                case "AR1_WAIT": Goal = sawHome || sawDealership ? MissionStep.WaitForLamar : MissionStep.WalkWithLamar; break;
            }
            if (!String.IsNullOrEmpty(o.Objective)) ObjectiveKey = o.Objective;
            if (sawHome)
            {
                if (!o.InCar) Goal = MissionStep.PersonalCar;
                else if (!o.IsDriver) Goal = MissionStep.Observe;
                else if (!o.LamarInPlayerCar && !o.NearHome) Goal = MissionStep.WaitForLamar;
                else Goal = MissionStep.Home;
            }
            else if (o.Wanted > 0 && (Goal == MissionStep.Dealership || Goal == MissionStep.EvadePolice)) Goal = MissionStep.EvadePolice;
            else if (Goal == MissionStep.EvadePolice && o.Wanted == 0) Goal = MissionStep.Dealership;
            else if (!o.InCar && o.PersonalCarAvailable && Goal != MissionStep.FollowLamar)
                Goal = MissionStep.PersonalCar;
            else if (!o.InCar && o.MissionCarAvailable && (Goal == MissionStep.Observe || Goal == MissionStep.WalkWithLamar))
                Goal = MissionStep.ChooseCar;
            else if (!o.InCar && Goal == MissionStep.Observe && o.LamarAvailable) Goal = MissionStep.WalkWithLamar;
            else if (o.IsDriver && o.LamarPlayback && (Goal == MissionStep.ChooseCar || Goal == MissionStep.Observe || Goal == MissionStep.WalkWithLamar)) Goal = MissionStep.FollowLamar;
            if (Goal == MissionStep.PersonalCar && o.IsDriver && o.LamarInPlayerCar) { sawHome = true; Goal = MissionStep.Home; }
            Step = Goal;
            return Step;
        }

        public static int Elapsed(int now, int then) { return unchecked(now - then); }
        public static string Label(MissionStep step)
        {
            switch (step)
            {
                case MissionStep.Armed: return "Waiting for Franklin and Lamar";
                case MissionStep.WalkWithLamar: return "Walk with Lamar";
                case MissionStep.ChooseCar: return "Get into a mission car";
                case MissionStep.FollowLamar: return "Follow Lamar";
                case MissionStep.Park: return "Park beside Lamar";
                case MissionStep.EvadePolice: return "Lose the cops";
                case MissionStep.Dealership: return "Return the car to Simeon";
                case MissionStep.PersonalCar: return "Get into Franklin's car";
                case MissionStep.Home: return "Take Lamar home";
                case MissionStep.WaitForLamar: return "Wait for Lamar";
                case MissionStep.AwaitResult: return "Waiting for GTA's mission result";
                case MissionStep.Passed: return "Mission passed — confirmed by GTA";
                case MissionStep.Failed: return "Mission failed";
                case MissionStep.Unconfirmed: return "Mission result not confirmed";
                default: return "Waiting for mission control / objectives";
            }
        }
    }

    public sealed class SteeringCommand
    {
        public float Steering, Throttle, Brake, Handbrake;
    }

    public static class MissionSteering
    {
        public static SteeringCommand Follow(float forwardX, float forwardY, float deltaX, float deltaY, float speed, float desiredSpeed)
        {
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (distance < .5 || !Finite(distance) || !Finite(speed) || !Finite(desiredSpeed)) return new SteeringCommand { Brake = speed > .5f ? 1 : 0, Handbrake = 1 };
            double right = forwardY * deltaX - forwardX * deltaY;
            double ahead = forwardX * deltaX + forwardY * deltaY;
            double angle = Math.Atan2(right, ahead);
            double curvature = 2 * right / (distance * distance);
            float steer = Clamp(Math.Atan(2.8 * curvature) / .55, -1, 1);
            if (ahead < 0) steer = right >= 0 ? 1 : -1;
            float cornerSpeed = (float)Math.Sqrt(5.5 / Math.Max(.006, Math.Abs(curvature)));
            float target = Math.Max(0, Math.Min(desiredSpeed, cornerSpeed));
            if (Math.Abs(angle) > 1) target = Math.Min(target, 5);
            float error = target - speed;
            return new SteeringCommand { Steering = steer, Throttle = error > 0 ? Clamp(.18 + error * .16, 0, 1) : 0, Brake = error < -1 ? Clamp(-error * .16, 0, 1) : 0 };
        }
        private static bool Finite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
        private static float Clamp(double value, double min, double max) { return (float)Math.Max(min, Math.Min(max, value)); }
    }
}
