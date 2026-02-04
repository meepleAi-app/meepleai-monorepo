using Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Unit tests for ContextAssembler.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "ContextEngineering")]
public class ContextAssemblerTests
{
    private readonly Mock<IContextSource> _mockSource1;
    private readonly Mock<IContextSource> _mockSource2;
    private readonly Mock<IContextRetrievalStrategy> _mockStrategy;

    public ContextAssemblerTests()
    {
        _mockSource1 = new Mock<IContextSource>();
        _mockSource1.Setup(s => s.SourceId).Returns("source1");
        _mockSource1.Setup(s => s.SourceName).Returns("Source 1");
        _mockSource1.Setup(s => s.DefaultPriority).Returns(80);
        _mockSource1.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _mockSource2 = new Mock<IContextSource>();
        _mockSource2.Setup(s => s.SourceId).Returns("source2");
        _mockSource2.Setup(s => s.SourceName).Returns("Source 2");
        _mockSource2.Setup(s => s.DefaultPriority).Returns(60);
        _mockSource2.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _mockStrategy = new Mock<IContextRetrievalStrategy>();
        _mockStrategy.Setup(s => s.StrategyId).Returns("test_strategy");
        _mockStrategy.Setup(s => s.SupportedSourceTypes).Returns(new List<string> { "source" });
    }

    [Fact]
    public void Constructor_WithNoSources_ShouldThrow()
    {
        // Arrange
        var sources = Enumerable.Empty<IContextSource>();
        var strategies = new[] { _mockStrategy.Object };

        // Act
        var act = () => new ContextAssembler(sources, strategies);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sources");
    }

    [Fact]
    public void Constructor_WithNullSources_ShouldThrow()
    {
        // Arrange & Act
        var act = () => new ContextAssembler(null!, new[] { _mockStrategy.Object });

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sources");
    }

    [Fact]
    public async Task AssembleAsync_WithAvailableSources_ShouldReturnAssembledContext()
    {
        // Arrange
        var items1 = new List<RetrievedContextItem>
        {
            new() { Id = "1", Content = "Item 1", Relevance = 0.9, TokenCount = 100, ContentType = "test" }
        };

        _mockSource1.Setup(s => s.RetrieveAsync(It.IsAny<ContextRetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextRetrievalResult
            {
                SourceId = "source1",
                Items = items1,
                TotalTokens = 100,
                IsSuccess = true
            });

        var items2 = new List<RetrievedContextItem>
        {
            new() { Id = "2", Content = "Item 2", Relevance = 0.7, TokenCount = 80, ContentType = "test" }
        };

        _mockSource2.Setup(s => s.RetrieveAsync(It.IsAny<ContextRetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextRetrievalResult
            {
                SourceId = "source2",
                Items = items2,
                TotalTokens = 80,
                IsSuccess = true
            });

        var assembler = new ContextAssembler(
            new[] { _mockSource1.Object, _mockSource2.Object },
            new[] { _mockStrategy.Object });

        var request = new ContextAssemblyRequest
        {
            Query = "test query",
            MaxTotalTokens = 8000
        };

        // Act
        var result = await assembler.AssembleAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Query.Should().Be("test query");
        result.Items.Should().HaveCount(2);
        result.TotalTokens.Should().Be(180);
        result.Metrics.Should().NotBeNull();
        result.BudgetSnapshot.Should().NotBeNull();
    }

    [Fact]
    public async Task AssembleAsync_WithUnavailableSource_ShouldSkipSource()
    {
        // Arrange
        _mockSource1.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var items2 = new List<RetrievedContextItem>
        {
            new() { Id = "2", Content = "Item 2", Relevance = 0.7, TokenCount = 80, ContentType = "test" }
        };

        _mockSource2.Setup(s => s.RetrieveAsync(It.IsAny<ContextRetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextRetrievalResult
            {
                SourceId = "source2",
                Items = items2,
                TotalTokens = 80,
                IsSuccess = true
            });

        var assembler = new ContextAssembler(
            new[] { _mockSource1.Object, _mockSource2.Object },
            new[] { _mockStrategy.Object });

        var request = new ContextAssemblyRequest
        {
            Query = "test query",
            MaxTotalTokens = 8000
        };

        // Act
        var result = await assembler.AssembleAsync(request);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Metrics.BySource.Should().ContainKey("source1");
        result.Metrics.BySource["source1"].IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task AssembleAsync_ShouldRespectTokenBudget()
    {
        // Arrange
        var items1 = new List<RetrievedContextItem>
        {
            new() { Id = "1", Content = "Large item", Relevance = 0.9, TokenCount = 5000, ContentType = "test" }
        };

        _mockSource1.Setup(s => s.RetrieveAsync(It.IsAny<ContextRetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextRetrievalResult
            {
                SourceId = "source1",
                Items = items1,
                TotalTokens = 5000,
                IsSuccess = true
            });

        var items2 = new List<RetrievedContextItem>
        {
            new() { Id = "2", Content = "Another large item", Relevance = 0.8, TokenCount = 4000, ContentType = "test" }
        };

        _mockSource2.Setup(s => s.RetrieveAsync(It.IsAny<ContextRetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextRetrievalResult
            {
                SourceId = "source2",
                Items = items2,
                TotalTokens = 4000,
                IsSuccess = true
            });

        var assembler = new ContextAssembler(
            new[] { _mockSource1.Object, _mockSource2.Object },
            new[] { _mockStrategy.Object });

        var request = new ContextAssemblyRequest
        {
            Query = "test query",
            MaxTotalTokens = 6000 // Not enough for both
        };

        // Act
        var result = await assembler.AssembleAsync(request);

        // Assert
        result.TotalTokens.Should().BeLessThanOrEqualTo(6000);
        result.Items.Should().HaveCount(1); // Only one item fits
    }

    [Fact]
    public async Task AssembleAsync_WithSourceError_ShouldContinueWithOtherSources()
    {
        // Arrange
        _mockSource1.Setup(s => s.RetrieveAsync(It.IsAny<ContextRetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var items2 = new List<RetrievedContextItem>
        {
            new() { Id = "2", Content = "Item 2", Relevance = 0.7, TokenCount = 80, ContentType = "test" }
        };

        _mockSource2.Setup(s => s.RetrieveAsync(It.IsAny<ContextRetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextRetrievalResult
            {
                SourceId = "source2",
                Items = items2,
                TotalTokens = 80,
                IsSuccess = true
            });

        var assembler = new ContextAssembler(
            new[] { _mockSource1.Object, _mockSource2.Object },
            new[] { _mockStrategy.Object });

        var request = new ContextAssemblyRequest
        {
            Query = "test query",
            MaxTotalTokens = 8000
        };

        // Act
        var result = await assembler.AssembleAsync(request);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Metrics.BySource["source1"].Error.Should().Contain("Test error");
    }

    [Fact]
    public void BuildContextString_WithGroupBySource_ShouldFormatCorrectly()
    {
        // Arrange
        var context = new AssembledContext
        {
            Query = "test",
            Items = new List<AssembledContextItem>
            {
                new()
                {
                    SourceId = "conversation_memory",
                    Item = new RetrievedContextItem
                    {
                        Id = "1", Content = "Message 1", Relevance = 0.9, TokenCount = 50, ContentType = "message"
                    },
                    Priority = 80
                },
                new()
                {
                    SourceId = "game_state",
                    Item = new RetrievedContextItem
                    {
                        Id = "2", Content = "State data", Relevance = 0.8, TokenCount = 100, ContentType = "state"
                    },
                    Priority = 90
                }
            },
            TotalTokens = 150,
            BudgetSnapshot = new ContextBudgetSnapshot
            {
                TotalBudget = 8000,
                AllocatedTokens = 150,
                UsedTokens = 150,
                SourceAllocations = new Dictionary<string, SourceAllocationSnapshot>()
            },
            Metrics = new AssemblyMetrics(),
            AssembledAt = DateTime.UtcNow
        };

        var options = new ContextFormatOptions
        {
            GroupBySource = true,
            IncludeSourceHeaders = true
        };

        // Act
        var result = ContextAssembler.BuildContextString(context, options);

        // Assert
        result.Should().Contain("## Recent Conversation");
        result.Should().Contain("## Current Game State");
        result.Should().Contain("Message 1");
        result.Should().Contain("State data");
    }

    [Fact]
    public void BuildContextString_WithPreamble_ShouldIncludePreamble()
    {
        // Arrange
        var context = new AssembledContext
        {
            Query = "test",
            Items = new List<AssembledContextItem>
            {
                new()
                {
                    SourceId = "test",
                    Item = new RetrievedContextItem
                    {
                        Id = "1", Content = "Content", Relevance = 0.9, TokenCount = 50, ContentType = "test"
                    },
                    Priority = 80
                }
            },
            TotalTokens = 50,
            BudgetSnapshot = new ContextBudgetSnapshot
            {
                TotalBudget = 8000,
                SourceAllocations = new Dictionary<string, SourceAllocationSnapshot>()
            },
            Metrics = new AssemblyMetrics(),
            AssembledAt = DateTime.UtcNow
        };

        var options = new ContextFormatOptions
        {
            Preamble = "You are an AI assistant."
        };

        // Act
        var result = ContextAssembler.BuildContextString(context, options);

        // Assert
        result.Should().StartWith("You are an AI assistant.");
    }

    [Fact]
    public void BuildContextString_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var context = new AssembledContext
        {
            Query = "test",
            Items = new List<AssembledContextItem>
            {
                new()
                {
                    SourceId = "test",
                    Item = new RetrievedContextItem
                    {
                        Id = "1", Content = "Content", Relevance = 0.85, TokenCount = 50, ContentType = "message"
                    },
                    Priority = 80
                }
            },
            TotalTokens = 50,
            BudgetSnapshot = new ContextBudgetSnapshot
            {
                TotalBudget = 8000,
                SourceAllocations = new Dictionary<string, SourceAllocationSnapshot>()
            },
            Metrics = new AssemblyMetrics(),
            AssembledAt = DateTime.UtcNow
        };

        var options = new ContextFormatOptions
        {
            IncludeMetadata = true,
            GroupBySource = false
        };

        // Act
        var result = ContextAssembler.BuildContextString(context, options);

        // Assert
        result.Should().Contain("[test:message]");
        result.Should().Contain("relevance: 0.85");
    }
}
