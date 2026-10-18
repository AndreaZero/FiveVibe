using System;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    public enum SpectatorView { Gameplay, FirstPerson, ThirdPerson, Cinema, NativeCinema }

    public sealed class CinemaFraming
    {
        public float Right, Forward, Height, Fov, Lead;
        public static readonly string[] ShotNames = { "Chase", "Side tracking", "Front quarter", "High wide", "Low tracking", "Passing portrait" };
        public static CinemaFraming Sample(int shot, float progress, float speed, bool vehicle, float length)
        {
            progress = Clamp(progress, 0, 1); speed = Clamp(speed, 0, 70);
            float scale = vehicle ? Clamp(length / 4.5f, .8f, 1.7f) : .52f;
            var frame = new CinemaFraming();
            switch ((shot % 6 + 6) % 6)
            {
                case 0: frame.Right=2.8f+progress; frame.Forward=-9; frame.Height=3; frame.Fov=52; break;
                case 1: frame.Right=-8; frame.Forward=-2+progress*4; frame.Height=1.9f; frame.Fov=46; break;
                case 2: frame.Right=4.8f-progress; frame.Forward=10; frame.Height=2.4f; frame.Fov=48; break;
                case 3: frame.Right=-5; frame.Forward=-15; frame.Height=9; frame.Fov=54; break;
                case 4: frame.Right=5; frame.Forward=-4+progress*2; frame.Height=1.1f; frame.Fov=50; break;
                default: frame.Right=-8+progress*4; frame.Forward=7; frame.Height=4; frame.Fov=46; break;
            }
            frame.Right *= scale; frame.Forward *= scale;
            frame.Height *= vehicle ? Math.Min(1.25f,scale) : .65f;
            frame.Fov += Math.Min(5, speed*.12f);
            frame.Lead = vehicle ? Math.Min(2, speed*.07f) : .15f;
            return frame;
        }
        public static float Blend(float seconds) { return (float)(1-Math.Exp(-5*Math.Max(0,Math.Min(.1,seconds)))); }
        public static float Clearance(float distance, bool hit, float hitDistance)
        {
            if (!Finite(distance) || distance <= 0 || (hit && !Finite(hitDistance))) return 0;
            return hit ? Clamp((hitDistance-.65f)/distance,0,1) : 1;
        }
        private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
        private static float Clamp(float value,float min,float max) { return Finite(value) ? Math.Max(min,Math.Min(max,value)) : min; }
    }

    public sealed class CameraShotClock
    {
        public int Shot { get; private set; }
        public int Started { get; private set; }
        public const int Duration=8500;
        public void Reset(int now) { Shot=0; Started=now; }
        public void Next(int now) { Shot=(Shot+1)%6; Started=now; }
        public bool Update(int now)
        {
            if(unchecked(now-Started)<Duration) return false;
            Next(now); return true;
        }
        public float Progress(int now) { return Math.Max(0,Math.Min(1,unchecked(now-Started)/(float)Duration)); }
    }

    public static class CameraCommands
    {
        public static bool IsCamera(string action) { return action != null && action.StartsWith("camera_",StringComparison.Ordinal); }
        public static string Label(SpectatorView view)
        {
            switch(view) {
                case SpectatorView.FirstPerson:return "First person"; case SpectatorView.ThirdPerson:return "Third person";
                case SpectatorView.Cinema:return "Cinema director"; case SpectatorView.NativeCinema:return "GTA cinema";
                default:return "Gameplay";
            }
        }
        public static bool TryParse(string request,out CommandPlan plan)
        {
            plan=null; string text=LocalPlanner.Normalize(request);
            if(Regex.IsMatch(text,@"\b(?:non|not|never|don't|dont|without|senza)\b")) return false;
            bool requestVerb=Regex.IsMatch(text,@"\b(?:set|switch|enable|start|show|use|change|give|put|hide|remove|disable|toggle|restore|return|metti|attiva|avvia|mostra|usa|cambia|passa|voglio|vorrei|torna|ritorna|ripristina|togli|rimuovi|nascondi|alterna)\b");
            bool cameraWord=Regex.IsMatch(text,@"\b(?:camera|view|visuale|cinema|cinematic|cinematica|cinematiche|inquadratura|ripresa|riprese|persona|person|shot|letterbox|bande nere|black bars|cockpit)\b");
            if(!cameraWord) return false;
            string action=null;
            if(Regex.IsMatch(text,@"\b(?:next shot|next camera shot|prossima (?:ripresa|inquadratura)|cambia (?:ripresa|inquadratura))\b")) action="camera_next";
            else if(Regex.IsMatch(text,@"\b(?:letterbox|bande nere|black bars)\b") && requestVerb)
                action=Regex.IsMatch(text,@"\b(?:hide|remove|disable|off|togli|rimuovi|nascondi)\b")?"camera_bars_off":
                    Regex.IsMatch(text,@"\b(?:toggle|alterna|cambia)\b")?"camera_bars":"camera_bars_on";
            else if(requestVerb || Regex.IsMatch(text,@"^(?:prima persona|first person|terza persona|third person|cinema|cinematic camera|camera cinema)[.! ]*$"))
            {
                if(Regex.IsMatch(text,@"\b(?:prima persona|1(?:a)? persona|first person|1st person|cockpit)\b")) action="camera_first";
                else if(Regex.IsMatch(text,@"\b(?:terza persona|3(?:a)? persona|third person|3rd person)\b")) action="camera_third";
                else if(Regex.IsMatch(text,@"\b(?:normale|normal|gameplay|default|originale)\b") && !Regex.IsMatch(text,@"\b(?:cinema|cinematic)\b")) action="camera_gameplay";
                else if(Regex.IsMatch(text,@"\b(?:cinema|cinematic|cinematica|cinematiche|riprese)\b"))
                    action=Regex.IsMatch(text,@"\b(?:gta|native|nativa|originale|original)\b") ? "camera_native" : "camera_cinema";
                else if(Regex.IsMatch(text,@"\b(?:change|cycle|cambia|alterna)\b")) action="camera_cycle";
            }
            if(action==null) return false;
            plan=CommandPlan.Create(action,"current",45); return true;
        }
    }
}
