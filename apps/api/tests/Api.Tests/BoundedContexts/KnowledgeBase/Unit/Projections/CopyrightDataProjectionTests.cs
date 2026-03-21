using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Projections;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Projections;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit.Projections;

/// <summary>
/// Unit tests for CopyrightDataProjection.
/// Validates cross-BC read-only projection for copyright tier resolution.
/// Uses InMemory DbContext following existing test patterns.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class CopyrightDataProjectionTests
{
    #region PdfCopyrightInfo Record Tests

    [Fact]
    public void PdfCopyrightInfo_Record_Stores_All_Fields()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var uploaderId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var info = new PdfCopyrightInfo(
            docId,
            LicenseType.CreativeCommons,
            DocumentCategory.Rulebook,
            uploaderId,
            gameId,
            null,
            true);

        // Assert
        info.DocumentId.Should().Be(docId);
        info.LicenseType.Should().Be(LicenseType.CreativeCommons);
        info.DocumentCategory.Should().Be(DocumentCategory.Rulebook);
        info.UploadedByUserId.Should().Be(uploaderId);
        info.GameId.Should().Be(gameId);
        info.PrivateGameId.Should().BeNull();
        info.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void PdfCopyrightInfo_With_PrivateGameId_And_NoGameId()
    {
        var privateGameId = Guid.NewGuid();

        var info = new PdfCopyrightInfo(
            "doc-1",
            LicenseType.PublicDomain,
            DocumentCategory.Expansion,
            Guid.NewGuid(),
            null,
            privateGameId,
            false);

        info.GameId.Should().BeNull();
        info.PrivateGameId.Should().Be(privateGameId);
        info.IsPublic.Should().BeFalse();
        info.LicenseType.Should().Be(LicenseType.PublicDomain);
    }

    [Fact]
    public void PdfCopyrightInfo_Record_Equality_Works()
    {
        var docId = Guid.NewGuid().ToString();
        var uploaderId = Guid.NewGuid();

        var a = new PdfCopyrightInfo(docId, LicenseType.Copyrighted, DocumentCategory.Other, uploaderId, null, null, false);
        var b = new PdfCopyrightInfo(docId, LicenseType.Copyrighted, DocumentCategory.Other, uploaderId, null, null, false);

        a.Should().Be(b);
    }

    #endregion

    #region GetPdfCopyrightInfoAsync Tests

    [Fact]
    public async Task GetPdfCopyrightInfoAsync_EmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var projection = CreateProjection(out _);

        // Act
        var result = await projection.GetPdfCopyrightInfoAsync(
            Array.Empty<string>(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPdfCopyrightInfoAsync_InvalidGuids_ReturnsEmptyDictionary()
    {
        var projection = CreateProjection(out _);

        var result = await projection.GetPdfCopyrightInfoAsync(
            new[] { "not-a-guid", "also-invalid" }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPdfCopyrightInfoAsync_NonExistentIds_ReturnsEmptyDictionary()
    {
        var projection = CreateProjection(out _);

        var result = await projection.GetPdfCopyrightInfoAsync(
            new[] { Guid.NewGuid().ToString() }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPdfCopyrightInfoAsync_ExistingDocument_ReturnsCopyrightInfo()
    {
        // Arrange
        var projection = CreateProjection(out var db);
        var docId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();

        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            GameId = gameId,
            UploadedByUserId = uploaderId,
            FileName = "rules.pdf",
            FilePath = "/uploads/rules.pdf",
            LicenseType = (int)LicenseType.CreativeCommons,
            DocumentCategory = nameof(DocumentCategory.Rulebook),
            IsPublic = true
        });
        await db.SaveChangesAsync();

        // Act
        var result = await projection.GetPdfCopyrightInfoAsync(
            new[] { docId.ToString() }, CancellationToken.None);

        // Assert
        result.Should().ContainKey(docId.ToString());
        var info = result[docId.ToString()];
        info.LicenseType.Should().Be(LicenseType.CreativeCommons);
        info.DocumentCategory.Should().Be(DocumentCategory.Rulebook);
        info.UploadedByUserId.Should().Be(uploaderId);
        info.GameId.Should().Be(gameId);
        info.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task GetPdfCopyrightInfoAsync_MultipleDocs_ReturnsBatch()
    {
        var projection = CreateProjection(out var db);
        var doc1Id = Guid.NewGuid();
        var doc2Id = Guid.NewGuid();

        db.PdfDocuments.AddRange(
            new PdfDocumentEntity
            {
                Id = doc1Id,
                UploadedByUserId = Guid.NewGuid(),
                FileName = "rules1.pdf",
                FilePath = "/uploads/rules1.pdf",
                LicenseType = (int)LicenseType.Copyrighted,
                DocumentCategory = nameof(DocumentCategory.Rulebook),
                IsPublic = false
            },
            new PdfDocumentEntity
            {
                Id = doc2Id,
                UploadedByUserId = Guid.NewGuid(),
                FileName = "rules2.pdf",
                FilePath = "/uploads/rules2.pdf",
                LicenseType = (int)LicenseType.PublicDomain,
                DocumentCategory = nameof(DocumentCategory.Expansion),
                IsPublic = true
            });
        await db.SaveChangesAsync();

        var result = await projection.GetPdfCopyrightInfoAsync(
            new[] { doc1Id.ToString(), doc2Id.ToString() }, CancellationToken.None);

        result.Should().HaveCount(2);
        result[doc1Id.ToString()].LicenseType.Should().Be(LicenseType.Copyrighted);
        result[doc2Id.ToString()].LicenseType.Should().Be(LicenseType.PublicDomain);
    }

    [Fact]
    public async Task GetPdfCopyrightInfoAsync_UnknownLicenseType_DefaultsToCopyrighted()
    {
        var projection = CreateProjection(out var db);
        var docId = Guid.NewGuid();

        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            UploadedByUserId = Guid.NewGuid(),
            FileName = "unknown.pdf",
            FilePath = "/uploads/unknown.pdf",
            LicenseType = 999, // Invalid enum value
            DocumentCategory = nameof(DocumentCategory.Rulebook),
            IsPublic = false
        });
        await db.SaveChangesAsync();

        var result = await projection.GetPdfCopyrightInfoAsync(
            new[] { docId.ToString() }, CancellationToken.None);

        result[docId.ToString()].LicenseType.Should().Be(LicenseType.Copyrighted);
    }

    [Fact]
    public async Task GetPdfCopyrightInfoAsync_UnknownDocumentCategory_DefaultsToOther()
    {
        var projection = CreateProjection(out var db);
        var docId = Guid.NewGuid();

        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            UploadedByUserId = Guid.NewGuid(),
            FileName = "weird.pdf",
            FilePath = "/uploads/weird.pdf",
            LicenseType = (int)LicenseType.Copyrighted,
            DocumentCategory = "InvalidCategory",
            IsPublic = false
        });
        await db.SaveChangesAsync();

        var result = await projection.GetPdfCopyrightInfoAsync(
            new[] { docId.ToString() }, CancellationToken.None);

        result[docId.ToString()].DocumentCategory.Should().Be(DocumentCategory.Other);
    }

    [Fact]
    public async Task GetPdfCopyrightInfoAsync_PrivateGameDocument_ReturnsPrivateGameId()
    {
        var projection = CreateProjection(out var db);
        var docId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();

        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            UploadedByUserId = Guid.NewGuid(),
            FileName = "private.pdf",
            FilePath = "/uploads/private.pdf",
            LicenseType = (int)LicenseType.Copyrighted,
            DocumentCategory = nameof(DocumentCategory.Rulebook),
            GameId = null,
            PrivateGameId = privateGameId,
            IsPublic = false
        });
        await db.SaveChangesAsync();

        var result = await projection.GetPdfCopyrightInfoAsync(
            new[] { docId.ToString() }, CancellationToken.None);

        var info = result[docId.ToString()];
        info.GameId.Should().BeNull();
        info.PrivateGameId.Should().Be(privateGameId);
    }

    #endregion

    #region CheckOwnershipAsync Tests

    [Fact]
    public async Task CheckOwnershipAsync_EmptyUserId_ReturnsFalseForAll()
    {
        var projection = CreateProjection(out _);
        var gameIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var result = await projection.CheckOwnershipAsync(
            Guid.Empty, gameIds, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Values.Should().AllSatisfy(v => v.Should().BeFalse());
    }

    [Fact]
    public async Task CheckOwnershipAsync_EmptyGameIds_ReturnsEmptyDictionary()
    {
        var projection = CreateProjection(out _);

        var result = await projection.CheckOwnershipAsync(
            Guid.NewGuid(), Array.Empty<Guid>(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckOwnershipAsync_UserOwnsGame_ReturnsTrue()
    {
        var projection = CreateProjection(out var db);
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = gameId,
            OwnershipDeclaredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await projection.CheckOwnershipAsync(
            userId, new[] { gameId }, CancellationToken.None);

        result[gameId].Should().BeTrue();
    }

    [Fact]
    public async Task CheckOwnershipAsync_UserDoesNotOwnGame_ReturnsFalse()
    {
        var projection = CreateProjection(out var db);
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Entry exists but no ownership declared
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = gameId,
            OwnershipDeclaredAt = null // Not declared
        });
        await db.SaveChangesAsync();

        var result = await projection.CheckOwnershipAsync(
            userId, new[] { gameId }, CancellationToken.None);

        result[gameId].Should().BeFalse();
    }

    [Fact]
    public async Task CheckOwnershipAsync_DifferentUser_ReturnsFalse()
    {
        var projection = CreateProjection(out var db);
        var ownerId = Guid.NewGuid();
        var queryUserId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            SharedGameId = gameId,
            OwnershipDeclaredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await projection.CheckOwnershipAsync(
            queryUserId, new[] { gameId }, CancellationToken.None);

        result[gameId].Should().BeFalse();
    }

    [Fact]
    public async Task CheckOwnershipAsync_PrivateGameOwnership_ReturnsTrue()
    {
        var projection = CreateProjection(out var db);
        var userId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();

        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = null,
            PrivateGameId = privateGameId,
            OwnershipDeclaredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await projection.CheckOwnershipAsync(
            userId, new[] { privateGameId }, CancellationToken.None);

        result[privateGameId].Should().BeTrue();
    }

    [Fact]
    public async Task CheckOwnershipAsync_MixedOwnership_ReturnsCorrectResults()
    {
        var projection = CreateProjection(out var db);
        var userId = Guid.NewGuid();
        var ownedGameId = Guid.NewGuid();
        var notOwnedGameId = Guid.NewGuid();

        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = ownedGameId,
            OwnershipDeclaredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await projection.CheckOwnershipAsync(
            userId, new[] { ownedGameId, notOwnedGameId }, CancellationToken.None);

        result[ownedGameId].Should().BeTrue();
        result[notOwnedGameId].Should().BeFalse();
    }

    [Fact]
    public async Task CheckOwnershipAsync_NoLibraryEntries_ReturnsFalseForAll()
    {
        var projection = CreateProjection(out _);
        var gameIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var result = await projection.CheckOwnershipAsync(
            Guid.NewGuid(), gameIds, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Values.Should().AllSatisfy(v => v.Should().BeFalse());
    }

    #endregion

    #region Helpers

    private static CopyrightDataProjection CreateProjection(out MeepleAiDbContext db)
    {
        db = TestDbContextFactory.CreateInMemoryDbContext();
        return new CopyrightDataProjection(db);
    }

    #endregion
}
