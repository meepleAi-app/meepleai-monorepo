using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4166 (ADR-095 D6): il modello di riserva per il degrado è quello della versione pubblicata del
/// profilo agente, letto al più una volta al minuto.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4166")]
public sealed class PublishedProfileReserveModelProviderTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.Zero));
    private readonly Mock<IAgentProfileRepository> _profiles = new();
    private readonly ServiceProvider _services;
    private readonly PublishedProfileReserveModelProvider _sut;

    public PublishedProfileReserveModelProviderTests()
    {
        _profiles.Setup(p => p.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentProfile.CreateWithPublishedVersion(AgentProfileTests.Content()));
        _services = new ServiceCollection().AddScoped(_ => _profiles.Object).BuildServiceProvider();
        _sut = new PublishedProfileReserveModelProvider(
            _services.GetRequiredService<IServiceScopeFactory>(), _time, NullLogger<PublishedProfileReserveModelProvider>.Instance);
    }

    public void Dispose() => _services.Dispose();

    private Task<string?> ReserveAsync() => _sut.GetReserveModelAsync(TestContext.Current.CancellationToken);

    private void ProfileReads(int times) =>
        _profiles.Verify(p => p.GetAsync(It.IsAny<CancellationToken>()), Times.Exactly(times));

    [Fact]
    public async Task TheReserve_IsTheFallbackModelOfThePublishedVersion()
    {
        (await ReserveAsync()).Should().Be("meta-llama/llama-3.3-70b-instruct:free");
    }

    [Fact]
    public async Task TheProfile_IsReadAtMostOnceAMinute()
    {
        await ReserveAsync();
        await ReserveAsync();
        ProfileReads(1);

        _time.Advance(PublishedProfileReserveModelProvider.CacheDuration);
        await ReserveAsync();

        ProfileReads(2);
    }

    [Fact]
    public async Task WithoutAProfile_ThereIsNoReserve()
    {
        _profiles.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile?)null);

        (await ReserveAsync()).Should().BeNull();
    }

    /// <summary>
    /// In degrado il gateway chiede la riserva a ogni chiamata: con il database giù, l'ultima riserva
    /// letta resta valida e la lettura si ritenta al minuto successivo, non a ogni chiamata.
    /// </summary>
    [Fact]
    public async Task AFailedRead_KeepsTheLastReserve_AndIsNotRepeatedByEveryCall()
    {
        await ReserveAsync();
        _profiles.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("database down"));
        _time.Advance(PublishedProfileReserveModelProvider.CacheDuration);

        (await ReserveAsync()).Should().Be("meta-llama/llama-3.3-70b-instruct:free");
        await ReserveAsync();
        await ReserveAsync();
        ProfileReads(2);

        _time.Advance(PublishedProfileReserveModelProvider.CacheDuration);
        await ReserveAsync();
        ProfileReads(3);
    }
}
