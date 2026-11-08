using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;

/// <summary>
/// <see cref="EmbeddingService"/> must carry the caller's <see cref="EmbeddingPurpose"/> down to
/// the provider, on the fallback leg too (issue #3737).
/// </summary>
/// <remarks>
/// A purpose that is accepted at the top of the stack and dropped halfway is worse than not
/// having one: the call sites would read as fixed while the request stayed wrong. The fallback
/// leg gets its own test because that is where a signal typically goes missing — it is the path
/// nobody exercises until the primary provider is already failing.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class EmbeddingServicePurposeTests
{
    private static readonly EmbeddingProviderResult Success =
        EmbeddingProviderResult.CreateSuccess(new List<float[]> { new[] { 0.1f, 0.2f } }, "test-model");

    // EmbeddingPurpose is internal, so it cannot appear in a public [Theory] signature (CS0051).

    [Fact]
    public async Task GenerateEmbeddingAsync_WithQueryPurpose_ForwardsQueryToThePrimaryProvider()
    {
        var seen = await CaptureForwardedPurposeAsync(EmbeddingPurpose.Query);
        seen.Should().Be(EmbeddingPurpose.Query);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithPassagePurpose_ForwardsPassageToThePrimaryProvider()
    {
        var seen = await CaptureForwardedPurposeAsync(EmbeddingPurpose.Passage);
        seen.Should().Be(EmbeddingPurpose.Passage);
    }

    private static async Task<EmbeddingPurpose?> CaptureForwardedPurposeAsync(EmbeddingPurpose purpose)
    {
        var primary = CreateProvider("Primary");
        EmbeddingPurpose? seen = null;
        SetupPurposeOverload(primary, r => seen = r, Success);

        var service = CreateService(primary, fallback: null);

        var result = await service.GenerateEmbeddingAsync("how do I set up Catan?", purpose);

        result.Success.Should().BeTrue();
        return seen;
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithLanguage_ForwardsBothLanguageAndPurpose()
    {
        var primary = CreateProvider("Primary");
        string? seenLanguage = null;
        EmbeddingPurpose? seenPurpose = null;
        primary
            .Setup(p => p.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<EmbeddingPurpose>(),
                It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<string> _, string language, EmbeddingPurpose purpose, CancellationToken _) =>
            {
                seenLanguage = language;
                seenPurpose = purpose;
            })
            .ReturnsAsync(Success);

        var service = CreateService(primary, fallback: null);

        await service.GenerateEmbeddingAsync("Come si prepara Catan?", "it", EmbeddingPurpose.Query);

        seenLanguage.Should().Be("it");
        seenPurpose.Should().Be(EmbeddingPurpose.Query);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenPrimaryFails_FallbackReceivesTheSamePurpose()
    {
        var primary = CreateProvider("Primary");
        SetupPurposeOverload(primary, _ => { }, EmbeddingProviderResult.CreateFailure("primary down"));

        var fallback = CreateProvider("Fallback");
        EmbeddingPurpose? seenByFallback = null;
        SetupPurposeOverload(fallback, r => seenByFallback = r, Success);

        var service = CreateService(primary, fallback);

        var result = await service.GenerateEmbeddingAsync("how do I set up Catan?", EmbeddingPurpose.Query);

        result.Success.Should().BeTrue();
        seenByFallback.Should().Be(EmbeddingPurpose.Query);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithoutPurpose_StillUsesThePreFixProviderOverload()
    {
        // The indexing path must keep hitting the 2-arg provider contract it hit before #3737.
        // Only the purpose overload is set up here, so if the service had been rerouted through
        // it this call would come back as a failure instead of a success.
        var primary = CreateProvider("Primary");
        primary
            .Setup(p => p.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success);

        var service = CreateService(primary, fallback: null);

        var result = await service.GenerateEmbeddingsAsync(new List<string> { "a chunk of rules text" });

        result.Success.Should().BeTrue();
        primary.Verify(
            p => p.GenerateBatchEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IEmbeddingProvider> CreateProvider(string name)
    {
        var provider = new Mock<IEmbeddingProvider>();
        provider.Setup(p => p.ProviderName).Returns(name);
        provider.Setup(p => p.ModelName).Returns("test-model");
        provider.Setup(p => p.Dimensions).Returns(768);
        return provider;
    }

    /// <summary>
    /// Moq does not invoke default interface implementations, so the purpose overload has to be
    /// set up explicitly — the same caveat the sibling <c>EmbeddingServiceTests</c> records for
    /// the language overload.
    /// </summary>
    private static void SetupPurposeOverload(
        Mock<IEmbeddingProvider> provider,
        Action<EmbeddingPurpose> record,
        EmbeddingProviderResult result)
    {
        provider
            .Setup(p => p.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<EmbeddingPurpose>(),
                It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<string> _, string _, EmbeddingPurpose purpose, CancellationToken _) => record(purpose))
            .ReturnsAsync(result);
    }

    private static EmbeddingService CreateService(
        Mock<IEmbeddingProvider> primary,
        Mock<IEmbeddingProvider>? fallback)
    {
        var factory = new Mock<IEmbeddingProviderFactory>();
        factory.Setup(f => f.GetPrimaryProvider()).Returns(primary.Object);
        factory.Setup(f => f.GetFallbackProvider()).Returns(fallback?.Object);

        var config = new EmbeddingConfiguration
        {
            Provider = EmbeddingProviderType.OllamaNomic,
            EnableFallback = true,
            BatchSize = 10
        };

        // Il gate di #3737 è ACCESO qui: questi test misurano che il purpose sopravviva fino al
        // provider, incluso sulla gamba di fallback. Con l'interruttore spento ogni Query
        // arriverebbe come Passage e i test passerebbero misurando l'interruttore, non la
        // propagazione — che è coperta a parte da EmbeddingServiceQueryPrefixGateTests.
        var configuration = new Mock<IConfigurationService>();
        configuration
            .Setup(c => c.GetValueAsync<bool?>(
                EmbeddingService.E5QueryPrefixEnabledKey, It.IsAny<bool?>(), It.IsAny<string?>()))
            .ReturnsAsync(true);

        return new EmbeddingService(
            factory.Object,
            Options.Create(config),
            NullLogger<EmbeddingService>.Instance,
            configuration.Object);
    }
}
