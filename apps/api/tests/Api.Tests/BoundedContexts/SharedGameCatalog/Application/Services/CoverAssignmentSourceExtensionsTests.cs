using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Domain.Covers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Epic #3470 Slice 1c (item #1) — the domain-facing <see cref="CoverAssignmentSource"/>
/// and the storage-key <see cref="CoverKind"/> DO NOT share numeric values
/// (Source.Pdf=0 vs Kind.Pdf=1), so a naive <c>(CoverKind)(int)source</c> cast is
/// silently wrong. This pins the explicit, value-by-value mapping.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CoverAssignmentSourceExtensionsTests
{
    [Fact]
    public void ToCoverKind_MapsPdfToPdf()
    {
        CoverAssignmentSource.Pdf.ToCoverKind().Should().Be(CoverKind.Pdf);
    }

    [Fact]
    public void ToCoverKind_MapsBggToBgg()
    {
        CoverAssignmentSource.Bgg.ToCoverKind().Should().Be(CoverKind.Bgg);
    }

    [Fact]
    public void ToCoverKind_MapsWikidataToWikidata()
    {
        CoverAssignmentSource.Wikidata.ToCoverKind().Should().Be(CoverKind.Wikidata);
    }

    [Fact]
    public void ToCoverKind_MapsManualToManual()
    {
        CoverAssignmentSource.Manual.ToCoverKind().Should().Be(CoverKind.Manual);
    }

    [Fact]
    public void ToCoverKind_IsNotTheNaiveIntCast_ForPdf()
    {
        // The trap this mapping exists to prevent: Source.Pdf=0 and Kind.User=0,
        // so the numeric cast would resolve a PDF assignment to the user-custom key.
        ((int)CoverAssignmentSource.Pdf).Should().Be(0);
        ((int)CoverKind.Pdf).Should().Be(1);

        CoverAssignmentSource.Pdf.ToCoverKind().Should().Be(CoverKind.Pdf);
        CoverAssignmentSource.Pdf.ToCoverKind().Should().NotBe((CoverKind)(int)CoverAssignmentSource.Pdf);
    }

    [Fact]
    public void ToCoverKind_MapsEveryDefinedSource_WithoutThrowing()
    {
        // Exhaustiveness guard: a new CoverAssignmentSource value with no mapping
        // arm must fail this test (ArgumentOutOfRangeException) rather than slip
        // through as a silent miscast.
        foreach (CoverAssignmentSource source in Enum.GetValues<CoverAssignmentSource>())
        {
            var act = () => source.ToCoverKind();
            act.Should().NotThrow($"every defined CoverAssignmentSource must map to a CoverKind ({source})");
        }
    }
}
