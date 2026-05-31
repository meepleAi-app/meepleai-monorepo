using Api.BoundedContexts.KnowledgeBase.Application.Queries.GlobalKbSearch;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GlobalKbSearchQueryHandler.
/// TDD coverage: RBAC filtering, enrichment join, pagination cursor, hasMore detection.
/// Issue #1661: cross-game KB search (Task 4).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GlobalKbSearchQueryHandlerTests : IDisposable
{
    private readonly Mock<IRagAccessService> _ragAccessMock = new(MockBehavior.Strict);
    private readonly Mock<IMultiGameHybridSearchService> _searchMock = new(MockBehavior.Strict);
    private readonly MeepleAiDbContext _db;

    public GlobalKbSearchQueryHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
    }

    public void Dispose() => _db.Dispose();

    private GlobalKbSearchQueryHandler CreateSut() =>
        new(_ragAccessMock.Object, _searchMock.Object, _db,
            NullLogger<GlobalKbSearchQueryHandler>.Instance);

    // ─── EC-1: no accessible games → empty, search never called ────────────────

    [Fact]
    public async Task NoAccessibleGames_ReturnsEmptyResponse_NoCallToSearch()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var query = BuildQuery(userId, "catan rules", limit: 10);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Results.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.NextCursor.Should().BeNull();
        _searchMock.VerifyNoOtherCalls();
    }

    // ─── RBAC: accessible games passed to search ────────────────────────────────

    [Fact]
    public async Task AccessibleGames_CallsSearchWithThoseGameIds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var accessibleIds = new[] { gameId1, gameId2 };

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleIds);

        _searchMock
            .Setup(s => s.SearchAsync(
                "catan rules",
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<int>(),
                SearchMode.Hybrid,
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MultiGameSearchResultItem>());

        var query = BuildQuery(userId, "catan rules", limit: 10);
        var sut = CreateSut();

        // Act
        await sut.Handle(query, CancellationToken.None);

        // Assert: search was called with exactly the accessible game IDs
        _searchMock.Verify(
            s => s.SearchAsync(
                "catan rules",
                It.Is<IReadOnlyList<Guid>>(ids =>
                    ids.Count == 2 && ids.Contains(gameId1) && ids.Contains(gameId2)),
                It.IsAny<int>(),
                SearchMode.Hybrid,
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Enrichment: docTitle, gameName, docType populated from EF join ─────────

    [Fact]
    public async Task EnrichmentPopulatesDocTitleGameNameDocType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfDocId = Guid.NewGuid();
        var vectorDocId = Guid.NewGuid();

        // Seed EF in-memory data
        var sharedGame = new SharedGameEntity
        {
            Id = gameId,
            Title = "Catan",
            IsDeleted = false,
            IsRagPublic = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var pdfDoc = new PdfDocumentEntity
        {
            Id = pdfDocId,
            FileName = "catan-rulebook.pdf",
            FilePath = "/files/catan-rulebook.pdf",
            UploadedByUserId = userId,
            DocumentType = "base",
            SharedGameId = gameId
        };
        var vectorDoc = new VectorDocumentEntity
        {
            Id = vectorDocId,
            PdfDocumentId = pdfDocId,
            SharedGameId = gameId,
            GameId = gameId,
            IndexingStatus = "completed"
        };

        _db.SharedGames.Add(sharedGame);
        _db.PdfDocuments.Add(pdfDoc);
        _db.VectorDocuments.Add(vectorDoc);
        await _db.SaveChangesAsync();

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        var searchResult = BuildSearchResult(gameId, vectorDocId.ToString(), pdfDocId.ToString(), chunkIndex: 0, score: 0.9f);
        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<int>(),
                It.IsAny<SearchMode>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MultiGameSearchResultItem> { searchResult });

        var query = BuildQuery(userId, "test", limit: 10);
        var sut = CreateSut();

        // Act
        var response = await sut.Handle(query, CancellationToken.None);

        // Assert
        response.Results.Should().ContainSingle();
        var item = response.Results[0];
        item.DocTitle.Should().Be("catan-rulebook.pdf");
        item.GameName.Should().Be("Catan");
        item.DocType.Should().Be("base");
        item.DocId.Should().Be(pdfDocId);
        item.GameId.Should().Be(gameId);
        item.Score.Should().BeApproximately(0.9f, 0.001f);
    }

    // ─── Missing enrichment row (race with delete) → result skipped ─────────────

    [Fact]
    public async Task MissingEnrichmentRow_ResultIsSkipped()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var orphanDocId = Guid.NewGuid(); // not in DB

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        // Search returns a result whose PdfDocumentId doesn't exist in DB
        var orphanResult = BuildSearchResult(gameId, orphanDocId.ToString(), orphanDocId.ToString(), chunkIndex: 0, score: 0.8f);
        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<int>(),
                It.IsAny<SearchMode>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MultiGameSearchResultItem> { orphanResult });

        var query = BuildQuery(userId, "test", limit: 10);
        var sut = CreateSut();

        // Act
        var response = await sut.Handle(query, CancellationToken.None);

        // Assert: missing enrichment row → result excluded (EC-2 indirizzato)
        response.Results.Should().BeEmpty();
        response.HasMore.Should().BeFalse();
    }

    // ─── hasMore detection: Limit+1 probe ───────────────────────────────────────

    [Fact]
    public async Task HasMoreDetectedViaLimitPlusOne()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        const int limit = 3;

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        // Seed limit+1 = 4 items in DB
        var pdfIds = Enumerable.Range(0, limit + 1).Select(_ => Guid.NewGuid()).ToList();
        var sharedGame = new SharedGameEntity
        {
            Id = gameId, Title = "HasMore Game", IsDeleted = false, IsRagPublic = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        _db.SharedGames.Add(sharedGame);
        foreach (var pdfId in pdfIds)
        {
            _db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId, FileName = $"doc-{pdfId}.pdf", FilePath = $"/f/{pdfId}",
                UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId
            });
            _db.VectorDocuments.Add(new VectorDocumentEntity
            {
                Id = pdfId, PdfDocumentId = pdfId, SharedGameId = gameId, GameId = gameId,
                IndexingStatus = "completed"
            });
        }
        await _db.SaveChangesAsync();

        // Search returns limit+1 results (handler calls Search with limit+1)
        var searchResults = pdfIds
            .Select((id, i) => BuildSearchResult(gameId, id.ToString(), id.ToString(), chunkIndex: i, score: 0.9f - i * 0.01f))
            .ToList();

        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                limit + 1, // handler must call with limit+1
                It.IsAny<SearchMode>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var query = BuildQuery(userId, "test", limit: limit);
        var sut = CreateSut();

        // Act
        var response = await sut.Handle(query, CancellationToken.None);

        // Assert
        response.Results.Should().HaveCount(limit);   // truncated to limit
        response.HasMore.Should().BeTrue();
    }

    // ─── No more results → nextCursor null ──────────────────────────────────────

    [Fact]
    public async Task NoMoreResults_NextCursorIsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        const int limit = 5;

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        // Fewer results than limit → no next page
        var pdfId = Guid.NewGuid();
        var sharedGame = new SharedGameEntity
        {
            Id = gameId, Title = "Game", IsDeleted = false, IsRagPublic = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        _db.SharedGames.Add(sharedGame);
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId, FileName = "doc.pdf", FilePath = "/f/doc",
            UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId
        });
        _db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = pdfId, PdfDocumentId = pdfId, SharedGameId = gameId, GameId = gameId,
            IndexingStatus = "completed"
        });
        await _db.SaveChangesAsync();

        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<int>(),
                It.IsAny<SearchMode>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MultiGameSearchResultItem>
            {
                BuildSearchResult(gameId, pdfId.ToString(), pdfId.ToString(), chunkIndex: 0, score: 0.8f)
            });

        var query = BuildQuery(userId, "test", limit: limit);
        var sut = CreateSut();

        // Act
        var response = await sut.Handle(query, CancellationToken.None);

        // Assert
        response.HasMore.Should().BeFalse();
        response.NextCursor.Should().BeNull();
    }

    // ─── hasMore → nextCursor encodes last score and chunkId ────────────────────

    [Fact]
    public async Task WithMoreResults_NextCursorEncodesLastScoreAndChunkId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        const int limit = 2;

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        var pdfIds = Enumerable.Range(0, limit + 1).Select(_ => Guid.NewGuid()).ToList();
        var sharedGame = new SharedGameEntity
        {
            Id = gameId, Title = "Cursor Game", IsDeleted = false, IsRagPublic = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        _db.SharedGames.Add(sharedGame);
        foreach (var id in pdfIds)
        {
            _db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = id, FileName = $"{id}.pdf", FilePath = $"/f/{id}",
                UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId
            });
            _db.VectorDocuments.Add(new VectorDocumentEntity
            {
                Id = id, PdfDocumentId = id, SharedGameId = gameId, GameId = gameId,
                IndexingStatus = "completed"
            });
        }
        await _db.SaveChangesAsync();

        // Scores descending so second item (index 1) is the last returned
        var results = pdfIds
            .Select((id, i) => BuildSearchResult(gameId, id.ToString(), id.ToString(), chunkIndex: i, score: 0.9f - i * 0.1f))
            .ToList();

        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(), limit + 1,
                It.IsAny<SearchMode>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var query = BuildQuery(userId, "test", limit: limit);
        var sut = CreateSut();

        // Act
        var response = await sut.Handle(query, CancellationToken.None);

        // Assert: cursor present, decodable as base64 with expected format
        response.HasMore.Should().BeTrue();
        response.NextCursor.Should().NotBeNullOrEmpty();

        // Decode cursor: base64("{score}|{chunkId}")
        var decoded = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(response.NextCursor!));
        decoded.Should().Contain("|");
        var parts = decoded.Split('|');
        parts.Should().HaveCount(2);
        parts[1].Should().Be(results[limit - 1].ChunkId); // last item in page
    }

    // ─── EC-5: RBAC leak prevention ─────────────────────────────────────────────

    [Fact]
    public async Task RbacLeakPrevention_SearchCalledOnlyWithAccessibleGameIds()
    {
        // Arrange: accessible = [A, B]; a rogue C would be injected only if search sees it
        var userId = Guid.NewGuid();
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();
        var gameC = Guid.NewGuid(); // NOT accessible

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameA, gameB });

        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<int>(),
                It.IsAny<SearchMode>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MultiGameSearchResultItem>());

        var query = BuildQuery(userId, "leak test", limit: 10);
        var sut = CreateSut();

        // Act
        await sut.Handle(query, CancellationToken.None);

        // Assert: search NEVER called with gameC
        _searchMock.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(),
                It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(gameC)),
                It.IsAny<int>(),
                It.IsAny<SearchMode>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert: search called with exactly [A, B]
        _searchMock.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(),
                It.Is<IReadOnlyList<Guid>>(ids =>
                    ids.Count == 2 && ids.Contains(gameA) && ids.Contains(gameB)),
                It.IsAny<int>(),
                It.IsAny<SearchMode>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Validator: empty query string rejected ──────────────────────────────────

    [Fact]
    public async Task EmptyQueryString_ValidatorRejects()
    {
        // Arrange
        var validator = new GlobalKbSearchQueryValidator();
        var query = BuildQuery(Guid.NewGuid(), "", limit: 10);

        // Act
        var result = await validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Query");
    }

    // ─── Validator: limit > hard cap rejected ────────────────────────────────────

    [Fact]
    public async Task LimitExceedsHardCap_ValidatorRejects()
    {
        // Arrange
        var validator = new GlobalKbSearchQueryValidator();
        var query = BuildQuery(Guid.NewGuid(), "test query", limit: 51); // hard cap = 50

        // Act
        var result = await validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Limit");
    }

    // ─── Validator: limit = 0 rejected ───────────────────────────────────────────

    [Fact]
    public async Task LimitZero_ValidatorRejects()
    {
        // Arrange
        var validator = new GlobalKbSearchQueryValidator();
        var query = BuildQuery(Guid.NewGuid(), "test query", limit: 0);

        // Act
        var result = await validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Limit");
    }

    // ─── headingPath is null best-effort (EC-6) ──────────────────────────────────

    [Fact]
    public async Task HeadingPath_IsNullBestEffort()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        var sharedGame = new SharedGameEntity
        {
            Id = gameId, Title = "Game EC-6", IsDeleted = false, IsRagPublic = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        _db.SharedGames.Add(sharedGame);
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId, FileName = "doc.pdf", FilePath = "/f/doc",
            UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId
        });
        _db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = pdfId, PdfDocumentId = pdfId, SharedGameId = gameId, GameId = gameId,
            IndexingStatus = "completed"
        });
        await _db.SaveChangesAsync();

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<int>(),
                It.IsAny<SearchMode>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MultiGameSearchResultItem>
            {
                BuildSearchResult(gameId, pdfId.ToString(), pdfId.ToString(), chunkIndex: 0, score: 0.8f)
            });

        var query = BuildQuery(userId, "test", limit: 5);
        var sut = CreateSut();

        // Act
        var response = await sut.Handle(query, CancellationToken.None);

        // Assert: headingPath is null (best-effort, not materialised in vector result)
        response.Results.Should().ContainSingle();
        response.Results[0].HeadingPath.Should().BeNull();
    }

    // ─── Issue #1686 Task 5: GameId facet intersects with RBAC ──────────────────

    [Fact]
    public async Task GameId_InAccessibleSet_NarrowsSearchToThatGameOnly()
    {
        // Arrange — accessible = [A, B, C]; request GameId = B → only B is searched
        var userId = Guid.NewGuid();
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();
        var gameC = Guid.NewGuid();

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameA, gameB, gameC });

        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<int>(),
                It.IsAny<SearchMode>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MultiGameSearchResultItem>());

        var query = BuildQuery(userId, "test", limit: 10, gameId: gameB);
        var sut = CreateSut();

        // Act
        await sut.Handle(query, CancellationToken.None);

        // Assert — search called with ONLY gameB (intersection of accessible and requested)
        _searchMock.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(),
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids.Contains(gameB)),
                It.IsAny<int>(),
                It.IsAny<SearchMode>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GameId_NotInAccessibleSet_ReturnsEmpty_NoCallToSearch()
    {
        // Arrange — accessible = [A, B]; request GameId = rogueC → 200 empty, NOT 403
        var userId = Guid.NewGuid();
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();
        var rogueC = Guid.NewGuid();

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameA, gameB });

        var query = BuildQuery(userId, "test", limit: 10, gameId: rogueC);
        var sut = CreateSut();

        // Act
        var response = await sut.Handle(query, CancellationToken.None);

        // Assert — empty response, search NEVER called (RBAC-safe: no info leak D-5)
        response.Results.Should().BeEmpty();
        response.HasMore.Should().BeFalse();
        response.NextCursor.Should().BeNull();
        _searchMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GameId_Null_KeepsAllAccessibleGames_BackwardCompatible()
    {
        // Arrange — accessible = [A, B]; gameId = null → search receives all accessible
        var userId = Guid.NewGuid();
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameA, gameB });

        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<int>(),
                It.IsAny<SearchMode>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MultiGameSearchResultItem>());

        // Default BuildQuery has gameId = null
        var query = BuildQuery(userId, "test", limit: 10);
        var sut = CreateSut();

        // Act
        await sut.Handle(query, CancellationToken.None);

        // Assert — search receives both accessible games (legacy D-3 behaviour)
        _searchMock.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(),
                It.Is<IReadOnlyList<Guid>>(ids =>
                    ids.Count == 2 && ids.Contains(gameA) && ids.Contains(gameB)),
                It.IsAny<int>(),
                It.IsAny<SearchMode>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Issue #1686 Task 6: DocType + Language push-down via EF pre-filter ────

    [Fact]
    public async Task DocType_FacetFiltersPdfDocsBeforeSearch()
    {
        // Arrange — 3 PDFs in one game: 2 "base", 1 "expansion"; request DocType=["base"]
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var baseDoc1 = Guid.NewGuid();
        var baseDoc2 = Guid.NewGuid();
        var expansionDoc = Guid.NewGuid();

        var sharedGame = new SharedGameEntity
        {
            Id = gameId, Title = "DocType Facet Game",
            IsDeleted = false, IsRagPublic = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        _db.SharedGames.Add(sharedGame);
        _db.PdfDocuments.AddRange(
            new PdfDocumentEntity { Id = baseDoc1, FileName = "b1.pdf", FilePath = "/f/b1", UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId, Language = "en" },
            new PdfDocumentEntity { Id = baseDoc2, FileName = "b2.pdf", FilePath = "/f/b2", UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId, Language = "en" },
            new PdfDocumentEntity { Id = expansionDoc, FileName = "e.pdf", FilePath = "/f/e", UploadedByUserId = userId, DocumentType = "expansion", SharedGameId = gameId, Language = "en" }
        );
        await _db.SaveChangesAsync();

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        IReadOnlyList<Guid>? capturedDocIds = null;
        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<int>(),
                It.IsAny<SearchMode>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<Guid>, int, SearchMode, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, _, _, _, _, docIds, _) => capturedDocIds = docIds)
            .ReturnsAsync(new List<MultiGameSearchResultItem>());

        var query = BuildQuery(userId, "test", limit: 10, docType: new[] { "base" });
        var sut = CreateSut();

        // Act
        await sut.Handle(query, CancellationToken.None);

        // Assert — only the 2 "base" PDF IDs reached the search (NOT the expansion)
        capturedDocIds.Should().NotBeNull();
        capturedDocIds!.Should().HaveCount(2);
        capturedDocIds.Should().Contain(baseDoc1).And.Contain(baseDoc2);
        capturedDocIds.Should().NotContain(expansionDoc);
    }

    [Fact]
    public async Task Language_FacetFiltersPdfDocsBeforeSearch()
    {
        // Arrange — 3 PDFs: 2 "en", 1 "it"; request Language="en"
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var enDoc1 = Guid.NewGuid();
        var enDoc2 = Guid.NewGuid();
        var itDoc = Guid.NewGuid();

        var sharedGame = new SharedGameEntity
        {
            Id = gameId, Title = "Lang Facet Game",
            IsDeleted = false, IsRagPublic = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        _db.SharedGames.Add(sharedGame);
        _db.PdfDocuments.AddRange(
            new PdfDocumentEntity { Id = enDoc1, FileName = "e1.pdf", FilePath = "/f/e1", UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId, Language = "en" },
            new PdfDocumentEntity { Id = enDoc2, FileName = "e2.pdf", FilePath = "/f/e2", UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId, Language = "en" },
            new PdfDocumentEntity { Id = itDoc, FileName = "i.pdf", FilePath = "/f/i", UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId, Language = "it" }
        );
        await _db.SaveChangesAsync();

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        IReadOnlyList<Guid>? capturedDocIds = null;
        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<int>(),
                It.IsAny<SearchMode>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<Guid>, int, SearchMode, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, _, _, _, _, docIds, _) => capturedDocIds = docIds)
            .ReturnsAsync(new List<MultiGameSearchResultItem>());

        var query = BuildQuery(userId, "test", limit: 10, language: "en");
        var sut = CreateSut();

        // Act
        await sut.Handle(query, CancellationToken.None);

        // Assert — only the 2 "en" PDF IDs reached the search
        capturedDocIds.Should().NotBeNull();
        capturedDocIds!.Should().HaveCount(2);
        capturedDocIds.Should().Contain(enDoc1).And.Contain(enDoc2);
        capturedDocIds.Should().NotContain(itDoc);
    }

    [Fact]
    public async Task DocTypeAndLanguage_BothFacetsAppliedAsAnd()
    {
        // Arrange — 4 PDFs: (base,en), (base,it), (expansion,en), (expansion,it)
        // Request DocType=["base"] AND Language="en" → only first PDF survives
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var match = Guid.NewGuid();
        var baseIt = Guid.NewGuid();
        var expEn = Guid.NewGuid();
        var expIt = Guid.NewGuid();

        var sharedGame = new SharedGameEntity
        {
            Id = gameId, Title = "Combined Facet Game",
            IsDeleted = false, IsRagPublic = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        _db.SharedGames.Add(sharedGame);
        _db.PdfDocuments.AddRange(
            new PdfDocumentEntity { Id = match, FileName = "m.pdf", FilePath = "/f/m", UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId, Language = "en" },
            new PdfDocumentEntity { Id = baseIt, FileName = "bi.pdf", FilePath = "/f/bi", UploadedByUserId = userId, DocumentType = "base", SharedGameId = gameId, Language = "it" },
            new PdfDocumentEntity { Id = expEn, FileName = "ee.pdf", FilePath = "/f/ee", UploadedByUserId = userId, DocumentType = "expansion", SharedGameId = gameId, Language = "en" },
            new PdfDocumentEntity { Id = expIt, FileName = "ei.pdf", FilePath = "/f/ei", UploadedByUserId = userId, DocumentType = "expansion", SharedGameId = gameId, Language = "it" }
        );
        await _db.SaveChangesAsync();

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        IReadOnlyList<Guid>? capturedDocIds = null;
        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<int>(),
                It.IsAny<SearchMode>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<Guid>, int, SearchMode, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, _, _, _, _, docIds, _) => capturedDocIds = docIds)
            .ReturnsAsync(new List<MultiGameSearchResultItem>());

        var query = BuildQuery(userId, "test", limit: 10, docType: new[] { "base" }, language: "en");
        var sut = CreateSut();

        // Act
        await sut.Handle(query, CancellationToken.None);

        // Assert — exactly one PDF matches both filters (D-4 AND semantics)
        capturedDocIds.Should().NotBeNull();
        capturedDocIds!.Should().ContainSingle().Which.Should().Be(match);
    }

    [Fact]
    public async Task NoFacets_PassesNullDocumentIdsToSearch_BackwardCompatible()
    {
        // Arrange — when no facets are present, search receives documentIds=null (legacy D-3)
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _ragAccessMock
            .Setup(s => s.GetAccessibleGameIdsAsync(userId, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { gameId });

        IReadOnlyList<Guid>? capturedDocIds = new[] { Guid.NewGuid() }; // sentinel - must be overwritten to null
        _searchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<int>(),
                It.IsAny<SearchMode>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<Guid>, int, SearchMode, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, _, _, _, _, docIds, _) => capturedDocIds = docIds)
            .ReturnsAsync(new List<MultiGameSearchResultItem>());

        // Default BuildQuery has docType = null, language = null
        var query = BuildQuery(userId, "test", limit: 10);
        var sut = CreateSut();

        // Act
        await sut.Handle(query, CancellationToken.None);

        // Assert — search received null documentIds (legacy path D-3)
        capturedDocIds.Should().BeNull();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static GlobalKbSearchQuery BuildQuery(
        Guid userId,
        string query,
        int limit = 10,
        string? cursor = null,
        SearchMode mode = SearchMode.Hybrid,
        IReadOnlyList<string>? docType = null,
        Guid? gameId = null,
        string? language = null) =>
        new(
            Query: query,
            Limit: limit,
            Cursor: cursor,
            Mode: mode,
            MinScore: 0.0,
            UserId: userId,
            Role: UserRole.User,
            DocType: docType,
            GameId: gameId,
            Language: language);

    private static MultiGameSearchResultItem BuildSearchResult(
        Guid gameId,
        string chunkId,
        string pdfDocumentId,
        int chunkIndex,
        float score) =>
        new()
        {
            GameId = gameId,
            ChunkId = chunkId,
            PdfDocumentId = pdfDocumentId,
            ChunkIndex = chunkIndex,
            PageNumber = chunkIndex + 1,
            Content = $"Content for chunk {chunkId}",
            HybridScore = score,
            Mode = SearchMode.Hybrid
        };
}
