using System.Collections.Generic;

namespace Yamaha.Yxc
{
    public sealed class DeviceInfo
    {
        public string ModelName { get; init; } = string.Empty;
        public string DeviceId { get; init; } = string.Empty;
        public string SystemId { get; init; } = string.Empty;
        public string ApiVersion { get; init; } = string.Empty;
        public string RawJson { get; init; } = string.Empty;
    }

    public sealed class ZoneStatus
    {
        public bool PowerOn { get; init; }
        public int Volume { get; init; }
        public int MaxVolume { get; init; }
        public bool Mute { get; init; }
        public string Input { get; init; } = string.Empty;
        public string InputText { get; init; } = string.Empty;
        public string SoundProgram { get; init; } = string.Empty;
        public string RawJson { get; init; } = string.Empty;
    }

    public sealed class DeviceFeatures
    {
        public IReadOnlyList<string> InputIds { get; init; } = new string[0];
        public int VolumeMin { get; init; }
        public int VolumeMax { get; init; } = 100;
        public int VolumeStep { get; init; } = 1;
        public string RawJson { get; init; } = string.Empty;
    }

    public sealed class PlayInfo
    {
        public string Playback { get; init; } = string.Empty;
        public string Input { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public string Album { get; init; } = string.Empty;
        public string Track { get; init; } = string.Empty;
        public string RawJson { get; init; } = string.Empty;
    }

    public static class YxcPower
    {
        public const string On = "on";
        public const string Standby = "standby";
        public const string Toggle = "toggle";
    }

    public static class YxcPlayback
    {
        public const string Play = "play";
        public const string Stop = "stop";
        public const string Pause = "pause";
        public const string Next = "next";
        public const string Previous = "previous";
    }
}
