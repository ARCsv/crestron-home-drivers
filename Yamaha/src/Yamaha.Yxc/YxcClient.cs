using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Yamaha.Yxc
{
    /// <summary>
    /// Yamaha Extended Control client. All control is HTTP GET on port 80:
    /// http://{host}/YamahaExtendedControl/v1/...
    /// </summary>
    public sealed class YxcClient : IDisposable
    {
        public const int DefaultPort = 80;
        public const string DefaultZone = "main";

        private readonly HttpClient _http;
        private readonly bool _ownsHttp;
        private readonly string _zone;

        public YxcClient(string host, int port = DefaultPort, string zone = DefaultZone, HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Receiver host/IP is required.", nameof(host));
            }

            _zone = string.IsNullOrWhiteSpace(zone) ? DefaultZone : zone;
            if (httpClient != null)
            {
                _http = httpClient;
                _ownsHttp = false;
            }
            else
            {
                _http = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };
                _ownsHttp = true;
            }

            var builder = new UriBuilder("http", host.Trim(), port)
            {
                Path = "/YamahaExtendedControl/"
            };
            BaseUri = builder.Uri;
        }

        public Uri BaseUri { get; }

        public string Zone
        {
            get { return _zone; }
        }

        public Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync("v1/system/getDeviceInfo", json => new DeviceInfo
            {
                ModelName = GetString(json, "model_name"),
                DeviceId = GetString(json, "device_id"),
                SystemId = GetString(json, "system_id"),
                ApiVersion = GetVersion(json, "api_version"),
                RawJson = json
            }, cancellationToken);
        }

        public Task<DeviceFeatures> GetFeaturesAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync("v1/system/getFeatures", json =>
            {
                var inputs = JsonField.ParseInputIds(json);
                JsonField.TryGetVolumeRange(json, out var min, out var max, out var step);
                if (inputs.Count == 0)
                {
                    inputs = InputCatalog.FallbackIds;
                }

                return new DeviceFeatures
                {
                    InputIds = inputs,
                    VolumeMin = min,
                    VolumeMax = max <= min ? 154 : max,
                    VolumeStep = step < 1 ? 1 : step,
                    RawJson = json
                };
            }, cancellationToken);
        }

        public Task<ZoneStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync("v1/" + _zone + "/getStatus", json =>
            {
                JsonField.TryGetString(json, "power", out var power);
                JsonField.TryGetInt(json, "volume", out var volume);
                JsonField.TryGetInt(json, "max_volume", out var maxVolume);
                JsonField.TryGetBool(json, "mute", out var mute);
                JsonField.TryGetString(json, "input", out var input);
                JsonField.TryGetString(json, "input_text", out var inputText);
                JsonField.TryGetString(json, "sound_program", out var program);
                return new ZoneStatus
                {
                    PowerOn = string.Equals(power, YxcPower.On, StringComparison.OrdinalIgnoreCase),
                    Volume = volume,
                    MaxVolume = maxVolume,
                    Mute = mute,
                    Input = input,
                    InputText = inputText,
                    SoundProgram = program,
                    RawJson = json
                };
            }, cancellationToken);
        }

        public Task SetPowerAsync(string power, CancellationToken cancellationToken = default)
        {
            return GetAsync("v1/" + _zone + "/setPower?power=" + Uri.EscapeDataString(power), IgnoreBody, cancellationToken);
        }

        public Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default)
        {
            return GetAsync("v1/" + _zone + "/setVolume?volume=" + volume, IgnoreBody, cancellationToken);
        }

        public Task NudgeVolumeAsync(string direction, int step = 1, CancellationToken cancellationToken = default)
        {
            var path = "v1/" + _zone + "/setVolume?volume=" + Uri.EscapeDataString(direction) + "&step=" + step;
            return GetAsync(path, IgnoreBody, cancellationToken);
        }

        public Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default)
        {
            return SetMuteCoreAsync(muted, cancellationToken);
        }

        private async Task SetMuteCoreAsync(bool muted, CancellationToken cancellationToken)
        {
            var enable = muted ? "true" : "false";
            try
            {
                await GetAsync(
                    "v1/" + _zone + "/setMute?enable=" + enable,
                    IgnoreBody,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (YxcException)
            {
                await GetAsync(
                    "v1/" + _zone + "/setMute?enable=" + (muted ? "1" : "0"),
                    IgnoreBody,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public Task SetInputAsync(string inputId, CancellationToken cancellationToken = default)
        {
            return SetInputCoreAsync(inputId, cancellationToken);
        }

        private async Task SetInputCoreAsync(string inputId, CancellationToken cancellationToken)
        {
            try
            {
                await GetAsync(
                    "v1/" + _zone + "/prepareInputChange?input=" + Uri.EscapeDataString(inputId),
                    IgnoreBody,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            await GetAsync(
                "v1/" + _zone + "/setInput?input=" + Uri.EscapeDataString(inputId),
                IgnoreBody,
                cancellationToken).ConfigureAwait(false);
        }

        public Task<PlayInfo> GetPlayInfoAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync("v1/netusb/getPlayInfo", json =>
            {
                JsonField.TryGetString(json, "playback", out var playback);
                JsonField.TryGetString(json, "input", out var input);
                JsonField.TryGetString(json, "artist", out var artist);
                JsonField.TryGetString(json, "album", out var album);
                JsonField.TryGetString(json, "track", out var track);
                return new PlayInfo
                {
                    Playback = playback,
                    Input = input,
                    Artist = artist,
                    Album = album,
                    Track = track,
                    RawJson = json
                };
            }, cancellationToken);
        }

        public Task SetPlaybackAsync(string playback, CancellationToken cancellationToken = default)
        {
            return GetAsync(
                "v1/netusb/setPlayback?playback=" + Uri.EscapeDataString(playback),
                IgnoreBody,
                cancellationToken);
        }

        public void Dispose()
        {
            if (_ownsHttp)
            {
                _http.Dispose();
            }
        }

        private static string IgnoreBody(string json)
        {
            return json;
        }

        private static string GetString(string json, string name)
        {
            JsonField.TryGetString(json, name, out var value);
            return value;
        }

        private static string GetVersion(string json, string name)
        {
            if (JsonField.TryGetString(json, name, out var text) && !string.IsNullOrEmpty(text))
            {
                return text;
            }

            var match = Regex.Match(
                json,
                "\"" + Regex.Escape(name) + "\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)?)",
                RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private async Task<T> GetAsync<T>(string relativePath, Func<string, T> map, CancellationToken cancellationToken)
        {
            var uri = new Uri(BaseUri, relativePath);
            HttpResponseMessage response;
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    request.Headers.TryAddWithoutValidation("X-AppName", "MusicCast/1.0");
                    response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                throw new YxcException("HTTP request to " + uri + " failed.", ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new YxcException("HTTP " + (int)response.StatusCode + " from " + uri + ": " + body);
                }

                JsonField.RequireCode(body);
                return map(body);
            }
        }
    }
}
