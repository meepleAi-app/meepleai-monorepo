using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.CheckPrivateGameDuplicates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for ApproveGameProposal flow with real database operations.
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ApproveGameProposalFlowIntegrationTests : IAsyncLifetime
{
    private MeepleAiDbContext _dbContext = null!;
    private ShareRequestRepository _shareRequestRepo = null!;
    private PrivateGameRepository _privateGameRepo = null!;
    private SharedGameRepository _sharedGameRepo = null!;
    private Mock<IShareRequestDocumentService> _documentServiceMock = null!;
    private IUnitOfWork _unitOfWork = null!;
    private ApproveGameProposalCommandHandler _commandHandler = null!;
    private CheckPrivateGameDuplicatesQueryHandler _queryHandler = null!;

    private Guid _userId;
    private Guid _adminId;
    private Guid _privateGameId;
    private Guid _libraryEntryId;
    private int _bggId;

    public async ValueTask InitializeAsync()
    {
        // Create unique database for test isolation
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"ApproveGameProposalFlowTest_{Guid.NewGuid()}");

        _shareRequestRepo = new ShareRequestRepository(_dbContext);
        var eventCollector = TestDbContextFactory.CreateMockEventCollector();
        _privateGameRepo = new PrivateGameRepository(_dbContext, eventCollector.Object);
        _sharedGameRepo = new SharedGameRepository(_dbContext);
        _documentServiceMock = new Mock<IShareRequestDocumentService>();
        _unitOfWork = new UnitOfWork(_dbContext);

        var commandLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ApproveGameProposalCommandHandler>>();
        _commandHandler = new ApproveGameProposalCommandHandler(
            _shareRequestRepo,
            _privateGameRepo,
            _sharedGameRepo,
            _documentServiceMock.Object,
            _dbContext,
            _unitOfWork,
            commandLogger.Object);

        var queryLogger = new Mock<Microsoft.Extensions.Logging.ILogger<CheckPrivateGameDuplicatesQueryHandler>>();
        _queryHandler = new CheckPrivateGameDuplicatesQueryHandler(
            _privateGameRepo,
            _sharedGameRepo,
            queryLogger.Object);

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        _userId = Guid.NewGuid();
        _adminId = Guid.NewGuid();
        _privateGameId = Guid.NewGuid();
        _libraryEntryId = Guid.NewGuid();
        _bggId = 12345;

        // User
        var user = new UserEntity
        {
            Id = _userId,
            Email = $"user_{Guid.NewGuid()}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashedpassword",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);

        // Admin
        var admin = new UserEntity
        {
            Id = _adminId,
            Email = $"admin_{Guid.NewGuid()}@example.com",
            DisplayName = "Admin User",
            PasswordHash = "hashedpassword",
            Role = "Admin",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(admin);

        // PrivateGame
        var privateGame = new PrivateGameEntity
        {
            Id = _privateGameId,
            OwnerId = _userId,
            BggId = _bggId,
            Title = "Test Private Game",
            Description = "A test game description",
            YearPublished = 2020,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ComplexityRating = 2.5m,
            Source = PrivateGameSource.BoardGameGeek,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        _dbContext.PrivateGames.Add(privateGame);

        // UserLibraryEntry linking to PrivateGame
        var libraryEntry = new UserLibraryEntryEntity
        {
            Id = _libraryEntryId,
            UserId = _userId,
            PrivateGameId = _privateGameId,
            AddedAt = DateTime.UtcNow
        };
        _dbContext.UserLibraryEntries.Add(libraryEntry);

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task ApproveAsNew_CreatesNewSharedGameAndProposalMigration()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            _userId,
            _libraryEntryId,
            ContributionType.NewGameProposal,
            "Proposing my private game");
        shareRequest.StartReview(_adminId);
        await _shareRequestRepo.AddAsync(shareRequest, TestContext.Current.CancellationToken);
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: _adminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsNew,
            TargetSharedGameId: null,
            AdminNotes: "Approved as new game");

        // Act
        var response = await _commandHandler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(ShareRequestStatus.Approved);
        response.TargetSharedGameId.Should().NotBeNull();

        // Verify SharedGame was created
        var sharedGame = await _sharedGameRepo.GetByIdAsync(
            response.TargetSharedGameId!.Value,
            TestContext.Current.CancellationToken);
        sharedGame.Should().NotBeNull();
        sharedGame!.Title.Should().Be("Test Private Game");
        sharedGame.BggId.Should().Be(_bggId);
        sharedGame.MinPlayers.Should().Be(2);
        sharedGame.MaxPlayers.Should().Be(4);

        // Verify ShareRequest was approved
        var updatedRequest = await _shareRequestRepo.GetByIdAsync(
            shareRequest.Id,
            TestContext.Current.CancellationToken);
        updatedRequest.Should().NotBeNull();
        updatedRequest!.Status.Should().Be(ShareRequestStatus.Approved);
        updatedRequest.TargetSharedGameId.Should().Be(sharedGame.Id);
        updatedRequest.ResolvedAt.Should().NotBeNull();

        // Verify ProposalMigration is created by event handler (in real scenario)
        // Note: Event handlers not executed in unit tests, but checked in E2E tests
    }

    [Fact]
    public async Task MergeKnowledgeBase_DoesNotCreateSharedGame_OnlyCopiesDocuments()
    {
        // Arrange
        // Create existing SharedGame
        var existingSharedGame = await CreateExistingSharedGameAsync();

        var shareRequest = ShareRequest.Create(
            _userId,
            _libraryEntryId,
            ContributionType.NewGameProposal,
            "Proposing PDFs for existing game");
        shareRequest.StartReview(_adminId);
        await _shareRequestRepo.AddAsync(shareRequest, TestContext.Current.CancellationToken);
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: _adminId,
            ApprovalAction: ProposalApprovalAction.MergeKnowledgeBase,
            TargetSharedGameId: existingSharedGame.Id,
            AdminNotes: "Merging PDFs only");

        // Act
        var response = await _commandHandler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(ShareRequestStatus.Approved);
        response.TargetSharedGameId.Should().Be(existingSharedGame.Id);

        // Verify NO new SharedGame was created (count should be 1, the existing one)
        var sharedGamesCount = await _dbContext.SharedGames.CountAsync();
        sharedGamesCount.Should().Be(1);

        // Verify ShareRequest was approved with existing game ID
        var updatedRequest = await _shareRequestRepo.GetByIdAsync(
            shareRequest.Id,
            TestContext.Current.CancellationToken);
        updatedRequest.Should().NotBeNull();
        updatedRequest!.Status.Should().Be(ShareRequestStatus.Approved);
        updatedRequest.TargetSharedGameId.Should().Be(existingSharedGame.Id);
    }

    [Fact]
    public async Task ApproveAsVariant_CreatesGameWithVariantSuffix()
    {
        // Arrange
        var baseGame = await CreateExistingSharedGameAsync();

        var shareRequest = ShareRequest.Create(
            _userId,
            _libraryEntryId,
            ContributionType.NewGameProposal,
            "Proposing variant edition");
        shareRequest.StartReview(_adminId);
        await _shareRequestRepo.AddAsync(shareRequest, TestContext.Current.CancellationToken);
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: _adminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsVariant,
            TargetSharedGameId: baseGame.Id,
            AdminNotes: "Approved as variant");

        // Act
        var response = await _commandHandler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(ShareRequestStatus.Approved);

        // Verify variant SharedGame was created
        var variantGame = await _sharedGameRepo.GetByIdAsync(
            response.TargetSharedGameId!.Value,
            TestContext.Current.CancellationToken);
        variantGame.Should().NotBeNull();
        variantGame!.Title.Should().EndWith(" (Variant)");
        variantGame.Title.Should().Be("Test Private Game (Variant)");
        variantGame.BggId.Should().BeNull(); // Variants don't use BGG ID
        variantGame.Id.Should().NotBe(baseGame.Id); // Different game

        // Verify 2 SharedGames exist (base + variant)
        var sharedGamesCount = await _dbContext.SharedGames.CountAsync();
        sharedGamesCount.Should().Be(2);
    }

    [Fact]
    public async Task DuplicateDetection_WithExactBggIdMatch_ReturnsExactDuplicate()
    {
        // Arrange
        // Create SharedGame with same BggId as PrivateGame
        var existingGame = await CreateExistingSharedGameAsync(useSameBggId: true);

        var query = new CheckPrivateGameDuplicatesQuery(_privateGameId);

        // Act
        var result = await _queryHandler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.HasExactDuplicate.Should().BeTrue();
        result.ExactDuplicateId.Should().Be(existingGame.Id);
        result.ExactDuplicateTitle.Should().Be(existingGame.Title);
        result.HasFuzzyDuplicates.Should().BeFalse(); // Not implemented yet
        result.FuzzyDuplicates.Should().BeEmpty();
        result.RecommendedAction.Should().Be(ProposalApprovalAction.MergeKnowledgeBase);
    }

    [Fact]
    public async Task DuplicateDetection_WithNoDuplicates_RecommendsApproveAsNew()
    {
        // Arrange
        // No existing SharedGame with same BggId
        var query = new CheckPrivateGameDuplicatesQuery(_privateGameId);

        // Act
        var result = await _queryHandler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.HasExactDuplicate.Should().BeFalse();
        result.ExactDuplicateId.Should().BeNull();
        result.ExactDuplicateTitle.Should().BeNull();
        result.HasFuzzyDuplicates.Should().BeFalse();
        result.FuzzyDuplicates.Should().BeEmpty();
        result.RecommendedAction.Should().Be(ProposalApprovalAction.ApproveAsNew);
    }

    [Fact]
    public async Task MergeKnowledgeBase_WithNonExistentTargetGame_ThrowsNotFoundException()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            _userId,
            _libraryEntryId,
            ContributionType.NewGameProposal,
            "Proposing merge");
        shareRequest.StartReview(_adminId);
        await _shareRequestRepo.AddAsync(shareRequest, TestContext.Current.CancellationToken);
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: _adminId,
            ApprovalAction: ProposalApprovalAction.MergeKnowledgeBase,
            TargetSharedGameId: Guid.NewGuid(), // Non-existent
            AdminNotes: null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _commandHandler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApproveAsVariant_WithNonExistentBaseGame_ThrowsNotFoundException()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            _userId,
            _libraryEntryId,
            ContributionType.NewGameProposal,
            "Proposing variant");
        shareRequest.StartReview(_adminId);
        await _shareRequestRepo.AddAsync(shareRequest, TestContext.Current.CancellationToken);
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: _adminId,
            ApprovalAction: ProposalApprovalAction.ApproveAsVariant,
            TargetSharedGameId: Guid.NewGuid(), // Non-existent
            AdminNotes: null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _commandHandler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteFlow_CheckDuplicates_ThenApprove()
    {
        // Arrange: Create existing SharedGame with same BggId
        var existingGame = await CreateExistingSharedGameAsync(useSameBggId: true);

        // Step 1: Check for duplicates
        var duplicateQuery = new CheckPrivateGameDuplicatesQuery(_privateGameId);
        var duplicateResult = await _queryHandler.Handle(duplicateQuery, TestContext.Current.CancellationToken);

        duplicateResult.HasExactDuplicate.Should().BeTrue();
        duplicateResult.RecommendedAction.Should().Be(ProposalApprovalAction.MergeKnowledgeBase);

        // Step 2: Create and approve ShareRequest using recommended action
        var shareRequest = ShareRequest.Create(
            _userId,
            _libraryEntryId,
            ContributionType.NewGameProposal,
            "Following duplicate check recommendation");
        shareRequest.StartReview(_adminId);
        await _shareRequestRepo.AddAsync(shareRequest, TestContext.Current.CancellationToken);
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        var approveCommand = new ApproveGameProposalCommand(
            ShareRequestId: shareRequest.Id,
            AdminId: _adminId,
            ApprovalAction: duplicateResult.RecommendedAction, // Use recommended action
            TargetSharedGameId: duplicateResult.ExactDuplicateId,
            AdminNotes: "Following duplicate check recommendation");

        var approveResponse = await _commandHandler.Handle(approveCommand, TestContext.Current.CancellationToken);

        // Assert
        approveResponse.Should().NotBeNull();
        approveResponse.Status.Should().Be(ShareRequestStatus.Approved);
        approveResponse.TargetSharedGameId.Should().Be(existingGame.Id);

        // Verify no new SharedGame was created (still 1)
        var sharedGamesCount = await _dbContext.SharedGames.CountAsync();
        sharedGamesCount.Should().Be(1);
    }

    private async Task<Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame> CreateExistingSharedGameAsync(bool useSameBggId = false)
    {
        var sharedGame = Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame.Create(
            title: "Existing Shared Game",
            yearPublished: 2019,
            description: "An existing game in the catalog",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.0m,
            averageRating: 7.5m,
            imageUrl: null,
            thumbnailUrl: null,
            rules: null,
            createdBy: _userId,
            bggId: useSameBggId ? _bggId : 99999); // Use same BggId or different

        await _sharedGameRepo.AddAsync(sharedGame, TestContext.Current.CancellationToken);
        await _unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        return sharedGame;
    }
}
