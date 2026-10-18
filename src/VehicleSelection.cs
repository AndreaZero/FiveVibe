using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    public static class VehicleSelection
    {
        public static readonly string[] Types={"any","car","motorcycle","suv","offroad","sports","super","sedan","muscle","van","pickup"};
        public static string FromText(string request)
        {
            string text=LocalPlanner.Normalize(request);
            if(Regex.IsMatch(text,@"\b(?:moto|motocicletta|motorcycle|motorbike|scooter)\b")) return "motorcycle";
            if(Regex.IsMatch(text,@"\b(?:jeep|fuoristrada|offroad|off road|4x4)\b")) return "offroad";
            if(Regex.IsMatch(text,@"\b(?:suv|suvs)\b")) return "suv";
            if(Regex.IsMatch(text,@"\b(?:supercar|super car)\b")) return "super";
            if(Regex.IsMatch(text,@"\b(?:sportiva|sportive|sportscar|sports car|sports coupe)\b")) return "sports";
            if(Regex.IsMatch(text,@"\b(?:pickup|pick up)\b")) return "pickup";
            if(Regex.IsMatch(text,@"\b(?:berlina|sedan)\b")) return "sedan";
            if(Regex.IsMatch(text,@"\b(?:muscle|muscle car)\b")) return "muscle";
            if(Regex.IsMatch(text,@"\b(?:furgone|van)\b")) return "van";
            if(Regex.IsMatch(text,@"\b(?:auto|macchina|car)\b")) return "car";
            return "any";
        }
        public static bool Matches(string type,int vehicleClass,bool car,bool motorcycle,string model,bool searching)
        {
            if(!Types.Contains(type) || (!car && !motorcycle)) return false;
            switch(type) {
                case "any":return car || (!searching && motorcycle);
                case "car":return car;
                case "motorcycle":return motorcycle;
                case "suv":return car && vehicleClass==2;
                case "offroad":return car && vehicleClass==9;
                case "sports":return car && vehicleClass==6;
                case "super":return car && vehicleClass==7;
                case "sedan":return car && vehicleClass==1;
                case "muscle":return car && vehicleClass==4;
                case "van":return car && vehicleClass==12;
                case "pickup":return car && new[] {"bison","bison2","bison3","bobcatxl","sadler","sadler2","sandking","sandking2","rebel","rebel2"}.Contains((model??"").ToLowerInvariant());
                default:return false;
            }
        }
        public static string Label(string type)
        { return type=="any" || type=="car" ? "car" : type=="offroad" ? "off-road vehicle" : type=="sports" ? "sports car" : type=="super" ? "supercar" : type=="suv" ? "SUV" : type; }
        public static int EntryTimeout(float metres)
        { return Single.IsNaN(metres) || Single.IsInfinity(metres)?22000:Math.Max(22000,Math.Min(90000,(int)(Math.Max(0,metres)*700)+10000)); }
    }
}
