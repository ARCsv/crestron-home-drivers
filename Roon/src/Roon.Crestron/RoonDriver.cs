using Crestron.RAD.Common.Interfaces;
using Crestron.RAD.DeviceTypes.VideoServer;
using Crestron.SimplSharp;
using Roon.Api;

namespace Roon.Crestron
{
    public sealed class RoonDriver : ABasicVideoServer, ITcp
    {
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

        private void InitializeHost(string host, int port)
        {
            var transport = new RoonWsTransport();
            transport.ConfigureExtension(
                new RoonExtensionInfo
                {
                    Id = "com.cadenceworks.crestron.roon",
                    DisplayName = "Cadence Works Crestron Roon",
                    DisplayVersion = "1.0005.0000"
                },
                "/user/Roon.Crestron.token");
            transport.ZoneName = RoonSession.DefaultZoneName;
            transport.Initialize(host, port);
            ConnectionTransport = transport;

            var protocol = new RoonProtocol(transport, Id);
            VideoServerProtocol = protocol;
            DeviceProtocol = protocol;
            protocol.Initialize(VideoServerData);
        }
    }
}
