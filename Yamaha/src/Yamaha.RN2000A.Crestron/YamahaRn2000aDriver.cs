using Crestron.RAD.Common.Interfaces;
using Crestron.RAD.DeviceTypes.RADAVReceiver;
using Crestron.SimplSharp;
using Yamaha.Yxc;

namespace Yamaha.RN2000A.Crestron
{
    /// <summary>
    /// Crestron Home A/V Receiver driver for the Yamaha R-N2000A.
    /// Transport is ITcp (IP + port 80) but the protocol is YXC HTTP JSON, not YNCA.
    /// </summary>
    public sealed class YamahaRn2000aDriver : ABasicAVReceiver, ITcp
    {
        public YamahaRn2000aDriver()
        {
        }

        int ITcp.Port
        {
            get { return YxcClient.DefaultPort; }
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
            var transport = new YxcHttpTransport();
            transport.Initialize(host, port);
            ConnectionTransport = transport;

            var protocol = new YamahaYxcProtocol(transport, Id);
            ReceiverProtocol = protocol;
            DeviceProtocol = protocol;
            protocol.Initialize(AvrData);
        }
    }
}
