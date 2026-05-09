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
/// Canary test for Issue #886: catches EF Core model-finalization failures (broken value
/// converters, primitive-collection conflicts, mis-configured entity mappings) at a fast,
/// dedicated entry point — before they cascade into hundreds of seemingly-unrelated handler
/// tests with confusing stack traces.
///
/// Background: in #886 a single broken HasConversion in TranslatedParagraphConfiguration
/// caused ~855 unit tests to fail because every test that accessed a DbSet triggered model
/// finalization, which threw ArgumentException. The signal-to-noise was destroyed and the
/// real culprit was buried.
///
/// This test runs in &lt;100ms and asserts the model finalizes cleanly. If it fails, the
/// failure message points directly at the misconfigured property — no need to bisect 855
/// failures to find the one that matters.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "886")]
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
}
