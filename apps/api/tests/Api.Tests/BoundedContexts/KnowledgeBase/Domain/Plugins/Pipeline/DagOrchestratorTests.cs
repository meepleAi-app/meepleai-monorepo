// =============================================================================
// MeepleAI - RAG Plugin System Tests
// Issue #3415 - DAG Orchestrator
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline;

public class DagOrchestratorTests
{
    private readonly DagOrchestrator _orchestrator;
    private readonly Dictionary<string, IRagPlugin> _plugins;

    public DagOrchestratorTests()
    {
        _plugins = new Dictionary<string, IRagPlugin>(StringComparer.Ordinal);
        _orchestrator = new DagOrchestrator(
            pluginId => _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null,
            NullLogger<DagOrchestrator>.Instance);
    }

    #region Basic Execution Tests

    [Fact]
    public async Task ExecuteAsync_SingleNodePipeline_ReturnsSuccess()
    {
        // Arrange
        var plugin = new TestPlugin("plugin1", success: true);
        _plugins["plugin1"] = plugin;

        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        var input = CreateInput();

        // Act
        var result = await _orchestrator.ExecuteAsync(pipeline, input);

        // Assert
        result.Success.Should().BeTrue();
        result.FinalOutputs.Should().ContainKey("node1");
        result.NodesExecuted.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_LinearPipeline_ExecutesInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        _plugins["plugin1"] = new TestPlugin("plugin1", success: true, onExecute: () => executionOrder.Add("node1"));
        _plugins["plugin2"] = new TestPlugin("plugin2", success: true, onExecute: () => executionOrder.Add("node2"));
        _plugins["plugin3"] = new TestPlugin("plugin3", success: true, onExecute: () => executionOrder.Add("node3"));

        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node2", PluginId = "plugin2" },
                new PipelineNode { Id = "node3", PluginId = "plugin3" }
            ],
            Edges =
            [
                new PipelineEdge { From = "node1", To = "node2" },
                new PipelineEdge { From = "node2", To = "node3" }
            ],
            EntryPoint = "node1",
            ExitPoints = ["node3"]
        };

        var input = CreateInput();

        // Act
        var result = await _orchestrator.ExecuteAsync(pipeline, input);

        // Assert
        result.Success.Should().BeTrue();
        result.NodesExecuted.Should().Be(3);
        executionOrder.Should().BeEquivalentTo(["node1", "node2", "node3"], opt => opt.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteAsync_DiamondPipeline_ExecutesParallel()
    {
        // Arrange: node1 -> node2, node3 -> node4 (diamond shape)
        _plugins["plugin1"] = new TestPlugin("plugin1", success: true);
        _plugins["plugin2"] = new TestPlugin("plugin2", success: true);
        _plugins["plugin3"] = new TestPlugin("plugin3", success: true);
        _plugins["plugin4"] = new TestPlugin("plugin4", success: true);

        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node2", PluginId = "plugin2" },
                new PipelineNode { Id = "node3", PluginId = "plugin3" },
                new PipelineNode { Id = "node4", PluginId = "plugin4" }
            ],
            Edges =
            [
                new PipelineEdge { From = "node1", To = "node2" },
                new PipelineEdge { From = "node1", To = "node3" },
                new PipelineEdge { From = "node2", To = "node4" },
                new PipelineEdge { From = "node3", To = "node4" }
            ],
            EntryPoint = "node1",
            ExitPoints = ["node4"]
        };

        var input = CreateInput();

        // Act
        var result = await _orchestrator.ExecuteAsync(pipeline, input);

        // Assert
        result.Success.Should().BeTrue();
        result.NodesExecuted.Should().Be(4);
    }

    #endregion

    #region Failure Handling Tests

    [Fact]
    public async Task ExecuteAsync_PluginNotFound_ReturnsFailure()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "nonexistent" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        var input = CreateInput();

        // Act
        var result = await _orchestrator.ExecuteAsync(pipeline, input);

        // Assert
        result.Success.Should().BeFalse();
        result.FinalOutputs["node1"].ErrorCode.Should().Be("PLUGIN_NOT_FOUND");
    }

    [Fact]
    public async Task ExecuteAsync_PluginFails_ReturnsFailure()
    {
        // Arrange
        _plugins["plugin1"] = new TestPlugin("plugin1", success: false, errorMessage: "Test failure");

        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        var input = CreateInput();

        // Act
        var result = await _orchestrator.ExecuteAsync(pipeline, input);

        // Assert
        result.Success.Should().BeFalse();
        result.NodesFailed.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPipeline_ReturnsValidationError()
    {
        // Arrange - pipeline with cycle
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node2", PluginId = "plugin2" }
            ],
            Edges =
            [
                new PipelineEdge { From = "node1", To = "node2" },
                new PipelineEdge { From = "node2", To = "node1" } // Cycle
            ],
            EntryPoint = "node1",
            ExitPoints = ["node2"]
        };

        var input = CreateInput();

        // Act
        var result = await _orchestrator.ExecuteAsync(pipeline, input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("validation failed");
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task ExecuteAsync_GlobalTimeout_ReturnsTimeout()
    {
        // Arrange
        _plugins["plugin1"] = new TestPlugin("plugin1", success: true, delayMs: 5000);

        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"],
            GlobalTimeoutMs = 100
        };

        var input = CreateInput();

        // Act
        var result = await _orchestrator.ExecuteAsync(pipeline, input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_ThrowsOperationCanceled()
    {
        // Arrange
        _plugins["plugin1"] = new TestPlugin("plugin1", success: true, delayMs: 5000);

        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        var input = CreateInput();
        using var cts = new CancellationTokenSource(100);

        // Act & Assert
        Func<Task> act = async () => await _orchestrator.ExecuteAsync(pipeline, input, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Condition Evaluation Tests

    [Fact]
    public async Task ExecuteAsync_ConditionalEdge_FollowsCondition()
    {
        // Arrange
        _plugins["router"] = new TestPlugin("router", success: true, resultJson: """{"type": "rules"}""");
        _plugins["rules-handler"] = new TestPlugin("rules-handler", success: true);
        _plugins["faq-handler"] = new TestPlugin("faq-handler", success: true);

        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "router", PluginId = "router" },
                new PipelineNode { Id = "rules", PluginId = "rules-handler" },
                new PipelineNode { Id = "faq", PluginId = "faq-handler" }
            ],
            Edges =
            [
                new PipelineEdge { From = "router", To = "rules", Condition = "output.type == 'rules'" },
                new PipelineEdge { From = "router", To = "faq", Condition = "output.type == 'faq'" }
            ],
            EntryPoint = "router",
            ExitPoints = ["rules"] // Only rules as exit point since faq will be skipped
        };

        var input = CreateInput();

        // Act
        var result = await _orchestrator.ExecuteAsync(pipeline, input);

        // Assert
        result.Success.Should().BeTrue();
        result.FinalOutputs.Should().ContainKey("rules");
        result.NodesSkipped.Should().Be(1); // faq should be skipped
    }

    #endregion

    #region Helper Methods

    private static PluginInput CreateInput()
    {
        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse("{}"),
            UserId = Guid.NewGuid(),
            GameId = Guid.NewGuid()
        };
    }

    #endregion

    #region Test Plugin

    private sealed class TestPlugin : IRagPlugin
    {
        private readonly string _id;
        private readonly bool _success;
        private readonly string? _errorMessage;
        private readonly int _delayMs;
        private readonly string? _resultJson;
        private readonly Action? _onExecute;

        public TestPlugin(
            string id,
            bool success,
            string? errorMessage = null,
            int delayMs = 0,
            string? resultJson = null,
            Action? onExecute = null)
        {
            _id = id;
            _success = success;
            _errorMessage = errorMessage;
            _delayMs = delayMs;
            _resultJson = resultJson;
            _onExecute = onExecute;
        }

        public string Id => _id;
        public string Name => $"Test Plugin {_id}";
        public string Version => "1.0.0";
        public PluginCategory Category => PluginCategory.Transform;
        public JsonDocument InputSchema => JsonDocument.Parse("{}");
        public JsonDocument OutputSchema => JsonDocument.Parse("{}");
        public JsonDocument ConfigSchema => JsonDocument.Parse("{}");
        public PluginMetadata Metadata => new()
        {
            Id = _id,
            Name = Name,
            Version = Version,
            Category = PluginCategory.Transform,
            Description = "Test plugin for unit tests"
        };

        public async Task<PluginOutput> ExecuteAsync(
            PluginInput input,
            PluginConfig? config = null,
            CancellationToken cancellationToken = default)
        {
            _onExecute?.Invoke();

            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs, cancellationToken);
            }

            if (_success)
            {
                return new PluginOutput
                {
                    ExecutionId = input.ExecutionId,
                    Success = true,
                    Confidence = 0.95,
                    Result = JsonDocument.Parse(_resultJson ?? "{}")
                };
            }

            return PluginOutput.Failed(input.ExecutionId, _errorMessage ?? "Test failure", "TEST_ERROR");
        }

        public Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Healthy());
        }

        public ValidationResult ValidateConfig(PluginConfig config)
        {
            return ValidationResult.Success();
        }

        public ValidationResult ValidateInput(PluginInput input)
        {
            return ValidationResult.Success();
        }
    }

    #endregion
}
