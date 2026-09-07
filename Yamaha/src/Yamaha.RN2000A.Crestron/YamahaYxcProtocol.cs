using System;
using System.Threading.Tasks;
using Crestron.RAD.Common.BasicDriver;
using Crestron.RAD.Common.Enums;
using Crestron.RAD.Common.Transports;
using Crestron.RAD.DeviceTypes.RADAVReceiver;
using Yamaha.Yxc;

namespace Yamaha.RN2000A.Crestron
{
    /// <summary>
    /// Maps Crestron Home receiver commands onto YXC JSON GETs.
    /// Polling uses /main/getStatus rather than YNCA feedback tokens.
    /// </summary>
    public sealed class YamahaYxcProtocol : AAVReceiverProtocol
    {
        private readonly YxcHttpTransport _transport;
        private DeviceFeatures _features;
        private bool _muted;
        private int _lastDeviceVolume = 114;
        private DateTime _powerOnUtc = DateTime.MinValue;
        private bool _applyingVolumeFeedback;
        private const int TapStep = 2;

        public YamahaYxcProtocol(ATransportDriver transport, byte id)
            : base(transport, id)
        {
            _transport = (YxcHttpTransport)transport;
        }

        private YxcClient Client
        {
            get { return _transport.Client; }
        }

        public override void PowerOn()
        {
            _powerOnUtc = DateTime.UtcNow;
            Run(RestorePowerAndVolumeAsync);
        }

        public override void PowerOff()
        {
            Run(() => Client.SetPowerAsync(YxcPower.Standby));
        }

        public override void MuteOn()
        {
            // Crestron Home remotes often send MuteOn on every press and never MuteOff
            // unless mute feedback is believed. Treat MuteOn as a toggle.
            SetMuted(!_muted);
        }

        public override void MuteOff()
        {
            SetMuted(false);
        }

        public override void Mute()
        {
            SetMuted(!_muted);
        }

        public override void SetInput(VideoConnections input)
        {
            SelectInput(InputMap.FromVideo(input));
        }

        public override void SetVideoInput(VideoConnections input)
        {
            SelectInput(InputMap.FromVideo(input));
        }

        public override void SetAudioInput(AudioConnections input)
        {
            SelectInput(InputMap.FromAudio(input));
        }

        public override void SetVolume(uint volume)
        {
            if (_applyingVolumeFeedback)
            {
                return;
            }

            EnsureFeatures();
            var deviceVolume = VolumeScaler.FromPercent(
                volume,
                VolumeMin,
                VolumeMax,
                VolumeStep);
            if (ShouldIgnoreStartupZero(deviceVolume))
            {
                PushVolumeFeedback(_lastDeviceVolume);
                return;
            }

            Run(() => Client.SetVolumeAsync(deviceVolume));
            _lastDeviceVolume = deviceVolume;
            PushVolumeFeedback(deviceVolume);
        }

        public override void PressVolumeUp()
        {
            NudgeVolumeOnce(1, TapStep);
        }

        public override void PressVolumeDown()
        {
            NudgeVolumeOnce(-1, TapStep);
        }

        public override void IncrementVolume()
        {
            NudgeVolumeOnce(1, TapStep);
        }

        public override void DecrementVolume()
        {
            NudgeVolumeOnce(-1, TapStep);
        }

        public override void ReleaseVolume()
        {
        }

        public override bool OverrideZoneVolumeUp(CommonCommandGroupType zone, CommandAction action)
        {
            if (action == CommandAction.Hold || action == CommandAction.Release)
            {
                return true;
            }

            NudgeVolumeOnce(1, TapStep);
            return true;
        }

        public override bool OverrideZoneVolumeDown(CommonCommandGroupType zone, CommandAction action)
        {
            if (action == CommandAction.Hold || action == CommandAction.Release)
            {
                return true;
            }

            NudgeVolumeOnce(-1, TapStep);
            return true;
        }

        public override bool OverrideZonePowerOn(CommonCommandGroupType zone)
        {
            PowerOn();
            return true;
        }

        public override bool OverrideZonePowerOff(CommonCommandGroupType zone)
        {
            PowerOff();
            return true;
        }

        public override bool OverrideZoneMuteOn(CommonCommandGroupType zone)
        {
            MuteOn();
            return true;
        }

        public override bool OverrideZoneMuteOff(CommonCommandGroupType zone)
        {
            MuteOff();
            return true;
        }

        public override bool OverrideZoneMute(CommonCommandGroupType zone)
        {
            Mute();
            return true;
        }

        public override bool OverrideZoneSetVolume(CommonCommandGroupType zone, uint volume)
        {
            SetVolume(volume);
            return true;
        }

        public override bool OverrideZoneIncrementVolume(CommonCommandGroupType zone)
        {
            NudgeVolumeOnce(1, TapStep);
            return true;
        }

        public override bool OverrideZoneDecrementVolume(CommonCommandGroupType zone)
        {
            NudgeVolumeOnce(-1, TapStep);
            return true;
        }

        public override bool OverrideZoneSetVideoInputSource(CommonCommandGroupType zone, VideoConnections input)
        {
            SelectInput(InputMap.FromVideo(input));
            return true;
        }

        public override bool OverrideZoneSetAudioInputSource(CommonCommandGroupType zone, AudioConnections input)
        {
            SelectInput(InputMap.FromAudio(input));
            return true;
        }

        public override void Spotify()
        {
            SelectInput("spotify");
        }

        public override void Tidal()
        {
            SelectInput("tidal");
        }

        public override void Airplay()
        {
            SelectInput("airplay");
        }

        public override void InternetRadio()
        {
            SelectInput("net_radio");
        }

        public override void Deezer()
        {
            SelectInput("deezer");
        }

        protected override void Poll()
        {
            try
            {
                EnsureFeatures();
                var status = Client.GetStatusAsync().GetAwaiter().GetResult();
                if (status.MaxVolume > 0)
                {
                    VolumeMax = status.MaxVolume;
                }

                if (!ShouldIgnoreStartupZero(status.Volume))
                {
                    _lastDeviceVolume = status.Volume;
                }

                _muted = status.Mute;

                DeConstructPower(status.PowerOn ? "ON" : "OFF");
                ApplyMuteFeedback(_muted);
                PushVolumeFeedback(_lastDeviceVolume);
                if (!string.IsNullOrEmpty(status.Input))
                {
                    ApplyInputFeedback(status.Input);
                }
            }
            catch (Exception ex)
            {
                Log("YXC poll failed: " + ex.Message);
            }
        }

        protected override void ConnectionChangedEvent(bool connected)
        {
            base.ConnectionChangedEvent(connected);
            if (connected)
            {
                try
                {
                    EnsureFeatures();
                }
                catch (Exception ex)
                {
                    Log("YXC connect handshake failed: " + ex.Message);
                }
            }
        }

        protected override void ChooseDeconstructMethod(ValidatedRxData validatedData)
        {
        }

        private int VolumeMin = 0;
        private int VolumeMax = 154;
        private int VolumeStep = 1;

        private void EnsureFeatures()
        {
            if (_features != null)
            {
                return;
            }

            _features = Client.GetFeaturesAsync().GetAwaiter().GetResult();
            VolumeMin = _features.VolumeMin;
            VolumeMax = _features.VolumeMax <= _features.VolumeMin ? 154 : _features.VolumeMax;
            VolumeStep = _features.VolumeStep < 1 ? 1 : _features.VolumeStep;
            MinVolume = 0;
            MaxVolume = 100;
        }

        private async Task RestorePowerAndVolumeAsync()
        {
            await Client.SetPowerAsync(YxcPower.On).ConfigureAwait(false);
            try
            {
                await Task.Delay(500).ConfigureAwait(false);
                var status = await Client.GetStatusAsync().ConfigureAwait(false);
                var restore = Math.Max(_lastDeviceVolume, status.Volume);
                if (restore < 30)
                {
                    restore = 114;
                }

                if (status.Volume + 4 < restore)
                {
                    await Client.SetVolumeAsync(restore).ConfigureAwait(false);
                    _lastDeviceVolume = restore;
                }
                else
                {
                    _lastDeviceVolume = status.Volume > 0 ? status.Volume : restore;
                }
            }
            catch (Exception)
            {
                if (_lastDeviceVolume >= 30)
                {
                    await Client.SetVolumeAsync(_lastDeviceVolume).ConfigureAwait(false);
                }
            }
        }

        private bool ShouldIgnoreStartupZero(int deviceVolume)
        {
            if (deviceVolume > 8)
            {
                return false;
            }

            if (_lastDeviceVolume <= 8)
            {
                return false;
            }

            return (DateTime.UtcNow - _powerOnUtc).TotalSeconds < 12;
        }

        private void NudgeVolumeOnce(int direction, int step)
        {
            EnsureFeatures();
            var dir = direction >= 0 ? "up" : "down";
            step = Math.Max(step, VolumeStep);
            Run(() => Client.NudgeVolumeAsync(dir, step));
            if (direction >= 0)
            {
                _lastDeviceVolume = Math.Min(VolumeMax, _lastDeviceVolume + step);
            }
            else
            {
                _lastDeviceVolume = Math.Max(VolumeMin, _lastDeviceVolume - step);
            }

            PushVolumeFeedback(_lastDeviceVolume);
        }

        private void PushVolumeFeedback(int deviceVolume)
        {
            if (_applyingVolumeFeedback)
            {
                return;
            }

            var percent = VolumeScaler.ToPercent(deviceVolume, VolumeMin, VolumeMax);
            _applyingVolumeFeedback = true;
            try
            {
                UnscaledVolumeIs = percent;
                UnscaledRampingVolumeIs = percent;
                DeConstructVolume(percent.ToString());
                FireEvent(AvrStateObjects.Volume, new Volume
                {
                    MuteIsOn = _muted,
                    VolumeIs = percent
                });
            }
            finally
            {
                _applyingVolumeFeedback = false;
            }
        }

        private void SetMuted(bool muted)
        {
            Run(async () =>
            {
                await Client.SetMuteAsync(muted).ConfigureAwait(false);
                if (!muted)
                {
                    await Client.SetVolumeAsync(_lastDeviceVolume).ConfigureAwait(false);
                }
            });
            ApplyMuteFeedback(muted);
        }

        private void SelectInput(string inputId)
        {
            if (string.IsNullOrWhiteSpace(inputId))
            {
                inputId = "usb_dac";
            }

            Run(() => Client.SetInputAsync(inputId));
            ApplyInputFeedback(inputId);
        }

        private void ApplyMuteFeedback(bool muted)
        {
            _muted = muted;
            MuteIsOn = muted;
            DeConstructMute(muted ? "ON" : "OFF");
            FireEvent(AvrStateObjects.Mute, muted);
            PushVolumeFeedback(_lastDeviceVolume);
        }

        private void ApplyInputFeedback(string inputId)
        {
            DeConstructInput(inputId);
            DeConstructAudioInput(inputId);
        }

        private void Run(Func<Task> work)
        {
            try
            {
                work().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log("YXC command failed: " + ex.Message);
            }
        }
    }
}
