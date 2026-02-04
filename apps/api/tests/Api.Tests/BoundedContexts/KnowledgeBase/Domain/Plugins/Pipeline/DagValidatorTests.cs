// =============================================================================
// MeepleAI - RAG Plugin System Tests
// Issue #3415 - DAG Orchestrator
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Validation;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline;

public class DagValidatorTests
{
    private readonly DagValidator _validator;

    public DagValidatorTests()
    {
        _validator = new DagValidator();
    }

    #region Basic Validation Tests

    [Fact]
    public void Validate_ValidPipeline_ReturnsSuccess()
    {
        // Arrange
        var pipeline = CreateValidPipeline();

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyNodes_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [],
            Edges = [],
            EntryPoint = "start",
            ExitPoints = ["end"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "NO_NODES");
    }

    [Fact]
    public void Validate_DuplicateNodeIds_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node1", PluginId = "plugin2" } // Duplicate
            ],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "DUPLICATE_NODE_IDS");
    }

    [Fact]
    public void Validate_MissingEntryPoint_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "MISSING_ENTRY_POINT");
    }

    [Fact]
    public void Validate_InvalidEntryPoint_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "nonexistent",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_ENTRY_POINT");
    }

    [Fact]
    public void Validate_NoExitPoints_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = []
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "NO_EXIT_POINTS");
    }

    [Fact]
    public void Validate_InvalidExitPoint_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["nonexistent"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_EXIT_POINT");
    }

    #endregion

    #region Edge Validation Tests

    [Fact]
    public void Validate_EdgeWithInvalidSourceNode_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node2", PluginId = "plugin2" }
            ],
            Edges = [new PipelineEdge { From = "nonexistent", To = "node2" }],
            EntryPoint = "node1",
            ExitPoints = ["node2"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_SOURCE_NODE");
    }

    [Fact]
    public void Validate_EdgeWithInvalidTargetNode_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node2", PluginId = "plugin2" }
            ],
            Edges = [new PipelineEdge { From = "node1", To = "nonexistent" }],
            EntryPoint = "node1",
            ExitPoints = ["node2"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_TARGET_NODE");
    }

    #endregion

    #region Cycle Detection Tests

    [Fact]
    public void Validate_PipelineWithCycle_ReturnsError()
    {
        // Arrange
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
                new PipelineEdge { From = "node2", To = "node3" },
                new PipelineEdge { From = "node3", To = "node1" } // Creates cycle
            ],
            EntryPoint = "node1",
            ExitPoints = ["node3"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "CYCLE_DETECTED");
    }

    [Fact]
    public void Validate_PipelineWithSelfLoop_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [new PipelineEdge { From = "node1", To = "node1" }], // Self-loop
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "CYCLE_DETECTED");
    }

    #endregion

    #region Connectivity Tests

    [Fact]
    public void Validate_UnreachableNode_ReturnsWarning()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node2", PluginId = "plugin2" },
                new PipelineNode { Id = "isolated", PluginId = "plugin3" } // Not connected
            ],
            Edges = [new PipelineEdge { From = "node1", To = "node2" }],
            EntryPoint = "node1",
            ExitPoints = ["node2"]
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Code == "UNREACHABLE_NODES");
    }

    [Fact]
    public void Validate_UnreachableExitPoint_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node2", PluginId = "plugin2" },
                new PipelineNode { Id = "node3", PluginId = "plugin3" } // Not connected
            ],
            Edges = [new PipelineEdge { From = "node1", To = "node2" }],
            EntryPoint = "node1",
            ExitPoints = ["node2", "node3"] // node3 is unreachable
        };

        // Act
        var result = _validator.Validate(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "UNREACHABLE_EXIT_POINTS");
    }

    #endregion

    #region Topological Sort Tests

    [Fact]
    public void TopologicalSort_LinearPipeline_ReturnsCorrectOrder()
    {
        // Arrange
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

        // Act
        var sorted = _validator.TopologicalSort(pipeline).ToList();

        // Assert
        sorted.Should().HaveCount(3);
        sorted.IndexOf("node1").Should().BeLessThan(sorted.IndexOf("node2"));
        sorted.IndexOf("node2").Should().BeLessThan(sorted.IndexOf("node3"));
    }

    [Fact]
    public void TopologicalSort_DiamondPipeline_ReturnsValidOrder()
    {
        // Arrange: node1 -> node2, node3 -> node4 (diamond shape)
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

        // Act
        var sorted = _validator.TopologicalSort(pipeline).ToList();

        // Assert
        sorted.Should().HaveCount(4);
        sorted.IndexOf("node1").Should().BeLessThan(sorted.IndexOf("node2"));
        sorted.IndexOf("node1").Should().BeLessThan(sorted.IndexOf("node3"));
        sorted.IndexOf("node2").Should().BeLessThan(sorted.IndexOf("node4"));
        sorted.IndexOf("node3").Should().BeLessThan(sorted.IndexOf("node4"));
    }

    #endregion

    #region Helper Methods

    private static PipelineDefinition CreateValidPipeline()
    {
        return new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "start", PluginId = "routing-v1" },
                new PipelineNode { Id = "process", PluginId = "transform-v1" },
                new PipelineNode { Id = "end", PluginId = "generation-v1" }
            ],
            Edges =
            [
                new PipelineEdge { From = "start", To = "process" },
                new PipelineEdge { From = "process", To = "end" }
            ],
            EntryPoint = "start",
            ExitPoints = ["end"]
        };
    }

    #endregion
}
