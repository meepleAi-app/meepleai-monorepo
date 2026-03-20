using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Unit tests for CreateProposalMigrationOnApprovalHandler.
/// Issue #3666: Phase 5 - verifies migration creation when share request is approved.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class CreateProposalMigrationOnApprovalHandlerTests : IDisposable
{
    private readonly Mock<IShareRequestRepository> _mockShareRequestRepo = new();
    private readonly Mock<IProposalMigrationRepository> _mockMigrationRepo = new();
    private readonly Mock<ILogger<CreateProposalMigrationOnApprovalHandler>> _mockLogger = new();
    private readonly MeepleAiDbContext _db;
    private readonly CreateProposalMigrationOnApprovalHandler _handler;

    public CreateProposalMigrationOnApprovalHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new CreateProposalMigrationOnApprovalHandler(
            _db,
            _mockShareRequestRepo.Object,
            _mockMigrationRepo.Object,
            _mockLogger.Object);
    }

    public void Dispose() => _db.Dispose();

    #region Handle Tests — Early Exit Paths

    [Fact]
    public async Task Handle_WhenShareRequestNotFound_DoesNotCreateMigration()
    {
        // Arrange
        var domainEvent = new ShareRequestApprovedEvent(
            shareRequestId: Guid.NewGuid(),
            adminId: Guid.NewGuid(),
            targetSharedGameId: Guid.NewGuid());

        _mockShareRequestRepo
            .Setup(r => r.GetByIdAsync(domainEvent.ShareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.SharedGameCatalog.Domain.Entities.ShareRequest?)null);

        // Act
        var act = () => _handler.Handle(domainEvent, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _mockMigrationRepo.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserLibrary.Domain.Entities.ProposalMigration>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenContributionTypeIsNotNewGameProposal_DoesNotCreateMigration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sourceGameId = Guid.NewGuid();

        var shareRequest = Api.BoundedContexts.SharedGameCatalog.Domain.Entities.ShareRequest.Create(
            userId, sourceGameId, ContributionType.NewGame);

        var domainEvent = new ShareRequestApprovedEvent(
            shareRequestId: shareRequest.Id,
            adminId: Guid.NewGuid(),
            targetSharedGameId: Guid.NewGuid());

        _mockShareRequestRepo
            .Setup(r => r.GetByIdAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act
        var act = () => _handler.Handle(domainEvent, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _mockMigrationRepo.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserLibrary.Domain.Entities.ProposalMigration>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTargetSharedGameIdIsNull_DoesNotCreateMigration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sourceGameId = Guid.NewGuid();

        var shareRequest = Api.BoundedContexts.SharedGameCatalog.Domain.Entities.ShareRequest.Create(
            userId, sourceGameId, ContributionType.NewGameProposal);

        var domainEvent = new ShareRequestApprovedEvent(
            shareRequestId: shareRequest.Id,
            adminId: Guid.NewGuid(),
            targetSharedGameId: null);

        _mockShareRequestRepo
            .Setup(r => r.GetByIdAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Act
        var act = () => _handler.Handle(domainEvent, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _mockMigrationRepo.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserLibrary.Domain.Entities.ProposalMigration>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLibraryEntryNotFound_DoesNotCreateMigration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sourceGameId = Guid.NewGuid(); // used as UserLibraryEntry.Id in query

        var shareRequest = Api.BoundedContexts.SharedGameCatalog.Domain.Entities.ShareRequest.Create(
            userId, sourceGameId, ContributionType.NewGameProposal);

        var domainEvent = new ShareRequestApprovedEvent(
            shareRequestId: shareRequest.Id,
            adminId: Guid.NewGuid(),
            targetSharedGameId: Guid.NewGuid());

        _mockShareRequestRepo
            .Setup(r => r.GetByIdAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // No UserLibraryEntry seeded → FirstOrDefaultAsync returns null

        // Act
        var act = () => _handler.Handle(domainEvent, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _mockMigrationRepo.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserLibrary.Domain.Entities.ProposalMigration>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLibraryEntryHasNoPrivateGameId_DoesNotCreateMigration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var libraryEntryId = Guid.NewGuid();

        var shareRequest = Api.BoundedContexts.SharedGameCatalog.Domain.Entities.ShareRequest.Create(
            userId, libraryEntryId, ContributionType.NewGameProposal);

        var domainEvent = new ShareRequestApprovedEvent(
            shareRequestId: shareRequest.Id,
            adminId: Guid.NewGuid(),
            targetSharedGameId: Guid.NewGuid());

        _mockShareRequestRepo
            .Setup(r => r.GetByIdAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        // Seed a library entry with no PrivateGameId
        _db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = libraryEntryId,
            UserId = userId,
            PrivateGameId = null
        });
        await _db.SaveChangesAsync();

        // Act
        var act = () => _handler.Handle(domainEvent, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _mockMigrationRepo.Verify(
            r => r.AddAsync(
                It.IsAny<Api.BoundedContexts.UserLibrary.Domain.Entities.ProposalMigration>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
