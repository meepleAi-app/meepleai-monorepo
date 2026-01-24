using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.UserLibrary.Application.Handlers;

public sealed class RecordGameSessionCommandHandlerTests
{
    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Arrange
        var mockUow = new Mock<IUnitOfWork>();
        var mockCache = new Mock<HybridCache>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RecordGameSessionCommandHandler(null!, mockUow.Object, mockCache.Object, NullLogger<RecordGameSessionCommandHandler>.Instance));
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var mockRepo = new Mock<IUserLibraryRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        var mockCache = new Mock<HybridCache>();

        // Act
        var handler = new RecordGameSessionCommandHandler(
            mockRepo.Object,
            mockUow.Object,
            mockCache.Object,
            NullLogger<RecordGameSessionCommandHandler>.Instance);

        // Assert
        handler.Should().NotBeNull();
    }
}
