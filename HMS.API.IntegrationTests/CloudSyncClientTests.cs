using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HMS.API.IntegrationTests
{
    public class CloudSyncClientTests
    {
        private class SequenceHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
            public int Calls { get; private set; }

            public void EnqueueResponse(Func<HttpRequestMessage, HttpResponseMessage> factory)
            {
                _responses.Enqueue(factory);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                if (_responses.Count == 0)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new object[0]) });
                }

                var f = _responses.Dequeue();
                return Task.FromResult(f(request));
            }
        }

        [Fact]
        public async Task PushAsync_RetriesOnServerError_ThenSucceeds()
        {
            var handler = new SequenceHandler();

            // first two responses are 500, then 200
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.EnqueueResponse(req => new HttpResponseMessage(HttpStatusCode.OK));

            var http = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://example.com/")
            };

            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { { "CloudSync:Url", "https://example.com/" } }).Build();
            var client = new HMS.API.Infrastructure.Sync.CloudSyncClient(http, cfg);

            Func<Task> act = async () => await client.PushAsync("TestEntity", new object[] { new { Id = Guid.NewGuid() } });

            await act.Should().NotThrowAsync();
            handler.Calls.Should().BeGreaterOrEqualTo(3);
        }

        [Fact]
        public async Task PullAsync_RetriesOnServerError_ThenReturnsArray()
        {
            var handler = new SequenceHandler();

            // two 500s then 200 with JSON array
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            handler.EnqueueResponse(req =>
            {
                var msg = new HttpResponseMessage(HttpStatusCode.OK);
                msg.Content = JsonContent.Create(new object[] { new { Id = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow } });
                return msg;
            });

            var http = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://example.com/")
            };

            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { { "CloudSync:Url", "https://example.com/" } }).Build();
            var client = new HMS.API.Infrastructure.Sync.CloudSyncClient(http, cfg);

            var result = await client.PullAsync("TestEntity", DateTimeOffset.UtcNow.AddHours(-1));
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
            handler.Calls.Should().BeGreaterOrEqualTo(3);
        }

        [Fact]
        public async Task PushAsync_FailsAfterRetries_Throws()
        {
            var handler = new SequenceHandler();
            // enqueue 4 server errors (initial attempt + 3 retries)
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { { "CloudSync:Url", "https://example.com/" } }).Build();
            var client = new HMS.API.Infrastructure.Sync.CloudSyncClient(http, cfg);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.PushAsync("TestEntity", new object[] { new { Id = Guid.NewGuid() } }));
            handler.Calls.Should().BeGreaterOrEqualTo(4);
        }

        [Fact]
        public async Task PushAsync_DoesNotRetryOnBadRequest()
        {
            var handler = new SequenceHandler();
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

            var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { { "CloudSync:Url", "https://example.com/" } }).Build();
            var client = new HMS.API.Infrastructure.Sync.CloudSyncClient(http, cfg);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.PushAsync("TestEntity", new object[] { new { Id = Guid.NewGuid() } }));
            handler.Calls.Should().Be(1);
        }

        [Fact]
        public async Task PullAsync_FailsAfterRetries_ReturnsEmpty()
        {
            var handler = new SequenceHandler();
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { { "CloudSync:Url", "https://example.com/" } }).Build();
            var client = new HMS.API.Infrastructure.Sync.CloudSyncClient(http, cfg);

            var result = await client.PullAsync("TestEntity", DateTimeOffset.UtcNow.AddHours(-1));
            result.Should().NotBeNull();
            result.Length.Should().Be(0);
            handler.Calls.Should().BeGreaterOrEqualTo(4);
        }

        [Fact]
        public async Task PullAsync_DoesNotRetryOnBadRequest()
        {
            var handler = new SequenceHandler();
            handler.EnqueueResponse(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

            var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { { "CloudSync:Url", "https://example.com/" } }).Build();
            var client = new HMS.API.Infrastructure.Sync.CloudSyncClient(http, cfg);

            var result = await client.PullAsync("TestEntity", DateTimeOffset.UtcNow.AddHours(-1));
            result.Should().NotBeNull();
            result.Length.Should().Be(0);
            handler.Calls.Should().Be(1);
        }
    }
}
