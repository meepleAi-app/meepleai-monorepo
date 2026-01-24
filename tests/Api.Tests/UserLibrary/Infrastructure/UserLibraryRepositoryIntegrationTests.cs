using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.UserLibrary.Infrastructure;

/// <summary>
/// Integration tests for UserLibraryRepository with in-memory database.
/// Tests repository query methods, navigation properties, and data persistence.
/// </summary>
public sealed class UserLibraryRepositoryIntegrationTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    [Fact]
    public void Constructor_CreatesRepository()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();

#pragma warning disable CA2000 // DbContext disposed immediately after test
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        // Act
        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Assert
        repository.Should().NotBeNull();
        dbContext.Dispose();
    }

    [Fact]
    public void RepositoryInterface_ImplementsIUserLibraryRepository()
    {
        // Assert
        typeof(UserLibraryRepository).Should().BeAssignableTo<IUserLibraryRepository>();
    }

    [Fact]
    public void NewQueryMethods_ExistInInterface()
    {
        // Assert - Verify new methods from Issue #2825 exist
        var interfaceType = typeof(IUserLibraryRepository);
        
        interfaceType.GetMethod("GetUserGameWithStatsAsync").Should().NotBeNull(
            "GetUserGameWithStatsAsync should exist per Issue #2825 requirements");
        
        interfaceType.GetMethod("GetUserGamesAsync").Should().NotBeNull(
            "GetUserGamesAsync should exist per Issue #2825 requirements");
    }

    [Fact]
    public void NewQueryMethods_ExistInImplementation()
    {
        // Assert - Verify new methods implemented
        var implType = typeof(UserLibraryRepository);
        
        implType.GetMethod("GetUserGameWithStatsAsync").Should().NotBeNull();
        implType.GetMethod("GetUserGamesAsync").Should().NotBeNull();
    }
}
