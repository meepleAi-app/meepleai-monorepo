using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetLoanStatusQueryHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _repositoryMock = new();
    private readonly GetLoanStatusQueryHandler _handler;

    public GetLoanStatusQueryHandlerTests()
    {
        _handler = new GetLoanStatusQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_GameOnLoan_ReturnsLoanDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        entry.MarkAsOnLoan("Mario Rossi");

        _repositoryMock
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var query = new GetLoanStatusQuery(userId, gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsOnLoan);
        Assert.Equal("Mario Rossi", result.BorrowerInfo);
        Assert.NotNull(result.LoanedSince);
    }

    [Fact]
    public async Task Handle_GameNotOnLoan_ReturnsIsOnLoanFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        // Entry starts in Nuovo state — not on loan

        _repositoryMock
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var query = new GetLoanStatusQuery(userId, gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsOnLoan);
        Assert.Null(result.BorrowerInfo);
        Assert.Null(result.LoanedSince);
    }

    [Fact]
    public async Task Handle_GameNotInLibrary_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        var query = new GetLoanStatusQuery(userId, gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }
}
