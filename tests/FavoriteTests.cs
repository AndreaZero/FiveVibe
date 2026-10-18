using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using FiveVibe;

internal static class FavoriteTests
{
    private static int passed;
    private static void Check(bool condition,string name) {if(!condition)throw new Exception(name);passed++;}
    private static void Reject(Action action,string name)
    {try{action();}catch(ArgumentException){passed++;return;}catch(FormatException){passed++;return;}catch(InvalidOperationException){passed++;return;}throw new Exception(name);}
    public static int Main()
    {
        string directory=Path.Combine(Path.GetTempPath(),"FiveVibe-favorites-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"favorites.json");
        try
        {
            var store=new FavoriteStore(path);store.Load();bool updated;
            Check(store.Ready && store.All.Length==0 && !File.Exists(path),"Missing file is an empty store without an unsolicited write");
            var home=store.Save("  Casa   sul lago  ",111.25f,-222.5f,33.75f,out updated);
            Check(!updated && home.Name=="Casa sul lago" && FavoriteStore.ValidId(home.Id),"Save current observed coordinates with normalized name and generated ID");
            Check(home.X==111.25f && home.Y==-222.5f && home.Z==33.75f,"Coordinates preserved, not generated from a name");
            var copy=store.All;copy[0].X=999;
            Check(store.Get(home.Id).X==111.25f,"Public snapshots cannot mutate the saved store");
            var loaded=new FavoriteStore(path);loaded.Load();
            Check(loaded.Get(home.Id).Name==home.Name && loaded.FindName("CASA SUL LAGO").Z==33.75f,"Reload preserves stable IDs, names and coordinates");
            var next=loaded.Save("casa sul lago",10,20,30,out updated);
            Check(updated && next.Id==home.Id && loaded.All.Length==1 && loaded.Get(home.Id).X==10,"Reusing a name updates its position instead of duplicating it");
            loaded.Save("Caffè",1,2,3,out updated);var cafe=loaded.FindName("caffe");
            Check(cafe!=null,"Names are accent-insensitive");
            loaded.Save("CAFFE",4,5,6,out updated);
            Check(updated && loaded.FindName("Caffè").Id==cafe.Id,"Case/accent update preserves identity");
            foreach(string invalid in new[]{"","   ","...","../Home","Home\\spot","Name\nOther","<script>",new string('x',61)})
                Reject(()=>loaded.Save(invalid,1,2,3,out updated),"Invalid name rejected: "+invalid.Length);
            foreach(float invalid in new[]{Single.NaN,Single.PositiveInfinity,10000f,-10000f})
                Reject(()=>loaded.Save("Bad coordinates",invalid,2,3,out updated),"Invalid map coordinate rejected");
            Reject(()=>loaded.Save("Too high",1,2,2000,out updated),"Out-of-range height rejected");
            string original=File.ReadAllText(path);
            File.WriteAllText(path,"external edit");
            Reject(()=>loaded.Save("Do not overwrite",1,2,3,out updated),"External file edit is not overwritten");
            Check(File.ReadAllText(path)=="external edit" && loaded.FindName("Do not overwrite")==null,"Failed save leaves disk and memory intact");
            Reject(()=>loaded.Load(),"Malformed file rejected");
            Check(!loaded.Ready,"Unreadable store disables saves");
            Reject(()=>loaded.Save("No reset",1,2,3,out updated),"Bad file cannot be silently reset");
            File.WriteAllText(path,original);loaded.Load();
            var serializer=new JavaScriptSerializer();
            File.WriteAllText(path,serializer.Serialize(new FavoriteFile {Version=1,Places=new[]{home,home}}));
            Reject(()=>loaded.Load(),"Duplicate IDs/names rejected atomically");
            File.WriteAllText(path,serializer.Serialize(new FavoriteFile {Version=2,Places=new FavoritePlace[0]}));
            Reject(()=>loaded.Load(),"Unknown data version rejected");
            File.WriteAllText(path,new string(' ',65537));Reject(()=>loaded.Load(),"Oversized file rejected");
            File.WriteAllText(path,original);loaded.Load();
            for(int i=loaded.All.Length;i<FavoriteStore.Limit;i++)loaded.Save("Spot "+i,i,2,3,out updated);
            Check(loaded.All.Length==50,"Up to 50 favorites persist");
            Reject(()=>loaded.Save("Spot overflow",1,2,3,out updated),"Limit enforced for new favorites");
            loaded.Save("Spot 49",9,8,7,out updated);Check(updated && loaded.All.Length==50,"Updates still work at capacity");
            Check(Directory.GetFiles(directory,"*.tmp").Length==0,"Atomic saves leave no staging files");
            File.WriteAllText(path,original);loaded.Load();
            string blocker=Path.Combine(directory,"blocked");File.WriteAllText(blocker,"not a directory");
            var unwritable=new FavoriteStore(Path.Combine(blocker,"favorites.json"));unwritable.Load();
            bool failedWrite=false;try{unwritable.Save("No ghost place",1,2,3,out updated);}catch(IOException){failedWrite=true;}
            Check(failedWrite && unwritable.All.Length==0,"Failed disk write does not create a ghost favorite in memory");
            FavoriteRequest request;
            foreach(string prompt in new[]{"Salva qui come Casa","Salva la posizione attuale come Casa","Save this place as Home","Remember this spot as Home","Save my current position as Home","Salva questo posto con il nome Casa"})
                Check(FavoriteCommands.TryParse(prompt,loaded,out request) && request.Action=="save" && request.Error==null,"Local save command: "+prompt);
            Check(FavoriteCommands.TryParse("Salva qui come \"Casa di Trevor\"",loaded,out request) && request.Name=="Casa di Trevor","Quoted names parsed literally");
            Check(FavoriteCommands.TryParse("Save this place as Home.",loaded,out request) && request.Name=="Home","Trailing sentence punctuation is not part of an unquoted name");
            Check(FavoriteCommands.TryParse("Save this place as \"L.A.\".",loaded,out request) && request.Name=="L.A.","Quoted literal punctuation preserved");
            foreach(string prompt in new[]{"Torna al preferito Casa sul lago","Go to favorite casa sul lago","Drive to favourite casa sul lago","Return to favorite casa sul lago"})
                Check(FavoriteCommands.TryParse(prompt,loaded,out request) && request.Action=="go" && !request.Walk,"Local return command: "+prompt);
            Check(FavoriteCommands.TryParse("Walk to favorite Casa sul lago",loaded,out request) && request.Walk,"Explicit walking return");
            Check(FavoriteCommands.TryParse("Vai a piedi al preferito Casa sul lago",loaded,out request) && request.Walk,"Italian walking return");
            Check(FavoriteCommands.TryParse("Torna a casa sul lago",loaded,out request) && request.Action=="go","Exact saved-name shortcut");
            Check(FavoriteCommands.TryParse("Go to favorite Unknown",loaded,out request) && loaded.FindName(request.Name)==null,"Unknown explicit favorite does not fall back to an invented destination");
            Check(FavoriteCommands.TryParse("Mostra i miei preferiti",loaded,out request) && request.Action=="list","List favorites without movement");
            Check(FavoriteCommands.TryParse("Show my favorites",loaded,out request) && request.Action=="list","English list");
            Check(FavoriteCommands.TryParse("Save here as Casa e poi guida",loaded,out request) && request.Error!=null,"Combined command rejected, no save");
            Check(!FavoriteCommands.TryParse("Non salvare qui come Casa",loaded,out request),"Negated save cannot write a favorite");
            Check(!FavoriteCommands.TryParse("Stop",loaded,out request),"Takeover is never intercepted");
            var park=loaded.Save("Mirror Park",1,2,3,out updated);PlaceCatalog.SetFavorites(loaded.All);
            Check(!FavoriteCommands.TryParse("Drive to Mirror Park",loaded,out request),"Built-in destination retains precedence over a same-name shortcut");
            Check(FavoriteCommands.TryParse("Go to favorite Mirror Park",loaded,out request),"Explicit favorite resolves a built-in name collision");
            Check(PlaceCatalog.Get(park.Id).X==1 && CommandPlan.Places.Contains(park.Id),"Persisted favorites join validated destination catalog");
            Check(LocalPlanner.Parse("Drive to Favorite: Mirror Park",45).Place==park.Id,"Explicit favorite catalog alias wins over nested built-in name");
            var trevor=loaded.Save("Trevor spot",10,20,30,out updated);PlaceCatalog.SetFavorites(loaded.All);
            Check(LocalPlanner.Parse("Drive to Favorite: Trevor spot",45).Actor=="current","Favorite protagonist name does not switch character");
            Check(PlaceCatalog.Describe().Contains("Favorite: Trevor spot"),"AI sees saved IDs/names through the catalog");
            Check(!FavoriteCommands.TryParse("Run to favorite Trevor spot",loaded,out request) && LocalPlanner.Parse("Run to favorite Trevor spot",45).Pace=="run","Running remains an ordinary catalog movement, not a walking shortcut");
            string custom=Path.Combine(directory,"custom.json");
            File.WriteAllText(custom,"[{\"Id\":\"fav_reserved\",\"Name\":\"Reserved\",\"X\":1,\"Y\":2,\"Z\":3}]");
            Reject(()=>PlaceCatalog.LoadCustom(custom),"Custom locations cannot claim the reserved favorite namespace");
            File.Delete(custom);PlaceCatalog.LoadCustom(custom);
            Check(PlaceCatalog.Get(trevor.Id)!=null,"Reloading custom locations retains private favorites");
            Console.WriteLine(passed+" favorite checks passed.");return 0;
        }
        catch(Exception e) {Console.Error.WriteLine("After "+passed+" favorite checks: "+e.Message);return 1;}
        finally
        {
            PlaceCatalog.SetFavorites(null);
            foreach(string file in Directory.GetFiles(directory))File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
