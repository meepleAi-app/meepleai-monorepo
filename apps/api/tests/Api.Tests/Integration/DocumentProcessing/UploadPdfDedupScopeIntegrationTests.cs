using System.Security.Cryptography;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

public sealed class UploadPdfDedupHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "upload_pdf_dedup");

/// <summary>
/// #4326 and #4317 (both absorbed by #4325), on the upload without chunks. <c>UploadPdfCommandHandler</c> decides
/// "this file already exists" with queries of its own, not with <c>PdfDeduplicationService</c>: for any upload it looked
/// first at the PDFs of the user, on any game ("Existing KB found in your uploads"). The same file on two private games
/// gave the second a success answer and no PDF of its own (#4317), and a standard upload of a file the user keeps on a
/// private game was told it already existed.
/// </summary>
/// <remarks>
/// <para>
/// The standard upload names a catalog game (<c>GameId</c>, which has to exist: the automatic creation is gone).
/// Decided: a private game has its own PDF and its own index, even for the same content (the one reuse left is the
/// same file on the same game), and a private PDF is not a document to reuse for anything else. The search among the
/// PDFs of the catalog (<c>Existing KB found in shared catalog</c>) is not touched.
/// </para>
/// <para>
/// On Postgres, through the mediator, with a minimal PDF. When the file is NOT an existing KB the handler goes on to
/// store it; these tests look at the answer to the dedup question (<c>ExistingKb</c>) and not at the rest of that flow.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4326")]
public sealed class UploadPdfDedupScopeIntegrationTests
    : IClassFixture<UploadPdfDedupHostFixture>, IAsyncLifetime
{
    private static readonly byte[] PdfBytes =
        "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\nxref\n0 2\n0000000000 65535 f \n0000000009 00000 n \ntrailer\n<< /Root 1 0 R /Size 2 >>\nstartxref\n58\n%%EOF"u8.ToArray();

    private static readonly string Hash = Convert.ToHexStringLower(SHA256.HashData(PdfBytes));

    private readonly UploadPdfDedupHostFixture _hostFixture;
    private Guid _userId;
    private Guid _firstGame;
    private Guid _secondGame;

    public UploadPdfDedupScopeIntegrationTests(UploadPdfDedupHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        _firstGame = await SeedPrivateGameAsync(db);
        _secondGame = await SeedPrivateGameAsync(db);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ─── Private upload ──────────────────────────────────────────

    /// <summary>The case of #4317: the same file on a second private game of the same user.</summary>
    [Fact]
    public async Task APrivateUpload_OfAFileOnAnotherPrivateGame_StoresAPdfOfItsOwn()
    {
        await SeedPdfAsync(privateGameId: _firstGame);

        var result = await UploadAsync(privateGameId: _secondGame);

        result.ExistingKb.Should().BeNull("a private game has its own PDF and its own index");
        (await PdfsOfAsync(privateGameId: _secondGame)).Should().Be(1, "the second game has to end up with a PDF of its own");
    }

    /// <summary>The one reuse left: the same file again on the same private game.</summary>
    [Fact]
    public async Task APrivateUpload_OfAFileOnTheSameGame_IsTheExistingKb()
    {
        var existing = await SeedPdfAsync(privateGameId: _firstGame);

        var result = await UploadAsync(privateGameId: _firstGame);

        result.Success.Should().BeTrue();
        result.ExistingKb.Should().NotBeNull();
        result.ExistingKb!.PdfDocumentId.Should().Be(existing);
        result.ExistingKb.Source.Should().Be("user");
        (await PdfsOfAsync(privateGameId: _firstGame)).Should().Be(1, "nothing new is stored");
    }

    /// <summary>
    /// A PDF of the catalog with the same content is not a KB for a private game either. The second check of this
    /// branch answered "Existing KB found in shared catalog", the web ignores <c>existingKb</c>, and the private game
    /// stayed without a PDF (retrieval reads a private game's documents by its own id).
    /// </summary>
    [Fact]
    public async Task APrivateUpload_OfAFileOnACatalogGame_StoresAPdfOfItsOwn()
    {
        var catalogGame = await SeedCatalogGameAsync();
        await SeedPdfAsync(sharedGameId: catalogGame);

        var result = await UploadAsync(privateGameId: _firstGame);

        result.ExistingKb.Should().BeNull("a PDF of the catalog is not a KB of a private game");
        (await PdfsOfAsync(privateGameId: _firstGame)).Should().Be(1);
    }

    /// <summary>
    /// The PDF of somebody else on the same private game is not an existing KB of this user. The deduplication runs
    /// before the check on who owns the game, so the answer would carry the file name and state of a PDF that is not
    /// theirs (a PDF of another user lands on a private game through the chunked upload of an admin or editor).
    /// </summary>
    [Fact]
    public async Task APrivateUpload_OfAFileOfAnotherUserOnTheSameGame_IsNotAnExistingKb()
    {
        var otherUser = await SeedOtherUserAsync();
        await SeedPdfAsync(privateGameId: _firstGame, uploadedBy: otherUser);

        var result = await UploadAsync(privateGameId: _firstGame);

        result.ExistingKb.Should().BeNull("only the PDF of the same user is an existing KB");
        (await PdfsOfAsync(privateGameId: _firstGame)).Should().Be(2, "the other user's PDF stays and a new one is stored");
    }

    /// <summary>A failed PDF is not a KB to point at: the deduplication service says the same.</summary>
    [Fact]
    public async Task APrivateUpload_OfAFileFailedOnTheSameGame_IsNotAnExistingKb()
    {
        await SeedPdfAsync(privateGameId: _firstGame, state: "Failed");

        var result = await UploadAsync(privateGameId: _firstGame);

        result.ExistingKb.Should().BeNull();
        (await PdfsOfAsync(privateGameId: _firstGame)).Should().Be(2, "the failed row stays and a new one is stored");
    }

    // ─── Standard upload ─────────────────────────────────────────

    /// <summary>
    /// A file the user keeps on a private game is not a document to reuse for a game of the catalog: the standard upload
    /// was told "Existing KB found in your uploads" and the game stayed without one.
    /// </summary>
    [Fact]
    public async Task AStandardUpload_OfAFileOnAPrivateGame_StoresAPdfOfItsOwn()
    {
        var catalogGame = await SeedCatalogGameAsync();
        await SeedPdfAsync(privateGameId: _firstGame);

        var result = await UploadAsync(gameId: catalogGame);

        result.Message.Should().NotContain("Game not found", "the test has to reach the question it asks");
        result.ExistingKb.Should().BeNull("a private PDF is not a document to reuse for another game");
        (await PdfsOfAsync(sharedGameId: catalogGame)).Should().Be(1, "the catalog game has to end up with a PDF of its own");
    }

    /// <summary>
    /// The PDF of ANOTHER catalog game is not a document of this one. Retrieval reads a game's documents by its own
    /// id, and the web ignores <c>existingKb</c>: the second game stayed without one.
    /// </summary>
    [Fact]
    public async Task AStandardUpload_OfAFileOnAnotherCatalogGame_StoresAPdfOfItsOwn()
    {
        var firstCatalogGame = await SeedCatalogGameAsync();
        var secondCatalogGame = await SeedCatalogGameAsync();
        await SeedPdfAsync(sharedGameId: firstCatalogGame);

        var result = await UploadAsync(gameId: secondCatalogGame);

        result.Message.Should().NotContain("Game not found", "the test has to reach the question it asks");
        result.ExistingKb.Should().BeNull("the PDF of another game is not a document of this one");
        (await PdfsOfAsync(sharedGameId: secondCatalogGame)).Should().Be(1);
    }

    /// <summary>The user's own PDF of the same catalog game is still found first, as before.</summary>
    [Fact]
    public async Task AStandardUpload_OfAFileOnTheSameCatalogGame_IsTheExistingKb()
    {
        var catalogGame = await SeedCatalogGameAsync();
        var existing = await SeedPdfAsync(sharedGameId: catalogGame);

        var result = await UploadAsync(gameId: catalogGame);

        result.ExistingKb.Should().NotBeNull(result.Message);
        result.ExistingKb!.PdfDocumentId.Should().Be(existing);
        result.ExistingKb.Source.Should().Be("user");
        (await PdfsOfAsync(sharedGameId: catalogGame)).Should().Be(1, "nothing new is stored");
    }

    /// <summary>The reuse across users stays, on the same catalog game.</summary>
    [Fact]
    public async Task AStandardUpload_OfAFileOfAnotherUserOnTheSameCatalogGame_IsTheSharedKb()
    {
        var catalogGame = await SeedCatalogGameAsync();
        var otherUser = await SeedOtherUserAsync();
        var existing = await SeedPdfAsync(sharedGameId: catalogGame, uploadedBy: otherUser);

        var result = await UploadAsync(gameId: catalogGame);

        result.ExistingKb.Should().NotBeNull(result.Message);
        result.ExistingKb!.PdfDocumentId.Should().Be(existing);
        result.ExistingKb.Source.Should().Be("shared");
    }

    /// <summary>A failed PDF of the user is not a KB to point at, as in the deduplication service.</summary>
    [Fact]
    public async Task AStandardUpload_OfAFileFailedOnTheSameCatalogGame_IsNotAnExistingKb()
    {
        var catalogGame = await SeedCatalogGameAsync();
        await SeedPdfAsync(sharedGameId: catalogGame, state: "Failed");

        var result = await UploadAsync(gameId: catalogGame);

        result.Message.Should().NotContain("Game not found", "the test has to reach the question it asks");
        result.ExistingKb.Should().BeNull();
        (await PdfsOfAsync(sharedGameId: catalogGame)).Should().Be(2, "the failed row stays and a new one is stored");
    }

    /// <summary>The same for the failed PDF of another user, which the second check looks at.</summary>
    [Fact]
    public async Task AStandardUpload_OfAFileFailedForAnotherUserOnTheSameCatalogGame_IsNotAnExistingKb()
    {
        var catalogGame = await SeedCatalogGameAsync();
        var otherUser = await SeedOtherUserAsync();
        await SeedPdfAsync(sharedGameId: catalogGame, uploadedBy: otherUser, state: "Failed");

        var result = await UploadAsync(gameId: catalogGame);

        result.Message.Should().NotContain("Game not found", "the test has to reach the question it asks");
        result.ExistingKb.Should().BeNull();
        (await PdfsOfAsync(sharedGameId: catalogGame)).Should().Be(2, "the failed row stays and a new one is stored");
    }

    // ─── Support ────────────────────────────────────────────────

    private async Task<PdfUploadResult> UploadAsync(Guid? privateGameId = null, Guid? gameId = null)
    {
        var stream = new MemoryStream(PdfBytes);
        var file = new FormFile(stream, 0, PdfBytes.Length, "file", "rulebook.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };

        using var scope = _hostFixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new UploadPdfCommand(
                GameId: gameId?.ToString(),
                Metadata: null,
                PrivateGameId: privateGameId,
                UserId: _userId,
                File: file), Ct);
    }

    private async Task<Guid> SeedPrivateGameAsync(MeepleAiDbContext db)
    {
        var id = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = id,
            OwnerId = _userId,
            Title = "A prototype",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
        });
        await db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<Guid> SeedCatalogGameAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var id = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = "A catalog game",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<Guid> SeedOtherUserAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (otherUserId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        return otherUserId;
    }

    /// <summary>How many PDFs with this content the database holds for one game.</summary>
    private async Task<int> PdfsOfAsync(Guid? sharedGameId = null, Guid? privateGameId = null)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.PdfDocuments.CountAsync(
            p => p.ContentHash == Hash
                 && ((sharedGameId.HasValue && p.SharedGameId == sharedGameId)
                     || (privateGameId.HasValue && p.PrivateGameId == privateGameId)), Ct);
    }

    private async Task<Guid> SeedPdfAsync(
        Guid? sharedGameId = null, Guid? privateGameId = null, Guid? uploadedBy = null, string state = "Ready")
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var id = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = $"rulebook-{id:N}.pdf",
            FilePath = $"/tmp/rulebook-{id:N}.pdf",
            FileSizeBytes = PdfBytes.Length,
            UploadedByUserId = uploadedBy ?? _userId,
            ProcessingState = state,
            ContentHash = Hash,
            SharedGameId = sharedGameId,
            PrivateGameId = privateGameId,
        });
        await db.SaveChangesAsync(Ct);
        return id;
    }
}
