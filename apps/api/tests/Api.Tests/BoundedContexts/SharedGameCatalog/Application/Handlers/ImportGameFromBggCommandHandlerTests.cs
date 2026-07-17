using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for <see cref="ImportGameFromBggCommandHandler"/>.
/// Issue #3099: duplicate BGG ID must surface as 409 Conflict (ConflictException),
/// not a fabricated 500 via InvalidOperationException.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "3099")]
public sealed class ImportGameFromBggCommandHandlerTests : IDisposable
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IBggApiService> _bggApiServiceMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<ImportGameFromBggCommandHandler>> _loggerMock;
    private readonly ImportGameFromBggCommandHandler _handler;

    public ImportGameFromBggCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _bggApiServiceMock = new Mock<IBggApiService>();
        _loggerMock = new Mock<ILogger<ImportGameFromBggCommandHandler>>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"ImportBggTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new ImportGameFromBggCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _bggApiServiceMock.Object,
            _dbContext,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithDuplicateBggId_ThrowsConflictException()
    {
        // Arrange
        var command = new ImportGameFromBggCommand(BggId: 13, UserId: Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.ExistsByBggIdAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists in the catalog*");

        // The conflict must short-circuit before any BGG API call or persistence.
        _bggApiServiceMock.Verify(
            s => s.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
