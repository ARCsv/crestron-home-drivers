using System;

namespace Roon.Api
{
    public sealed class RoonExtensionInfo
    {
        public string Id = "com.cadenceworks.crestron.roon";
        public string DisplayName = "Cadence Works Crestron Roon";
        public string DisplayVersion = "1.0005.0000";
    }

    public sealed class RoonNowPlaying
    {
        public string ZoneId = string.Empty;
        public string ZoneName = string.Empty;
        public string State = string.Empty;
        public string Line = string.Empty;
        public string Title = string.Empty;
        public string Artist = string.Empty;
        public string Album = string.Empty;
        public string ImageKey = string.Empty;
        public string ImageUrl = string.Empty;
        public int SeekSeconds;
        public int LengthSeconds;
    }

    public sealed class RoonSession
    {
        public const int DefaultPort = 9330;
        public const string DefaultZoneName = "Living Room";

        private readonly Action<string> _send;
        private readonly Action<string> _log;
        private readonly Action<string> _saveToken;
        private readonly string _imageBase;
        private readonly RoonExtensionInfo _info;
        private int _nextId;
        private string _token;
        private string _coreId = string.Empty;
        private bool _zonesSubscribed;

        public RoonSession(
            string zoneName,
            string token,
            Action<string> send,
            Action<string> log,
            Action<string> saveToken,
            string imageBase,
            RoonExtensionInfo info)
        {
            ZoneName = string.IsNullOrWhiteSpace(zoneName) ? DefaultZoneName : zoneName.Trim();
            _token = token ?? string.Empty;
            _send = send;
            _log = log ?? (_ => { });
            _saveToken = saveToken ?? (_ => { });
            _imageBase = imageBase ?? string.Empty;
            _info = info ?? new RoonExtensionInfo();
            if (string.IsNullOrWhiteSpace(_info.Id))
            {
                _info.Id = "com.cadenceworks.crestron.roon";
            }

            if (string.IsNullOrWhiteSpace(_info.DisplayName))
            {
                _info.DisplayName = "Cadence Works Crestron Roon";
            }

            if (string.IsNullOrWhiteSpace(_info.DisplayVersion))
            {
                _info.DisplayVersion = "1.0005.0000";
            }
        }

        public string ZoneName { get; }
        public string ZoneId { get; private set; }
        public RoonNowPlaying NowPlaying { get; private set; }
        public event Action<RoonNowPlaying> NowPlayingChanged;

        public void StartHandshake()
        {
            SendRequest("com.roonlabs.registry:1/info", null);
            SendRequest("com.roonlabs.registry:1/register", RegisterJson());
        }

        public void PlayPause()
        {
            Control("playpause");
        }

        public void Play()
        {
            Control("play");
        }

        public void Pause()
        {
            Control("pause");
        }

        public void Stop()
        {
            Control("stop");
        }

        public void Next()
        {
            Control("next");
        }

        public void Previous()
        {
            Control("previous");
        }

        public void HandleRaw(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            var msg = MooCodec.Parse(raw);
            if (msg.Verb == "REQUEST" && msg.Service == "com.roonlabs.ping:1")
            {
                _send(MooCodec.Complete(msg.RequestId, "Success", null));
                return;
            }

            if (msg.Verb == "REQUEST" && msg.Service == "com.roonlabs.pairing:1")
            {
                var body = "{\"paired_core_id\":" + JsonUtil.Quote(_coreId) + "}";
                if (msg.Name == "subscribe_pairing")
                {
                    _send(MooCodec.Continue(msg.RequestId, "Subscribed", body));
                }
                else
                {
                    _send(MooCodec.Complete(msg.RequestId, "Success", body));
                }

                return;
            }

            if (msg.Verb == "COMPLETE" && msg.Name == "Success" && msg.Body.IndexOf("core_id", StringComparison.Ordinal) >= 0)
            {
                var id = JsonUtil.GetString(msg.Body, "core_id");
                if (!string.IsNullOrEmpty(id))
                {
                    _coreId = id;
                }
            }

            if (msg.Verb == "CONTINUE" && msg.Name == "Registered")
            {
                _coreId = JsonUtil.GetString(msg.Body, "core_id");
                var token = JsonUtil.GetString(msg.Body, "token");
                if (!string.IsNullOrEmpty(token))
                {
                    _token = token;
                    _saveToken(token);
                }

                _log("Roon registered with " + JsonUtil.GetString(msg.Body, "display_name"));
                SubscribeZones();
                return;
            }

            if (msg.Verb == "COMPLETE" && msg.Name == "Unauthorized")
            {
                _log("Roon extension is not enabled. Enable it in Roon Settings → Extensions.");
                _zonesSubscribed = false;
                return;
            }

            if (msg.Verb == "CONTINUE" && (msg.Name == "Subscribed" || msg.Name == "Changed"))
            {
                ApplyZones(msg.Body);
            }
        }

        private void SubscribeZones()
        {
            if (_zonesSubscribed)
            {
                return;
            }

            _zonesSubscribed = true;
            SendRequest("com.roonlabs.transport:2/subscribe_zones", "{\"subscription_key\":0}");
        }

        private void Control(string action)
        {
            if (string.IsNullOrEmpty(ZoneId))
            {
                _log("Roon zone '" + ZoneName + "' is not available yet.");
                return;
            }

            var body = "{\"zone_or_output_id\":" + JsonUtil.Quote(ZoneId) + ",\"control\":" + JsonUtil.Quote(action) + "}";
            SendRequest("com.roonlabs.transport:2/control", body);
        }

        private void ApplyZones(string json)
        {
            var marker = "\"display_name\":" + JsonUtil.Quote(ZoneName);
            var idx = json.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
            {
                return;
            }

            var start = json.LastIndexOf("\"zone_id\":", idx, StringComparison.Ordinal);
            if (start < 0)
            {
                return;
            }

            var chunk = json.Substring(start, Math.Min(json.Length - start, 12000));
            var zoneId = JsonUtil.GetString(chunk, "zone_id");
            var state = JsonUtil.GetString(chunk, "state");
            var now = JsonUtil.ExtractObject(chunk, "now_playing");
            var three = JsonUtil.ExtractObject(now, "three_line");
            var two = JsonUtil.ExtractObject(now, "two_line");
            var title = JsonUtil.GetString(three, "line1");
            var artist = JsonUtil.GetString(three, "line2");
            var album = JsonUtil.GetString(three, "line3");
            if (string.IsNullOrEmpty(title))
            {
                title = JsonUtil.GetString(two, "line1");
                artist = JsonUtil.GetString(two, "line2");
            }

            if (string.IsNullOrEmpty(title))
            {
                title = ZoneName;
            }

            var imageKey = JsonUtil.GetString(now, "image_key");
            var seek = JsonUtil.GetInt(now, "seek_position");
            var length = JsonUtil.GetInt(now, "length");
            ZoneId = zoneId;
            var np = new RoonNowPlaying
            {
                ZoneId = zoneId,
                ZoneName = ZoneName,
                State = state,
                Title = title,
                Artist = artist,
                Album = album,
                ImageKey = imageKey,
                ImageUrl = BuildImageUrl(imageKey),
                SeekSeconds = seek,
                LengthSeconds = length,
                Line = BuildLine(title, artist, album)
            };
            NowPlaying = np;
            var handler = NowPlayingChanged;
            if (handler != null)
            {
                handler(np);
            }
        }

        private string BuildImageUrl(string imageKey)
        {
            if (string.IsNullOrEmpty(_imageBase) || string.IsNullOrEmpty(imageKey))
            {
                return string.Empty;
            }

            return _imageBase + "/api/image/" + imageKey + "?scale=fit&width=512&height=512&format=image/jpeg";
        }

        private static string BuildLine(string title, string artist, string album)
        {
            if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(album))
            {
                return title + " — " + artist + " — " + album;
            }

            if (!string.IsNullOrEmpty(artist))
            {
                return title + " — " + artist;
            }

            return title;
        }

        private void SendRequest(string method, string body)
        {
            var id = _nextId++;
            _send(MooCodec.Request(id, method, body));
        }

        private string RegisterJson()
        {
            var token = string.IsNullOrEmpty(_token)
                ? string.Empty
                : ",\"token\":" + JsonUtil.Quote(_token);
            return "{"
                + "\"extension_id\":" + JsonUtil.Quote(_info.Id) + ","
                + "\"display_name\":" + JsonUtil.Quote(_info.DisplayName) + ","
                + "\"display_version\":" + JsonUtil.Quote(_info.DisplayVersion) + ","
                + "\"publisher\":\"Cadence Works/Cursor\","
                + "\"email\":\"roon@cadenceworks.local\","
                + "\"required_services\":[\"com.roonlabs.transport:2\"],"
                + "\"optional_services\":[],"
                + "\"provided_services\":[\"com.roonlabs.pairing:1\",\"com.roonlabs.ping:1\"]"
                + token
                + "}";
        }
    }
}
