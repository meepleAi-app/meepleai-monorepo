// =============================================================================
// MeepleAI - RAG Plugin System Tests
// Issue #3416 - Pipeline Definition Schema
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Schema;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Schema;

public class PipelineSchemaValidatorTests
{
    private readonly PipelineSchemaValidator _validator = new();

    #region JSON Validation Tests

    [Fact]
    public void ValidateJson_ValidPipeline_ReturnsSuccess()
    {
        // Arrange
        var json = """
        {
            "id": "test-pipeline",
            "name": "Test Pipeline",
            "nodes": [
                { "id": "node1", "pluginId": "plugin1" }
            ],
            "edges": [],
            "entryPoint": "node1",
            "exitPoints": ["node1"]
        }
        """;

        // Act
        var result = _validator.ValidateJson(json);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateJson_EmptyJson_ReturnsError()
    {
        // Act
        var result = _validator.ValidateJson("");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "EMPTY_JSON");
    }

    [Fact]
    public void ValidateJson_InvalidJson_ReturnsError()
    {
        // Arrange
        var json = "{ invalid json }";

        // Act
        var result = _validator.ValidateJson(json);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "JSON_ERROR");
    }

    [Fact]
    public void ValidateJson_MissingRequiredFields_ReturnsJsonError()
    {
        // Arrange - JSON missing required properties (Id, Name, Edges, EntryPoint, ExitPoints)
        // System.Text.Json throws JsonException for missing required properties during deserialization
        var json = """
        {
            "nodes": []
        }
        """;

        // Act
        var result = _validator.ValidateJson(json);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "JSON_ERROR");
        result.Errors.First().Message.Should().Contain("required properties");
    }

    #endregion

    #region Definition Validation Tests

    [Fact]
    public void ValidateDefinition_ValidPipeline_ReturnsSuccess()
    {
        // Arrange
        var pipeline = CreateValidPipeline();

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDefinition_InvalidId_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "invalid id with spaces!",
            Name = "Test",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_ID_FORMAT");
    }

    [Fact]
    public void ValidateDefinition_DuplicateNodeIds_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node1", PluginId = "plugin2" }
            ],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "DUPLICATE_NODE_ID");
    }

    [Fact]
    public void ValidateDefinition_NodeMissingPluginId_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "" }
            ],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "MISSING_PLUGIN_ID");
    }

    [Fact]
    public void ValidateDefinition_InvalidEdgeReferences_ReturnsErrors()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [new PipelineEdge { From = "node1", To = "nonexistent" }],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_EDGE_TO");
    }

    [Fact]
    public void ValidateDefinition_InvalidEntryPoint_ReturnsError()
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
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_ENTRY_POINT");
    }

    [Fact]
    public void ValidateDefinition_InvalidExitPoint_ReturnsError()
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
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_EXIT_POINT");
    }

    [Fact]
    public void ValidateDefinition_ShortTimeout_ReturnsWarning()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes =
            [
                new PipelineNode { Id = "node1", PluginId = "plugin1", TimeoutMs = 50 }
            ],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Code == "SHORT_TIMEOUT");
    }

    [Fact]
    public void ValidateDefinition_InvalidMaxParallelism_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"],
            MaxParallelism = 0
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_MAX_PARALLELISM");
    }

    [Fact]
    public void ValidateDefinition_InvalidSchemaVersion_ReturnsError()
    {
        // Arrange
        var pipeline = new PipelineDefinition
        {
            SchemaVersion = "not-a-version",
            Id = "test-pipeline",
            Name = "Test Pipeline",
            Nodes = [new PipelineNode { Id = "node1", PluginId = "plugin1" }],
            Edges = [],
            EntryPoint = "node1",
            ExitPoints = ["node1"]
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_SCHEMA_VERSION");
    }

    #endregion

    #region Condition Expression Validation Tests

    [Fact]
    public void ValidateDefinition_ValidConditionKeywords_ReturnsSuccess()
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
            Edges = [new PipelineEdge { From = "node1", To = "node2", Condition = "always" }],
            EntryPoint = "node1",
            ExitPoints = ["node2"]
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDefinition_ValidConditionExpression_ReturnsSuccess()
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
            Edges = [new PipelineEdge { From = "node1", To = "node2", Condition = "confidence >= 0.7" }],
            EntryPoint = "node1",
            ExitPoints = ["node2"]
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDefinition_UnsupportedOperator_ReturnsWarning()
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
            Edges = [new PipelineEdge { From = "node1", To = "node2", Condition = "success === true" }],
            EntryPoint = "node1",
            ExitPoints = ["node2"]
        };

        // Act
        var result = _validator.ValidateDefinition(pipeline);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Code == "UNSUPPORTED_OPERATOR");
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
                new PipelineNode { Id = "node1", PluginId = "plugin1" },
                new PipelineNode { Id = "node2", PluginId = "plugin2" }
            ],
            Edges = [new PipelineEdge { From = "node1", To = "node2" }],
            EntryPoint = "node1",
            ExitPoints = ["node2"]
        };
    }

    #endregion
}
