using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Profiling;
using MiniProfilerExtensions = UluBilge.Api.Extentions.MiniProfilerExtensions;

namespace UluBilge.Api.Profiling
{
    public sealed class RequestProfilingMiddleware(RequestDelegate next, IOptions<MiniProfilerOptions> options)
    {
        private const int BodyLimit = 16 * 1024;
        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(MiniProfilerExtensions.Route) || context.Request.Path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            var request = context.Request;
            var name = $"{request.Method} {request.Path}";
            var profiler = options.Value.StartProfiler(name);
            if (profiler is null)
            {
                await next(context);
                return;
            }
            string? body = null;//Yalnızca küçük, uzunluğu bilinen gövdeleri okuyup başa sar.
            if (request.ContentLength is > 0 and <= BodyLimit)
            {
                request.EnableBuffering();
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                body = await reader.ReadToEndAsync(context.RequestAborted);
                request.Body.Position = 0;
            }
            using (var timing = profiler.CustomTiming("http", JsonSerializer.Serialize(new
            {
                request.Method,
                Url = request.GetEncodedUrl(),
                request.Protocol,
                request.ContentType,
                request.ContentLength,
                request.Headers,
                Body = FormatBody(body)
            }, Json), "Request", includeStackTrace: false)) { }

            var originalBody = context.Response.Body;
            var isMcp = request.Path.StartsWithSegments("/mcp");
            using var responseBody = new MemoryStream();
            // MCP akışını bekletmeden ilet; yalnızca ilk BodyLimit baytı log için kopyala.
            using var captureStream = isMcp ? new ResponseCaptureStream(originalBody, responseBody, BodyLimit) : null;
            context.Response.Body = captureStream ?? (Stream)responseBody;
            var status = 0;
            try
            {
                await next(context);
            }
            catch
            {
                status = context.RequestAborted.IsCancellationRequested ? 499 : 500;
                throw;
            }
            finally
            {
                context.Response.Body = originalBody;
                try
                {
                    if (status == 0) status = context.Response.StatusCode;
                    profiler.Name = $"{name} [{status}]";
                    profiler.Root.Name = profiler.Name;
                    using (var timing = profiler.CustomTiming("http", JsonSerializer.Serialize(new
                    {
                        StatusCode = status,
                        context.Response.ContentType,
                        context.Response.ContentLength,
                        context.Response.Headers,
                        Body = FormatResponseBody(Encoding.UTF8.GetString(responseBody.GetBuffer(), 0, (int)Math.Min(responseBody.Length, BodyLimit)), context.Response.ContentType)
                    }, Json), "Response", includeStackTrace: false)) { }

                    if (!isMcp)
                    {
                        responseBody.Position = 0;
                        await responseBody.CopyToAsync(originalBody, context.RequestAborted);
                    }
                }
                finally
                {
                    await profiler.StopAsync();
                }
            }
        }

        private static object? FormatResponseBody(string body, string? contentType)
        {
            if (!string.Equals(contentType?.Split(';', 2)[0].Trim(), "text/event-stream", StringComparison.OrdinalIgnoreCase))
                return FormatResponseMessage(body);

            // SSE çerçevesi yerine data alanındaki JSON'u yalnızca profiler görünümü için ayrıştır.
            var events = body.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Split("\n\n");
            var messages = new List<object?>();
            foreach (var frame in events[..^1])
            {
                var data = frame.Split('\n')
                    .Where(line => line == "data" || line.StartsWith("data:", StringComparison.Ordinal))
                    .Select(line =>
                    {
                        var value = line == "data" ? string.Empty : line[5..];
                        return value.StartsWith(' ') ? value[1..] : value;
                    })
                    .ToArray();
                if (data.Length > 0)
                    messages.Add(FormatResponseMessage(string.Join('\n', data)));
            }

            // Kayıt sınırında veya bağlantı kesildiğinde yarım kalan mesajı kaybetme.
            if (!string.IsNullOrWhiteSpace(events[^1]))
                return new { Messages = messages, IncompleteEvent = events[^1] };

            return messages.Count switch
            {
                0 => body,
                1 => messages[0],
                _ => messages
            };
        }

        private static object? FormatResponseMessage(string body)
        {
            var formatted = FormatBody(body);
            if (formatted is not JsonElement { ValueKind: JsonValueKind.Object } element)
                return formatted;

            var message = JsonNode.Parse(element.GetRawText())!;
            if (message["result"] is not JsonObject result || result["content"] is not JsonArray content)
                return formatted;

            foreach (var block in content.OfType<JsonObject>())
            {
                if (block["type"] is not JsonValue type || !type.TryGetValue<string>(out var name) || name != "text"
                    || block["text"] is not JsonValue text || !text.TryGetValue<string>(out var value))
                    continue;

                // Yalnızca log kopyasında JSON içeren text alanını nesne/dizi olarak göster.
                try
                {
                    var parsed = JsonNode.Parse(value);
                    if (parsed is JsonObject or JsonArray)
                        block["text"] = parsed;
                }
                catch (JsonException) { }
            }

            return message;
        }

        private static object? FormatBody(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return body;
            try
            {
                using var document = JsonDocument.Parse(body);
                return document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return body;
            }
        }
    }
}
