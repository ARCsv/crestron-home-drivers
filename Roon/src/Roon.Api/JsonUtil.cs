using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Roon.Api
{
    internal static class JsonUtil
    {
        public static bool TryGetString(string json, string name, out string value)
        {
            var match = Regex.Match(
                json,
                "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                value = string.Empty;
                return false;
            }

            value = Unescape(match.Groups[1].Value);
            return true;
        }

        public static string GetString(string json, string name)
        {
            return TryGetString(json, name, out var value) ? value : string.Empty;
        }

        public static int GetInt(string json, string name)
        {
            var match = Regex.Match(
                json,
                "\"" + Regex.Escape(name) + "\"\\s*:\\s*(-?\\d+)",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return 0;
            }

            int n;
            return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)
                ? n
                : 0;
        }

        public static string ExtractObject(string json, string name)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var key = "\"" + name + "\":";
            var i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0)
            {
                return string.Empty;
            }

            var start = json.IndexOf('{', i + key.Length);
            if (start < 0)
            {
                return string.Empty;
            }

            var depth = 0;
            for (var p = start; p < json.Length; p++)
            {
                var c = json[p];
                if (c == '"')
                {
                    p++;
                    while (p < json.Length && json[p] != '"')
                    {
                        if (json[p] == '\\')
                        {
                            p++;
                        }

                        p++;
                    }

                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(start, p - start + 1);
                    }
                }
            }

            return string.Empty;
        }

        public static string Quote(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string Unescape(string value)
        {
            return value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\/", "/");
        }
    }
}
