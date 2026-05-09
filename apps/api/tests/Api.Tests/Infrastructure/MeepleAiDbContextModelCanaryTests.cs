using System.Reflection;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Canary tests for EF Core model finalization. Catches broken value converters,
/// primitive-collection conflicts, and entity mis-mappings at a fast, dedicated
/// entry point — before they cascade into hundreds of unrelated handler tests.
///
/// <para>
/// <b>What this canary catches:</b>
/// <list type="bullet">
///   <item>ArgumentException at <c>ProcessModelFinalizing</c> from incompatible
///         value converters (e.g. Issue #886: HasConversion vs primitive-collection
///         element converter on <c>IReadOnlyList&lt;T&gt;</c>).</item>
///   <item>Misconfigured entity property types that fail provider-agnostic
///         convention validation (FK conventions, key conventions, conversions
///         that are detected even by InMemory).</item>
///   <item>Any registered <c>DbSet</c> that throws on first access for the same
///         reason as above (the failure mode in Issue #886).</item>
/// </list>
/// </para>
/// <para>
/// <b>What this canary does NOT catch:</b>
/// <list type="bullet">
///   <item>Provider-specific type-mapping errors (Postgres-only converters,
///         pgvector mappings, JSON column mappings) — those surface only under
///         Npgsql. See <c>MeepleAiDbContextNpgsqlCanaryTests</c> for the
///         relational counterpart.</item>
///   <item>Schema/migration mismatches with the database — InMemory has no
///         schema concept. Caught by integration tests via
///         <c>EnsureCreated</c>/<c>Migrate</c>.</item>
///   <item>Entities mapped via separate persistence-proxy classes (e.g.
///         <c>GameStrategy</c> via <c>GameStrategyEntity</c>): the canary only
///         exercises the entities directly registered in <c>OnModelCreating</c>.
///         If a domain entity is later promoted to direct mapping, the canary
///         coverage extends to it automatically via the reflection-based test.</item>
/// </list>
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "886")]
[Trait("Issue", "889")]
public sealed class MeepleAiDbContextModelCanaryTests
{
    private static MeepleAiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"model-canary-{Guid.NewGuid()}")
            .Options;

        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    [Fact]
    public void Model_Finalizes_Without_Exception()
    {
        using var context = CreateContext();

        // Touching .Model triggers OnModelCreating + ProcessModelFinalizing.
        // Broken value converters surface here, not at the first DbSet access deep in
        // application code — fast feedback for entity-configuration mistakes.
        Action accessModel = () => _ = context.Model;

        accessModel.Should().NotThrow(
            because: "model finalization must succeed; a broken HasConversion or value converter "
                   + "composition (Issue #886) would manifest as ArgumentException here");
    }

    [Fact]
    public void DbSet_Access_Does_Not_Throw()
    {
        using var context = CreateContext();

        // Concrete reproduction of the Issue #886 failure mode: every handler test that
        // touches a DbSet triggered model finalization. If the model is broken, this throws.
        Action touchDbSet = () => _ = context.PdfDocuments.Local;

        touchDbSet.Should().NotThrow(
            because: "DbSet access must not bubble up model-finalization errors — those "
                   + "should be caught by Model_Finalizes_Without_Exception above");
    }

    /// <summary>
    /// Issue #889 (review concern on PR #888): the original two tests only exercised
    /// the <c>PdfDocuments</c> DbSet. A new entity registered with a broken converter
    /// could slip through unless its DbSet is actively touched. Reflection-based
    /// enumeration ensures every <c>DbSet&lt;T&gt;</c> property on the context is
    /// exercised, so adding a new DbSet automatically extends coverage with no test
    /// edit required.
    /// </summary>
    [Fact]
    public void Every_DbSet_Property_Resolves_Without_Exception()
    {
        using var context = CreateContext();

        var dbSetProps = typeof(MeepleAiDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType
                     && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToList();

        dbSetProps.Should().NotBeEmpty(
            because: "MeepleAiDbContext must expose at least one DbSet — otherwise the "
                   + "context is empty and the canary's coverage is meaningless");

        var failures = new List<string>();
        foreach (var prop in dbSetProps)
        {
            try
            {
                // Resolving a DbSet triggers model finalization for that entity. If the
                // entity (or any sibling registered together) has a broken converter,
                // this throws — and we collect the property name so the failure message
                // points to the actual culprit instead of a random unrelated test.
                var dbSet = prop.GetValue(context);
                dbSet.Should().NotBeNull();
            }
            catch (Exception ex)
            {
                failures.Add($"{prop.Name}: {ex.GetType().Name} — {ex.Message}");
            }
        }

        failures.Should().BeEmpty(
            because: "every DbSet property on MeepleAiDbContext must resolve cleanly; "
                   + "any broken value converter or entity mapping would surface here "
                   + "with the offending DbSet name in the failure list");
    }
}
