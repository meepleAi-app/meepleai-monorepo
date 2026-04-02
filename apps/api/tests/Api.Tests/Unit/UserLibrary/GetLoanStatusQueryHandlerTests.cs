using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.UserLibrary;

/// <summary>
/// Unit tests for GetLoanStatusQueryHandler.
/// Verifies loan status retrieval for InPrestito, non-loan, and missing library entries.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetLoanStatusQueryHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _mockRepository;
    private readonly GetLoanStatusQueryHandler _handler;

    public GetLoanStatusQueryHandlerTests()
    {
        _mockRepository = new Mock<IUserLibraryRepository>();
        _handler = new GetLoanStatusQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_GameOnLoan_ReturnsLoanDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var entry = new UserLibraryEntry(entryId, userId, gameId);
        entry.MarkAsOwned(); // Nuovo → Owned (valid transition)
        entry.MarkAsOnLoan("Mario Rossi"); // Owned → InPrestito

        _mockRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var query = new GetLoanStatusQuery(userId, gameId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsOnLoan.Should().BeTrue();
        result.BorrowerInfo.Should().Be("Mario Rossi");
        result.LoanedSince.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_GameNotOnLoan_ReturnsIsOnLoanFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var entry = new UserLibraryEntry(entryId, userId, gameId);
        entry.MarkAsOwned(); // Nuovo → Owned

        _mockRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var query = new GetLoanStatusQuery(userId, gameId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsOnLoan.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_GameNotInLibrary_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        var query = new GetLoanStatusQuery(userId, gameId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
