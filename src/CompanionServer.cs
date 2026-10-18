using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace FiveVibe
{
    public sealed class CompanionCommand { public string Text; public bool Stop; }

    // Loopback only. Worker threads never call GTA; commands are consumed by the script Tick.
    public sealed class CompanionServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Thread worker;
        private readonly ConcurrentQueue<CompanionCommand> commands = new ConcurrentQueue<CompanionCommand>();
        private readonly string token, origin, host, page;
        private string snapshot = "{\"status\":\"Starting\",\"paused\":false,\"messages\":[],\"places\":[]}";
        private volatile bool disposed;
        private int clients;
        public string Url { get; private set; }
        public string DiscoveryPath { get; private set; }

        public CompanionServer(int port, bool writeDiscovery = true)
        {
            if (port < 1024 || port > 65535) throw new ArgumentOutOfRangeException("port");
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            host = "127.0.0.1:" + port;
            origin = "http://" + host;
            Url = origin + "/#" + token;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FiveVibe.Companion.html"))
            {
                if (stream == null) throw new InvalidOperationException("Companion page resource is missing.");
                using (var reader = new StreamReader(stream)) page = reader.ReadToEnd();
            }
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(8);
            try
            {
                if (writeDiscovery)
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FiveVibe");
                    Directory.CreateDirectory(folder);
                    DiscoveryPath = Path.Combine(folder, "companion.json");
                    File.WriteAllText(DiscoveryPath, new JavaScriptSerializer().Serialize(new { url=Url, processId=System.Diagnostics.Process.GetCurrentProcess().Id }));
                }
                worker = new Thread(Listen) { IsBackground=true, Name="FiveVibe companion" };
                worker.Start();
            }
            catch { listener.Stop(); throw; }
        }
        public void Publish(string json) { Interlocked.Exchange(ref snapshot, json); }
        public bool TryTake(out CompanionCommand command) { lock (commands) return commands.TryDequeue(out command); }
        private void Listen()
        {
            while (!disposed)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch (SocketException) { if (disposed) return; continue; }
                catch (ObjectDisposedException) { return; }
                if (Interlocked.Increment(ref clients) > 4) { Interlocked.Decrement(ref clients); client.Close(); continue; }
                ThreadPool.QueueUserWorkItem(delegate {
                    try { Handle(client); } catch (Exception) { /* Invalid/disconnected requests never reach the game. */ }
                    finally { client.Close(); Interlocked.Decrement(ref clients); }
                });
            }
        }
        private void Handle(TcpClient client)
        {
            client.ReceiveTimeout = client.SendTimeout = 1500;
            using (var stream = client.GetStream())
            {
                var headerBytes = new List<byte>();
                while (headerBytes.Count < 8192)
                {
                    int b = stream.ReadByte();
                    if (b < 0) return;
                    headerBytes.Add((byte)b);
                    int n = headerBytes.Count;
                    if (n >= 4 && headerBytes[n-4] == 13 && headerBytes[n-3] == 10 && headerBytes[n-2] == 13 && headerBytes[n-1] == 10) break;
                }
                string raw = Encoding.ASCII.GetString(headerBytes.ToArray());
                if (!raw.EndsWith("\r\n\r\n")) { Reply(stream, 431, "{}", false); return; }
                string[] lines = raw.Split(new[] { "\r\n" }, StringSplitOptions.None);
                string[] first = lines[0].Split(' ');
                var headers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                for (int i=1; i<lines.Length && lines[i].Length > 0; i++)
                {
                    int colon = lines[i].IndexOf(':');
                    if (colon < 1 || headers.ContainsKey(lines[i].Substring(0,colon))) { Reply(stream,400,"{}",false); return; }
                    headers.Add(lines[i].Substring(0,colon),lines[i].Substring(colon+1).Trim());
                }
                string value;
                if (first.Length != 3 || !headers.TryGetValue("Host",out value) || !value.Equals(host,StringComparison.OrdinalIgnoreCase) ||
                    (headers.TryGetValue("Origin",out value) && value != origin) || headers.ContainsKey("Transfer-Encoding"))
                { Reply(stream,403,"{}",false); return; }
                if (first[0] == "GET" && first[1] == "/") { Reply(stream,200,page,true); return; }
                byte[] body = new byte[0];
                // Consume bounded request bodies before replying, including Stop and
                // rejected authentication, so closing the socket does not reset HTTP.
                if (first[0] == "POST")
                {
                    int length;
                    if (!headers.TryGetValue("Content-Length",out value) || !int.TryParse(value,out length) || length < 1 || length > 2048 ||
                        !headers.TryGetValue("Content-Type",out value) || !value.StartsWith("application/json",StringComparison.OrdinalIgnoreCase))
                    { Reply(stream,400,"{}",false); return; }
                    body = new byte[length];
                    for (int count=0; count<length; ) { int read=stream.Read(body,count,length-count); if (read==0) return; count+=read; }
                }
                if (!headers.TryGetValue("Authorization",out value) || value != "Bearer " + token)
                { Reply(stream,401,"{}",false); return; }
                if (first[0] == "GET" && first[1] == "/api/state") { Reply(stream,200,snapshot,false); return; }
                if (first[0] != "POST" || (first[1] != "/api/message" && first[1] != "/api/stop"))
                { Reply(stream,404,"{}",false); return; }
                if (first[1] == "/api/stop")
                {
                    lock (commands)
                    {
                        CompanionCommand discarded;
                        while (commands.TryDequeue(out discarded)) { }
                        commands.Enqueue(new CompanionCommand { Stop=true });
                    }
                    Reply(stream,202,"{\"queued\":true}",false); return;
                }
                Dictionary<string,object> message;
                try { message = new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(body)); }
                catch (Exception) { Reply(stream,400,"{}",false); return; }
                object text;
                if (message == null || !message.TryGetValue("text",out text) || !(text is string) || string.IsNullOrWhiteSpace((string)text) || ((string)text).Length > 500)
                { Reply(stream,400,"{}",false); return; }
                lock (commands)
                {
                    if (commands.Count >= 8) { Reply(stream,429,"{}",false); return; }
                    commands.Enqueue(new CompanionCommand { Text=((string)text).Trim() });
                }
                Reply(stream,202,"{\"queued\":true}",false);
            }
        }
        private static void Reply(Stream stream, int status, string content, bool html)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            string reason = status==200 ? "OK" : status==202 ? "Accepted" : "Request Rejected";
            string head = "HTTP/1.1 " + status + " " + reason + "\r\nContent-Type: " + (html ? "text/html" : "application/json") +
                "; charset=utf-8\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n" +
                "Content-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'\r\n\r\n";
            byte[] header = Encoding.ASCII.GetBytes(head);
            stream.Write(header,0,header.Length); stream.Write(bytes,0,bytes.Length);
        }
        public void Dispose()
        {
            disposed = true; listener.Stop();
            if (worker != null) worker.Join(2000);
            if (DiscoveryPath != null)
                try { if (File.Exists(DiscoveryPath) && File.ReadAllText(DiscoveryPath).Contains(token)) File.Delete(DiscoveryPath); } catch (IOException) { }
        }
    }
}
