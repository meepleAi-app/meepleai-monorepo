using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class HttpEmbeddingProviderLanguageTests
{
    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithItalianLanguage_SendsCorrectLanguageToService()
    {
        // Arrange
        string? capturedLanguage = null;
        var handler = new DelegatingHandlerStub(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedLanguage = doc.RootElement.GetProperty("language").GetString();

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(JsonSerializer.Serialize(new
            {
                embeddings = new[] { new[] { 0.1f, 0.2f } },
                model = "intfloat/multilingual-e5-base"
            }), Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var config = new EmbeddingConfiguration
        {
            LocalServiceUrl = "http://localhost:8000",
            Model = "intfloat/multilingual-e5-base",
            Dimensions = 2
        };
        IEmbeddingProvider provider = new HttpEmbeddingProvider(
            httpClient,
            NullLogger<HttpEmbeddingProvider>.Instance,
            config);

        // Act
        await provider.GenerateBatchEmbeddingsAsync(new[] { "testo di test" }, "it");

        // Assert
        Assert.Equal("it", capturedLanguage);
    }
}

internal class DelegatingHandlerStub(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
}
