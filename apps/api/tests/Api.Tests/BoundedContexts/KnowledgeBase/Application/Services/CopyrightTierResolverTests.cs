using Api.SharedKernel.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Projections;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CopyrightTierResolverTests
{
    private readonly Mock<ICopyrightDataProjection> _projection = new();
    private readonly CopyrightTierResolver _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public CopyrightTierResolverTests()
    {
        _sut = new CopyrightTierResolver(_projection.Object);
    }

    private static ChunkCitation MakeCitation(string docId = "doc-1", int page = 1)
        => new(docId, page, 0.9f, "some text");

    private PdfCopyrightInfo MakeInfo(
        string docId = "doc-1",
        LicenseType license = LicenseType.Copyrighted,
        DocumentCategory category = DocumentCategory.Rulebook,
        Guid? uploadedBy = null,
        Guid? gameId = null,
        bool isPublic = false)
        => new(docId, license, category, uploadedBy ?? Guid.NewGuid(), gameId ?? _gameId, null, isPublic);

    private void SetupProjection(params PdfCopyrightInfo[] infos)
    {
        var dict = infos.ToDictionary(i => i.DocumentId, i => i)
            as IReadOnlyDictionary<string, PdfCopyrightInfo>;
        _projection
            .Setup(p => p.GetPdfCopyrightInfoAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dict!);
    }

    private void SetupOwnership(params (Guid gameId, bool owns)[] entries)
    {
        var dict = entries.ToDictionary(e => e.gameId, e => e.owns)
            as IReadOnlyDictionary<Guid, bool>;
        _projection
            .Setup(p => p.CheckOwnershipAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dict!);
    }

    // ── Test 1: CreativeCommons → Full regardless of ownership ──

    [Fact]
    public async Task ResolveAsync_CreativeCommons_ReturnsFull()
    {
        var info = MakeInfo(license: LicenseType.CreativeCommons, category: DocumentCategory.Rulebook);
        SetupProjection(info);
        SetupOwnership();

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);
    }

    // ── Test 2: PublicDomain → Full ──

    [Fact]
    public async Task ResolveAsync_PublicDomain_ReturnsFull()
    {
        var info = MakeInfo(license: LicenseType.PublicDomain, category: DocumentCategory.Rulebook);
        SetupProjection(info);
        SetupOwnership();

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);
    }

    // ── Test 3: Non-protected categories → Full ──

    [Theory]
    [InlineData(DocumentCategory.QuickStart)]
    [InlineData(DocumentCategory.Reference)]
    [InlineData(DocumentCategory.PlayerAid)]
    [InlineData(DocumentCategory.Other)]
    public async Task ResolveAsync_NonProtectedCategory_ReturnsFull(DocumentCategory category)
    {
        var info = MakeInfo(license: LicenseType.Copyrighted, category: category);
        SetupProjection(info);
        SetupOwnership();

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);
    }

    // ── Test 4: Protected category + owner + uploader → Full ──

    [Theory]
    [InlineData(DocumentCategory.Rulebook)]
    [InlineData(DocumentCategory.Expansion)]
    [InlineData(DocumentCategory.Errata)]
    public async Task ResolveAsync_ProtectedCategory_UploaderAndOwner_ReturnsFull(DocumentCategory category)
    {
        var info = MakeInfo(
            license: LicenseType.Copyrighted,
            category: category,
            uploadedBy: _userId,
            gameId: _gameId);
        SetupProjection(info);
        SetupOwnership((_gameId, true));

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);
    }

    // ── Test 5: Rulebook + not owned → Protected ──

    [Fact]
    public async Task ResolveAsync_Rulebook_NotOwned_ReturnsProtected()
    {
        var otherUser = Guid.NewGuid();
        var info = MakeInfo(
            license: LicenseType.Copyrighted,
            category: DocumentCategory.Rulebook,
            uploadedBy: otherUser,
            gameId: _gameId);
        SetupProjection(info);
        SetupOwnership((_gameId, false));

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
    }

    // ── Test 6: Rulebook + owned but NOT uploader → Protected ──

    [Fact]
    public async Task ResolveAsync_Rulebook_OwnerButNotUploader_ReturnsProtected()
    {
        var otherUploader = Guid.NewGuid();
        var info = MakeInfo(
            license: LicenseType.Copyrighted,
            category: DocumentCategory.Rulebook,
            uploadedBy: otherUploader,
            gameId: _gameId);
        SetupProjection(info);
        SetupOwnership((_gameId, true));

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
    }

    // ── Test 7: Rulebook + uploader but NOT owner → Protected ──

    [Fact]
    public async Task ResolveAsync_Rulebook_UploaderButNotOwner_ReturnsProtected()
    {
        var info = MakeInfo(
            license: LicenseType.Copyrighted,
            category: DocumentCategory.Rulebook,
            uploadedBy: _userId,
            gameId: _gameId);
        SetupProjection(info);
        SetupOwnership((_gameId, false));

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
    }

    // ── Test 8: Anonymous user (Guid.Empty) → always Protected ──

    [Fact]
    public async Task ResolveAsync_AnonymousUser_ReturnsProtected()
    {
        var info = MakeInfo(
            license: LicenseType.Copyrighted,
            category: DocumentCategory.Rulebook,
            uploadedBy: Guid.Empty,
            gameId: _gameId);
        SetupProjection(info);

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, Guid.Empty, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
        // Verify CheckOwnershipAsync was NOT called for anonymous
        _projection.Verify(
            p => p.CheckOwnershipAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Test 9: Unknown document (not in projection) → Protected ──

    [Fact]
    public async Task ResolveAsync_UnknownDocument_ReturnsProtected()
    {
        _projection
            .Setup(p => p.GetPdfCopyrightInfoAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, PdfCopyrightInfo>());

        var result = await _sut.ResolveAsync(new[] { MakeCitation("unknown-doc") }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
        Assert.False(result[0].IsPublic);
    }

    // ── Test 10: Multi-game chunks resolved independently ──

    [Fact]
    public async Task ResolveAsync_MultipleDocuments_ResolvedIndependently()
    {
        var gameId2 = Guid.NewGuid();

        var info1 = MakeInfo(
            docId: "doc-1",
            license: LicenseType.CreativeCommons,
            category: DocumentCategory.Rulebook,
            gameId: _gameId);

        var info2 = MakeInfo(
            docId: "doc-2",
            license: LicenseType.Copyrighted,
            category: DocumentCategory.Rulebook,
            uploadedBy: Guid.NewGuid(),
            gameId: gameId2);

        SetupProjection(info1, info2);
        SetupOwnership((_gameId, false), (gameId2, false));

        var citations = new[]
        {
            MakeCitation("doc-1"),
            MakeCitation("doc-2")
        };

        var result = await _sut.ResolveAsync(citations, _userId, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);      // CC license
        Assert.Equal(CopyrightTier.Protected, result[1].CopyrightTier); // Copyrighted rulebook, not owner
    }

    // ── Test 11: IsPublic propagated correctly ──

    [Fact]
    public async Task ResolveAsync_IsPublic_PropagatedFromProjection()
    {
        var info = MakeInfo(
            license: LicenseType.Copyrighted,
            category: DocumentCategory.Rulebook,
            isPublic: true);
        SetupProjection(info);
        SetupOwnership((_gameId, false));

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.True(result[0].IsPublic);
        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
    }

    [Fact]
    public async Task ResolveAsync_IsPublic_PropagatedForFullTier()
    {
        var info = MakeInfo(
            license: LicenseType.CreativeCommons,
            category: DocumentCategory.Rulebook,
            isPublic: true);
        SetupProjection(info);
        SetupOwnership();

        var result = await _sut.ResolveAsync(new[] { MakeCitation() }, _userId, CancellationToken.None);

        Assert.Single(result);
        Assert.True(result[0].IsPublic);
        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);
    }

    // ── Test 12: Empty citations list → returns empty ──

    [Fact]
    public async Task ResolveAsync_EmptyCitations_ReturnsEmpty()
    {
        var result = await _sut.ResolveAsync(Array.Empty<ChunkCitation>(), _userId, CancellationToken.None);

        Assert.Empty(result);
        // Verify projection was NOT called
        _projection.Verify(
            p => p.GetPdfCopyrightInfoAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
