using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Extensions;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Extensions;

[Trait("Category", "Unit")]
public sealed class SessionTrackingDbExtensionsTests : IDisposable
{
    private readonly MeepleAiDbContext _db;

    public SessionTrackingDbExtensionsTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"SessionTrackingDbExt_{Guid.NewGuid()}")
            .Options;

        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    [Fact]
    public async Task ResolveGameNightIdAsync_WhenLinked_ReturnsNightId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var nightId = Guid.NewGuid();

        _db.GameNightSessions.Add(new GameNightSessionEntity
        {
            Id = Guid.NewGuid(),
            GameNightEventId = nightId,
            SessionId = sessionId,
            GameId = Guid.NewGuid(),
            GameTitle = "Test Game",
            PlayOrder = 1,
            Status = "Pending"
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _db.ResolveGameNightIdAsync(sessionId);

        // Assert
        result.Should().Be(nightId);
    }

    [Fact]
    public async Task ResolveGameNightIdAsync_WhenNotLinked_ReturnsNull()
    {
        // Arrange — no link rows exist
        var sessionId = Guid.NewGuid();

        // Act
        var result = await _db.ResolveGameNightIdAsync(sessionId);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
