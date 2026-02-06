using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using HMS.API.Application.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using System.Net;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace HMS.API.Infrastructure.Common
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly ConcurrentQueue<string> _recent = new ConcurrentQueue<string>();
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public NotificationService(ILogger<NotificationService> logger, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        public async Task NotifyAsync(string channel, object payload)
        {
            var msg = JsonSerializer.Serialize(new { channel, payload, at = DateTimeOffset.UtcNow });
            _recent.Enqueue(msg);
            while (_recent.Count > 100) _recent.TryDequeue(out _);
            _logger.LogInformation("Notify {Channel}: {Payload}", channel, payload);

            try
            {
                if (string.Equals(channel, "email", StringComparison.OrdinalIgnoreCase))
                {
                    await SendEmailAsync(payload);
                }
                else if (string.Equals(channel, "sms", StringComparison.OrdinalIgnoreCase))
                {
                    await SendSmsAsync(payload);
                }
                else
                {
                    _logger.LogWarning("Unknown notification channel: {Channel}", channel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification via {Channel}", channel);
            }
        }

        private async Task SendEmailAsync(object payload)
        {
            // payload expected to contain: to (string), subject (string), body (string)
            var doc = JsonSerializer.SerializeToElement(payload);
            if (!doc.TryGetProperty("to", out var toProp)) return;
            var to = toProp.GetString();
            var subject = doc.TryGetProperty("subject", out var s) ? s.GetString() : "Notification";
            var body = doc.TryGetProperty("body", out var b) ? b.GetString() : string.Empty;

            var host = _config["Smtp:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogWarning("SMTP host not configured, skipping email");
                return;
            }

            var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 25;
            var user = _config["Smtp:User"];
            var pass = _config["Smtp:Pass"];
            var from = _config["Smtp:From"] ?? "no-reply@example.com";

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) && ssl
            };
            if (!string.IsNullOrWhiteSpace(user))
            {
                client.Credentials = new NetworkCredential(user, pass);
            }

            var mail = new MailMessage(from, to)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await client.SendMailAsync(mail);
        }

        private async Task SendSmsAsync(object payload)
        {
            // payload expected: to (string), message (string)
            var doc = JsonSerializer.SerializeToElement(payload);
            if (!doc.TryGetProperty("to", out var toProp)) return;
            var to = toProp.GetString();
            var message = doc.TryGetProperty("message", out var m) ? m.GetString() : string.Empty;

            var accountSid = _config["Twilio:AccountSid"];
            var authToken = _config["Twilio:AuthToken"];
            var from = _config["Twilio:FromNumber"];

            if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(from))
            {
                _logger.LogWarning("Twilio not configured, skipping SMS");
                return;
            }

            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("From", from),
                new KeyValuePair<string,string>("To", to!),
                new KeyValuePair<string,string>("Body", message ?? string.Empty)
            });
            var auth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Twilio response: {Status} {Body}", resp.StatusCode, await resp.Content.ReadAsStringAsync());
            }
        }

        public Task<IEnumerable<string>> GetRecentAsync()
        {
            return Task.FromResult<IEnumerable<string>>(_recent.ToArray().Reverse());
        }
    }
}