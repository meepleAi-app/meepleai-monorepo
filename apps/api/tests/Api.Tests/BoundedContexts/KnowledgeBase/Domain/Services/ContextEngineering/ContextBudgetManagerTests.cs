using Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Unit tests for ContextBudgetManager.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "ContextEngineering")]
public class ContextBudgetManagerTests
{
    [Fact]
    public void Constructor_WithPositiveBudget_ShouldSucceed()
    {
        // Arrange & Act
        var manager = new ContextBudgetManager(8000);

        // Assert
        manager.TotalBudget.Should().Be(8000);
        manager.RemainingBudget.Should().Be(8000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithNonPositiveBudget_ShouldThrow(int budget)
    {
        // Arrange & Act
        var act = () => new ContextBudgetManager(budget);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("totalBudget");
    }

    [Fact]
    public void RegisterSource_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);

        // Act
        manager.RegisterSource("memory", priority: 80, minTokens: 500, maxTokens: 2000);
        manager.RegisterSource("state", priority: 90, minTokens: 1000, maxTokens: 3000);

        // Assert
        var allocations = manager.CalculateAllocations();
        allocations.Should().ContainKey("memory");
        allocations.Should().ContainKey("state");
    }

    [Fact]
    public void RegisterSource_WithEmptySourceId_ShouldThrow()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);

        // Act
        var act = () => manager.RegisterSource("", priority: 50);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sourceId");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void RegisterSource_WithInvalidPriority_ShouldThrow(int priority)
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);

        // Act
        var act = () => manager.RegisterSource("test", priority: priority);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("priority");
    }

    [Fact]
    public void CalculateAllocations_WithNoSources_ShouldReturnEmpty()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);

        // Act
        var allocations = manager.CalculateAllocations();

        // Assert
        allocations.Should().BeEmpty();
    }

    [Fact]
    public void CalculateAllocations_ShouldRespectMinTokens()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);
        manager.RegisterSource("memory", priority: 50, minTokens: 2000);
        manager.RegisterSource("state", priority: 50, minTokens: 1000);

        // Act
        var allocations = manager.CalculateAllocations();

        // Assert
        allocations["memory"].Should().BeGreaterThanOrEqualTo(2000);
        allocations["state"].Should().BeGreaterThanOrEqualTo(1000);
    }

    [Fact]
    public void CalculateAllocations_ShouldRespectMaxTokens()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);
        manager.RegisterSource("memory", priority: 100, maxTokens: 2000);

        // Act
        var allocations = manager.CalculateAllocations();

        // Assert
        allocations["memory"].Should().BeLessThanOrEqualTo(2000);
    }

    [Fact]
    public void CalculateAllocations_HigherPriority_ShouldGetMoreTokens()
    {
        // Arrange
        var manager = new ContextBudgetManager(10000);
        manager.RegisterSource("high", priority: 90);
        manager.RegisterSource("low", priority: 10);

        // Act
        var allocations = manager.CalculateAllocations();

        // Assert
        allocations["high"].Should().BeGreaterThan(allocations["low"]);
    }

    [Fact]
    public void RecordUsage_ShouldReturnUnusedTokens()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);
        manager.RegisterSource("memory", priority: 100);
        var allocations = manager.CalculateAllocations();
        var allocated = allocations["memory"];

        // Act
        var unused = manager.RecordUsage("memory", usedTokens: allocated / 2);

        // Assert
        unused.Should().Be(allocated / 2);
    }

    [Fact]
    public void RecordUsage_WithUnknownSource_ShouldThrow()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);

        // Act
        var act = () => manager.RecordUsage("unknown", usedTokens: 100);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sourceId");
    }

    [Fact]
    public void ReallocateUnused_ShouldDistributeToPendingSources()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);
        manager.RegisterSource("memory", priority: 80);
        manager.RegisterSource("state", priority: 90);
        var allocations = manager.CalculateAllocations();

        // Memory only uses half its allocation
        manager.RecordUsage("memory", allocations["memory"] / 2);

        // Act
        var reallocation = manager.ReallocateUnused(["memory"]);

        // Assert
        reallocation.Should().ContainKey("state");
        // State should get the reallocated tokens
        reallocation["state"].Should().BeGreaterThanOrEqualTo(allocations["state"]);
    }

    [Fact]
    public void GetStatus_ShouldReturnStatusForAllSources()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);
        manager.RegisterSource("memory", priority: 80);
        manager.RegisterSource("state", priority: 90);
        manager.CalculateAllocations();
        manager.RecordUsage("memory", 500);

        // Act
        var status = manager.GetStatus();

        // Assert
        status.Should().HaveCount(2);
        status.Should().Contain(s => s.SourceId == "memory" && s.UsedTokens == 500);
    }

    [Fact]
    public void CreateSnapshot_ShouldCaptureCurrentState()
    {
        // Arrange
        var manager = new ContextBudgetManager(8000);
        manager.RegisterSource("memory", priority: 100);
        manager.CalculateAllocations();
        manager.RecordUsage("memory", 1000);

        // Act
        var snapshot = manager.CreateSnapshot();

        // Assert
        snapshot.TotalBudget.Should().Be(8000);
        snapshot.UsedTokens.Should().Be(1000);
        snapshot.SourceAllocations.Should().ContainKey("memory");
        snapshot.SourceAllocations["memory"].Used.Should().Be(1000);
    }
}
