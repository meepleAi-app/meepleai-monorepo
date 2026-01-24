using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.UserLibrary.Application.Handlers;

public sealed class GetGameDetailQueryHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo;
    private readonly Mock<ISharedGameRepository> _mockSharedGameRepo;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public GetGameDetailQueryHandlerTests()
    {
        _mockLibraryRepo = new Mock<IUserLibraryRepository>();
        _mockSharedGameRepo = new Mock<ISharedGameRepository>();
    }

    [Fact]
    public void Constructor_WithNullLibraryRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var mockCache = new Mock<HybridCache>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetGameDetailQueryHandler(null!, _mockSharedGameRepo.Object, mockCache.Object, NullLogger<GetGameDetailQueryHandler>.Instance));
    }

    [Fact]
    public void Constructor_WithNullSharedGameRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var mockCache = new Mock<HybridCache>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetGameDetailQueryHandler(_mockLibraryRepo.Object, null!, mockCache.Object, NullLogger<GetGameDetailQueryHandler>.Instance));
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetGameDetailQueryHandler(_mockLibraryRepo.Object, _mockSharedGameRepo.Object, null!, NullLogger<GetGameDetailQueryHandler>.Instance));
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var mockCache = new Mock<HybridCache>();

        // Act
        var handler = new GetGameDetailQueryHandler(
            _mockLibraryRepo.Object,
            _mockSharedGameRepo.Object,
            mockCache.Object,
            NullLogger<GetGameDetailQueryHandler>.Instance);

        // Assert
        handler.Should().NotBeNull();
    }

    // Note: Full integration tests with HybridCache require real cache instance
    // or advanced mocking frameworks. Behavioral tests deferred to integration test suite.
}
