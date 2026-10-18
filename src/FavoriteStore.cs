using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FiveVibe
{
    public sealed class FavoritePlace
    {
        public string Id {get;set;} public string Name {get;set;}
        public float X {get;set;} public float Y {get;set;} public float Z {get;set;}
    }
    public sealed class FavoriteFile
    { public int Version {get;set;} public FavoritePlace[] Places {get;set;} }

    // Private per-user data, separate from the shipped/custom destination configuration.
    public sealed class FavoriteStore
    {
        public const int Limit=50;
        private readonly string path;
        private List<FavoritePlace> places=new List<FavoritePlace>();
        private string loadedText;
        public bool Ready {get;private set;}
        public FavoriteStore(string file) { path=file; }
        public FavoritePlace[] All {get {return places.Select(Clone).ToArray();}}
        public static bool ValidId(string id) {return Regex.IsMatch(id??"",@"^fav_[a-f0-9]{32}$");}
        public static string CleanName(string name)
        {
            if(name!=null)name=name.Normalize(NormalizationForm.FormC);
            if(name==null || name.Any(c=>Char.IsControl(c) || !(Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c) || "'-_.()".IndexOf(c)>=0)))
                throw new ArgumentException("Use letters, numbers, spaces, apostrophes, periods, hyphens, underscores or parentheses for a favorite name.");
            name=Regex.Replace(name.Trim(),@"\s+"," ");
            if(name.Length<1 || name.Length>60 || !name.Any(Char.IsLetterOrDigit)) throw new ArgumentException("Choose a favorite name of 1-60 characters, including a letter or number.");
            return name;
        }
        public static string NameKey(string name)
        {
            var key=new StringBuilder();
            foreach(char c in CleanName(name).Normalize(NormalizationForm.FormD))
                if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark) key.Append(Char.ToLowerInvariant(c));
            return key.ToString().Normalize(NormalizationForm.FormC);
        }
        public FavoritePlace Get(string id) {var p=places.FirstOrDefault(f=>f.Id==id);return p==null?null:Clone(p);}
        public FavoritePlace FindName(string name)
        {string key=NameKey(name);var p=places.FirstOrDefault(f=>NameKey(f.Name)==key);return p==null?null:Clone(p);}
        public void Load()
        {
            Ready=false;
            string raw=ReadDisk();
            var next=new List<FavoritePlace>();
            if(raw!=null)
            {
                var data=new JavaScriptSerializer().Deserialize<FavoriteFile>(raw);
                if(data==null || data.Version!=1 || data.Places==null || data.Places.Length>Limit) throw new FormatException("Invalid favorites file version or count.");
                foreach(var p in data.Places)
                {
                    Validate(p);
                    if(next.Any(f=>f.Id==p.Id || NameKey(f.Name)==NameKey(p.Name))) throw new FormatException("Duplicate favorite name or ID.");
                    next.Add(Clone(p));
                }
            }
            places=next;loadedText=raw;Ready=true;
        }
        public FavoritePlace Save(string name,float x,float y,float z,out bool updated)
        {
            if(!Ready) throw new InvalidOperationException("Favorites could not be loaded. Fix favorites.json and reload scripts before saving; the file is preserved.");
            name=CleanName(name);
            var existing=FindName(name);
            updated=existing!=null;
            if(!updated && places.Count>=Limit) throw new InvalidOperationException("You can save up to 50 favorites. Reuse a name to update its position.");
            var saved=new FavoritePlace {Id=updated?existing.Id:"fav_"+Guid.NewGuid().ToString("N"),Name=name,X=x,Y=y,Z=z};
            Validate(saved);
            if(ReadDisk()!=loadedText) throw new InvalidOperationException("Favorites changed on disk. Reload scripts before saving again; the file is preserved.");
            var next=places.Where(f=>f.Id!=saved.Id).Select(Clone).ToList();next.Add(saved);
            string raw=new JavaScriptSerializer().Serialize(new FavoriteFile {Version=1,Places=next.ToArray()});
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string stage=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(stage,raw,new UTF8Encoding(false));
                if(File.Exists(path)) File.Replace(stage,path,null);else File.Move(stage,path);
            }
            finally {if(File.Exists(stage))File.Delete(stage);}
            places=next;loadedText=raw;return Clone(saved);
        }
        private string ReadDisk()
        {
            if(!File.Exists(path))return null;
            if(new FileInfo(path).Length>65536)throw new FormatException("Favorites file exceeds 64 KB.");
            string raw=File.ReadAllText(path);
            if(raw.Length>65536)throw new FormatException("Favorites file exceeds 64 KB.");
            return raw;
        }
        private static void Validate(FavoritePlace p)
        {
            if(p==null || !ValidId(p.Id) || CleanName(p.Name)!=p.Name || !Finite(p.X,10000) || !Finite(p.Y,10000) || !Finite(p.Z,2000))
                throw new FormatException("Invalid favorite name, ID or map coordinates.");
        }
        private static bool Finite(float n,float limit) {return !Single.IsNaN(n) && !Single.IsInfinity(n) && Math.Abs(n)<limit;}
        private static FavoritePlace Clone(FavoritePlace p) {return new FavoritePlace {Id=p.Id,Name=p.Name,X=p.X,Y=p.Y,Z=p.Z};}
    }
}
