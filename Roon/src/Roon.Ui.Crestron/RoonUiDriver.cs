using System;
using Crestron.RAD.Common.Enums;
using Crestron.RAD.Common.Interfaces;
using Crestron.RAD.Common.Interfaces.ExtensionDevice;
using Crestron.RAD.DeviceTypes.ExtensionDevice;
using Crestron.SimplSharp;
using Roon.Api;

namespace Roon.Crestron
{
    public sealed class RoonUiDriver : AExtensionDevice, ITcp
    {
        private readonly PropertyValue<string> _title;
        private readonly PropertyValue<string> _artist;
        private readonly PropertyValue<string> _album;
        private readonly PropertyValue<string> _elapsed;
        private readonly PropertyValue<string> _remaining;
        private readonly PropertyValue<int> _progress;
        private readonly PropertyValue<string> _status;
        private readonly PropertyValue<string> _icon;
        private readonly PropertyValue<string> _state;

        private RoonWsTransport _transport;
        private CTimer _tick;
        private RoonNowPlaying _playing;

        public RoonUiDriver()
        {
            _title = CreateProperty<string>(new PropertyDefinition("title", null, DevicePropertyType.String));
            _artist = CreateProperty<string>(new PropertyDefinition("artist", null, DevicePropertyType.String));
            _album = CreateProperty<string>(new PropertyDefinition("album", null, DevicePropertyType.String));
            _elapsed = CreateProperty<string>(new PropertyDefinition("elapsed", null, DevicePropertyType.String));
            _remaining = CreateProperty<string>(new PropertyDefinition("remaining", null, DevicePropertyType.String));
            _progress = CreateProperty<int>(new PropertyDefinition("progress", null, DevicePropertyType.Int32, 0, 100, 1));
            _status = CreateProperty<string>(new PropertyDefinition("status", null, DevicePropertyType.String));
            _icon = CreateProperty<string>(new PropertyDefinition("icon", null, DevicePropertyType.String));
            _state = CreateProperty<string>(new PropertyDefinition("state", null, DevicePropertyType.String));
            _icon.Value = "icMusic";
            _status.Value = "Roon";
            _title.Value = "Nothing playing";
            _artist.Value = string.Empty;
            _album.Value = string.Empty;
            _elapsed.Value = "0:00";
            _remaining.Value = "0:00";
            _state.Value = string.Empty;
        }

        int ITcp.Port
        {
            get { return RoonSession.DefaultPort; }
        }

        public void Initialize(IPAddress ipAddress, int port)
        {
            InitializeHost(ipAddress.ToString(), port);
        }

        public void Initialize(string address, int port)
        {
            InitializeHost(address, port);
        }

        public override void Connect()
        {
            if (_transport != null)
            {
                _transport.Start();
            }

            Connected = true;
            StartTick();
            Commit();
        }

        public override void Disconnect()
        {
            StopTick();
            if (_transport != null)
            {
                _transport.Stop();
            }

            Connected = false;
            Commit();
        }

        protected override IOperationResult DoCommand(string command, string[] parameters)
        {
            var session = _transport != null ? _transport.Session : null;
            if (session == null)
            {
                return new OperationResult(OperationResultCode.Error, "Roon is not connected.");
            }

            switch (command)
            {
                case "playpause":
                    session.PlayPause();
                    break;
                case "play":
                    session.Play();
                    break;
                case "pause":
                    session.Pause();
                    break;
                case "stop":
                    session.Stop();
                    break;
                case "next":
                    session.Next();
                    break;
                case "previous":
                    session.Previous();
                    break;
                default:
                    return new OperationResult(OperationResultCode.Error, "Unknown command");
            }

            return new OperationResult(OperationResultCode.Success);
        }

        protected override IOperationResult SetDriverPropertyValue<T>(string propertyKey, T value)
        {
            return new OperationResult(OperationResultCode.Success);
        }

        protected override IOperationResult SetDriverPropertyValue<T>(string objectId, string propertyKey, T value)
        {
            return new OperationResult(OperationResultCode.Success);
        }

        private void InitializeHost(string host, int port)
        {
            _transport = new RoonWsTransport();
            _transport.ConfigureExtension(
                new RoonExtensionInfo
                {
                    Id = "com.cadenceworks.crestron.roon.ui",
                    DisplayName = "Cadence Works Crestron Roon iPad",
                    DisplayVersion = "1.0000.0000"
                },
                "/user/Roon.Ui.Crestron.token");
            _transport.ZoneName = RoonSession.DefaultZoneName;
            _transport.Initialize(host, port);
            _transport.NowPlayingChanged += OnNowPlaying;
            ConnectionTransport = _transport;

            var protocol = new RoonUiProtocol(_transport, Id);
            DeviceProtocol = protocol;
            protocol.Initialize(DriverData);
        }

        private void OnNowPlaying(RoonNowPlaying playing)
        {
            _playing = playing;
            ApplyPlaying(playing);
        }

        private void ApplyPlaying(RoonNowPlaying playing)
        {
            if (playing == null)
            {
                return;
            }

            _title.Value = string.IsNullOrEmpty(playing.Title) ? "Nothing playing" : playing.Title;
            _artist.Value = playing.Artist ?? string.Empty;
            _album.Value = playing.Album ?? string.Empty;
            _state.Value = playing.State ?? string.Empty;
            _status.Value = _title.Value;
            _elapsed.Value = FormatTime(playing.SeekSeconds);
            var remain = playing.LengthSeconds > playing.SeekSeconds
                ? playing.LengthSeconds - playing.SeekSeconds
                : 0;
            _remaining.Value = FormatTime(remain);
            _progress.Value = playing.LengthSeconds > 0
                ? Math.Min(100, (playing.SeekSeconds * 100) / playing.LengthSeconds)
                : 0;
            _icon.Value = "icMusic";
            Commit();
        }

        private void StartTick()
        {
            StopTick();
            _tick = new CTimer(_ => TickSeek(), null, 1000, 1000);
        }

        private void StopTick()
        {
            if (_tick == null)
            {
                return;
            }

            _tick.Stop();
            _tick.Dispose();
            _tick = null;
        }

        private void TickSeek()
        {
            var playing = _playing;
            if (playing == null || !string.Equals(playing.State, "playing", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (playing.LengthSeconds > 0 && playing.SeekSeconds < playing.LengthSeconds)
            {
                playing.SeekSeconds++;
            }

            ApplyPlaying(playing);
        }

        private static string FormatTime(int seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            var m = seconds / 60;
            var s = seconds % 60;
            return m.ToString() + ":" + (s < 10 ? "0" : "") + s.ToString();
        }
    }
}
