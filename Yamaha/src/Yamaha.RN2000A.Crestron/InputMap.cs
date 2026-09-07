using System;
using Crestron.RAD.Common.Enums;

namespace Yamaha.RN2000A.Crestron
{
    internal static class InputMap
    {
        public static string FromVideo(VideoConnections input)
        {
            switch ((int)input)
            {
                case 1800:
                    return "optical1";
                case 1801:
                    return "optical2";
                case 1802:
                    return "coaxial";
                case 1803:
                    return "line1";
                case 1804:
                    return "line2";
                case 1805:
                    return "line_cd";
                case 1806:
                    return "phono";
                case 1807:
                    return "usb_dac";
                case 1200:
                case 1201:
                    return "usb_dac";
                case 1808:
                    return "tv";
                case 1809:
                    return "tuner";
                case 1600:
                    return "server";
                case 30220:
                    return "bluetooth";
                case 30200:
                    return "spotify";
                case 30201:
                    return "tidal";
                case 30202:
                    return "airplay";
                case 30203:
                    return "net_radio";
                case 30204:
                    return "deezer";
            }

            switch (input.ToString().ToLowerInvariant())
            {
                case "hdmi":
                case "hdmi1":
                case "tv":
                    return "tv";
                case "optical":
                case "optical1":
                    return "optical1";
                case "optical2":
                    return "optical2";
                case "coaxial":
                case "digital":
                    return "coaxial";
                case "phono":
                    return "phono";
                case "cd":
                    return "line_cd";
                case "tuner":
                case "antenna":
                    return "tuner";
                case "usb":
                case "usb1":
                case "usb2":
                case "usbdac":
                case "usb_dac":
                case "mediaplayer":
                case "mediaplayer1":
                case "musicplayer":
                    return "usb_dac";
                case "bluetooth":
                    return "bluetooth";
                case "network":
                case "lan":
                    return "server";
                case "aux":
                case "analog":
                case "analog1":
                case "line":
                case "line1":
                    return "line1";
                case "line2":
                case "analog2":
                    return "line2";
                default:
                    var name = input.ToString().ToLowerInvariant();
                    if (name.IndexOf("usb", StringComparison.Ordinal) >= 0
                        || name.IndexOf("roon", StringComparison.Ordinal) >= 0
                        || name.IndexOf("rune", StringComparison.Ordinal) >= 0
                        || name.IndexOf("dac", StringComparison.Ordinal) >= 0)
                    {
                        return "usb_dac";
                    }

                    return name;
            }
        }

        public static string FromAudio(AudioConnections input)
        {
            return FromVideo((VideoConnections)(int)input);
        }
    }
}
