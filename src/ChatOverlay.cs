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
        private Scaleform instructions;
        private string instructionState;
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
            if (e.KeyCode == Keys.Escape) { IsOpen = false; return null; }
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
            lock (pasteQueue) { foreach (string text in pasteQueue) if (IsOpen) Append(text); pasteQueue.Clear(); }
            if (wasOpen != IsOpen)
            {
                PlaySound(IsOpen ? "SELECT" : "BACK");
                wasOpen = IsOpen;
                instructionState = null;
            }
            if (cachedColor != HudColorId)
            {
                accent = HudColors.GetColor((HudColor)HudColorId);
                cachedColor = HudColorId;
            }
            if (notification != null)
            {
                if (!IsOpen) Notification.PostTicker("~h~Copilota~h~\n" + notification, false, true);
                notification = null;
            }
            var box = new HudLayout(Function.Call<float>(Hash.GET_ASPECT_RATIO, false), Function.Call<float>(Hash.GET_SAFE_ZONE_SIZE));
            if (!IsOpen)
            {
                if (Autonomous || Thinking)
                {
                    if (ShowDrivingHud) DrawDrivingHud(box);
                    DrawInstructions(openKey, takeoverKey);
                }
                else ReleaseInstructions();
                return null;
            }

            Hud.ShowCursorThisFrame();
            Function.Call(Hash.HIDE_HUD_COMPONENT_THIS_FRAME, 14);
            float mouseX = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, 239);
            float mouseY = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, 240);
            bool click = Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, 237);
            if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, 241)) scroll += 3;
            if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, 242)) scroll = Math.Max(0, scroll - 3);

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

            DrawHistory(box);
            Text(scroll > 0 ? "HISTORY  /  PgDn: latest messages" : "QUICK REQUESTS  /  TAB", x + p, y + .523f, .215f, Muted);
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
            DrawInput(x + p + .010f / box.Aspect, box.InputTop + .009f, inputWidth);

            Text(Provider == "OPENAI" ? "AI COMPANION" : Provider == "OPENAI_PENDING" ? "SET UP AI" : "LOCAL COMMANDS", x + p, y + .698f, .225f, Muted);
            TextRight(Input.Length + "/420", box.Right - p, y + .698f, .225f, Muted);
            Text("Write a request. Set the vibe.", x + p, y + .725f, .225f, Muted);
            DrawInstructions(openKey, takeoverKey);
            return click && sendHover ? SubmitInput() : null;
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
            if (cachedInput != Input)
            {
                inputLines = HudText.Wrap(Input, width, s => NativeMeasure(s, .315f));
                cachedInput = Input;
            }
            if (Input.Length == 0) Text("What's the vibe?", x, y, .315f, Muted);
            else
            {
                int start = Math.Max(0, inputLines.Length - 2);
                for (int i = start; i < inputLines.Length; i++) Text(inputLines[i], x, y + (i - start) * .026f, .315f, White);
            }
            if ((Environment.TickCount & int.MaxValue) % 1000 < 500)
            {
                string last = Input.Length == 0 || inputLines.Length == 0 ? "" : inputLines[inputLines.Length - 1];
                float caretX = x + Math.Min(width, NativeMeasure(last, .315f));
                float caretY = y + (Input.Length > 0 && inputLines.Length > 1 ? .026f : 0);
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

        private void DrawInstructions(string openKey, string takeoverKey)
        {
            if (instructions == null) instructions = Scaleform.RequestMovie("INSTRUCTIONAL_BUTTONS");
            if (instructions == null || !instructions.IsLoaded) return;
            string state = IsOpen ? "open" : "driving";
            if (instructionState != state)
            {
                instructions.CallFunction("CLEAR_ALL");
                instructions.CallFunction("TOGGLE_MOUSE_BUTTONS", false);
                instructions.CallFunction("SET_MAX_WIDTH", .65f);
                instructions.CallFunction("SET_BACKGROUND_COLOUR", 0, 0, 0, 165);
                instructions.CallFunction("SET_DATA_SLOT", 0, "t_" + takeoverKey, "Take over");
                if (IsOpen)
                {
                    instructions.CallFunction("SET_DATA_SLOT", 1, "t_ESC", "Close");
                    instructions.CallFunction("SET_DATA_SLOT", 2, "t_TAB", "Quick requests");
                    instructions.CallFunction("SET_DATA_SLOT", 3, "t_ENTER", "Send");
                }
                else instructions.CallFunction("SET_DATA_SLOT", 1, "t_" + openKey, "Open chat");
                instructions.CallFunction("DRAW_INSTRUCTIONAL_BUTTONS", 0);
                instructionState = state;
            }
            instructions.Render2D();
        }
        private void ReleaseInstructions()
        {
            if (instructions != null) { instructions.Dispose(); instructions = null; }
            instructionState = null;
        }
        public void Dispose()
        {
            disposed = true;
            ReleaseInstructions();
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
        private static void Components(string text)
        {
            for (int offset = 0; offset < text.Length; )
            {
                int length = Math.Min(32, text.Length - offset);
                if (offset + length < text.Length && char.IsHighSurrogate(text[offset + length - 1])) length--;
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text.Substring(offset, length));
                offset += length;
            }
        }
        private static float NativeMeasure(string text, float scale)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            SetFont(scale, 0);
            Function.Call(Hash.SET_TEXT_JUSTIFICATION, 1);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, 1f);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT, "STRING");
            Components(text);
            return Function.Call<float>(Hash.END_TEXT_COMMAND_GET_SCREEN_WIDTH_OF_DISPLAY_TEXT, true);
        }
        private static void Text(string text, float x, float y, float scale, Color color, int font = 0)
        {
            SetFont(scale, font);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_JUSTIFICATION, 1);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, 1f);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Components(text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y, 0);
        }
        private static void TextRight(string text, float right, float y, float scale, Color color, int font = 0)
        {
            SetFont(scale, font);
            Function.Call(Hash.SET_TEXT_COLOUR, color.R, color.G, color.B, color.A);
            Function.Call(Hash.SET_TEXT_JUSTIFICATION, 2);
            Function.Call(Hash.SET_TEXT_WRAP, 0f, right);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Components(text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, right, y, 0);
        }
        [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] state);
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint thread);
        [DllImport("user32.dll")] private static extern uint MapVirtualKeyEx(uint key, uint type, IntPtr layout);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ToUnicodeEx(uint key, uint scan, byte[] state, StringBuilder text, int capacity, uint flags, IntPtr layout);
    }
}
