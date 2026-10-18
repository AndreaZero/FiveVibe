using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTA.UI;

namespace FiveVibe
{
    public sealed class ChatMessage
    {
        public bool User;
        public string Text;
    }

    public sealed class ChatOverlay : IDisposable
    {
        public bool IsOpen;
        public string Input = "", Provider = "LOCAL", Status = "Ready";
        public string CharacterName = "Michael", Route = "Free roam";
        public bool Thinking, Autonomous, OnFoot;
        public bool ClockFrozen;
        public string WorldTime="", WeatherName="";
        public string HudToggleKeyName="F9";
        public string CameraMode="Gameplay", CameraState="Gameplay", CameraKeyName="F10";
        public bool CameraLetterbox=true;
        public bool AutoCinemaDriving=true,RealClock,RealWeather;
        public bool ShowDrivingHud = true, InterfaceSounds = true;
        public int CurrentSpeedKmh, HudColorId = 143;
        public readonly List<ChatMessage> Messages = new List<ChatMessage>();
        private int scroll, historyIndex, suggestionIndex = -1;
        private readonly List<string> inputHistory = new List<string>();
        private string draft = "", notification;
        private readonly List<string> pasteQueue = new List<string>();
        private readonly List<HistoryRow> rows = new List<HistoryRow>();
        private bool historyDirty = true, wasOpen, disposed;
        private float cachedWidth = -1;
        private string cachedInput;
        private string[] inputLines = new string[0];
        private int inputScroll;
        private float inputWidth = -1;
        private int menuTab, menuPage;
        private float menuScroll;
        private HudMenuViewport menuViewport;
        private float menuTextRight;
        private HudHintRow hintRow;
        private string hintState;
        private bool walkToPlace;
        private CommandPlan pendingUiPlan;
        private static readonly string[] MenuTabs={"CHAT","PLACES","IDEAS","WORLD","HUD","CAMERA"};
        public CommandPlan TakeUiPlan() { var result=pendingUiPlan; pendingUiPlan=null; return result; }
        private int cachedColor = -1, lastTextureRequest;
        private Color accent = Color.FromArgb(101, 180, 212);
        private static readonly Color White = Color.FromArgb(240, 240, 240);
        private static readonly Color Muted = Color.FromArgb(171, 174, 177);
        private static readonly string[] QuickLabels = { "CRUISE", "PLACES", "WAYPOINT" };
        private static readonly string[] QuickCommands = { "Take a chill drive", "Drive to Mirror Park", "Go to the waypoint" };
        private static readonly string[] FootLabels = { "STROLL", "SAY HELLO", "MISSION" };
        private static readonly string[] FootCommands = { "Take a stroll", "Greet the nearest person", "Go to the nearest mission marker" };

        private sealed class HistoryRow
        {
            public string Text;
            public bool User, Label, Gap;
            public float Height { get { return Gap ? .013f : Label ? .022f : .025f; } }
        }

        public void Add(bool user, string text)
        {
            text = Clean(text);
            if (text.Length > 700) text = text.Substring(0, 700);
            Messages.Add(new ChatMessage { User = user, Text = text });
            if (Messages.Count > 60) Messages.RemoveAt(0);
            historyDirty = true;
            scroll = 0;
            if (!user && !IsOpen) notification = text;
        }

        public string Key(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { if(menuTab!=0) { menuTab=menuPage=0; menuScroll=0; } else IsOpen=false; return null; }
            if(e.Control && (e.KeyCode==Keys.Left || e.KeyCode==Keys.Right))
            { menuTab=(menuTab+MenuTabs.Length+(e.KeyCode==Keys.Right?1:-1))%MenuTabs.Length; menuPage=0; menuScroll=0; return null; }
            if (e.Shift && e.KeyCode == Keys.PageUp) { inputScroll+=2; return null; }
            if (e.Shift && e.KeyCode == Keys.PageDown) { inputScroll=Math.Max(0,inputScroll-2); return null; }
            if((menuTab==1 || menuTab==2) && (e.KeyCode==Keys.PageUp || e.KeyCode==Keys.PageDown))
            { menuPage=Math.Max(0,menuPage+(e.KeyCode==Keys.PageDown?1:-1)); return null; }
            if((menuTab==3 || menuTab==5) && (e.KeyCode==Keys.PageUp || e.KeyCode==Keys.PageDown))
            { menuScroll=Math.Max(0,menuScroll+(e.KeyCode==Keys.PageDown?.22f:-.22f)); return null; }
            if (e.KeyCode == Keys.PageUp) { scroll += 6; return null; }
            if (e.KeyCode == Keys.PageDown) { scroll = Math.Max(0, scroll - 6); return null; }
            if (e.KeyCode == Keys.Tab)
            {
                suggestionIndex = (suggestionIndex + (e.Shift ? QuickCommands.Length - 1 : 1) + QuickCommands.Length) % QuickCommands.Length;
                Input = (OnFoot ? FootCommands : QuickCommands)[suggestionIndex];
                return null;
            }
            if (e.KeyCode == Keys.Enter) return SubmitInput();
            if (e.KeyCode == Keys.Back)
            {
                if (Input.Length > 0)
                {
                    int[] elements = System.Globalization.StringInfo.ParseCombiningCharacters(Input);
                    Input = Input.Substring(0, elements[elements.Length - 1]);
                }
                return null;
            }
            if (e.Control && e.KeyCode == Keys.A) { Input = ""; return null; }
            if (e.KeyCode == Keys.Up && inputHistory.Count > 0)
            {
                if (historyIndex == inputHistory.Count) draft = Input;
                historyIndex = Math.Max(0, historyIndex - 1);
                Input = inputHistory[historyIndex];
                return null;
            }
            if (e.KeyCode == Keys.Down && inputHistory.Count > 0)
            {
                historyIndex = Math.Min(inputHistory.Count, historyIndex + 1);
                Input = historyIndex == inputHistory.Count ? draft : inputHistory[historyIndex];
                return null;
            }
            if (e.Control && e.KeyCode == Keys.V)
            {
                var worker = new System.Threading.Thread(delegate() {
                    try { if (Clipboard.ContainsText()) lock (pasteQueue) pasteQueue.Add(Clipboard.GetText()); }
                    catch (ExternalException) { }
                });
                worker.IsBackground = true;
                worker.SetApartmentState(System.Threading.ApartmentState.STA);
                worker.Start();
                return null;
            }
            if (e.Control || e.Alt) return null;
            var keyboard = new byte[256];
            if (!GetKeyboardState(keyboard)) return null;
            if (e.Shift) keyboard[(int)Keys.ShiftKey] |= 128;
            var buffer = new StringBuilder(8);
            IntPtr layout = GetKeyboardLayout(0);
            int count = ToUnicodeEx((uint)e.KeyCode, MapVirtualKeyEx((uint)e.KeyCode, 0, layout), keyboard, buffer, 8, 4, layout);
            if (count > 0) Append(buffer.ToString());
            return null;
        }

        private string SubmitInput()
        {
            string submitted = Input.Trim();
            if (submitted.Length == 0) return null;
            inputHistory.Add(submitted);
            if (inputHistory.Count > 30) inputHistory.RemoveAt(0);
            historyIndex = inputHistory.Count;
            Input = draft = "";
            suggestionIndex = -1;
            PlaySound("SELECT");
            return submitted;
        }

        public string Draw(string openKey, string takeoverKey)
        {
            if (disposed) return null;
            menuViewport=null;
            lock (pasteQueue) { foreach (string text in pasteQueue) if (IsOpen) Append(text); pasteQueue.Clear(); }
            if (wasOpen != IsOpen)
            {
                PlaySound(IsOpen ? "SELECT" : "BACK");
                wasOpen = IsOpen;
            }
            if (cachedColor != HudColorId)
            {
                accent = HudColors.GetColor((HudColor)HudColorId);
                cachedColor = HudColorId;
            }
            if (notification != null)
            {
                if (!IsOpen) Notification.PostTicker("~h~FiveVibe~h~\n" + notification, false, true);
                notification = null;
            }
            var box = new HudLayout(Function.Call<float>(Hash.GET_ASPECT_RATIO, false), Function.Call<float>(Hash.GET_SAFE_ZONE_SIZE));
            if (!IsOpen)
            {
                if (Autonomous || Thinking)
                {
                    if (ShowDrivingHud) { DrawDrivingHud(box); DrawInstructions(box,openKey, takeoverKey); }
                }
                return null;
            }

            Hud.ShowCursorThisFrame();
            Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, 14);
            float mouseX = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, 239);
            float mouseY = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, 240);
            bool click = Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, 237);
            int wheel=Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED,0,241)?1:Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED,0,242)?-1:0;
            if(wheel!=0)
            {
                if(Hit(mouseX,mouseY,box.Left,box.InputTop,box.Width,.083f)) inputScroll=Math.Max(0,inputScroll+wheel);
                else if(Hit(mouseX,mouseY,box.Left,box.HistoryTop,box.Width,box.HistoryBottom-box.HistoryTop))
                {
                    if(menuTab==1 || menuTab==2) menuPage=Math.Max(0,menuPage-wheel);
                    else if(menuTab==3 || menuTab==5)menuScroll=Math.Max(0,menuScroll-wheel*.037f);
                    else if(menuTab==0)scroll=Math.Max(0,scroll+wheel*3);
                }
            }

            float x = box.Left, y = box.Top, p = box.Padding;
            Rect(x, y, box.Width, box.Height, Color.FromArgb(226, 0, 0, 0));
            DrawMenuTexture(x, y, box.Width, .101f, accent);
            Rect(x, y + .098f, box.Width, .003f, accent);
            Text("FiveVibe", x + p, y + .007f, .90f, White, 1);
            Text("LOS SANTOS  /  YOUR AMBIENT COMPANION", x + p, y + .071f, .245f, White);

            float closeW = .029f / box.Aspect;
            float closeX = box.Right - p - closeW;
            bool closeHover = Hit(mouseX, mouseY, closeX, y + .015f, closeW, .032f);
            if (closeHover) Rect(closeX, y + .015f, closeW, .032f, Color.FromArgb(80, 255, 255, 255));
            Text("X", closeX + .007f / box.Aspect, y + .016f, .31f, White);
            if (click && closeHover) { IsOpen = false; return null; }

            Rect(x, y + .101f, box.Width, .034f, Color.Black);
            string state = Thinking ? "PLANNING YOUR REQUEST" : Autonomous ? Status.ToUpperInvariant() : "FREE ROAM";
            float stateSpace = box.TextWidth - NativeMeasure(CharacterName.ToUpperInvariant(), .265f) - .018f / box.Aspect;
            Text(HudText.Tail(state, stateSpace, s => NativeMeasure(s, .265f)), x + p, y + .106f, .265f, accent);
            TextRight(CharacterName.ToUpperInvariant(), box.Right - p, y + .106f, .265f, White);

            DrawTabs(box,mouseX,mouseY,click);
            if(menuTab==0) DrawHistory(box);
            else DrawSubmenu(box,mouseX,mouseY,click);
            Text(menuTab==0 && scroll > 0 ? "HISTORY  /  PgDn: latest messages" : "QUICK REQUESTS  /  TAB", x + p, y + .523f, .215f, Muted);
            float gap = .006f / box.Aspect;
            float chipW = (box.Width - p * 2 - gap * 2) / 3;
            string[] quickCommands = OnFoot ? FootCommands : QuickCommands;
            string[] quickLabels = OnFoot ? FootLabels : QuickLabels;
            for (int i = 0; i < QuickCommands.Length; i++)
            {
                float chipX = x + p + i * (chipW + gap);
                bool hover = Hit(mouseX, mouseY, chipX, box.QuickTop, chipW, .032f);
                bool selected = Input == quickCommands[i];
                Rect(chipX, box.QuickTop, chipW, .032f, hover || selected ? accent : Color.FromArgb(200, 39, 39, 39));
                Text(quickLabels[i], chipX + .008f / box.Aspect, box.QuickTop + .004f, .255f, hover || selected ? Color.Black : White, 4);
                if (click && hover) { Input = quickCommands[i]; suggestionIndex = i; PlaySound("NAV_UP_DOWN"); }
            }

            float composerWidth = box.Width - p * 2;
            Rect(x + p, box.InputTop, composerWidth, .083f, Color.FromArgb(255, 34, 34, 34));
            Rect(x + p, box.InputTop + .081f, composerWidth, .002f, accent);
            float sendW = .042f / box.Aspect;
            float sendX = box.Right - p - sendW;
            bool sendHover = Hit(mouseX, mouseY, sendX, box.InputTop, sendW, .083f);
            if (sendHover) Rect(sendX, box.InputTop, sendW, .081f, Color.FromArgb(50, accent));
            Text(">", sendX + .010f / box.Aspect, box.InputTop + .022f, .55f, Input.Length == 0 ? Muted : accent, 4);
            float inputWidth = composerWidth - sendW - .021f / box.Aspect;
            DrawInput(x + p + .010f / box.Aspect, box.InputTop + .006f, inputWidth);

            Text(Provider == "OPENAI" ? "AI COMPANION" : Provider == "OPENAI_PENDING" ? "SET UP AI" : "LOCAL COMMANDS", x + p, y + .698f, .225f, Muted);
            TextRight(Input.Length + "/420", box.Right - p, y + .698f, .225f, Muted);
            DrawInstructions(box,openKey, takeoverKey);
            return click && sendHover ? SubmitInput() : null;
        }

        private void DrawTabs(HudLayout box,float mx,float my,bool click)
        {
            float gap=.004f/box.Aspect, width=(box.TextWidth-gap*(MenuTabs.Length-1))/MenuTabs.Length;
            for(int i=0;i<MenuTabs.Length;i++)
            {
                float left=box.Left+box.Padding+i*(width+gap);
                if(Button(MenuTabs[i],left,box.Top+.146f,width,.031f,mx,my,click,i==menuTab))
                { menuTab=i; menuPage=0; menuScroll=0; }
            }
        }
        private bool Button(string label,float x,float y,float w,float h,float mx,float my,bool click,bool selected=false)
        {
            if(menuViewport!=null && !menuViewport.Contains(y,h))return false;
            bool hover=Hit(mx,my,x,y,w,h);
            Rect(x,y,w,h,hover||selected ? accent : Color.FromArgb(200,35,35,35));
            float aspect=Function.Call<float>(Hash.GET_ASPECT_RATIO,false);
            Text(HudText.Tail(label,w-.012f/aspect,s=>NativeMeasure(s,.235f)),x+.004f/aspect,y+.004f,.235f,hover||selected ? Color.Black : White);
            if(click&&hover) { PlaySound("NAV_UP_DOWN"); return true; }
            return false;
        }
        private void DrawSubmenu(HudLayout box,float mx,float my,bool click)
        {
            bool scrollable=menuTab==3 || menuTab==5;
            menuTextRight=box.Right-box.Padding;
            menuViewport=scrollable?new HudMenuViewport(box.HistoryTop,box.MenuBottom,box.Top+(menuTab==3?.645f:.571f),menuScroll):null;
            if(menuViewport!=null)menuScroll=menuViewport.Offset;
            float left=box.Left+box.Padding,width=box.TextWidth,y=box.Top-(scrollable?menuScroll:0);
            if(menuTab==1)
            {
                var places=PlaceCatalog.All;
                int pages=Math.Max(1,(places.Length+6)/7); menuPage=Math.Min(menuPage,pages-1);
                Text("CHOOSE A DESTINATION",left,y+.195f,.225f,Muted);
                if(Button(walkToPlace?"WALK":"DRIVE",box.Right-box.Padding-.077f/box.Aspect,y+.190f,.077f/box.Aspect,.029f,mx,my,click)) walkToPlace=!walkToPlace;
                for(int row=0;row<7 && menuPage*7+row<places.Length;row++)
                {
                    var place=places[menuPage*7+row];
                    if(Button(place.Name,left,y+.231f+row*.035f,width,.031f,mx,my,click))
                    { Input=(walkToPlace?"Walk to ":"Drive to ")+place.Name; menuTab=0; }
                }
                DrawPager(box,pages,mx,my,click);
            }
            else if(menuTab==2)
            {
                int pages=(RequestIdeas.All.Length+6)/7; menuPage=Math.Min(menuPage,pages-1);
                for(int row=0;row<7 && menuPage*7+row<RequestIdeas.All.Length;row++)
                {
                    var idea=RequestIdeas.All[menuPage*7+row];
                    if(Button(idea.Title,left,y+.198f+row*.039f,width,.034f,mx,my,click)) { Input=idea.Prompt; menuTab=0; }
                }
                DrawPager(box,pages,mx,my,click);
            }
            else if(menuTab==3)
            {
                Text("WEATHER  /  "+WeatherName.ToUpperInvariant(),left,y+.195f,.225f,Muted);
                string[] labels={"Clear","Sunny","Cloudy","Fog","Overcast","Rain","Thunder","Snow","Automatic"};
                string[] presets={"clear","sunny","clouds","fog","overcast","rain","thunder","snow","auto"};
                float gap=.006f/box.Aspect,cell=(width-gap*2)/3;
                for(int i=0;i<labels.Length;i++)
                    if(Button(labels[i],left+(i%3)*(cell+gap),y+.225f+(i/3)*.037f,cell,.032f,mx,my,click))
                        QueueWorld(CommandPlan.World(presets[i]),"Set weather: "+labels[i]);
                Text("TIME  /  "+WorldTime+(ClockFrozen?"  FROZEN":""),left,y+.344f,.225f,Muted);
                string[] times={"06:00","12:00","19:00","00:00"}; int[] hours={6,12,19,0};
                float timeCell=(width-gap*3)/4;
                for(int i=0;i<times.Length;i++)
                    if(Button(times[i],left+i*(timeCell+gap),y+.371f,timeCell,.033f,mx,my,click))
                        QueueWorld(CommandPlan.World("none",hours[i]),"Set time to "+times[i]);
                if(Button(ClockFrozen?"Resume clock":"Freeze clock",left,y+.419f,width,.034f,mx,my,click))
                    QueueWorld(CommandPlan.World("none",-1,0,ClockFrozen?"resume":"pause"),ClockFrozen?"Resume clock":"Freeze clock");
                Text("REAL WORLD  /  FREE ROAM",left,y+.468f,.225f,Muted);
                float realCell=(width-gap*2)/3;
                string[] realLabels={RealClock?"PC time: ON":"PC time",RealWeather?"Weather: ON":"Real weather","Sync OFF"};
                string[] realActions={RealClock?"realtime_clock_off":"realtime_clock",RealWeather?"realtime_weather_off":"realtime_weather","realtime_off"};
                for(int i=0;i<3;i++) if(Button(realLabels[i],left+i*(realCell+gap),y+.497f,realCell,.032f,mx,my,click))
                    pendingUiPlan=CommandPlan.Create(realActions[i],"current",45,"none",realLabels[i]);
                Text("Set weather location using the request below.",left,y+.543f,.215f,Muted);
                if(Button("Set weather city...",left,y+.572f,width,.034f,mx,my,click))
                {Input="Set weather location to ";menuTab=0;menuScroll=0;}
                Text("Type city + country, then press Enter.",left,y+.618f,.215f,Muted);
            }
            else if(menuTab==5)
            {
                Text("WATCH THE WORLD  /  "+CameraMode.ToUpperInvariant(),left,y+.195f,.225f,Muted);
                string[] labels={"Gameplay camera","First person / cockpit","Third person","Cinema director / dynamic shots","GTA native cinema"};
                string[] actions={"camera_gameplay","camera_first","camera_third","camera_cinema","camera_native"};
                string[] modes={"Gameplay","First person","Third person","Cinema director","GTA cinema"};
                for(int i=0;i<labels.Length;i++)
                    if(Button(labels[i],left,y+.226f+i*.037f,width,.032f,mx,my,click,CameraMode==modes[i]))
                        pendingUiPlan=CommandPlan.Create(actions[i],"current",45,"none",labels[i]);
                float gap=.006f/box.Aspect,half=(width-gap)/2;
                if(Button("Next shot",left,y+.419f,half,.033f,mx,my,click)) pendingUiPlan=CommandPlan.Create("camera_next","current",45,"none","Next cinema shot");
                if(Button(CameraLetterbox?"Letterbox: ON":"Letterbox: OFF",left+half+gap,y+.419f,half,.033f,mx,my,click,CameraLetterbox))
                    pendingUiPlan=CommandPlan.Create("camera_bars","current",45,"none","Toggle cinema letterbox");
                Text(CameraKeyName+": change view. Shift+"+CameraKeyName+": next shot.",left,y+.468f,.225f,Muted);
                Text("F8 restores gameplay. Director watches AI actions.",left,y+.493f,.225f,Muted);
                if(Button(AutoCinemaDriving?"Auto cinema while AI drives: ON":"Auto cinema while AI drives: OFF",left,y+.539f,width,.032f,mx,my,click,AutoCinemaDriving))
                    pendingUiPlan=CommandPlan.Create(AutoCinemaDriving?"camera_auto_off":"camera_auto_on","current",45,"none","Toggle automatic driving cinema");
            }
            else
            {
                Text("ACTIVE TASK DISPLAY",left,y+.202f,.29f,White);
                if(Button(ShowDrivingHud?"Task HUD: ON":"Task HUD: OFF",left,y+.252f,width,.042f,mx,my,click,ShowDrivingHud))
                { pendingUiPlan=CommandPlan.Create("hud_toggle","current",45,"none","Toggle task HUD"); }
                string[] notes={HudToggleKeyName+" toggles this display at any time.","Hiding it keeps your AI action running.","F8 always stops and returns control.","Your choice is saved for future sessions."};
                for(int i=0;i<notes.Length;i++) Text(notes[i],left,y+.326f+i*.035f,.245f,Muted);
            }
            var viewport=menuViewport;menuViewport=null;
            if(viewport!=null)DrawMenuScroll(box,viewport,mx,my,click);
        }
        private void DrawMenuScroll(HudLayout box,HudMenuViewport viewport,float mx,float my,bool click)
        {
            if(viewport.MaximumOffset<=0)return;
            float left=box.Left+box.Padding,buttonWidth=.035f/box.Aspect,gap=.006f/box.Aspect;
            if(Button("^",left,box.MenuScrollTop,buttonWidth,.021f,mx,my,click))menuScroll=Math.Max(0,menuScroll-.074f);
            if(Button("v",box.Right-box.Padding-buttonWidth,box.MenuScrollTop,buttonWidth,.021f,mx,my,click))menuScroll=Math.Min(viewport.MaximumOffset,menuScroll+.074f);
            string hint=viewport.Offset<.0001f?"MORE BELOW / wheel or PgDn":viewport.Offset>=viewport.MaximumOffset-.0001f?"MORE ABOVE / wheel or PgUp":"SCROLL / wheel or PgUp / PgDn";
            Text(hint,left+buttonWidth+gap,box.MenuScrollTop+.002f,.205f,Muted);
            float trackX=box.Right-.008f/box.Aspect,trackHeight=viewport.Bottom-viewport.Top;
            float thumbHeight=trackHeight*trackHeight/(trackHeight+viewport.MaximumOffset);
            Rect(trackX,viewport.Top,.002f/box.Aspect,trackHeight,Color.FromArgb(70,255,255,255));
            Rect(trackX,viewport.Top+(trackHeight-thumbHeight)*viewport.Offset/viewport.MaximumOffset,.002f/box.Aspect,thumbHeight,accent);
        }
        private void QueueWorld(CommandPlan plan,string request) { plan.Message=request; pendingUiPlan=plan; }
        private void DrawPager(HudLayout box,int pages,float mx,float my,bool click)
        {
            float left=box.Left+box.Padding,width=.070f/box.Aspect,y=box.Top+.487f;
            if(Button("<",left,y,width,.030f,mx,my,click)) menuPage=Math.Max(0,menuPage-1);
            Text("PAGE "+(menuPage+1)+" / "+pages+"  ·  PgUp / PgDn",left+width+.01f/box.Aspect,y+.005f,.215f,Muted);
            if(Button(">",box.Right-box.Padding-width,y,width,.030f,mx,my,click)) menuPage=Math.Min(pages-1,menuPage+1);
        }

        private void DrawHistory(HudLayout box)
        {
            if (Math.Abs(cachedWidth - box.TextWidth) > .0001f) { cachedWidth = box.TextWidth; historyDirty = true; cachedInput = null; }
            if (historyDirty)
            {
                rows.Clear();
                foreach (var message in Messages)
                {
                    rows.Add(new HistoryRow { Label = true, User = message.User, Text = message.User ? "YOU" : "FIVEVIBE" });
                    foreach (string line in HudText.Wrap(message.Text, box.TextWidth, s => NativeMeasure(s, .315f)))
                        rows.Add(new HistoryRow { Text = line, User = message.User });
                    rows.Add(new HistoryRow { Gap = true });
                }
                historyDirty = false;
            }
            int capacity = Math.Max(1, (int)((box.HistoryBottom - box.HistoryTop) / .025f));
            int maxScroll = Math.Max(0, rows.Count - capacity);
            scroll = Math.Max(0, Math.Min(scroll, maxScroll));
            int end = rows.Count - scroll, start = end;
            float height = 0;
            while (start > 0 && height + rows[start - 1].Height <= box.HistoryBottom - box.HistoryTop) height += rows[--start].Height;
            float y = box.HistoryBottom - height;
            for (int i = start; i < end; i++)
            {
                var row = rows[i];
                if (row.Label)
                {
                    Rect(box.Left + box.Padding, y + .006f, .003f / box.Aspect, .011f, row.User ? Muted : accent);
                    Text(row.Text, box.Left + box.Padding + .011f / box.Aspect, y, .225f, row.User ? Muted : accent);
                }
                else if (!row.Gap) Text(row.Text, box.Left + box.Padding, y, .315f, White);
                y += row.Height;
            }
            if (maxScroll > 0)
            {
                float trackHeight = box.HistoryBottom - box.HistoryTop;
                float thumbHeight = Math.Max(.028f, trackHeight * (end - start) / Math.Max(1, rows.Count));
                float thumbY = box.HistoryTop + (trackHeight - thumbHeight) * (1f - (float)scroll / maxScroll);
                Rect(box.Right - .006f / box.Aspect, box.HistoryTop, .002f / box.Aspect, trackHeight, Color.FromArgb(80, 255, 255, 255));
                Rect(box.Right - .006f / box.Aspect, thumbY, .002f / box.Aspect, thumbHeight, accent);
            }
        }

        private void DrawInput(float x, float y, float width)
        {
            if (cachedInput != Input || Math.Abs(inputWidth-width)>.0001f)
            {
                inputScroll=0;
                inputLines = HudText.Wrap(Input, width, s => NativeMeasure(s, .315f));
                cachedInput = Input;
                inputWidth=width;
            }
            string[] visible=HudText.InputWindow(inputLines,3,ref inputScroll);
            if (Input.Length == 0) Text("What's the vibe?", x, y, .315f, Muted);
            else
            {
                for (int i=0;i<visible.Length;i++) Text(visible[i],x,y+i*.026f,.315f,White);
            }
            if (inputScroll==0 && (Environment.TickCount & int.MaxValue) % 1000 < 500)
            {
                string last = Input.Length == 0 || inputLines.Length == 0 ? "" : inputLines[inputLines.Length - 1];
                float caretX = x + Math.Min(width, NativeMeasure(last, .315f));
                float caretY = y + Math.Max(0,visible.Length-1)*.026f;
                Rect(caretX, caretY + .004f, .0009f, .020f, accent);
            }
        }

        private void DrawDrivingHud(HudLayout box)
        {
            float width = .355f / box.Aspect, x = box.Right - width, y = .765f;
            Rect(x, y, width, .095f, Color.FromArgb(190, 0, 0, 0));
            Rect(x, y, .003f / box.Aspect, .095f, accent);
            Text("FIVEVIBE", x + box.Padding, y + .009f, .27f, accent, 4);
            Text(HudText.Tail(Thinking ? "Planning your request..." : Status, width - .095f / box.Aspect, s => NativeMeasure(s, .275f)), x + box.Padding, y + .033f, .275f, White);
            Text(HudText.Tail(Route, width - .095f / box.Aspect, s => NativeMeasure(s, .245f)), x + box.Padding, y + .063f, .245f, Muted);
            TextRight(OnFoot ? "ON FOOT" : CurrentSpeedKmh.ToString(), box.Right - box.Padding, y + .011f, OnFoot ? .29f : .65f, White, 4);
            if (!OnFoot) TextRight("KM/H", box.Right - box.Padding, y + .061f, .205f, Muted);
        }

        private void DrawMenuTexture(float x, float y, float width, float height, Color color)
        {
            if (!Function.Call<bool>(Hash.HAS_STREAMED_TEXTURE_DICT_LOADED, "commonmenu"))
            {
                if (lastTextureRequest == 0 || Game.GameTime - lastTextureRequest > 2000)
                { Function.Call(Hash.REQUEST_STREAMED_TEXTURE_DICT, "commonmenu", false); lastTextureRequest = Game.GameTime; }
                Rect(x, y, width, height, Color.FromArgb(230, color));
                return;
            }
            Function.Call(Hash.DRAW_SPRITE, "commonmenu", "interaction_bgd", x + width / 2, y + height / 2,
                width, height, 0f, color.R, color.G, color.B, 235, false, 0);
        }

        private void DrawInstructions(HudLayout box,string openKey, string takeoverKey)
        {
            string[] keys=IsOpen?new[]{"ENTER","TAB","ESC",takeoverKey}:new[]{openKey,takeoverKey};
            string[] labels=IsOpen?new[]{"Send","Quick",menuTab==0?"Close":"Back","Take over"}:new[]{"Open chat","Take over"};
            string state=string.Join("|",keys)+"/"+string.Join("|",labels)+"/"+box.Aspect+"/"+box.TextWidth;
            if(hintState!=state) {hintRow=HudHintRow.Fit(keys,labels,box.TextWidth,box.Aspect,NativeMeasure);hintState=state;}
            var row=hintRow;
            float left=box.Left+box.Padding,y=IsOpen?box.HintsTop:box.SafeBottom-.034f;
            if(!IsOpen)Rect(left-.006f/box.Aspect,y-.003f,row.Width+.012f/box.Aspect,.031f,Color.FromArgb(190,0,0,0));
            for(int i=0;i<keys.Length;i++)
            {
                float x=left+row.Starts[i];
                Rect(x,y,row.KeyWidths[i],.025f,White);
                Text(keys[i],x+.005f/box.Aspect,y+.002f,row.Scale,Color.Black);
                Text(labels[i],x+row.KeyWidths[i]+.005f/box.Aspect,y+.002f,row.Scale,White);
            }
        }
        public void Dispose()
        {
            disposed = true;
            if (lastTextureRequest != 0) Function.Call(Hash.SET_STREAMED_TEXTURE_DICT_AS_NO_LONGER_NEEDED, "commonmenu");
        }
        private void PlaySound(string sound)
        { if (InterfaceSounds) Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, sound, "HUD_FRONTEND_DEFAULT_SOUNDSET", false); }
        private static bool Hit(float mx, float my, float x, float y, float w, float h)
        { return mx >= x && mx <= x + w && my >= y && my <= y + h; }
        private void Append(string text)
        {
            Input += Clean(text).Replace('\n', ' ');
            if (Input.Length > 420) Input = Input.Substring(0, char.IsHighSurrogate(Input[419]) ? 419 : 420);
        }
        public static string Clean(string text)
        { return new string((text ?? "").Where(c => !char.IsControl(c) || c == '\n').ToArray()).Replace('~', '-'); }
        private static void Rect(float x, float y, float w, float h, Color color)
        { Function.Call(Hash.DRAW_RECT, x + w / 2, y + h / 2, w, h, color.R, color.G, color.B, color.A, false); }
        private static void SetFont(float scale, int font)
        {
            Function.Call(Hash.SET_TEXT_FONT, font);
            Function.Call(Hash.SET_TEXT_SCALE, 0f, scale);
        }
        private static float NativeMeasure(string text, float scale)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            SetFont(scale, 0);
            Function.Call(Hash.SET_TEXT_JUSTIFICATION, 1);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, 1f);
            HudText.PushNative(text,label=>Function.Call(Hash.BEGIN_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT,label),
                chunk=>Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,chunk));
            return Function.Call<float>(Hash.END_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT, true);
        }
        private void Text(string text, float x, float y, float scale, Color color, int font = 0)
        {
            if(menuViewport!=null && !menuViewport.Contains(y,.025f))return;
            if(menuViewport!=null)text=HudText.Tail(text,menuTextRight-x,s=>NativeMeasure(s,scale));
            SetFont(scale, font);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_JUSTIFICATION, 1);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, 1f);
            HudText.PushNative(text,label=>Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT,label),
                chunk=>Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,chunk));
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y, 0);
        }
        private static void TextRight(string text, float right, float y, float scale, Color color, int font = 0)
        {
            SetFont(scale, font);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_JUSTIFICATION, 2);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, right);
            HudText.PushNative(text,label=>Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT,label),
                chunk=>Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME,chunk));
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, right, y, 0);
        }
        [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] state);
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint thread);
        [DllImport("user32.dll")] private static extern uint MapVirtualKeyEx(uint key, uint type, IntPtr layout);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ToUnicodeEx(uint key, uint scan, byte[] state, StringBuilder text, int capacity, uint flags, IntPtr layout);
    }
}
