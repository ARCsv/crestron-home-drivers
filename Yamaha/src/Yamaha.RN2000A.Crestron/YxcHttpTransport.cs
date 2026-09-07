using System;
using Crestron.RAD.Common.Transports;
using Yamaha.Yxc;

namespace Yamaha.RN2000A.Crestron
{
    /// <summary>
    /// YXC is request/response HTTP, not a persistent YNCA TCP socket.
    /// This transport exists so Crestron Home can still pass IP + port 80 via ITcp.
    /// </summary>
    public sealed class YxcHttpTransport : ATransportDriver
    {
        public YxcClient Client { get; private set; }

        public void Initialize(string host, int port)
        {
            Client = new YxcClient(host, port <= 0 ? YxcClient.DefaultPort : port);
        }

        public override void Start()
        {
            IsConnected = Client != null;
        }

        public override void Stop()
        {
            IsConnected = false;
            if (Client != null)
            {
                Client.Dispose();
                Client = null;
            }
        }

        public override void SendMethod(string message, object[] parameters)
        {
        }
    }
}
