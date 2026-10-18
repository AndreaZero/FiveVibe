using System;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    public enum AutoCameraChange { None, StartCinema, RestoreGameplay }
    public sealed class AutoCinemaPolicy
    {
        public bool Enabled=true, Driving, OwnsView, ManualChoice;
        public AutoCameraChange Update(bool driving)
        {
            AutoCameraChange change=AutoCameraChange.None;
            if(driving && !Driving && Enabled && !ManualChoice) { OwnsView=true; change=AutoCameraChange.StartCinema; }
            if(!driving && Driving) { if(OwnsView) change=AutoCameraChange.RestoreGameplay; OwnsView=false; ManualChoice=false; }
            Driving=driving; return change;
        }
        public void ChooseManually() { ManualChoice=true; OwnsView=false; }
        public void Reset() { Driving=OwnsView=ManualChoice=false; }
    }
    public enum SpectatorView { Gameplay, FirstPerson, ThirdPerson, NativeCinema }

    public static class CameraCommands
    {
        public static bool IsCamera(string action) { return action != null && action.StartsWith("camera_",StringComparison.Ordinal); }
        public static string Label(SpectatorView view)
        {
            switch(view) {
                case SpectatorView.FirstPerson:return "First person"; case SpectatorView.ThirdPerson:return "Third person";
                case SpectatorView.NativeCinema:return "GTA cinema";
                default:return "Gameplay";
            }
        }
        public static bool TryParse(string request,out CommandPlan plan)
        {
            plan=null; string text=LocalPlanner.Normalize(request);
            if(Regex.IsMatch(text,@"\b(?:non|not|never|don't|dont|without|senza)\b")) return false;
            bool requestVerb=Regex.IsMatch(text,@"\b(?:set|switch|enable|start|show|use|change|give|put|hide|remove|disable|toggle|restore|return|metti|attiva|avvia|mostra|usa|cambia|passa|voglio|vorrei|torna|ritorna|ripristina|togli|rimuovi|nascondi|alterna|disattiva|spegni)\b");
            bool cameraWord=Regex.IsMatch(text,@"\b(?:camera|view|visuale|cinema|cinematic|cinematica|cinematiche|inquadratura|ripresa|riprese|persona|person|shot|letterbox|bande nere|black bars|cockpit)\b");
            if(!cameraWord || Regex.IsMatch(text,@"\b(?:letterbox|bande nere|black bars|next shot|prossima inquadratura)\b")) return false;
            string action=null;
            if(Regex.IsMatch(text,@"\b(?:auto|automatic|automatico|automatica)\b") && Regex.IsMatch(text,@"\b(?:cinema|camera)\b") && requestVerb)
                action=Regex.IsMatch(text,@"\b(?:disable|off|disattiva|spegni)\b")?"camera_auto_off":"camera_auto_on";
            else if(requestVerb || Regex.IsMatch(text,@"^(?:prima persona|first person|terza persona|third person|cinema|cinematic camera|camera cinema)[.! ]*$"))
            {
                if(Regex.IsMatch(text,@"\b(?:prima persona|1(?:a)? persona|first person|1st person|cockpit)\b")) action="camera_first";
                else if(Regex.IsMatch(text,@"\b(?:terza persona|3(?:a)? persona|third person|3rd person)\b")) action="camera_third";
                else if(Regex.IsMatch(text,@"\b(?:normale|normal|gameplay|default|originale)\b") && !Regex.IsMatch(text,@"\b(?:cinema|cinematic)\b")) action="camera_gameplay";
                else if(Regex.IsMatch(text,@"\b(?:cinema|cinematic|cinematica|cinematiche|riprese)\b"))
                    action="camera_native";
                else if(Regex.IsMatch(text,@"\b(?:change|cycle|cambia|alterna)\b")) action="camera_cycle";
            }
            if(action==null) return false;
            plan=CommandPlan.Create(action,"current",45); return true;
        }
    }
}
