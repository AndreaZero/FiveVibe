using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FiveVibe
{
    public sealed class RealLocation
    {
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public void Validate()
        {
            if(String.IsNullOrWhiteSpace(Name) || Name.Length>120 || Name.Any(Char.IsControl) ||
                Double.IsNaN(Latitude) || Double.IsInfinity(Latitude) || Math.Abs(Latitude)>90 ||
                Double.IsNaN(Longitude) || Double.IsInfinity(Longitude) || Math.Abs(Longitude)>180)
                throw new ArgumentException("Invalid weather location.");
            Latitude=Math.Round(Latitude,2); Longitude=Math.Round(Longitude,2);
        }
    }
    public sealed class RealWorldSettings
    {
        public bool ClockEnabled { get; set; }
        public bool WeatherEnabled { get; set; }
        public RealLocation Location { get; set; }
    }
    public sealed class RealWeather
    {
        public string Preset;
        public DateTime ObservedUtc;
        public static RealWeather Parse(string json,DateTime now)
        {
            var root=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
            var current=root!=null && root.ContainsKey("current") ? root["current"] as Dictionary<string,object> : null;
            if(current==null || !current.ContainsKey("time") || !current.ContainsKey("weather_code") || !current.ContainsKey("is_day")) throw new FormatException("Weather response is incomplete.");
            DateTime time=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(Convert.ToDouble(current["time"],CultureInfo.InvariantCulture));
            if(time<now.AddMinutes(-90) || time>now.AddMinutes(20)) throw new FormatException("Weather data is out of date.");
            return new RealWeather { Preset=PresetFor(Convert.ToInt32(current["weather_code"]),Convert.ToInt32(current["is_day"])==1),ObservedUtc=time };
        }
        public static string PresetFor(int code,bool day)
        {
            if(code==0 || code==1) return day?"sunny":"clear";
            if(code==2) return "clouds"; if(code==3) return "overcast";
            if(code==45 || code==48) return "fog";
            if(new[] {51,53,55,56,57,61,63,65,66,67,80,81,82}.Contains(code)) return "rain";
            if(new[] {71,73,75,77,85,86}.Contains(code)) return "snow";
            if(code==95 || code==96 || code==99) return "thunder";
            throw new FormatException("Weather condition is not supported.");
        }
    }
    public sealed class RealWorldService : IDisposable
    {
        private readonly string settingsPath;
        private readonly HttpClient http;
        private readonly Action<string> say;
        private CancellationTokenSource cancellation=new CancellationTokenSource();
        private Task<RealLocation> lookup;
        private Task<RealWeather> weather;
        private DateTime nextFetch=DateTime.MinValue;
        private RealWeather reading;
        public RealWorldSettings Settings { get; private set; }
        public string Status { get; private set; }
        public bool ClockEnabled { get { return Settings.ClockEnabled; } }
        public bool WeatherEnabled { get { return Settings.WeatherEnabled; } }
        public string LocationName { get { return Settings.Location==null ? "Not configured" : Settings.Location.Name; } }
        public RealWeather Current { get { return reading!=null && DateTime.UtcNow-reading.ObservedUtc<TimeSpan.FromMinutes(90) ? reading : null; } }
        public RealWorldService(string path,Action<string> report) : this(path,report,new HttpClient()) { }
        internal RealWorldService(string path,Action<string> report,HttpClient client)
        {
            settingsPath=path; say=report; http=client; http.Timeout=TimeSpan.FromSeconds(12); http.MaxResponseContentBufferSize=131072;
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            Settings=new RealWorldSettings(); Status="Real-world sync off";
            try
            {
                if(File.Exists(path) && new FileInfo(path).Length<8192)
                {
                    var loaded=new JavaScriptSerializer().Deserialize<RealWorldSettings>(File.ReadAllText(path));
                    if(loaded!=null) { if(loaded.Location!=null) loaded.Location.Validate(); Settings=loaded; }
                }
            }
            catch(Exception) { Settings=new RealWorldSettings(); }
        }
        public void SetClock(bool enabled) { Settings.ClockEnabled=enabled; Save(); }
        public void SetWeather(bool enabled)
        {
            Settings.WeatherEnabled=enabled;
            if(!enabled) { CancelRequests(); Status="Real weather off"; }
            else { nextFetch=DateTime.MinValue; Status=Settings.Location==null?"Set a city or use location in the companion":"Loading real weather"; }
            Save();
        }
        public void SetLocation(RealLocation location)
        {
            location.Validate(); CancelRequests(); Settings.Location=location; reading=null; nextFetch=DateTime.MinValue; Save();
            Status="Weather location saved"; say("Weather location saved. Check the location shown in the companion.");
        }
        public void SetCity(string query)
        {
            if(String.IsNullOrWhiteSpace(query) || query.Length>100 || query.Any(Char.IsControl)) throw new ArgumentException("Enter a city and country, such as Rome, Italy.");
            CancelRequests(); Settings.Location=null; reading=null; Save(); lookup=LookupAsync(query.Trim(),cancellation.Token); Status="Looking up weather location";
        }
        public void Tick(bool allowNetwork)
        {
            if(lookup!=null && lookup.IsCompleted)
            {
                var done=lookup; lookup=null;
                if(done.IsFaulted) { var error=done.Exception.GetBaseException(); Status=error is FormatException?error.Message:"Location lookup failed. Check the connection or enter a city and country."; say(Status); }
                else if(!done.IsCanceled) SetLocation(done.Result);
            }
            if(weather!=null && weather.IsCompleted)
            {
                var done=weather; weather=null;
                if(done.IsFaulted) { var observed=done.Exception; Status="Real weather unavailable; retrying in two minutes"; nextFetch=DateTime.UtcNow.AddMinutes(2); say(Status+"."); }
                else if(!done.IsCanceled) { reading=done.Result; Status="Real weather: "+reading.Preset; nextFetch=DateTime.UtcNow.AddMinutes(15); }
            }
            if(allowNetwork && Settings.WeatherEnabled && Settings.Location!=null && lookup==null && weather==null && DateTime.UtcNow>=nextFetch)
                weather=FetchAsync(Settings.Location,cancellation.Token);
        }
        private async Task<string> GetAsync(string url,CancellationToken token)
        {
            using(var response=await http.GetAsync(url,HttpCompletionOption.ResponseContentRead,token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode(); string json=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if(json.Length>131072) throw new FormatException("Weather response exceeded its size limit."); return json;
            }
        }
        private async Task<RealLocation> LookupAsync(string query,CancellationToken token)
        {
            string json=await GetAsync("https://geocoding-api.open-meteo.com/v1/search?name="+Uri.EscapeDataString(query)+"&count=1&language=en&format=json",token).ConfigureAwait(false);
            var root=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
            var results=root!=null && root.ContainsKey("results") ? root["results"] as IEnumerable : null;
            var first=results==null?null:results.Cast<object>().OfType<Dictionary<string,object>>().FirstOrDefault();
            if(first==null) throw new FormatException("No matching city. Include the country, for example Rome, Italy.");
            var location=new RealLocation { Name=Convert.ToString(first["name"])+", "+Convert.ToString(first.ContainsKey("country")?first["country"]:""),
                Latitude=Convert.ToDouble(first["latitude"],CultureInfo.InvariantCulture),Longitude=Convert.ToDouble(first["longitude"],CultureInfo.InvariantCulture) };
            location.Validate(); return location;
        }
        private async Task<RealWeather> FetchAsync(RealLocation location,CancellationToken token)
        {
            string url=String.Format(CultureInfo.InvariantCulture,"https://api.open-meteo.com/v1/forecast?latitude={0:F2}&longitude={1:F2}&current=weather_code,is_day&timeformat=unixtime",location.Latitude,location.Longitude);
            return RealWeather.Parse(await GetAsync(url,token).ConfigureAwait(false),DateTime.UtcNow);
        }
        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                string stage=settingsPath+".tmp"; File.WriteAllText(stage,new JavaScriptSerializer().Serialize(Settings),new UTF8Encoding(false));
                if(File.Exists(settingsPath)) File.Replace(stage,settingsPath,null); else File.Move(stage,settingsPath);
            }
            catch(IOException) { say("Real-world settings changed for this session; saving failed."); }
            catch(UnauthorizedAccessException) { say("Real-world settings changed for this session; saving failed."); }
        }
        private void CancelRequests()
        {
            cancellation.Cancel(); cancellation.Dispose(); cancellation=new CancellationTokenSource();
            if(lookup!=null) lookup.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            if(weather!=null) weather.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            lookup=null; weather=null;
        }
        public void Dispose() { CancelRequests(); cancellation.Dispose(); http.Dispose(); }
    }
    public static class RealWorldCommands
    {
        public static bool TryCity(string text,out string city)
        {
            city=null;
            var match=Regex.Match(text??"",@"^(?:set (?:real[- ](?:world|life) )?weather location (?:to|as)|(?:meteo reale|meteo irl) (?:a|di)|(?:imposta|usa) (?:la )?citta(?: meteo)?(?: a|:)?)[ :]+(.{2,100})[.!]?$",RegexOptions.IgnoreCase);
            if(!match.Success) return false; city=match.Groups[1].Value.Trim().TrimEnd('.','!'); return true;
        }
        public static bool TryPlan(string request,out CommandPlan plan)
        {
            plan=null; string text=LocalPlanner.Normalize(request);
            if(Regex.IsMatch(text,@"\b(?:non|not|never|don't)\b") || !Regex.IsMatch(text,@"\b(?:real|reale|reali|irl|pc time|ora del pc)\b")) return false;
            string action;
            if(Regex.IsMatch(text,@"\b(?:status|stato|info)\b")) action="realtime_status";
            else if(Regex.IsMatch(text,@"\b(?:disable|off|stop|disattiva|spegni)\b"))
            {
                bool w=Regex.IsMatch(text,@"\b(?:meteo|weather)\b"),c=Regex.IsMatch(text,@"\b(?:clock|time|ora|orario)\b");
                action=w && !c?"realtime_weather_off":c && !w?"realtime_clock_off":"realtime_off";
            }
            else if(Regex.IsMatch(text,@"\b(?:meteo|weather)\b") && Regex.IsMatch(text,@"\b(?:clock|time|ora|orario)\b")) action="realtime_both";
            else if(Regex.IsMatch(text,@"\b(?:meteo|weather)\b")) action="realtime_weather";
            else if(Regex.IsMatch(text,@"\b(?:clock|time|ora|orario)\b")) action="realtime_clock";
            else return false;
            if(!Regex.IsMatch(text,@"\b(?:enable|sync|synchronize|use|set|attiva|sincronizza|metti|usa|imposta|disattiva|spegni|disable|off|status|stato|info)\b")) return false;
            plan=CommandPlan.Create(action,"current",45); return true;
        }
    }
}
