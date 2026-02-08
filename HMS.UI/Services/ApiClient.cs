using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HMS.UI.Services;

public class ApiClient
{
    private readonly IHttpClientFactory _factory;

    public ApiClient(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient("HmsApi");
    }

    public async Task<T?> GetAsync<T>(string path)
    {
        var client = CreateClient();
        var res = await client.GetAsync(path);
        if (!res.IsSuccessStatusCode) return default;
        return await res.Content.ReadFromJsonAsync<T>();
    }

    public async Task<T?> PostAsync<T>(string path, object payload)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync(path, payload);
        if (!res.IsSuccessStatusCode) return default;
        return await res.Content.ReadFromJsonAsync<T>();
    }
}
