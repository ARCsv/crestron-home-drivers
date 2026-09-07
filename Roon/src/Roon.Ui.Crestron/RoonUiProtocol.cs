using System;
using Crestron.RAD.Common.BasicDriver;
using Crestron.RAD.Common.Transports;

namespace Roon.Crestron
{
    public sealed class RoonUiProtocol : ABaseDriverProtocol
    {
        private readonly RoonWsTransport _transport;

        public RoonUiProtocol(ATransportDriver transport, byte id)
            : base(transport, id)
        {
            _transport = (RoonWsTransport)transport;
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

        public override void SetUserAttribute(string attributeId, bool attributeValue)
        {
        }

        public override void SetUserAttribute(string attributeId, ushort attributeValue)
        {
        }
    }
}
