using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace HMS.API.Middleware
{
    // Middleware that logs incoming request details and unsuccessful responses (4xx/5xx)
    public class RequestDiagnosticsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Microsoft.Extensions.Logging.ILogger<RequestDiagnosticsMiddleware> _logger;

        public RequestDiagnosticsMiddleware(RequestDelegate next, Microsoft.Extensions.Logging.ILogger<RequestDiagnosticsMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var req = context.Request;

                // Read a small sample of the request body for diagnostics (if present)
                string bodySample = string.Empty;
                try
                {
                    req.EnableBuffering();
                    if (req.ContentLength.GetValueOrDefault() > 0 && req.Body.CanRead)
                    {
                        using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
                        var buffer = new char[4096];
                        var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                        bodySample = new string(buffer, 0, Math.Max(0, read));
                        req.Body.Seek(0, SeekOrigin.Begin);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "RequestDiagnostics: failed to read request body sample");
                }

                var hostHeader = req.Headers.ContainsKey("Host") ? req.Headers["Host"].ToString() : string.Empty;
                var xfh = req.Headers.ContainsKey("X-Forwarded-Host") ? req.Headers["X-Forwarded-Host"].ToString() : string.Empty;
                var remote = context.Connection.RemoteIpAddress?.ToString();

                _logger.LogInformation("Incoming request {Method} {Path} Host={HostHeader} X-Fwd-Host={XForwardedHost} Remote={Remote} BodySample={BodySample}", req.Method, req.Path + req.QueryString, hostHeader, xfh, remote, bodySample);

                await _next(context);

                try
                {
                    var status = context.Response?.StatusCode ?? 0;
                    if (status >= 400)
                    {
                        _logger.LogWarning("Request {Method} {Path} returned status {Status}. Host={HostHeader} X-Fwd-Host={XForwardedHost} Remote={Remote}", req.Method, req.Path + req.QueryString, status, hostHeader, xfh, remote);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "RequestDiagnostics: failed to evaluate response status");
                }
            }
            catch (Exception ex)
            {
                // Do not block request on diagnostics failures
                _logger.LogError(ex, "RequestDiagnosticsMiddleware unexpected error");
            } //catch (Exception ex) { try { System.Diagnostics.Trace.TraceError(ex.ToString()); } catch { } }
            await _next(context);
        }
    }
}

