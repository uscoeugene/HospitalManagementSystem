using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using System.Net;
using System.Text.Json;

namespace HMS.API.Infrastructure.Sync
{
    public class CloudSyncClient : HMS.API.Application.Sync.ICloudSyncClient
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public CloudSyncClient(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _baseUrl = cfg["CloudSync:Url"] ?? string.Empty;

            // Retry on transient network errors and 5xx responses with exponential backoff
            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(msg => ((int)msg.StatusCode) >= 500 || msg.StatusCode == HttpStatusCode.RequestTimeout)
                .WaitAndRetryAsync(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7) });
        }

        public async Task PushAsync(string entityName, object[] records)
        {
            if (string.IsNullOrWhiteSpace(_baseUrl)) return;
            var url = new Uri(new Uri(_baseUrl), $"/sync/push/{entityName}");

            var response = await _retryPolicy.ExecuteAsync(() => _http.PostAsJsonAsync(url, records));
            if (!response.IsSuccessStatusCode)
            {
                var body = await SafeRead(response);
                throw new HttpRequestException($"Push failed for {entityName}: {response.StatusCode} - {body}");
            }
        }

        public async Task<object[]> PullAsync(string entityName, DateTimeOffset? since)
        {
            if (string.IsNullOrWhiteSpace(_baseUrl)) return Array.Empty<object>();
            var url = new Uri(new Uri(_baseUrl), $"/sync/pull/{entityName}");
            var q = since.HasValue ? $"?since={since.Value.ToString("o")}" : string.Empty;

            var response = await _retryPolicy.ExecuteAsync(() => _http.GetAsync(url + q));
            if (!response.IsSuccessStatusCode) return Array.Empty<object>();

            var arr = await response.Content.ReadFromJsonAsync<object[]>();
            return arr ?? Array.Empty<object>();
        }

        private static async Task<string> SafeRead(HttpResponseMessage resp)
        {
            try
            {
                return await resp.Content.ReadAsStringAsync();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}