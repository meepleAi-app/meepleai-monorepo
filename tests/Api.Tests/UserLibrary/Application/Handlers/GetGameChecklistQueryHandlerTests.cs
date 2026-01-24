using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.UserLibrary.Application.Handlers;

public sealed class GetGameChecklistQueryHandlerTests
{
    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetGameChecklistQueryHandler(null!, NullLogger<GetGameChecklistQueryHandler>.Instance));
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var mockCache = new Mock<HybridCache>();

        // Act
        var handler = new GetGameChecklistQueryHandler(
            mockCache.Object,
            NullLogger<GetGameChecklistQueryHandler>.Instance);

        // Assert
        handler.Should().NotBeNull();
    }
}
