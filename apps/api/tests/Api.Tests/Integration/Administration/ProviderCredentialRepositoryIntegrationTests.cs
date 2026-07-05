using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for <see cref="ProviderCredentialRepository"/> against a real PostgreSQL
/// backend. Covers two bugs found in the #1859 provider-credential store:
///
/// 1. LINQ-translation: filtering on <c>c.ProviderName.Value</c> (a property of the
///    <see cref="ProviderName"/> value object mapped via a HasConversion converter) is NOT
///    translatable to SQL and threw <see cref="InvalidOperationException"/>
///    ("could not be translated") on every call — even on an empty table. That exception
///    propagated out of <c>ProviderCredentialResolver</c> before the env-var fallback, so the
///    LLM provider health check marked every provider unhealthy and RAG chat returned empty
///    answers. Fixed by comparing the whole value object (<c>c.ProviderName == target</c>).
///
/// 2. row_version NOT NULL on INSERT: <c>.IsRowVersion()</c> makes Npgsql treat row_version as
///    store-generated and omit it from the INSERT, so a NOT NULL bytea column raised a 23502
///    violation on the first credential INSERT. Fixed by making the column nullable.
///
/// A unit/mock test cannot catch either bug: an in-memory/mocked repository never translates the
/// query to SQL nor exercises the NOT NULL constraint. Only a real relational provider does.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class ProviderCredentialRepositoryIntegrationTests
    : IntegrationTestBase<IProviderCredentialRepository>
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);

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
    public async Task GetActiveAsync_WithActiveRow_InsertsAndReturnsThatRow()
    {
        // Also covers the row_version NOT-NULL-on-INSERT regression: AddAsync + SaveChangesAsync
        // must succeed (nullable column lets Npgsql omit the store-generated token).
        await ResetDatabaseAsync();
        var credential = ProviderCredential.Create(
            ProviderName.Create("openrouter"),
            "encrypted-ciphertext",
            KeyFingerprint.FromPlaintext("sk-xx-abcd1234"),
            Guid.NewGuid(),
            previousCredentialId: null,
            new FakeTimeProvider(FixedNow));

        await Repository.AddAsync(credential, CancellationToken.None);
        await Repository.SaveChangesAsync(CancellationToken.None);

        var result = await Repository.GetActiveAsync("openrouter", CancellationToken.None);

        result.Should().NotBeNull();
        result!.ProviderName.Value.Should().Be("openrouter");
        result.IsActive.Should().BeTrue();
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
