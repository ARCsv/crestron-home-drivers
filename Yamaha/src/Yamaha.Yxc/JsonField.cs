using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Yamaha.Yxc
{
    /// <summary>
    /// Tiny JSON field reader for YXC payloads. Avoids extra dependencies so the
    /// same library can compile into a Crestron net472 driver without ILRepack.
    /// </summary>
    internal static class JsonField
    {
        public static int RequireCode(string json)
        {
            if (!TryGetInt(json, "response_code", out var code))
            {
                throw new YxcException("YXC response did not include response_code.");
            }

            if (code != 0)
            {
                throw new YxcException("YXC request failed with response_code " + code + ".")
                {
                    ResponseCode = code
                };
            }

            return code;
        }

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

        public static bool TryGetInt(string json, string name, out int value)
        {
            var match = Regex.Match(
                json,
                "\"" + Regex.Escape(name) + "\"\\s*:\\s*(-?\\d+)",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                value = 0;
                return false;
            }

            return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryGetBool(string json, string name, out bool value)
        {
            var match = Regex.Match(
                json,
                "\"" + Regex.Escape(name) + "\"\\s*:\\s*(true|false)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                value = false;
                return false;
            }

            value = match.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        public static IReadOnlyList<string> ParseInputIds(string featuresJson)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Zone input_list is a string array and is the list Crestron should use.
            var zoneInputs = Regex.Match(
                featuresJson,
                "\"zone\"\\s*:\\s*\\[\\s*\\{[\\s\\S]*?\"input_list\"\\s*:\\s*\\[([^\\]]+)\\]",
                RegexOptions.CultureInvariant);
            if (zoneInputs.Success)
            {
                foreach (Match token in Regex.Matches(
                             zoneInputs.Groups[1].Value,
                             "\"([a-z0-9_]+)\"",
                             RegexOptions.CultureInvariant))
                {
                    AddInputId(ids, seen, token.Groups[1].Value);
                }

                if (ids.Count > 0)
                {
                    return ids;
                }
            }

            foreach (Match token in Regex.Matches(
                         featuresJson,
                         "\"input_list\"\\s*:\\s*\\[[\\s\\S]*?\\]",
                         RegexOptions.CultureInvariant))
            {
                foreach (Match idMatch in Regex.Matches(
                             token.Value,
                             "\"id\"\\s*:\\s*\"([a-z0-9_]+)\"",
                             RegexOptions.CultureInvariant))
                {
                    AddInputId(ids, seen, idMatch.Groups[1].Value);
                }
            }

            return ids;
        }

        private static void AddInputId(List<string> ids, HashSet<string> seen, string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "id" || id == "main")
            {
                return;
            }

            if (seen.Add(id))
            {
                ids.Add(id);
            }
        }

        public static bool TryGetVolumeRange(string featuresJson, out int min, out int max, out int step)
        {
            min = 0;
            max = 154;
            step = 1;

            // Zone volume on the R-N2000A is range_step { id: volume, min, max, step }.
            var idFirst = Regex.Match(
                featuresJson,
                "\"id\"\\s*:\\s*\"volume\"\\s*,\\s*\"min\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"max\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"step\"\\s*:\\s*(-?\\d+)",
                RegexOptions.CultureInvariant);
            if (idFirst.Success)
            {
                min = int.Parse(idFirst.Groups[1].Value, CultureInfo.InvariantCulture);
                max = int.Parse(idFirst.Groups[2].Value, CultureInfo.InvariantCulture);
                step = int.Parse(idFirst.Groups[3].Value, CultureInfo.InvariantCulture);
                if (step < 1)
                {
                    step = 1;
                }

                return true;
            }

            var volumeBlock = Regex.Match(
                featuresJson,
                "\"volume\"\\s*:\\s*\\{([^}]*)\\}",
                RegexOptions.CultureInvariant);
            if (volumeBlock.Success)
            {
                return ReadRange(volumeBlock.Groups[1].Value, out min, out max, out step);
            }

            var range = Regex.Match(
                featuresJson,
                "\"range_step\"\\s*:\\s*\\[\\s*\\{([^}]*\"id\"\\s*:\\s*\"volume\"[^}]*)\\}",
                RegexOptions.CultureInvariant);
            if (!range.Success)
            {
                return false;
            }

            return ReadRange(range.Groups[1].Value, out min, out max, out step);
        }

        private static bool ReadRange(string block, out int min, out int max, out int step)
        {
            var okMin = TryGetInt(block, "min", out min) || TryGetInt(block, "minimum", out min);
            var okMax = TryGetInt(block, "max", out max) || TryGetInt(block, "maximum", out max);
            if (!TryGetInt(block, "step", out step))
            {
                step = 1;
            }

            return okMin && okMax;
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
