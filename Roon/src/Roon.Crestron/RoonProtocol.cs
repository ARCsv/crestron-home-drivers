using System;
using Crestron.RAD.Common.BasicDriver;
using Crestron.RAD.Common.Transports;
using Crestron.RAD.DeviceTypes.VideoServer;
using Roon.Api;

namespace Roon.Crestron
{
    public sealed class RoonProtocol : AVideoServerProtocol
    {
        private readonly RoonWsTransport _transport;

        public RoonProtocol(ATransportDriver transport, byte id)
            : base(transport, id)
        {
            _transport = (RoonWsTransport)transport;
            _transport.NowPlayingChanged += OnNowPlaying;
        }

        public override void Play()
        {
            Control(s => s.Play());
        }

        public override void Pause()
        {
            Control(s => s.Pause());
        }

        public override void Stop()
        {
            Control(s => s.Stop());
        }

        public override void PlayPause()
        {
            Control(s => s.PlayPause());
        }

        public override void ForwardSkip()
        {
            Control(s => s.Next());
        }

        public override void ReverseSkip()
        {
            Control(s => s.Previous());
        }

        protected override void ConnectionChangedEvent(bool connected)
        {
        }

        protected override void ChooseDeconstructMethod(ValidatedRxData validatedData)
        {
        }

        public override void SetUserAttribute(string attributeId, string attributeValue)
        {
            if (string.Equals(attributeId, "ZoneName", StringComparison.OrdinalIgnoreCase))
            {
                _transport.ZoneName = attributeValue;
            }
        }

        private void Control(Action<RoonSession> action)
        {
            var session = _transport.Session;
            if (session == null)
            {
                Log("Roon is not connected.");
                return;
            }

            action(session);
        }

        private void OnNowPlaying(RoonNowPlaying playing)
        {
            try
            {
                DeConstructActiveMediaServiceFeedback("Roon");
            }
            catch
            {
            }
        }
    }
}
