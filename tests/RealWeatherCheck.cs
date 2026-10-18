using System;
using System.IO;
using System.Threading;
using FiveVibe;

internal static class RealWeatherCheck
{
    public static int Main()
    {
        string path=Path.Combine(Path.GetTempPath(),"fivevibe-weather-probe-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            using(var service=new RealWorldService(path,message=>Console.WriteLine(message)))
            {
                // Public reference city, unrelated to the player's location or settings.
                service.SetCity("Rome, Italy");service.SetWeather(true);
                for(int i=0;i<250;i++)
                {
                    service.Tick(true);
                    if(service.Current!=null) {Console.WriteLine("Public reference city: "+service.LocationName+"; mapped preset="+service.Current.Preset+". No game controlled; no player location used.");return 0;}
                    Thread.Sleep(100);
                }
                Console.Error.WriteLine(service.Status);return 1;
            }
        }
        catch(Exception) {Console.Error.WriteLine("Reference weather check failed. No game controlled.");return 1;}
        finally {if(File.Exists(path))File.Delete(path);if(File.Exists(path+".tmp"))File.Delete(path+".tmp");}
    }
}
