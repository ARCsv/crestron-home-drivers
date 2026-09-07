using System;
using System.Globalization;
using System.Text;

namespace Roon.Api
{
    internal static class MooCodec
    {
        public static string Request(int id, string serviceMethod, string jsonBody)
        {
            var sb = new StringBuilder();
            sb.Append("MOO/1 REQUEST ").Append(serviceMethod).Append('\n');
            sb.Append("Request-Id: ").Append(id.ToString(CultureInfo.InvariantCulture)).Append('\n');
            AppendBody(sb, jsonBody);
            return sb.ToString();
        }

        public static string Complete(int id, string status, string jsonBody)
        {
            var sb = new StringBuilder();
            sb.Append("MOO/1 COMPLETE ").Append(status).Append('\n');
            sb.Append("Request-Id: ").Append(id.ToString(CultureInfo.InvariantCulture)).Append('\n');
            AppendBody(sb, jsonBody);
            return sb.ToString();
        }

        public static string Continue(int id, string status, string jsonBody)
        {
            var sb = new StringBuilder();
            sb.Append("MOO/1 CONTINUE ").Append(status).Append('\n');
            sb.Append("Request-Id: ").Append(id.ToString(CultureInfo.InvariantCulture)).Append('\n');
            AppendBody(sb, jsonBody);
            return sb.ToString();
        }

        public static MooFrame Parse(string raw)
        {
            var nl = raw.IndexOf('\n');
            var first = nl < 0 ? raw : raw.Substring(0, nl);
            var parts = first.Split(new[] { ' ' }, 3);
            var verb = parts.Length > 1 ? parts[1] : string.Empty;
            var name = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            var service = string.Empty;
            var method = name;
            var slash = name.LastIndexOf('/');
            if (verb == "REQUEST" && slash > 0)
            {
                service = name.Substring(0, slash);
                method = name.Substring(slash + 1);
            }

            var requestId = -1;
            var headerEnd = raw.IndexOf("\n\n", StringComparison.Ordinal);
            var headers = headerEnd < 0 ? raw : raw.Substring(0, headerEnd);
            foreach (var line in headers.Split('\n'))
            {
                if (line.StartsWith("Request-Id:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line.Substring(11).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out requestId);
                }
            }

            var body = headerEnd < 0 ? string.Empty : raw.Substring(headerEnd + 2);
            return new MooFrame(verb, service, method, requestId, body);
        }

        private static void AppendBody(StringBuilder sb, string jsonBody)
        {
            if (string.IsNullOrEmpty(jsonBody))
            {
                sb.Append('\n');
                return;
            }

            var len = Encoding.UTF8.GetByteCount(jsonBody);
            sb.Append("Content-Type: application/json\n");
            sb.Append("Content-Length: ").Append(len.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append('\n');
            sb.Append(jsonBody);
        }
    }

    internal readonly struct MooFrame
    {
        public MooFrame(string verb, string service, string name, int requestId, string body)
        {
            Verb = verb;
            Service = service;
            Name = name;
            RequestId = requestId;
            Body = body ?? string.Empty;
        }

        public string Verb { get; }
        public string Service { get; }
        public string Name { get; }
        public int RequestId { get; }
        public string Body { get; }
    }
}
