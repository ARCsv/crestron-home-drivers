using System;
using System.Collections.Generic;

namespace Yamaha.Yxc
{
    /// <summary>
    /// R-N2000A-oriented labels. Live IDs always come from getFeatures;
    /// this table is only for friendly names and Crestron Home mapping.
    /// </summary>
    public static class InputCatalog
    {
        private static readonly Dictionary<string, string> Labels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "phono", "Phono" },
                { "line_cd", "CD" },
                { "tuner", "Tuner" },
                { "line1", "Line 1" },
                { "line2", "Line 2" },
                { "optical1", "Optical 1" },
                { "optical2", "Optical 2" },
                { "coaxial", "Coaxial" },
                { "tv", "TV" },
                { "usb_dac", "USB DAC" },
                { "bluetooth", "Bluetooth" },
                { "airplay", "AirPlay" },
                { "spotify", "Spotify" },
                { "tidal", "TIDAL" },
                { "qobuz", "Qobuz" },
                { "deezer", "Deezer" },
                { "napster", "Napster" },
                { "pandora", "Pandora" },
                { "siriusxm", "SiriusXM" },
                { "amazon_music", "Amazon Music" },
                { "server", "Server" },
                { "net_radio", "Net Radio" },
                { "mc_link", "MusicCast Link" }
            };

        public static readonly string[] FallbackIds =
        {
            "napster", "siriusxm", "pandora", "spotify", "qobuz", "tidal", "deezer",
            "amazon_music", "airplay", "mc_link", "server", "net_radio", "bluetooth",
            "tuner", "tv", "optical1", "optical2", "coaxial", "line1", "line2",
            "line_cd", "phono", "usb_dac"
        };

        public static string Label(string inputId)
        {
            if (string.IsNullOrWhiteSpace(inputId))
            {
                return "Unknown";
            }

            if (Labels.TryGetValue(inputId, out var label))
            {
                return label;
            }

            return inputId.Replace('_', ' ');
        }
    }
}
