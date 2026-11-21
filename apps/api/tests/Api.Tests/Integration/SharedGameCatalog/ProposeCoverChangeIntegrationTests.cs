using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ProposeCoverChange;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Integration test for Task 6 (Game Cover-da-PDF plan): the authenticated user
/// flow that proposes a cover-from-PDF for a SharedGame. Exercises the real
/// MediatR pipeline end-to-end — <see cref="ProposeCoverChangeCommand"/> orchestrates
/// the Task 3 <c>MaterializePdfCoverCommand</c> (materializes the pending cover) and
/// the Task 4 <c>ShareRequest.CreateCoverChange</c> factory (creates the Pending
/// proposal) against a real Postgres instance via Testcontainers.
///
/// SmolDocling dependency (named risk in the task brief): the materialization path
/// calls GetPdfPageImageQuery, which posts to the "SmolDoclingService" named
/// HttpClient. Since the real Python microservice isn't available under
/// Testcontainers, this test registers a fake primary HttpMessageHandler for that
/// named client (mirrors the pattern in WikimediaCircuitBreakerIntegrationTests)
/// that always returns a fixed JPEG byte array. IWebpVariantGenerator and
/// IPdfCoverUploadPipeline are mocked directly (both already internal-interface
/// seams designed for substitution; exercising real Magick.NET/R2 upload here would
/// test Task 3's own internals again, out of scope for this Task 6 test).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Collection("Integration-GroupC")]
public sealed class ProposeCoverChangeIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private ServiceProvider _serviceProvider = null!;

    public ProposeCoverChangeIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"proposecoverchange_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // #5100: domain events delivered inline, as PdfCoverPropagationIntegrationTests does. In the default
        // OutboxOnly mode PdfCoverGeneratedEvent only lands in the outbox, so a proposal that changes the game
        // would look harmless here.
        services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
            Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.Hybrid }));
        services.AddSingleton<ICacheInvalidationRetryPolicy, CacheInvalidationRetryPolicy>();

        // Real repositories needed by the command chain under test.
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IShareRequestRepository, ShareRequestRepository>();
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();

        // IWebpVariantGenerator / IPdfCoverUploadPipeline: mocked. Both are seams
        // designed for substitution (see MaterializePdfCoverCommandHandlerTests,
        // Task 3); exercising real Magick.NET encoding + R2 upload here would
        // re-test Task 3's own internals, out of scope for this Task 6 test.
        var webpMock = new Mock<IWebpVariantGenerator>();
        webpMock
            .Setup(w => w.GenerateWebpAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // RIFF (WebP container magic)
        services.AddScoped<IWebpVariantGenerator>(_ => webpMock.Object);

        var pipelineMock = new Mock<IPdfCoverUploadPipeline>();
        pipelineMock
            .Setup(p => p.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, byte[] _, CancellationToken _) => key);
        services.AddScoped<IPdfCoverUploadPipeline>(_ => pipelineMock.Object);

        // IBlobStorageService: GetPdfPageImageQueryHandler retrieves the PDF bytes
        // from storage before rendering. Returns a trivial non-empty stream — the
        // fake SmolDocling handler below ignores the request body entirely.
        var blobStorageMock = new Mock<IBlobStorageService>();
        blobStorageMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // "%PDF"
        services.AddSingleton<IBlobStorageService>(blobStorageMock.Object);

        // Fake "SmolDoclingService" named HttpClient: GetPdfPageImageQueryHandler
        // posts to /api/v1/page-image?page_number=N and expects JPEG bytes back.
        // No Python microservice is reachable under Testcontainers, so the
        // primary message handler is swapped for a fixed-response fake (same
        // pattern as WikimediaCircuitBreakerIntegrationTests.FixedResponseHandler).
        services.AddHttpClient("SmolDoclingService", client =>
            {
                client.BaseAddress = new Uri("http://smoldocling-service.test/");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new FakeSmolDoclingHandler());

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Propose_MaterializesPendingCoverAndCreatesPendingShareRequest()
    {
        // Arrange: seed a User (FK target for PdfDocument.UploadedByUserId) + a
        // SharedGame + a Ready PdfDocument pointed at it.
        var userId = Guid.NewGuid();

        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = userId,
            Email = $"propose-cover-{userId:N}@test.com",
            DisplayName = "Propose Cover Test User",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();

        var sharedGame = SharedGame.Create(
            title: "Wingspan",
            yearPublished: 2019,
            description: "Bird-collection engine builder",
            minPlayers: 1,
            maxPlayers: 5,
            playingTimeMinutes: 70,
            minAge: 10,
            complexityRating: 2.4m,
            averageRating: 8.1m,
            imageUrl: "https://example.com/wingspan.jpg",
            thumbnailUrl: "https://example.com/wingspan-thumb.jpg",
            rules: null,
            createdBy: userId);

        var pdfDocument = new PdfDocumentBuilder()
            .WithGameId(sharedGame.Id)
            .WithUploadedBy(userId)
            // GetPdfPageImageQueryHandler.ExtractFileIdFromPath expects the
            // "{fileId}_{originalName}" convention used by the real upload path.
            .WithFilePath("/uploads/00000000-0000-0000-0000-000000000001_test-rulebook.pdf")
            .ThatIsCompleted()
            .Build();

        using (var scope = _serviceProvider.CreateScope())
        {
            var sharedGameRepo = scope.ServiceProvider.GetRequiredService<ISharedGameRepository>();
            var pdfRepo = scope.ServiceProvider.GetRequiredService<IPdfDocumentRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await sharedGameRepo.AddAsync(sharedGame);
            await pdfRepo.AddAsync(pdfDocument);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        _dbContext.ChangeTracker.Clear();

        // Act
        using var actScope = _serviceProvider.CreateScope();
        var mediator = actScope.ServiceProvider.GetRequiredService<IMediator>();

        var cmd = new ProposeCoverChangeCommand(userId, sharedGame.Id, pdfDocument.Id, PageNumber: 2);

        var shareRequestId = await mediator.Send(cmd, CancellationToken.None);

        // Assert
        _dbContext.ChangeTracker.Clear();
        var shareRequestRepo = actScope.ServiceProvider.GetRequiredService<IShareRequestRepository>();
        var sr = await shareRequestRepo.GetByIdAsync(shareRequestId);

        sr.Should().NotBeNull();
        sr!.ContributionType.Should().Be(ContributionType.CoverChange);
        sr.Status.Should().Be(ShareRequestStatus.Pending);
        sr.PendingCoverR2Key.Should().NotBeNullOrWhiteSpace();
        // C1 fix: PageNumber: 2 (1-based) must store as CoverPageIndex 1 (0-based).
        sr.CoverPageIndex.Should().Be(1);
    }

    /// <summary>
    /// I2 fix regression guard: two proposals for the SAME SharedGame (e.g. two
    /// different users, or the same user proposing two different pages) must NOT
    /// collide on the same physical R2 pending-cover object. Prior to the I2 fix,
    /// <c>ProposeCoverChangeCommandHandler</c> built a per-GAME deterministic dbKey
    /// (<c>covers/{gameId}/pdf-cover-pending</c>) — a second proposal for the same
    /// game would overwrite the first proposal's R2 bytes at the same key and BOTH
    /// <see cref="ShareRequest"/> rows would persist the identical
    /// <see cref="ShareRequest.PendingCoverR2Key"/>. On approval, an admin approving
    /// proposal A would actually promote whatever bytes proposal B (or a later
    /// still-pending proposal) last wrote — silently promoting the wrong image.
    /// The fix makes the dbKey unique per-proposal (a fresh GUID segment), while
    /// preserving the same slash-containing key SHAPE (<c>covers/{gameId}/...</c>)
    /// so it still resolves via <c>GetPresignedUrlForRawKeyAsync</c> (R2 resolver fix,
    /// commit 05723c823).
    /// </summary>
    [Fact]
    public async Task Propose_TwoProposalsForSameGame_ProduceDistinctPendingCoverKeys()
    {
        // Arrange: seed a User + a SharedGame + a Ready PdfDocument pointed at it —
        // same fixture shape as the single-proposal test above.
        var userId = Guid.NewGuid();

        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = userId,
            Email = $"propose-cover-collision-{userId:N}@test.com",
            DisplayName = "Propose Cover Collision Test User",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();

        var sharedGame = SharedGame.Create(
            title: "Wingspan",
            yearPublished: 2019,
            description: "Bird-collection engine builder",
            minPlayers: 1,
            maxPlayers: 5,
            playingTimeMinutes: 70,
            minAge: 10,
            complexityRating: 2.4m,
            averageRating: 8.1m,
            imageUrl: "https://example.com/wingspan.jpg",
            thumbnailUrl: "https://example.com/wingspan-thumb.jpg",
            rules: null,
            createdBy: userId);

        var pdfDocument = new PdfDocumentBuilder()
            .WithGameId(sharedGame.Id)
            .WithUploadedBy(userId)
            .WithFilePath("/uploads/00000000-0000-0000-0000-000000000002_test-rulebook.pdf")
            .ThatIsCompleted()
            .Build();

        using (var scope = _serviceProvider.CreateScope())
        {
            var sharedGameRepo = scope.ServiceProvider.GetRequiredService<ISharedGameRepository>();
            var pdfRepo = scope.ServiceProvider.GetRequiredService<IPdfDocumentRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await sharedGameRepo.AddAsync(sharedGame);
            await pdfRepo.AddAsync(pdfDocument);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        _dbContext.ChangeTracker.Clear();

        // Act: propose TWO different pages of the SAME PdfDocument for the SAME
        // SharedGame — mirrors two concurrent proposals racing on the same game.
        using var actScope = _serviceProvider.CreateScope();
        var mediator = actScope.ServiceProvider.GetRequiredService<IMediator>();

        var cmdA = new ProposeCoverChangeCommand(userId, sharedGame.Id, pdfDocument.Id, PageNumber: 2);
        var shareRequestIdA = await mediator.Send(cmdA, CancellationToken.None);

        var cmdB = new ProposeCoverChangeCommand(userId, sharedGame.Id, pdfDocument.Id, PageNumber: 7);
        var shareRequestIdB = await mediator.Send(cmdB, CancellationToken.None);

        // Assert
        _dbContext.ChangeTracker.Clear();
        var shareRequestRepo = actScope.ServiceProvider.GetRequiredService<IShareRequestRepository>();
        var srA = await shareRequestRepo.GetByIdAsync(shareRequestIdA);
        var srB = await shareRequestRepo.GetByIdAsync(shareRequestIdB);

        srA.Should().NotBeNull();
        srB.Should().NotBeNull();
        srA!.PendingCoverR2Key.Should().NotBeNullOrWhiteSpace();
        srB!.PendingCoverR2Key.Should().NotBeNullOrWhiteSpace();

        // The core I2 assertion: no collision between concurrent proposals for the
        // same game.
        srA.PendingCoverR2Key.Should().NotBe(srB.PendingCoverR2Key);

        // Structural guard (not exact-value, per Guid.NewGuid() non-determinism):
        // both keys must still match the per-game slash-containing shape required
        // by the R2 resolver fix, with a unique 32-hex-char GUID segment.
        var keyPattern = $"^covers/{sharedGame.Id:D}/pdf-cover-[0-9a-f]{{32}}$";
        srA.PendingCoverR2Key.Should().MatchRegex(keyPattern);
        srB.PendingCoverR2Key.Should().MatchRegex(keyPattern);
    }

    /// <summary>
    /// #5100: the proposal used to mark its page as the PDF's cover, and <c>PdfCoverGeneratedEvent</c> copied that
    /// key onto the game during the proposal: the approval decided nothing and a rejection undid nothing. With the
    /// event delivered inline (see <see cref="InitializeAsync"/>), a pending proposal must leave both untouched.
    /// </summary>
    [Fact]
    public async Task Propose_LeavesTheGameAndItsPdfAsTheyWere()
    {
        var (userId, game, pdf) = await SeedGameWithPdfAsync("untouched", withCover: true);
        var original = OriginalCoverKey(pdf.Id);

        using var scope = _serviceProvider.CreateScope();
        var shareRequestId = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new ProposeCoverChangeCommand(userId, game.Id, pdf.Id, PageNumber: 3), CancellationToken.None);

        _dbContext.ChangeTracker.Clear();
        var gameRow = await _dbContext.Set<SharedGameEntity>().AsNoTracking().SingleAsync(g => g.Id == game.Id);
        var pdfRow = await _dbContext.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == pdf.Id);
        var request = await scope.ServiceProvider.GetRequiredService<IShareRequestRepository>().GetByIdAsync(shareRequestId);

        request!.PendingCoverR2Key.Should().NotBe(original, "the proposal renders its own image");
        gameRow.PdfCoverR2Key.Should().Be(original, "only the approval changes the game's cover");
        pdfRow.CoverR2Key.Should().Be(original);
        pdfRow.CoverPageIndex.Should().Be(0);
    }

    [Fact]
    public async Task Propose_WithAPdfOfAnotherGame_IsRefused()
    {
        var (userId, proposedFor, _) = await SeedGameWithPdfAsync("target", withCover: false);
        var (_, owner, othersPdf) = await SeedGameWithPdfAsync("owner", withCover: true);

        using var scope = _serviceProvider.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new ProposeCoverChangeCommand(userId, proposedFor.Id, othersPdf.Id, PageNumber: 2), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _dbContext.ChangeTracker.Clear();
        (await _dbContext.Set<ShareRequestEntity>().AsNoTracking().CountAsync(r => r.TargetSharedGameId == proposedFor.Id))
            .Should().Be(0);
        (await _dbContext.Set<SharedGameEntity>().AsNoTracking().SingleAsync(g => g.Id == owner.Id)).PdfCoverR2Key
            .Should().Be(OriginalCoverKey(othersPdf.Id));
    }

    [Fact]
    public async Task Propose_APageThePdfDoesNotHave_IsRefused()
    {
        // The builder's Ready PDF has 10 pages.
        var (userId, game, pdf) = await SeedGameWithPdfAsync("pages", withCover: false);

        using var scope = _serviceProvider.CreateScope();
        var act = () => scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new ProposeCoverChangeCommand(userId, game.Id, pdf.Id, PageNumber: 11), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    private static string OriginalCoverKey(Guid pdfId) => $"covers/pdf/{pdfId:D}/cover";

    /// <summary>
    /// A user, a game and a Ready 10-page PDF of that game; with <paramref name="withCover"/> the PDF already has its
    /// cover (page 1) and the game shows it, as after the processing pipeline.
    /// </summary>
    private async Task<(Guid UserId, SharedGame Game, PdfDocument Pdf)> SeedGameWithPdfAsync(string tag, bool withCover)
    {
        var userId = Guid.NewGuid();
        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = userId,
            Email = $"propose-cover-{tag}-{userId:N}@test.com",
            DisplayName = "Propose Cover Test User",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();

        var game = SharedGame.Create(
            title: $"Azul {tag}",
            yearPublished: 2017,
            description: "Tile-drafting",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 45,
            minAge: 8,
            complexityRating: 1.8m,
            averageRating: 7.8m,
            imageUrl: "https://example.com/azul.jpg",
            thumbnailUrl: "https://example.com/azul-thumb.jpg",
            rules: null,
            createdBy: userId);

        var pdf = new PdfDocumentBuilder()
            .WithGameId(game.Id)
            .WithUploadedBy(userId)
            .WithFilePath($"/uploads/{Guid.NewGuid():D}_rulebook.pdf")
            .ThatIsCompleted()
            .Build();

        if (withCover)
        {
            pdf.MarkCoverGenerated(OriginalCoverKey(pdf.Id), 0);
            game.SetPdfCoverR2Key(OriginalCoverKey(pdf.Id));
        }

        using (var scope = _serviceProvider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISharedGameRepository>().AddAsync(game);
            await scope.ServiceProvider.GetRequiredService<IPdfDocumentRepository>().AddAsync(pdf);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        _dbContext.ChangeTracker.Clear();
        return (userId, game, pdf);
    }

    /// <summary>
    /// Fixed-response fake for the "SmolDoclingService" named HttpClient. Always
    /// returns 200 OK with a minimal JPEG magic-number payload, regardless of the
    /// requested page number — sufficient for GetPdfPageImageQueryHandler, whose
    /// only postcondition on success is "returns the response body bytes".
    /// </summary>
    private sealed class FakeSmolDoclingHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }) // JPEG magic
            };
            return Task.FromResult(response);
        }
    }
}
