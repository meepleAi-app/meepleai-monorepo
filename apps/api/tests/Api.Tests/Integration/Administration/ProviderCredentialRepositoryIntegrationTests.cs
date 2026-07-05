using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for <see cref="ProviderCredentialRepository"/> against a real PostgreSQL
/// backend.
///
/// Regression coverage for the EF Core LINQ-translation bug: filtering on
/// <c>c.ProviderName.Value</c> (a property of the <see cref="Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials.ProviderName"/>
/// value object, mapped via a HasConversion value converter) is NOT translatable to SQL and threw
/// <see cref="InvalidOperationException"/> ("could not be translated") on every call — even on an
/// empty table. That exception propagated out of <c>ProviderCredentialResolver</c> BEFORE the
/// env-var fallback, so the periodic provider health check marked DeepSeek and OpenRouter
/// unhealthy every cycle and RAG chat produced empty answers.
///
/// The fix compares the whole value object (<c>c.ProviderName == target</c>), which the converter
/// translates to <c>provider_name = @p</c>. A unit/mock test cannot catch the original bug: an
/// in-memory/mocked repository never translates the query to SQL. Only a real relational provider
/// surfaces it — hence this Testcontainers test.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class ProviderCredentialRepositoryIntegrationTests
    : IntegrationTestBase<IProviderCredentialRepository>
{
    protected override string DatabaseName => "test_provider_credential_repo";

    protected override IProviderCredentialRepository CreateRepository(MeepleAiDbContext dbContext)
        => new ProviderCredentialRepository(dbContext);

    [Fact]
    public async Task GetActiveAsync_EmptyTable_ReturnsNull_AndDoesNotThrowLinqTranslation()
    {
        await ResetDatabaseAsync();

        var act = async () => await Repository.GetActiveAsync("deepseek", CancellationToken.None);

        var assertion = await act.Should().NotThrowAsync();
        assertion.Which.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveAsync_ProviderOutsideWhitelist_ReturnsNull()
    {
        await ResetDatabaseAsync();

        var result = await Repository.GetActiveAsync("ollama", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLastRotationAsync_EmptyTable_ReturnsNull_AndDoesNotThrowLinqTranslation()
    {
        await ResetDatabaseAsync();

        var act = async () => await Repository.GetLastRotationAsync("openrouter", CancellationToken.None);

        var assertion = await act.Should().NotThrowAsync();
        assertion.Which.Should().BeNull();
    }
}
