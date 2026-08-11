using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.BackgroundJobs;
using Api.Observability;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.BackgroundJobs;

/// <summary>
/// #3383 (ADR-087 D4) — il gauge dead-letter deve essere DB-derivato per restare corretto sotto
/// <c>max()</c> a più di un'istanza. Questi test coprono il contratto del refresh periodico.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WikidataDeadLetterMetricsRefreshServiceTests
{
    private static (WikidataDeadLetterMetricsRefreshService Sut, Mock<IWikidataCoverEnrichmentAttemptRepository> Repo) Build()
    {
        var repo = new Mock<IWikidataCoverEnrichmentAttemptRepository>();
        var services = new ServiceCollection();
        services.AddScoped(_ => repo.Object);
        var provider = services.BuildServiceProvider();

        var sut = new WikidataDeadLetterMetricsRefreshService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WikidataDeadLetterMetricsRefreshService>.Instance);

        return (sut, repo);
    }

    [Fact]
    public async Task RefreshOnceAsync_PushesTheCountedValueIntoTheGauge()
    {
        var (sut, repo) = Build();
        repo.Setup(r => r.CountDeadLettersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(42);

        await sut.RefreshOnceAsync(CancellationToken.None);

        repo.Verify(r => r.CountDeadLettersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshOnceAsync_RepositoryThrows_PropagatesSoTheLoopCanLogAndRetry()
    {
        // Il metodo NON deve inghiottire qui: è il loop di ExecuteAsync a catturare, loggare e
        // ritentare al tick successivo (stesso contratto di ImpersonationMetricsRefreshService).
        var (sut, repo) = Build();
        repo.Setup(r => r.CountDeadLettersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = async () => await sut.RefreshOnceAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
