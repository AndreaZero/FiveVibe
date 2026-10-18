using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using FiveVibe;

internal static class PlannerTests
{
    private static int passed;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
        passed++;
    }
    private static void Reject(Action action, string description)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } catch (FormatException) { rejected = true; }
        Check(rejected, description);
    }
    public static int Main()
    {
        try
        {
            var chill = LocalPlanner.Parse("prendi una macchina e porta micheal in giro nel chill", 45);
            Check(chill.Action == "cruise" && chill.Actor == "michael" && chill.SpeedKmh == 45, "Richiesta originale con alias Micheal");
            Check(LocalPlanner.Parse("VAI AL MARE", 45).Place == "beach", "Destinazione in italiano");
            Check(LocalPlanner.Parse("raggiungi il segnaposto", 45).Action == "waypoint", "Destinazione dinamica");
            Check(LocalPlanner.Parse("rallenta", 45).SpeedKmh == 35, "Velocita relativa al giro attuale");
            Check(LocalPlanner.Parse("vai a 40 km/h", 45).SpeedKmh == 40, "Velocita esplicita");
            Check(LocalPlanner.Parse("vai a 200 km/h", 45).Action == "say", "Velocita fuori intervallo rifiutata");
            Check(LocalPlanner.Parse("non andare al mare", 45).Action == "say", "Una negazione non avvia il viaggio");
            Check(LocalPlanner.Parse("vai al mare e poi all'aeroporto", 45).Action == "say", "Sequenza non supportata rifiutata");
            Check(LocalPlanner.Parse("vai al mare e all'aeroporto", 45).Action == "say", "Destinazioni multiple richiedono chiarimento");
            Check(LocalPlanner.Parse("spara e fai un giro", 45).Action == "say", "Richiesta oltre capacita rifiutata");
            Check(LocalPlanner.Parse("che bella la spiaggia", 45).Action == "say", "Citare un luogo non avvia un viaggio");
            Check(LocalPlanner.Parse("vai all'osservatorio", 45).Place == "observatory", "Apostrofi italiani");
            Check(LocalPlanner.Parse("scendi dalla macchina", 45).Action == "leave", "Uscita dal veicolo");
            Check(LocalPlanner.Parse("passa a micheal", 45).Action == "switch_character" && LocalPlanner.Parse("passa a micheal", 45).Actor == "michael", "Cambio protagonista con alias italiano");
            Check(LocalPlanner.Parse("passa a Trevor", 45).Actor == "trevor", "Selezione Trevor");
            Check(LocalPlanner.Parse("fai una passeggiata", 45).Action == "walk_wander", "Passeggiata indipendente dall'auto");
            var run = LocalPlanner.Parse("corri avanti per 50 metri", 45);
            Check(run.Action == "walk_forward" && run.Pace == "run" && run.DistanceMeters == 50, "Movimento a piedi con distanza e andatura");
            Check(LocalPlanner.Parse("cammina fino al segnaposto", 45).Action == "walk_waypoint", "Segnaposto raggiunto a piedi");
            Check(LocalPlanner.Parse("vai al mare a piedi", 45).Action == "walk_place", "Destinazione nota senza imporre auto");
            Check(LocalPlanner.Parse("saluta la persona davanti", 45).Action == "greet_npc" && LocalPlanner.Parse("saluta la persona davanti", 45).Target == "ahead", "Saluto al bersaglio davanti");
            Check(LocalPlanner.Parse("seguilo", 45).Action == "follow_npc" && LocalPlanner.Parse("seguilo", 45).Target == "current", "Riferimento alla persona selezionata prima");
            Check(LocalPlanner.Parse("avvicinati alla persona", 45).Action == "approach_npc", "Avvicinamento distinto da conversazione");
            Check(LocalPlanner.Parse("rispondi al telefono", 45).Action == "phone_answer", "Risposta alla chiamata");
            Check(LocalPlanner.Parse("chiudi il telefono", 45).Action == "phone_hangup", "Chiusura telefono");
            Check(LocalPlanner.Parse("raggiungi la missione di Lester", 45).Target == "lester", "Missione del contatto specificato");
            Check(LocalPlanner.Parse("stato della missione di Lester", 45).Action == "mission_status", "Stato missione senza navigazione implicita");
            Check(LocalPlanner.Parse("finisci tutta la storia", 45).Action == "say", "Completamento autonomo della storia non promesso");
            Check(LocalPlanner.Parse("Drive to Paleto Bay",45).Place=="paleto_bay", "English named destination");
            Check(LocalPlanner.Parse("portami a Sandy Shores",45).Place=="sandy_shores", "Italian named destination");
            Check(LocalPlanner.Parse("Walk to Mirror Park",45).Action=="walk_place", "English walking destination");
            Check(LocalPlanner.Parse("Drive to Sandy Shores Airfield",45).Place=="sandy_airfield", "Specific destination wins over district alias");
            Check(LocalPlanner.Parse("Drive to Mirror Park and Paleto Bay",45).Action=="say", "Multiple named destinations require clarification");
            Check(LocalPlanner.Parse("Drive to Atlantis",45).Action=="say", "Unknown place does not silently become a beach trip");
            Check(LocalPlanner.Parse("Answer the phone",45).Action=="phone_answer", "English phone command");
            Check(LocalPlanner.Parse("Run forward 50 meters",45).DistanceMeters==50, "English distance command");
            Check(LocalPlanner.Parse("Greet the person ahead",45).Action=="greet_npc", "English greeting");
            Check(LocalPlanner.Parse("Complete the entire story",45).Action=="say", "English story limit");
            Check(LocalPlanner.Parse("Go to Michael's house",45).Actor=="current", "A destination name does not switch the protagonist");
            Check(PlaceCatalog.All.Length==47 && PlaceCatalog.Ids.Distinct().Count()==48, "Unique built-in catalog IDs");
            var customFile=Path.GetTempFileName();
            try {
                File.WriteAllText(customFile,"[{\"Id\":\"my_spot\",\"Name\":\"Our quiet spot\",\"Aliases\":[\"il nostro posto\"],\"X\":195,\"Y\":-935,\"Z\":30}]");
                PlaceCatalog.LoadCustom(customFile);
                Check(LocalPlanner.Parse("vai al nostro posto",45).Action=="say", "Custom aliases match exactly, not guessed");
                Check(LocalPlanner.Parse("portami a il nostro posto",45).Place=="my_spot", "Custom location available to local planner");
                Check(CommandPlan.Places.Contains("my_spot") && PlaceCatalog.Describe().Contains("Our quiet spot"), "Custom location included in AI schema and description");
                File.WriteAllText(customFile,"[{\"Id\":\"beach\",\"Name\":\"Duplicate\",\"X\":1}]");
                Reject(()=>PlaceCatalog.LoadCustom(customFile), "Duplicate custom ID rejected");
                Check(PlaceCatalog.Get("my_spot")!=null, "Invalid file does not partially replace catalog");
                File.WriteAllText(customFile,"[{\"Id\":\"bad_place\",\"Name\":\"Invalid\",\"X\":999999}]");
                Reject(()=>PlaceCatalog.LoadCustom(customFile), "Out-of-map custom coordinates rejected");
            } finally { File.Delete(customFile); PlaceCatalog.LoadCustom(customFile); }
            Reject(() => CommandPlan.Create("switch_character", "current", 45), "Cambio senza un protagonista specifico rifiutato");
            Reject(() => CommandPlan.Create("greet_npc", "current", 45, "none", "", "walk", "lester"), "NPC non inventato da nome contatto");
            Reject(() => CommandPlan.Create("walk_forward", "current", 45, "none", "", "fly"), "Andatura sconosciuta rifiutata");
            Reject(() => CommandPlan.Create("walk_forward", "current", 45, "none", "", "run", "nearest", 1000), "Distanza fuori intervallo rifiutata");
            Check(GameplayRules.SwitchControl("michael") == 166 && GameplayRules.SwitchControl("franklin") == 167 && GameplayRules.SwitchControl("trevor") == 168 && GameplayRules.SwitchControl("current") == -1,
                "Cambio limitato ai comandi originali dei protagonisti, senza accesso Online");
            Check(GameplayRules.WalkSpeed("walk") < GameplayRules.WalkSpeed("jog") && GameplayRules.WalkSpeed("jog") < GameplayRules.WalkSpeed("run"), "Andature distinte");
            var switchMonitor=new CharacterSwitchMonitor();
            Check(switchMonitor.Observe(5500,false,false,true,true)==CharacterSwitchResult.Waiting,"Character wheel delay is not a rejected switch after five seconds");
            Check(switchMonitor.Observe(18000,false,false,true,true)==CharacterSwitchResult.Waiting,"Slow loading without the native transition flag remains pending");
            Check(switchMonitor.Observe(19000,true,true,false,false)==CharacterSwitchResult.Waiting,"Changed model during a camera transition is not ready");
            Check(switchMonitor.Observe(20000,true,false,true,false)==CharacterSwitchResult.Waiting,"Screen fade must finish before confirmation");
            Check(switchMonitor.Observe(21000,true,false,true,true)==CharacterSwitchResult.Waiting,"Playable target starts a stability check");
            Check(switchMonitor.Observe(21400,true,false,true,true)==CharacterSwitchResult.Waiting,"One short playable interval is not enough");
            Check(switchMonitor.Observe(21750,true,false,true,true)==CharacterSwitchResult.Confirmed,"Delayed successful switch is confirmed when stable");
            switchMonitor.Reset();
            switchMonitor.Observe(0,true,false,true,true);
            switchMonitor.Observe(500,true,true,false,false);
            switchMonitor.Observe(1000,true,false,true,true);
            Check(switchMonitor.Observe(1500,true,false,true,true)==CharacterSwitchResult.Waiting,"A transition flag flicker resets the stability check");
            Check(switchMonitor.Observe(1750,true,false,true,true)==CharacterSwitchResult.Confirmed,"Confirmation follows a fully stable playable interval");
            switchMonitor.Reset();
            switchMonitor.Observe(0,true,false,true,true);
            switchMonitor.Reset();
            Check(switchMonitor.Observe(1000,true,false,true,true)==CharacterSwitchResult.Waiting,"Cancellation clears the pending confirmation");
            switchMonitor.Reset();
            Check(switchMonitor.Observe(120000,false,false,true,true)==CharacterSwitchResult.Unconfirmed,"Unavailable target has a bounded neutral timeout");
            switchMonitor.Reset();
            switchMonitor.Observe(119900,true,false,true,true);
            Check(switchMonitor.Observe(120000,true,false,true,true)==CharacterSwitchResult.Waiting,"Late arrival is allowed to finish the stability check");
            Check(switchMonitor.Observe(120650,true,false,true,true)==CharacterSwitchResult.Confirmed,"Late stable success takes priority over the overall deadline");
            Check(LocalPlanner.IsImmediateStop("FERMATI!"), "Stop immediato anche senza API");
            Check(!LocalPlanner.IsImmediateStop("non fermarti"), "Stop non attivato da negazione");
            var gate = new RequestGate();
            int oldRequest = gate.Begin();
            gate.Cancel();
            Check(!gate.IsCurrent(oldRequest), "Risposta tardiva non eseguita dopo ripresa controllo");
            int nextRequest = gate.Begin();
            Check(gate.IsCurrent(nextRequest), "Nuova richiesta valida dopo annullamento");
            Reject(() => CommandPlan.Create("arbitrary_code", "current", 45), "Azioni sconosciute rifiutate");
            Reject(() => CommandPlan.Create("cruise", "current", 45, "beach"), "Destinazione incoerente rifiutata");
            Reject(() => CommandPlan.Create("cruise", "unknown", 45), "Protagonista sconosciuto rifiutato");
            var serializer = new JavaScriptSerializer();
            string arguments = serializer.Serialize(CommandPlan.Create("place", "current", 40, "airport"));
            string valid = serializer.Serialize(new { status = "completed", output = new object[] { new { type = "reasoning" }, new { type = "function_call", name = "plan_game_action", arguments = arguments } } });
            Check(OpenAiPlanner.ParseResponse(valid).Place == "airport", "Formato Responses API con output misti");
            Reject(() => OpenAiPlanner.ParseResponse("{\"status\":\"incomplete\",\"output\":[]}"), "Risposta incompleta non eseguita");
            Reject(() => OpenAiPlanner.ParseResponse("{\"status\":\"completed\",\"output\":[]}"), "Risposta senza funzione non eseguita");
            string multiple = serializer.Serialize(new { status = "completed", output = new[] {
                new { type = "function_call", name = "plan_game_action", arguments = arguments },
                new { type = "function_call", name = "plan_game_action", arguments = arguments } } });
            Reject(() => OpenAiPlanner.ParseResponse(multiple), "Nessuna azione eseguita da piani multipli");
            foreach (float aspect in new[] { 4f / 3f, 16f / 9f, 21f / 9f, 32f / 9f })
            {
                var layout = new HudLayout(aspect, .9f);
                Check(layout.Left > .05f && layout.Right < .95f && layout.Top > .05f && layout.Top + layout.Height < .95f,
                    "Pannello dentro area sicura: " + aspect);
                Check(Math.Abs(layout.Width * aspect - .53f) < .0001f, "Proporzioni stabili: " + aspect);
                Check(layout.HistoryTop < layout.HistoryBottom && layout.HistoryBottom < layout.QuickTop && layout.QuickTop + .032f < layout.InputTop,
                    "Cronologia, scorciatoie e input non si sovrappongono: " + aspect);
            }
            Func<string, float> proportional = s => s.Sum(c => c == 'W' ? 3f : 1f);
            string[] wrapped = HudText.Wrap("WWWW vai al mare con calma", 9, proportional);
            Check(wrapped.All(line => proportional(line) <= 9), "Righe rispettano la misura proporzionale del font");
            Check(string.Join("", wrapped).Replace(" ", "") == "WWWWvaialmareconcalma", "Nessun testo perso durante il ritorno a capo");
            string longMessage = new string('W', 700);
            string[] longRows = HudText.Wrap(longMessage, 9, proportional);
            Check(string.Concat(longRows) == longMessage && longRows.All(line => proportional(line) <= 9), "Messaggio lungo interamente leggibile nella cronologia");
            string unicode = "\U0001F697e\u0300\U0001F697e\u0300";
            Func<string, float> graphemes = s => System.Globalization.StringInfo.ParseCombiningCharacters(s).Length;
            string[] unicodeRows = HudText.Wrap(unicode, 1, graphemes);
            Check(unicodeRows.Length == 4 && string.Concat(unicodeRows) == unicode, "Accenti e coppie surrogate restano integri");
            Check(HudText.Wrap("prima\n\nultima", 20, proportional).SequenceEqual(new[] { "prima", "", "ultima" }), "Paragrafi conservati");
            string tail = HudText.Tail("WWWW destinazione lontana", 12, proportional);
            Check(tail.StartsWith("...") && proportional(tail) <= 12 && tail.EndsWith("lontana"), "Etichette lunghe contenute nel mini HUD");
            foreach(int length in new[]{33,98,99,100,200,420}) {
                string input=new string('W',length-4)+"TAIL";
                Check(RenderNative(input)==input,"Native text renders beyond the old 32-character boundary: "+length);
            }
            string accented=string.Concat(Enumerable.Repeat("\u00e8\u4e00\U0001f697",100));
            Check(string.Concat(HudText.NativeChunks(accented))==accented && HudText.NativeChunks(accented).All(s=>System.Text.Encoding.UTF8.GetByteCount(s)<=99),"Native components respect UTF-8 byte limits and preserve Unicode");
            string composer=new string('W',416)+"TAIL";
            var composerLines=HudText.Wrap(composer,40,s=>RenderNative(s).Length);
            int inputScroll=0;
            Check(HudText.InputWindow(composerLines,3,ref inputScroll).Last().EndsWith("TAIL"),"420-character composer follows the latest text");
            inputScroll=999;
            Check(HudText.InputWindow(composerLines,3,ref inputScroll).First()==composerLines.First() && inputScroll==composerLines.Length-3,"Input can scroll back to the first lines");
            inputScroll=0;
            Check(HudText.InputWindow(composerLines,3,ref inputScroll).SequenceEqual(composerLines.Skip(composerLines.Length-3)),"Input scroll returns to the tail");
            var rain=LocalPlanner.Parse("Metti pioggia e imposta ora alle 22:30",45);
            Check(rain.Action=="world_settings" && rain.WeatherType=="rain" && rain.Hour==22 && rain.Minute==30,"Combined Italian weather and time request");
            Check(LocalPlanner.Parse("Set time to 18:45",45).Hour==18 && LocalPlanner.Parse("Set time to 18:45",45).Minute==45,"Custom clock time");
            Check(LocalPlanner.Parse("Metti il tramonto",45).Hour==19,"Named sunset preset");
            Check(LocalPlanner.Parse("Freeze the clock",45).Clock=="pause" && LocalPlanner.Parse("Resume the clock",45).Clock=="resume","Clock pause and resume");
            Check(LocalPlanner.Parse("Hide the task HUD",45).Action=="hud_hide" && LocalPlanner.Parse("mostra HUD",45).Action=="hud_show","HUD display commands");
            Check(LocalPlanner.Parse("Do not hide the HUD",45).Action=="say" && LocalPlanner.Parse("I like rain",45).Action=="say","Mentions and negations do not change world or HUD");
            Check(LocalPlanner.Parse("Set time to 25:90",45).Action=="say","Invalid local time rejected");
            Reject(()=>CommandPlan.World("rain",24),"Invalid structured hour rejected");
            Reject(()=>CommandPlan.World("rain",-1,30),"Minutes without an hour rejected");
            Reject(()=>CommandPlan.World("hurricane"),"Unknown structured weather rejected");
            Reject(()=>CommandPlan.World(),"Empty world action rejected");
            var wrongWorld=CommandPlan.Create("cruise","current",45); wrongWorld.WeatherType="rain";
            Reject(()=>wrongWorld.Validate(),"Movement cannot smuggle a weather change");
            var preferences=Path.Combine(Path.GetTempPath(),"FiveVibe-test-"+Guid.NewGuid().ToString("N")+".json");
            try {
                Check(HudPreferences.Load(preferences,true),"HUD preference defaults from INI");
                HudPreferences.Save(preferences,false);
                Check(!HudPreferences.Load(preferences,true),"Hidden HUD persists across sessions");
                HudPreferences.Save(preferences,true);
                Check(HudPreferences.Load(preferences,false),"HUD preference can be restored atomically");
            } finally { if(File.Exists(preferences))File.Delete(preferences); }
            var envValues = AiConfiguration.Parse("\uFEFF# comment\nexport OPENAI_API_KEY = 'test-only-key' # local\nOPENAI_MODEL=\"gpt-4.1-mini\"\nOTHER=value\n");
            Check(envValues["OPENAI_API_KEY"] == "test-only-key" && envValues["OPENAI_MODEL"] == "gpt-4.1-mini", "Env con BOM, commenti, virgolette ed export");
            Check(envValues.Count == 2, "Nessuna variabile estranea viene caricata");
            string secretMarker = "do-not-print-this-test-value";
            bool redacted = false;
            try { AiConfiguration.Parse("OPENAI_API_KEY=\"" + secretMarker); }
            catch (FormatException error) { redacted = !error.Message.Contains(secretMarker); }
            Check(redacted, "Errori di parsing non mostrano la chiave");
            string envFixture = Path.GetTempFileName();
            try
            {
                File.WriteAllText(envFixture, "OPENAI_API_KEY=\nOPENAI_MODEL=gpt-4.1-mini\n");
                Check(!AiConfiguration.Load(envFixture, "gpt-4.1-mini", "legacy-test", () => "environment-test").HasApiKey,
                    "Env esplicito vuoto non riusa altre credenziali");
                File.WriteAllText(envFixture, "OPENAI_API_KEY=test-only-first\nOPENAI_MODEL=gpt-4.1-mini\n");
                Check(AiConfiguration.Load(envFixture, "gpt-4.1-mini").HasApiKey, "Chiave caricata dal file locale");
                File.WriteAllText(envFixture, "OPENAI_API_KEY=test-only-second\nOPENAI_MODEL=gpt-4.1\n");
                var changed = AiConfiguration.Load(envFixture, "gpt-4.1-mini");
                Check(changed.ApiKey == "test-only-second" && changed.Model == "gpt-4.1", "Configurazione riletta dopo una modifica");
                File.WriteAllText(envFixture, "OPENAI_API_KEY=bad key\nOPENAI_MODEL=gpt-4.1-mini\n");
                Reject(() => AiConfiguration.Load(envFixture, "gpt-4.1-mini"), "Chiavi con spazi rifiutate prima della rete");
            }
            finally { File.Delete(envFixture); }
            var handler = new PlannerHandler(valid, HttpStatusCode.OK);
            using (var client = new HttpClient(handler))
            using (var planner = new OpenAiPlanner("test-only-token", "gpt-4.1-mini", client))
            {
                Check(planner.PlanAsync("una richiesta libera", "context", new string[0], CancellationToken.None).GetAwaiter().GetResult().Place == "airport", "Percorso HTTP asincrono completo con risposta simulata");
                var requestBody = serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(handler.Body);
                Check(handler.CorrectDestination && handler.BearerConfigured && !(bool)requestBody["store"] && !(bool)requestBody["parallel_tool_calls"],
                    "Richiesta solo a OpenAI con credenziale Bearer, store false e singola azione");
            }
            using (var client = new HttpClient(new PlannerHandler(secretMarker, HttpStatusCode.Unauthorized)))
            using (var planner = new OpenAiPlanner("test-only-token", "gpt-4.1-mini", client))
            {
                bool safeError = false;
                try { planner.PlanAsync("test", "context", new string[0], CancellationToken.None).GetAwaiter().GetResult(); }
                catch (InvalidOperationException error) { safeError = error.Message.Contains("401") && !error.Message.Contains(secretMarker); }
                Check(safeError, "Errore API leggibile senza corpo HTTP o credenziali");
            }
            Console.WriteLine(passed + " checks passed (commands, API, env configuration, cancellation and HUD).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static string RenderNative(string text)
    {
        string label="", rendered=""; int components=0;
        HudText.PushNative(text,value=>label=value,chunk=> {
            if(label=="CELL_EMAIL_BCON" || components==0) rendered+=chunk;
            components++;
        });
        return rendered;
    }
    private sealed class PlannerHandler : HttpMessageHandler
    {
        private readonly string response;
        private readonly HttpStatusCode status;
        public string Body;
        public bool CorrectDestination, BearerConfigured;
        public PlannerHandler(string json, HttpStatusCode code) { response = json; status = code; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CorrectDestination = request.RequestUri.AbsoluteUri == "https://api.openai.com/v1/responses";
            BearerConfigured = request.Headers.Authorization != null && request.Headers.Authorization.Scheme == "Bearer";
            Body = await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(status) { Content = new StringContent(response) };
        }
    }
}
