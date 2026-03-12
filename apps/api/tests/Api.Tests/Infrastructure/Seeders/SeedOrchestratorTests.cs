using Api.Infrastructure.Seeders;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

[Trait("Category", TestCategories.Unit)]
public sealed class SeedOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_ProfileNone_SkipsAllSeeding()
    {
        // Arrange
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var logger = new Mock<ILogger<SeedOrchestrator>>();
        var orchestrator = new SeedOrchestrator(
            SeedProfile.None, scopeFactory.Object, logger.Object);

        // Act
        await orchestrator.ExecuteAsync(CancellationToken.None);

        // Assert - no scopes created means no seeding happened
        scopeFactory.Verify(f => f.CreateScope(), Times.Never);
    }
}
