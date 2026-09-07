using System.Net;
using System.Net.Http;
using System.Text;
using Yamaha.Yxc;
using Xunit;

namespace Yamaha.Yxc.Tests
{
    public sealed class VolumeScalerTests
    {
        [Fact]
        public void Maps_device_range_to_percent()
        {
            Assert.Equal(0u, VolumeScaler.ToPercent(0, 0, 154));
            Assert.Equal(100u, VolumeScaler.ToPercent(154, 0, 154));
        }

        [Fact]
        public void Maps_percent_back_to_device_steps()
        {
            Assert.Equal(0, VolumeScaler.FromPercent(0, 0, 154, 1));
            Assert.Equal(154, VolumeScaler.FromPercent(100, 0, 154, 1));
        }

        [Fact]
        public void Mid_slider_is_near_normal_yamaha_listening_level()
        {
            var device = VolumeScaler.FromPercent(50, 0, 154, 1);
            Assert.InRange(device, 100, 125);
        }
    }

    public sealed class JsonFieldTests
    {
        [Fact]
        public void Parses_input_list_objects_and_strings()
        {
            const string json = """
                {"response_code":0,"zone":[{"id":"main","input_list":["optical1","spotify"]}],
                 "system":{"input_list":[{"id":"phono"},{"id":"hdmi"}]}}
                """;
            var ids = JsonField.ParseInputIds(json);
            Assert.Contains("optical1", ids);
            Assert.Contains("spotify", ids);
            Assert.DoesNotContain("distribution_enable", ids);
        }
    }

    public sealed class YxcClientTests
    {
        [Fact]
        public async Task GetStatus_reads_zone_fields()
        {
            var handler = new StubHandler(
                """{"response_code":0,"power":"on","volume":42,"mute":true,"input":"optical1","max_volume":161}""");
            var http = new HttpClient(handler);
            using var client = new YxcClient("192.168.1.50", httpClient: http);

            var status = await client.GetStatusAsync();

            Assert.True(status.PowerOn);
            Assert.Equal(42, status.Volume);
            Assert.True(status.Mute);
            Assert.Equal("optical1", status.Input);
            Assert.Contains("v1/main/getStatus", handler.LastUri.ToString());
        }

        [Fact]
        public async Task SetPower_uses_query_string()
        {
            var handler = new StubHandler("""{"response_code":0}""");
            using var client = new YxcClient("192.168.1.50", httpClient: new HttpClient(handler));
            await client.SetPowerAsync(YxcPower.Standby);
            Assert.Contains("setPower?power=standby", handler.LastUri.ToString());
        }

        [Fact]
        public async Task NonZero_response_code_throws()
        {
            var handler = new StubHandler("""{"response_code":3}""");
            using var client = new YxcClient("192.168.1.50", httpClient: new HttpClient(handler));
            var ex = await Assert.ThrowsAsync<YxcException>(() => client.GetDeviceInfoAsync());
            Assert.Equal(3, ex.ResponseCode);
        }

        [Fact]
        public async Task GetFeatures_reads_rn2000a_snapshot()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "live-getFeatures.json");
            Assert.True(File.Exists(path), "Expected docs/live-getFeatures.json copied to test output.");
            var handler = new StubHandler(File.ReadAllText(path));
            using var client = new YxcClient("192.168.1.50", httpClient: new HttpClient(handler));

            var features = await client.GetFeaturesAsync();

            Assert.Equal(0, features.VolumeMin);
            Assert.Equal(154, features.VolumeMax);
            Assert.Contains("optical1", features.InputIds);
            Assert.Contains("phono", features.InputIds);
            Assert.Contains("usb_dac", features.InputIds);
            Assert.DoesNotContain("hdmi", features.InputIds);
            Assert.DoesNotContain("distribution_enable", features.InputIds);
            Assert.Equal(23, features.InputIds.Count);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly string _body;

            public StubHandler(string body)
            {
                _body = body;
            }

            public Uri LastUri { get; private set; } = new Uri("http://localhost/");

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastUri = request.RequestUri!;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_body, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        }
    }
}
