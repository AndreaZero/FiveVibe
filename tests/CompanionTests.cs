using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using FiveVibe;

internal static class CompanionTests
{
    static int passed;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
    static HttpResponseMessage Request(HttpClient client, string path, string token, string body=null, string origin=null, string host=null)
    {
        var request = new HttpRequestMessage(body==null ? HttpMethod.Get : HttpMethod.Post,path);
        if (token!=null) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        if (origin!=null) request.Headers.Add("Origin",origin);
        if (host!=null) request.Headers.Host=host;
        if (body!=null) request.Content=new StringContent(body,Encoding.UTF8,"application/json");
        return client.SendAsync(request).GetAwaiter().GetResult();
    }
    public static int Main(string[] args)
    {
        try
        {
            var reservation=new TcpListener(IPAddress.Loopback,0); reservation.Start();
            int port=((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            using (var server=new CompanionServer(port,false))
            using (var client=new HttpClient(new HttpClientHandler { UseProxy=false }) { BaseAddress=new Uri("http://127.0.0.1:"+port), Timeout=TimeSpan.FromSeconds(5) })
            {
                string token=new Uri(server.Url).Fragment.Substring(1);
                if(args.Length>0 && args[0]=="--preview")
                {
                    server.Publish("{\"actor\":\"Michael\",\"status\":\"Chill drive\",\"route\":\"To Paleto Bay\",\"active\":true,\"provider\":\"OPENAI\",\"paused\":false,\"messages\":[{\"user\":true,\"text\":\"Take a chill drive to Paleto Bay.\"},{\"user\":false,\"text\":\"Driving to Paleto Bay. Close chat and enjoy the ride.\"}],\"places\":[{\"name\":\"Mirror Park\"},{\"name\":\"Paleto Bay\"},{\"name\":\"Sandy Shores\"},{\"name\":\"Del Perro Pier\"}]} ");
                    Console.WriteLine(server.Url); Thread.Sleep(300000); return 0;
                }
                Check(Request(client,"/",null).Content.ReadAsStringAsync().Result.Contains("FiveVibe"),"Embedded page loads");
                Check(Request(client,"/api/state",null).StatusCode==HttpStatusCode.Unauthorized,"State requires session authentication");
                Check(Request(client,"/api/state","wrong").StatusCode==HttpStatusCode.Unauthorized,"Wrong token rejected");
                Check(Request(client,"/api/state",token,null,"http://evil.invalid").StatusCode==HttpStatusCode.Forbidden,"Cross-origin requests rejected");
                Check(Request(client,"/api/state",token,null,null,"evil.invalid").StatusCode==HttpStatusCode.Forbidden,"Rebinding Host rejected");
                server.Publish("{\"status\":\"test\"}");
                Check(Request(client,"/api/state",token).Content.ReadAsStringAsync().Result.Contains("test"),"Published state available");
                Check(Request(client,"/api/message",token,"{\"text\":\"Drive to Paleto Bay\"}").StatusCode==HttpStatusCode.Accepted,"Request queued");
                CompanionCommand command;
                Check(server.TryTake(out command) && command.Text=="Drive to Paleto Bay" && !command.Stop,"Game thread receives exact text");
                Check(Request(client,"/api/message",token,"{\"text\":\"\"}").StatusCode==HttpStatusCode.BadRequest,"Empty request rejected");
                Check(Request(client,"/api/message",token,"{\"text\":\""+new string('a',501)+"\"}").StatusCode==HttpStatusCode.BadRequest,"Oversized prompt rejected");
                for(int i=0;i<8;i++) Request(client,"/api/message",token,"{\"text\":\"hello\"}");
                Check((int)Request(client,"/api/message",token,"{\"text\":\"overflow\"}").StatusCode==429,"Queue bounded");
                Check(Request(client,"/api/stop",token,"{}").StatusCode==HttpStatusCode.Accepted,"Stop accepted even with full queue");
                Check(server.TryTake(out command) && command.Stop && !server.TryTake(out command),"Stop discards previously queued actions");
                Check(Request(client,"/api/unknown",token).StatusCode==HttpStatusCode.NotFound,"Unknown endpoint rejected");
                Check(Request(client,"/api/message",null,"{\"text\":\"drive\"}").StatusCode==HttpStatusCode.Unauthorized,"Unauthenticated actions rejected");
                Check(Request(client,"/api/location",null,"{\"city\":\"Rome, Italy\"}").StatusCode==HttpStatusCode.Unauthorized,"Location requires authentication");
                Check(Request(client,"/api/location",token,"{\"latitude\":95,\"longitude\":10}").StatusCode==HttpStatusCode.BadRequest,"Impossible latitude rejected");
                Check(Request(client,"/api/location",token,"{\"city\":\"Rome\\nItaly\"}").StatusCode==HttpStatusCode.BadRequest,"Control characters in city rejected before reaching game thread");
                Check(Request(client,"/api/location",token,"{\"latitude\":true,\"longitude\":10}").StatusCode==HttpStatusCode.BadRequest,"Boolean is not a location coordinate");
                Check(Request(client,"/api/location",token,"{\"city\":\"Rome\",\"latitude\":42,\"longitude\":12}").StatusCode==HttpStatusCode.BadRequest,"Ambiguous location payload rejected");
                Check(Request(client,"/api/location",token,"{\"latitude\":41.9,\"longitude\":12.49}").StatusCode==HttpStatusCode.Accepted,"Browser coordinates accepted");
                Check(server.TryTake(out command) && command.Location && command.City==null && Math.Abs(command.Latitude-41.9)<.001,"Game receives location without a chat message");
                Check(Request(client,"/api/location",token,"{\"city\":\"Rome, Italy\"}").StatusCode==HttpStatusCode.Accepted,"City query accepted");
                Check(server.TryTake(out command) && command.Location && command.City=="Rome, Italy","City delivered only to local game queue");
            }
            Console.WriteLine(passed+" companion checks passed."); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine("After " + passed + " checks: " + e.GetBaseException().Message); return 1; }
    }
}
