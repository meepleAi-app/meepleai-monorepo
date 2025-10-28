using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Api.Tests.Integration;

/// <summary>
/// CONFIG-07: Base class for configuration integration tests with authenticated request helpers
/// </summary>
public abstract class ConfigIntegrationTestBase : AdminTestFixture
{
    protected ConfigIntegrationTestBase(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Helper for authenticated POST JSON requests
    /// </summary>
    protected async Task<HttpResponseMessage> PostAsJsonAuthenticatedAsync<T>(
        HttpClient client,
        List<string> cookies,
        string uri,
        T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        AddCookies(request, cookies);
        return await client.SendAsync(request);
    }

    /// <summary>
    /// Helper for authenticated GET requests
    /// </summary>
    protected async Task<HttpResponseMessage> GetAuthenticatedAsync(
        HttpClient client,
        List<string> cookies,
        string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        AddCookies(request, cookies);
        return await client.SendAsync(request);
    }

    /// <summary>
    /// Helper for authenticated PUT JSON requests
    /// </summary>
    protected async Task<HttpResponseMessage> PutAsJsonAuthenticatedAsync<T>(
        HttpClient client,
        List<string> cookies,
        string uri,
        T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(payload)
        };
        AddCookies(request, cookies);
        return await client.SendAsync(request);
    }

    /// <summary>
    /// Helper for authenticated DELETE requests
    /// </summary>
    protected async Task<HttpResponseMessage> DeleteAuthenticatedAsync(
        HttpClient client,
        List<string> cookies,
        string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        AddCookies(request, cookies);
        return await client.SendAsync(request);
    }
}
