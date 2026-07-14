using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for ShareRequestRepository.
/// Covers the round-trip persistence of the pending-cover fields
/// (PendingCoverR2Key, CoverPageIndex, SourcePdfDocumentId) added in Task 4
/// (Game Cover-da-PDF, ContributionType.CoverChange).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ShareRequestRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private IShareRequestRepository _repository = null!;
    private ISharedGameRepository _sharedGameRepository = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();

    public ShareRequestRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"sharerequest_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        var mockMediator = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        _repository = new ShareRequestRepository(_dbContext, eventCollectorMock.Object);
        _sharedGameRepository = new SharedGameRepository(_dbContext, eventCollectorMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsCoverChangeFields()
    {
        // Arrange: a shared game must exist to satisfy the SourceGameId/TargetSharedGameId FK
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
            createdBy: TestUserId);

        await _sharedGameRepository.AddAsync(sharedGame);
        await _dbContext.SaveChangesAsync();

        var sourcePdfDocumentId = Guid.NewGuid();
        const string pendingCoverR2Key = "covers/wingspan/pdf-page-cover.png";
        const int coverPageIndex = 4;

        var shareRequest = ShareRequest.CreateCoverChange(
            userId: TestUserId,
            targetSharedGameId: sharedGame.Id,
            sourcePdfDocumentId: sourcePdfDocumentId,
            pendingCoverR2Key: pendingCoverR2Key,
            coverPageIndex: coverPageIndex,
            userNotes: "Suggested cover from rulebook page 5");

        // Act: persist, then force a fresh reload (no first-level cache) via a clean context
        await _repository.AddAsync(shareRequest);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdAsync(shareRequest.Id);

        // Assert
        reloaded.Should().NotBeNull();
        reloaded!.ContributionType.Should().Be(ContributionType.CoverChange);
        reloaded.PendingCoverR2Key.Should().Be(pendingCoverR2Key);
        reloaded.CoverPageIndex.Should().Be(coverPageIndex);
        reloaded.SourcePdfDocumentId.Should().Be(sourcePdfDocumentId);
    }
}
