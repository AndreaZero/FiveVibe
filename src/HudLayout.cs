using System;
using System.Collections.Generic;
using System.Globalization;

namespace FiveVibe
{
    // Screen-height proportions keep the panel compact on ultrawide displays too.
    public sealed class HudLayout
    {
        public readonly float Aspect, Left, Right, Top, Width, Height, Padding;
        public float HistoryTop { get { return Top + .153f; } }
        public float HistoryBottom { get { return Top + .520f; } }
        public float QuickTop { get { return Top + .546f; } }
        public float InputTop { get { return Top + .596f; } }
        public float TextWidth { get { return Width - Padding * 2 - .008f / Aspect; } }
        public HudLayout(float aspect, float safeZone)
        {
            Aspect = Math.Max(1.25f, Math.Min(4f, aspect));
            safeZone = Math.Max(.8f, Math.Min(1f, safeZone));
            float inset = (1f - safeZone) * .5f;
            Right = 1f - inset - .015f / Aspect;
            Width = .530f / Aspect;
            Left = Right - Width;
            Top = inset + .027f;
            Height = .758f;
            Padding = .018f / Aspect;
        }
    }

    public static class HudText
    {
        // The game supplies real font measurements. Tests can run without GTA.
        public static string[] Wrap(string text, float width, Func<string, float> measure)
        {
            var result = new List<string>();
            foreach (string paragraph in (text ?? "").Replace("\r", "").Split('\n'))
            {
                string line = "";
                foreach (string original in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = line.Length == 0 ? original : line + " " + original;
                    if (measure(candidate) <= width) { line = candidate; continue; }
                    if (line.Length > 0) { result.Add(line); line = ""; }
                    var elements = StringInfo.GetTextElementEnumerator(original);
                    while (elements.MoveNext())
                    {
                        string element = elements.GetTextElement();
                        if (line.Length > 0 && measure(line + element) > width) { result.Add(line); line = ""; }
                        line += element;
                    }
                }
                if (line.Length > 0) result.Add(line);
                else if (paragraph.Length == 0) result.Add("");
            }
            return result.ToArray();
        }
        public static string Tail(string text, float width, Func<string, float> measure)
        {
            if (measure(text) <= width) return text;
            var positions = StringInfo.ParseCombiningCharacters(text);
            for (int i = 1; i < positions.Length; i++)
            {
                string tail = "..." + text.Substring(positions[i]);
                if (measure(tail) <= width) return tail;
            }
            return "...";
        }
    }
}
