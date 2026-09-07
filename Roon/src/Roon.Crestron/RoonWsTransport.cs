using System;
using System.Text;
using Crestron.RAD.Common.Transports;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronWebSocketClient;
using Roon.Api;

namespace Roon.Crestron
{
    public sealed class RoonWsTransport : ATransportDriver
    {
        private WebSocketClient _socket;
        private CTimer _reconnect;
        private string _host = "127.0.0.1";
        private int _port = RoonSession.DefaultPort;
        private string _zoneName = RoonSession.DefaultZoneName;
        private string _tokenPath = "/user/Roon.Crestron.token";
        private RoonExtensionInfo _info = new RoonExtensionInfo();
        private bool _started;

        public event Action<RoonNowPlaying> NowPlayingChanged;
        public RoonSession Session { get; private set; }

        public void ConfigureExtension(RoonExtensionInfo info, string tokenPath)
        {
            if (info != null)
            {
                _info = info;
            }

            if (!string.IsNullOrWhiteSpace(tokenPath))
            {
                _tokenPath = tokenPath;
            }
        }

        public string ZoneName
        {
            get { return _zoneName; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                var next = value.Trim();
                if (string.Equals(_zoneName, next, StringComparison.Ordinal))
                {
                    return;
                }

                _zoneName = next;
                if (_started)
                {
                    ConnectSocket();
                }
            }
        }

        public void Initialize(string host, int port)
        {
            _host = host;
            _port = port > 0 ? port : RoonSession.DefaultPort;
        }

        public override void Start()
        {
            _started = true;
            ConnectSocket();
        }

        public override void Stop()
        {
            _started = false;
            StopReconnect();
            CloseSocket();
            IsConnected = false;
        }

        public override void SendMethod(string message, object[] parameters)
        {
        }

        private void ConnectSocket()
        {
            CloseSocket();
            Session = new RoonSession(
                _zoneName,
                LoadToken(),
                SendMoo,
                LogSafe,
                SaveToken,
                "http://" + _host + ":" + _port,
                _info);
            Session.NowPlayingChanged += playing =>
            {
                var handler = NowPlayingChanged;
                if (handler != null)
                {
                    handler(playing);
                }
            };
            _socket = new WebSocketClient();
            _socket.Port = (uint)_port;
            _socket.SSL = false;
            _socket.KeepAlive = false;
            _socket.VerifyServerCertificate = false;
            _socket.Host = _host;
            _socket.URL = "ws://" + _host + ":" + _port + "/api";
            _socket.ReceiveCallBack = OnReceive;
            var result = _socket.Connect();
            if (result != WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                LogSafe("Roon websocket connect failed: " + result);
                IsConnected = false;
                ScheduleReconnect();
                return;
            }

            IsConnected = true;
            _socket.ReceiveAsync();
            Session.StartHandshake();
        }

        private int OnReceive(
            byte[] data,
            uint datalen,
            WebSocketClient.WEBSOCKET_PACKET_TYPES opcode,
            WebSocketClient.WEBSOCKET_RESULT_CODES error)
        {
            if (error != WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                if (_started)
                {
                    IsConnected = false;
                    ScheduleReconnect();
                }

                return (int)error;
            }

            if (data == null || datalen == 0)
            {
                if (_started && _socket != null)
                {
                    _socket.ReceiveAsync();
                }

                return 0;
            }

            try
            {
                var text = Encoding.UTF8.GetString(data, 0, (int)datalen);
                if (Session != null)
                {
                    Session.HandleRaw(text);
                }
            }
            catch (Exception ex)
            {
                LogSafe("Roon receive: " + ex.Message);
            }

            if (_started && _socket != null)
            {
                _socket.ReceiveAsync();
            }

            return 0;
        }

        private void SendMoo(string moo)
        {
            if (_socket == null || !IsConnected)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(moo);
            _socket.Send(
                bytes,
                (uint)bytes.Length,
                WebSocketClient.WEBSOCKET_PACKET_TYPES.LWS_WS_OPCODE_07__BINARY_FRAME);
        }

        private void ScheduleReconnect()
        {
            StopReconnect();
            _reconnect = new CTimer(
                _ =>
                {
                    if (_started)
                    {
                        ConnectSocket();
                    }
                },
                5000);
        }

        private void StopReconnect()
        {
            if (_reconnect == null)
            {
                return;
            }

            _reconnect.Stop();
            _reconnect.Dispose();
            _reconnect = null;
        }

        private void CloseSocket()
        {
            if (_socket == null)
            {
                return;
            }

            try
            {
                _socket.Disconnect();
            }
            catch
            {
            }

            try
            {
                _socket.Dispose();
            }
            catch
            {
            }

            _socket = null;
        }

        private void LogSafe(string message)
        {
            try
            {
                Log(message);
            }
            catch
            {
            }
        }

        private string TokenPath()
        {
            return _tokenPath;
        }

        private string LoadToken()
        {
            try
            {
                if (!File.Exists(TokenPath()))
                {
                    return string.Empty;
                }

                return File.ReadToEnd(TokenPath(), Encoding.UTF8).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private void SaveToken(string token)
        {
            try
            {
                using (var stream = File.Create(TokenPath()))
                {
                    var bytes = Encoding.UTF8.GetBytes(token ?? string.Empty);
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch
            {
            }
        }
    }
}
