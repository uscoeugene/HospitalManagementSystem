using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Http;

namespace HMS.API.Middleware
{
    // Middleware that wraps successful JSON responses into ApiResponse<T> shape
    public class ApiResponseWrappingMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiResponseWrappingMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var originalBody = context.Response.Body;
            await using var mem = new MemoryStream();
            context.Response.Body = mem;

            await _next(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            // If not JSON or empty, just copy through
            if (string.IsNullOrWhiteSpace(responseBody) || !IsJsonResponse(context))
            {
                context.Response.Body.Seek(0, SeekOrigin.Begin);
                await context.Response.Body.CopyToAsync(originalBody);
                context.Response.Body = originalBody;
                return;
            }

            // If already wrapped (contains "success" property) skip wrapping
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("success", out _))
                {
                    context.Response.Body.Seek(0, SeekOrigin.Begin);
                    await context.Response.Body.CopyToAsync(originalBody);
                    context.Response.Body = originalBody;
                    return;
                }
            }
            catch
            {
                // ignore parse errors and proceed to wrap raw body
            }

            // Try parse the existing JSON to object
            object? payload = null;
            try
            {
                payload = JsonSerializer.Deserialize<object>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                payload = responseBody;
            }

            var status = context.Response.StatusCode;
            var wrappedType = typeof(ApiResponse<object>);
            var wrapped = ApiResponse<object>.ForSuccess(payload, status);

            context.Response.ContentType = "application/json";
            context.Response.Body = originalBody;
            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(JsonSerializer.Serialize(wrapped, opts), Encoding.UTF8);
        }

        private static bool IsJsonResponse(HttpContext context)
        {
            var ct = context.Response.ContentType ?? string.Empty;
            return ct.Contains("application/json") || ct.Contains("text/json");
        }
    }
}
