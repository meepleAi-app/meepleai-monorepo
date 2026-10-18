using System.Net;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #4446: <c>GET /admin/embedding/info</c> wrote <c>dimension = 1024</c> in the code while the service
/// ran e5-base at 768, so every admin page showed 1024. The dimension now comes from
/// <see cref="IEmbeddingService"/>, the rest from the service. The fake service answers 768 and a
/// <c>max_text_chars</c> of 2048: neither number was ever written in the endpoint.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetEmbeddingServiceInfoQueryHandlerTests
{
    private const string HealthyBody =
        """{"status":"healthy","model":"intfloat/multilingual-e5-base","device":"cpu","supported_languages":["en","it","de"]}""";

    private const string ConfigBody =
        """{"model":"intfloat/multilingual-e5-base","max_text_chars":2048,"max_total_chars":200000}""";

    [Fact]
    public async Task HealthyService_TheDimensionIsTheEmbeddingServicesAndTheRestComesFromTheService()
    {
        var info = await HandleAsync(new StubHandler()
            .Answer("/health", HttpStatusCode.OK, HealthyBody)
            .Answer("/config", HttpStatusCode.OK, ConfigBody));

        info.Dimension.Should().Be(768);
        info.Status.Should().Be("healthy");
        info.Model.Should().Be("intfloat/multilingual-e5-base");
        info.Device.Should().Be("cpu");
        info.SupportedLanguages.Should().Equal("en", "it", "de");
        info.MaxInputChars.Should().Be(2048);
    }

    [Fact]
    public async Task UnhealthyService_NothingFromTheServiceButTheDimensionIsStillKnown()
    {
        var info = await HandleAsync(new StubHandler()
            .Answer("/health", HttpStatusCode.ServiceUnavailable, """{"detail":"Model not loaded"}"""));

        info.Status.Should().Be("unhealthy");
        info.Model.Should().BeNull();
        info.Device.Should().BeNull();
        info.SupportedLanguages.Should().BeEmpty();
        info.MaxInputChars.Should().BeNull();
        info.Dimension.Should().Be(768);
    }

    [Fact]
    public async Task UnreachableService_IsUnavailable()
    {
        var info = await HandleAsync(new StubHandler().Throw(new HttpRequestException("connection refused")));

        info.Status.Should().Be("unavailable");
        info.Model.Should().BeNull();
        info.Dimension.Should().Be(768);
    }

    [Fact]
    public async Task ServiceWithoutConfig_LeavesOnlyTheInputLimitUnknown()
    {
        var info = await HandleAsync(new StubHandler()
            .Answer("/health", HttpStatusCode.OK, HealthyBody)
            .Answer("/config", HttpStatusCode.NotFound, """{"detail":"Not Found"}"""));

        info.Status.Should().Be("healthy");
        info.Model.Should().Be("intfloat/multilingual-e5-base");
        info.MaxInputChars.Should().BeNull();
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => HandleAsync(
            new StubHandler().Answer("/health", HttpStatusCode.OK, HealthyBody), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Task<EmbeddingServiceInfoDto> HandleAsync(StubHandler stub, CancellationToken? ct = null)
    {
        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService.Setup(e => e.GetEmbeddingDimensions()).Returns(768);

        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(GetEmbeddingServiceInfoQueryHandler.HttpClientName))
            .Returns(() => new HttpClient(stub) { BaseAddress = new Uri("http://embedding-service:8000") });

        var handler = new GetEmbeddingServiceInfoQueryHandler(
            factory.Object,
            embeddingService.Object,
            NullLogger<GetEmbeddingServiceInfoQueryHandler>.Instance);

        return handler.Handle(new GetEmbeddingServiceInfoQuery(), ct ?? TestContext.Current.CancellationToken);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _answers =
            new(StringComparer.Ordinal);
        private Exception? _failure;

        public StubHandler Answer(string path, HttpStatusCode status, string body)
        {
            _answers[path] = (status, body);
            return this;
        }

        public StubHandler Throw(Exception failure)
        {
            _failure = failure;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_failure is not null)
            {
                throw _failure;
            }

            var (status, body) = _answers.TryGetValue(request.RequestUri!.AbsolutePath, out var answer)
                ? answer
                : (HttpStatusCode.NotFound, "{}");

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
