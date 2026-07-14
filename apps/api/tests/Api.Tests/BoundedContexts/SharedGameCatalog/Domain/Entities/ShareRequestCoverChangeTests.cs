using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Unit tests for ShareRequest.CreateCoverChange (cover-from-PDF proposal).
/// Task 4: ContributionType.CoverChange + pending cover fields.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ShareRequestCoverChangeTests
{
    [Fact]
    public void CreateCoverChange_WithValidData_SetsCoverChangeType()
    {
        var target = Guid.NewGuid();
        var pdf = Guid.NewGuid();
        var req = ShareRequest.CreateCoverChange(
            userId: Guid.NewGuid(), targetSharedGameId: target, sourcePdfDocumentId: pdf,
            pendingCoverR2Key: "covers/g/pdf-cover", coverPageIndex: 3, userNotes: null);

        req.ContributionType.Should().Be(ContributionType.CoverChange);
        req.TargetSharedGameId.Should().Be(target);
        req.PendingCoverR2Key.Should().Be("covers/g/pdf-cover");
        req.CoverPageIndex.Should().Be(3);
        req.SourcePdfDocumentId.Should().Be(pdf);
        req.Status.Should().Be(ShareRequestStatus.Pending);
    }

    [Fact]
    public void CreateCoverChange_WithEmptyPendingKey_Throws()
    {
        var act = () => ShareRequest.CreateCoverChange(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  ", 0, null);
        act.Should().Throw<ArgumentException>();
    }
}
