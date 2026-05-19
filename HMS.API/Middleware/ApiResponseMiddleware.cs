using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using Microsoft.AspNetCore.Http;

namespace HMS.API.Middleware
{
    public class ApiResponseMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiResponseMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ApiResponseMiddleware(RequestDelegate next, ILogger<ApiResponseMiddleware> logger, IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing request {Method} {Path}", context.Request.Method, context.Request.Path);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            int status;
            string code;
            string message = ex.Message;

            switch (ex)
            {
                case UnauthorizedAccessException:
                    status = StatusCodes.Status401Unauthorized;
                    code = "UNAUTHORIZED";
                    break;
                case InvalidOperationException:
                    status = StatusCodes.Status400BadRequest;
                    code = "INVALID_OPERATION";
                    break;
                default:
                    status = StatusCodes.Status500InternalServerError;
                    code = "SERVER_ERROR";
                    break;
            }

            // In development include stacktrace in message to ease debugging
            if (_env.IsDevelopment())
            {
                try
                {
                    message = ex.ToString();
                }
                catch { }
            }

            var apiResp = ApiResponse<object>.ForError(code, message, status);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(apiResp, opts);
            await context.Response.WriteAsync(json);
        }
    }
}
