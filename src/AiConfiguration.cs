using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FiveVibe
{
    public sealed class AiConfiguration
    {
        public string ApiKey { get; private set; }
        public string Model { get; private set; }
        public bool HasApiKey { get { return !string.IsNullOrWhiteSpace(ApiKey); } }

        public static AiConfiguration Load(string envPath, string defaultModel, string legacyKey = null, Func<string> environmentKey = null)
        {
            var values = File.Exists(envPath) ? Parse(File.ReadAllText(envPath)) : null;
            string key, model;
            if (values != null)
            {
                // An explicit file is authoritative. An empty key disables OpenAI;
                // do not silently reuse credentials from a different environment.
                key = values.ContainsKey("OPENAI_API_KEY") ? values["OPENAI_API_KEY"] : "";
                model = values.ContainsKey("OPENAI_MODEL") ? values["OPENAI_MODEL"] : defaultModel;
            }
            else
            {
                key = !string.IsNullOrWhiteSpace(legacyKey) ? legacyKey : environmentKey == null ? "" : environmentKey();
                model = defaultModel;
            }
            key = (key ?? "").Trim();
            model = (model ?? "").Trim();
            if (key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
                throw new FormatException("OPENAI_API_KEY contains invalid whitespace or control characters. Check .env.");
            if (!Regex.IsMatch(model, @"^[A-Za-z0-9][A-Za-z0-9._:\-]{0,119}$"))
                throw new FormatException("Invalid OPENAI_MODEL. Check the model name in .env.");
            return new AiConfiguration { ApiKey = key, Model = model };
        }

        public static Dictionary<string, string> Parse(string contents)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            int lineNumber = 0;
            foreach (string original in contents.Replace("\r", "").Split('\n'))
            {
                lineNumber++;
                string line = original.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("export ", StringComparison.Ordinal)) line = line.Substring(7).TrimStart();
                int equals = line.IndexOf('=');
                if (equals < 1) throw Error(lineNumber);
                string name = line.Substring(0, equals).Trim();
                if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$")) throw Error(lineNumber);
                // Other application settings may share the file; never evaluate or export them.
                if (name != "OPENAI_API_KEY" && name != "OPENAI_MODEL") continue;
                string value = line.Substring(equals + 1).Trim();
                if (value.Length > 0 && (value[0] == '\'' || value[0] == '"'))
                {
                    char quote = value[0];
                    int close = value.IndexOf(quote, 1);
                    if (close < 0) throw Error(lineNumber);
                    string trailing = value.Substring(close + 1).Trim();
                    if (trailing.Length > 0 && !trailing.StartsWith("#")) throw Error(lineNumber);
                    value = value.Substring(1, close - 1);
                }
                else
                {
                    int comment = value.IndexOf(" #", StringComparison.Ordinal);
                    if (comment >= 0) value = value.Substring(0, comment).TrimEnd();
                }
                values[name] = value;
            }
            return values;
        }

        private static FormatException Error(int line)
        {
            // Never include the source line or its value: it may contain a real key.
            return new FormatException("Invalid .env format on line " + line + ". Use NAME=value on one line.");
        }
    }
}
