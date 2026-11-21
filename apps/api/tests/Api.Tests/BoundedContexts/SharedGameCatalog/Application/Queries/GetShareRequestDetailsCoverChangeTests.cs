using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// #5084: the review of a cover change showed neither the proposed cover nor the current one, so the admin approved or
/// rejected it blind. The details now carry both, presigned like the other covers from a PDF.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetShareRequestDetailsCoverChangeTests : IDisposable
{
    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ACoverChange_CarriesTheProposedCover_TheCurrentOne_AndThePage()
    {
        const string pending = "covers/game/pdf-cover-proposed";
        const string current = "covers/pdf/current/cover";
        SeedGame(pdfCoverKey: current);
        var requestId = SeedRequest(ContributionType.CoverChange, pendingKey: pending, coverPageIndex: 2);
        AnswerPresigned(pending, "https://r2.test/proposed");
        AnswerPresigned(current, "https://r2.test/current");

        var details = await Handler().Handle(new GetShareRequestDetailsQuery(requestId, _userId), TestContext.Current.CancellationToken);

        details.CoverChange.Should().NotBeNull();
        details.CoverChange!.ProposedCoverUrl.Should().Be("https://r2.test/proposed");
        details.CoverChange.CurrentCoverUrl.Should().Be("https://r2.test/current");
        details.CoverChange.ProposedFromPage.Should().Be(3, "the stored index is 0-based, the page shown is 1-based");
    }

    [Fact]
    public async Task TheCurrentCover_IsTheOneTheGamePageShows_AnAdminPinnedCoverIncluded()
    {
        // The game has a cover from a PDF, but the admin pinned another for the game page (Hero): the proposal would
        // replace that one, and the context-blind resolver would have shown the PDF one.
        const string pdfCover = "covers/pdf/current/cover";
        const string heroCrop = "covers/hero/pinned.webp";
        SeedGame(pdfCoverKey: pdfCover);
        _db.Set<GameCoverAssignmentEntity>().Add(new GameCoverAssignmentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = _gameId,
            Context = CoverContext.Hero,
            Source = CoverAssignmentSource.Bgg,
            GeneratedR2Key = heroCrop,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _userId,
        });
        _db.SaveChanges();
        var requestId = SeedRequest(ContributionType.CoverChange, pendingKey: "covers/game/pdf-cover-p", coverPageIndex: 0);
        AnswerPresigned(pdfCover, "https://r2.test/pdf");
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync(heroCrop, CoverUrlResolver.CoverPresignExpirySeconds))
            .ReturnsAsync("https://r2.test/pinned");

        var details = await Handler().Handle(new GetShareRequestDetailsQuery(requestId, _userId), TestContext.Current.CancellationToken);

        details.CoverChange!.CurrentCoverUrl.Should().Be("https://r2.test/pinned");
    }

    [Fact]
    public async Task ACoverChange_WhoseKeyTheStorageCannotServe_HasNoProposedUrl()
    {
        // The local storage holds no raw-key objects: no URL, rather than a broken one.
        SeedGame(pdfCoverKey: null);
        var requestId = SeedRequest(ContributionType.CoverChange, pendingKey: "covers/game/pdf-cover-x", coverPageIndex: 0);

        var details = await Handler().Handle(new GetShareRequestDetailsQuery(requestId, _userId), TestContext.Current.CancellationToken);

        details.CoverChange.Should().NotBeNull();
        details.CoverChange!.ProposedCoverUrl.Should().BeNull();
        details.CoverChange.ProposedFromPage.Should().Be(1);
    }

    [Fact]
    public async Task AnotherContribution_HasNoCoverChange()
    {
        SeedGame(pdfCoverKey: "covers/pdf/current/cover");
        var requestId = SeedRequest(ContributionType.AdditionalContent, pendingKey: null, coverPageIndex: null);

        var details = await Handler().Handle(new GetShareRequestDetailsQuery(requestId, _userId), TestContext.Current.CancellationToken);

        details.CoverChange.Should().BeNull();
        _blob.Verify(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    private GetShareRequestDetailsQueryHandler Handler() =>
        new(_db, _blob.Object, NullLogger<GetShareRequestDetailsQueryHandler>.Instance);

    private void AnswerPresigned(string dbKey, string url) =>
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync(
                CoverKeyBuilder.PhysicalKeyFor(CoverKind.Pdf, dbKey), CoverUrlResolver.CoverPresignExpirySeconds))
            .ReturnsAsync(url);

    private void SeedGame(string? pdfCoverKey)
    {
        _db.Users.Add(new UserEntity { Id = _userId, Email = $"u-{_userId:N}@test.local", DisplayName = "Contributor" });
        _db.Set<SharedGameEntity>().Add(new SharedGameEntity { Id = _gameId, Title = "Tile Kingdoms", PdfCoverR2Key = pdfCoverKey });
        _db.SaveChanges();
    }

    private Guid SeedRequest(ContributionType type, string? pendingKey, int? coverPageIndex)
    {
        var id = Guid.NewGuid();
        _db.Set<ShareRequestEntity>().Add(new ShareRequestEntity
        {
            Id = id,
            UserId = _userId,
            SourceGameId = _gameId,
            TargetSharedGameId = _gameId,
            Status = (int)ShareRequestStatus.Pending,
            ContributionType = (int)type,
            PendingCoverR2Key = pendingKey,
            CoverPageIndex = coverPageIndex,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _userId,
        });
        _db.SaveChanges();
        return id;
    }
}
