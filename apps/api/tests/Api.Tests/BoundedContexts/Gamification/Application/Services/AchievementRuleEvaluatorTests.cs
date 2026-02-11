using Api.BoundedContexts.Gamification.Application.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Gamification.Application.Services;

/// <summary>
/// Unit tests for AchievementRuleEvaluator constructor guard clauses.
/// EvaluateProgressAsync requires a real DbContext and is covered by integration tests.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public sealed class AchievementRuleEvaluatorTests
{
    [Fact]
    public void Constructor_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AchievementRuleEvaluator>>();

        // Act
        var act = () => new AchievementRuleEvaluator(null!, loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act
        var act = () => new AchievementRuleEvaluator(context, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }
}
