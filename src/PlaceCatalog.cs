using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FiveVibe
{
    public sealed class PlaceDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string[] Aliases { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    // Approximate outdoor approach points, not entrances or guaranteed mission triggers.
    // Kept independent from GTA so catalog validation and planning can be tested offline.
    public static class PlaceCatalog
    {
        private static readonly List<PlaceDefinition> BuiltIns = new List<PlaceDefinition> {
            P("beach", "Vespucci Beach", -1223,-1498,4, "mare|spiaggia|beach|vespucci"),
            P("airport", "Los Santos International Airport", -1034,-2731,20, "airport|lsia|aeroporto"),
            P("observatory", "Galileo Observatory", -410,1178,325, "observatory|osservatorio|galileo"),
            P("downtown", "Downtown Los Santos", 215,-810,30, "downtown|centro|city center"),
            P("michael_home", "Michael's House", -817,178,72, "casa di michael|casa di micheal|michael home|michael house|michael's house"),
            P("franklin_home", "Franklin's Hills House", 9,548,175, "casa di franklin|franklin house|franklin's house"),
            P("trevor_home", "Trevor's Trailer", 1985,3815,32, "casa di trevor|roulotte di trevor|trevor trailer|trevor's trailer"),
            P("del_perro_pier", "Del Perro Pier", -1616,-1061,13, "del perro pier|molo|luna park|pier"),
            P("vinewood_sign", "Vinewood Sign", 706,1199,348, "vinewood sign|scritta vinewood"),
            P("vinewood", "Vinewood Boulevard", 303,193,104, "vinewood boulevard|vinewood|walk of fame"),
            P("rockford_hills", "Rockford Hills", -706,-131,38, "rockford hills|rockford"),
            P("mirror_park", "Mirror Park", 1129,-645,56, "mirror park|parco mirror"),
            P("legion_square", "Legion Square", 195,-935,30, "legion square|piazza legion"),
            P("maze_bank", "Maze Bank Tower", -75,-818,30, "maze bank|torre maze"),
            P("casino", "Diamond Casino", 935,46,81, "casino|diamond casino|casino diamond"),
            P("golf_club", "Los Santos Golf Club", -1336,59,54, "golf club|golf|campo da golf"),
            P("kortz_center", "Kortz Center", -2243,264,174, "kortz center|kortz|museo"),
            P("university", "University of San Andreas", -1610,181,60, "university|universita|uls a|ulsa"),
            P("pacific_bluffs", "Pacific Bluffs", -2070,-314,13, "pacific bluffs"),
            P("chumash", "Chumash", -3170,1087,20, "chumash"),
            P("tongva_hills", "Tongva Hills", -1490,2080,57, "tongva hills|tongva"),
            P("sandy_shores", "Sandy Shores", 1854,3725,33, "sandy shores|sandy|deserto|desert"),
            P("sandy_airfield", "Sandy Shores Airfield", 1742,3271,41, "sandy shores airfield|sandy airfield|aeroporto sandy"),
            P("grapeseed", "Grapeseed", 1704,4919,42, "grapeseed"),
            P("paleto_bay", "Paleto Bay", -104,6476,31, "paleto bay|paleto"),
            P("paleto_forest", "Paleto Forest", -755,5532,33, "paleto forest|foresta paleto"),
            P("chiliad_base", "Mount Chiliad Cable Car", -771,5595,34, "mount chiliad|monte chiliad|chiliad|funivia|cable car"),
            P("fort_zancudo", "Fort Zancudo Approach", -2297,3385,31, "fort zancudo|zancudo|base militare|military base"),
            P("alamo_sea", "Alamo Sea", 1294,3602,34, "alamo sea|alamo|lago|lake"),
            P("route_68", "Route 68", 544,2670,42, "route 68|strada 68"),
            P("harmony", "Harmony", 591,2736,42, "harmony"),
            P("davis", "Davis", 105,-1940,20, "davis"),
            P("grove_street", "Grove Street", 105,-1940,20, "grove street|grove"),
            P("strawberry", "Strawberry", 282,-1298,29, "strawberry"),
            P("la_mesa", "La Mesa", 804,-994,26, "la mesa"),
            P("cypress_flats", "Cypress Flats", 916,-2118,30, "cypress flats"),
            P("port", "Port of Los Santos", 1204,-3115,5, "port of los santos|porto|docks|port"),
            P("simeon_dealership", "Simeon's Dealership", -44,-1097,26, "simeon dealership|concessionario|premium deluxe motorsport"),
            P("mission_row", "Mission Row Police Station", 425,-980,30, "mission row|police station|stazione di polizia"),
            P("pillbox_hill", "Pillbox Hill Hospital", 298,-584,43, "pillbox hill|pillbox|hospital|ospedale"),
            P("tataviam", "Tataviam Mountains", 2550,480,108, "tataviam|tataviam mountains"),
            P("humane_labs", "Humane Labs Approach", 3616,3744,28, "humane labs|laboratori humane")
        };
        private static List<PlaceDefinition> places = new List<PlaceDefinition>(BuiltIns);
        private static PlaceDefinition P(string id, string name, float x, float y, float z, string aliases)
        { return new PlaceDefinition { Id=id, Name=name, X=x, Y=y, Z=z, Aliases=aliases.Split('|') }; }
        public static string[] Ids { get { return new[] { "none" }.Concat(places.Select(p => p.Id)).ToArray(); } }
        public static PlaceDefinition[] All { get { return places.ToArray(); } }
        public static PlaceDefinition Get(string id) { return places.FirstOrDefault(p => p.Id == id); }
        public static void LoadCustom(string file)
        {
            var next = new List<PlaceDefinition>(BuiltIns);
            if (File.Exists(file))
            {
                if (new FileInfo(file).Length > 65536) throw new FormatException("The locations file exceeds 64 KB.");
                var custom = new JavaScriptSerializer().Deserialize<PlaceDefinition[]>(File.ReadAllText(file));
                if (custom == null || custom.Length > 100) throw new FormatException("Use an array with at most 100 custom locations.");
                foreach (var p in custom)
                {
                    if (p == null || !Regex.IsMatch(p.Id ?? "", "^[a-z][a-z0-9_]{1,47}$") || p.Id == "none" ||
                        string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 80 || next.Any(existing => existing.Id == p.Id) ||
                        !Finite(p.X, 10000) || !Finite(p.Y, 10000) || !Finite(p.Z, 2000) ||
                        (p.Aliases != null && (p.Aliases.Length > 12 || p.Aliases.Any(a => string.IsNullOrWhiteSpace(a) || a.Length > 80))))
                        throw new FormatException("Invalid or duplicate custom location. Check IDs, names, aliases and map coordinates.");
                    next.Add(p);
                }
            }
            places = next; // Invalid files never partially change the catalog.
        }
        private static bool Finite(float n, float limit) { return !float.IsNaN(n) && !float.IsInfinity(n) && Math.Abs(n) < limit; }
        public static string Describe()
        { return string.Join("; ", places.Select(p => p.Id + " = " + p.Name + " (" + string.Join(", ", p.Aliases ?? new string[0]) + ")")); }
        public static string[] Match(string text)
        {
            string normalized = LocalPlanner.Normalize(text);
            // Prefer specific names: "Sandy Shores Airfield" is not a second request for Sandy Shores.
            var matches = places.Select(p => new { Place=p, Length=new[] { p.Name }.Concat(p.Aliases ?? new string[0])
                .Where(a => Regex.IsMatch(normalized, @"(?<!\w)" + Regex.Escape(LocalPlanner.Normalize(a)) + @"(?!\w)"))
                .Select(a => a.Length).DefaultIfEmpty(0).Max() }).Where(p => p.Length > 0).ToArray();
            return matches.Where(p => !matches.Any(other => other.Length > p.Length &&
                (other.Place.Name.IndexOf(p.Place.Name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (p.Place.Aliases ?? new string[0]).Any(a => (other.Place.Aliases ?? new string[0]).Any(b => b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)))))
                .Select(p => p.Place.Id).Distinct().ToArray();
        }
    }
}
