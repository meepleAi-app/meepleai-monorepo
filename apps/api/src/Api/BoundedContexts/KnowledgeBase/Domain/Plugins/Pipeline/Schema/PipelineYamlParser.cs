// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3416 - Pipeline Definition Schema
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Schema;

/// <summary>
/// Parses pipeline definitions from YAML format.
/// </summary>
public sealed class PipelineYamlParser
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    /// <summary>
    /// Creates a new YAML parser with standard configuration.
    /// </summary>
    public PipelineYamlParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Parses a YAML string into a pipeline definition.
    /// </summary>
    /// <param name="yaml">The YAML content to parse.</param>
    /// <returns>The parsed pipeline definition.</returns>
    /// <exception cref="PipelineParseException">Thrown when parsing fails.</exception>
    public PipelineDefinition Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new PipelineParseException("YAML content cannot be empty");
        }

        try
        {
            // First deserialize to intermediate DTO for flexibility
            var dto = _deserializer.Deserialize<PipelineDefinitionDto>(yaml);
            if (dto == null)
            {
                throw new PipelineParseException("Failed to parse YAML: result was null");
            }

            return MapToDefinition(dto);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new PipelineParseException($"YAML syntax error at line {ex.Start.Line}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Tries to parse a YAML string into a pipeline definition.
    /// </summary>
    /// <param name="yaml">The YAML content to parse.</param>
    /// <param name="result">The parsed pipeline definition, if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public bool TryParse(string yaml, out PipelineDefinition? result)
    {
        try
        {
            result = Parse(yaml);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Converts a pipeline definition to YAML format.
    /// </summary>
    /// <param name="definition">The pipeline definition to serialize.</param>
    /// <returns>The YAML representation.</returns>
    public string ToYaml(PipelineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var dto = MapToDto(definition);
        return _serializer.Serialize(dto);
    }

    private static PipelineDefinition MapToDefinition(PipelineDefinitionDto dto)
    {
        return new PipelineDefinition
        {
            SchemaVersion = dto.SchemaVersion ?? "1.0",
            Id = dto.Id ?? throw new PipelineParseException("Pipeline ID is required"),
            Name = dto.Name ?? throw new PipelineParseException("Pipeline name is required"),
            Version = dto.Version ?? "1.0.0",
            Description = dto.Description,
            Nodes = dto.Nodes?.Select(MapNode).ToList() ?? throw new PipelineParseException("Pipeline nodes are required"),
            Edges = dto.Edges?.Select(MapEdge).ToList() ?? [],
            EntryPoint = dto.EntryPoint ?? throw new PipelineParseException("Entry point is required"),
            ExitPoints = dto.ExitPoints ?? throw new PipelineParseException("Exit points are required"),
            GlobalTimeoutMs = dto.GlobalTimeoutMs ?? 120000,
            MaxParallelism = dto.MaxParallelism ?? 5,
            Tags = dto.Tags ?? []
        };
    }

    private static PipelineNode MapNode(PipelineNodeDto dto)
    {
        return new PipelineNode
        {
            Id = dto.Id ?? throw new PipelineParseException("Node ID is required"),
            PluginId = dto.Plugin ?? dto.PluginId ?? throw new PipelineParseException($"Node '{dto.Id}' is missing plugin"),
            Config = dto.Config != null ? CreatePluginConfig(dto.Config) : null,
            TimeoutMs = dto.Timeout ?? dto.TimeoutMs ?? 30000,
            Retry = dto.Retry != null ? MapRetryConfig(dto.Retry) : null
        };
    }

    private static PipelineEdge MapEdge(PipelineEdgeDto dto)
    {
        return new PipelineEdge
        {
            From = dto.From ?? throw new PipelineParseException("Edge 'from' is required"),
            To = dto.To ?? throw new PipelineParseException("Edge 'to' is required"),
            Condition = dto.Condition,
            Transform = dto.Transform,
            Priority = dto.Priority ?? 0
        };
    }

    private static RetryConfig MapRetryConfig(RetryConfigDto dto)
    {
        return new RetryConfig
        {
            MaxAttempts = dto.MaxAttempts ?? 3,
            BackoffMs = dto.BackoffMs ?? 1000,
            ExponentialBackoff = dto.ExponentialBackoff ?? true,
            MaxBackoffMs = dto.MaxBackoffMs ?? 30000
        };
    }

    private static PluginConfig CreatePluginConfig(Dictionary<string, object> config)
    {
        var json = JsonSerializer.Serialize(config);
        return PluginConfig.Create(customConfig: JsonDocument.Parse(json));
    }

    private static PipelineDefinitionDto MapToDto(PipelineDefinition definition)
    {
        return new PipelineDefinitionDto
        {
            SchemaVersion = definition.SchemaVersion,
            Id = definition.Id,
            Name = definition.Name,
            Version = definition.Version,
            Description = definition.Description,
            Nodes = definition.Nodes.Select(n => new PipelineNodeDto
            {
                Id = n.Id,
                PluginId = n.PluginId,
                Config = n.Config?.CustomConfig != null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(n.Config.CustomConfig.RootElement.GetRawText())
                    : null,
                TimeoutMs = n.TimeoutMs,
                Retry = n.Retry != null ? new RetryConfigDto
                {
                    MaxAttempts = n.Retry.MaxAttempts,
                    BackoffMs = n.Retry.BackoffMs,
                    ExponentialBackoff = n.Retry.ExponentialBackoff,
                    MaxBackoffMs = n.Retry.MaxBackoffMs
                } : null
            }).ToList(),
            Edges = definition.Edges.Select(e => new PipelineEdgeDto
            {
                From = e.From,
                To = e.To,
                Condition = e.Condition,
                Transform = e.Transform,
                Priority = e.Priority
            }).ToList(),
            EntryPoint = definition.EntryPoint,
            ExitPoints = definition.ExitPoints.ToList(),
            GlobalTimeoutMs = definition.GlobalTimeoutMs,
            MaxParallelism = definition.MaxParallelism,
            Tags = definition.Tags.ToList()
        };
    }

    #region DTOs for YAML parsing

    private sealed class PipelineDefinitionDto
    {
        public string? SchemaVersion { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Description { get; set; }
        public List<PipelineNodeDto>? Nodes { get; set; }
        public List<PipelineEdgeDto>? Edges { get; set; }
        public string? EntryPoint { get; set; }
        public List<string>? ExitPoints { get; set; }
        public int? GlobalTimeoutMs { get; set; }
        public int? MaxParallelism { get; set; }
        public List<string>? Tags { get; set; }
    }

    private sealed class PipelineNodeDto
    {
        public string? Id { get; set; }
#pragma warning disable S3459, S1144 // Properties are used for YAML deserialization
        public string? Plugin { get; set; }
#pragma warning restore S3459, S1144
        public string? PluginId { get; set; }
        public Dictionary<string, object>? Config { get; set; }
#pragma warning disable S3459, S1144 // Properties are used for YAML deserialization
        public int? Timeout { get; set; }
#pragma warning restore S3459, S1144
        public int? TimeoutMs { get; set; }
        public RetryConfigDto? Retry { get; set; }
    }

    private sealed class PipelineEdgeDto
    {
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Condition { get; set; }
        public string? Transform { get; set; }
        public int? Priority { get; set; }
    }

    private sealed class RetryConfigDto
    {
        public int? MaxAttempts { get; set; }
        public int? BackoffMs { get; set; }
        public bool? ExponentialBackoff { get; set; }
        public int? MaxBackoffMs { get; set; }
    }

    #endregion
}

/// <summary>
/// Exception thrown when pipeline parsing fails.
/// </summary>
public sealed class PipelineParseException : Exception
{
    /// <summary>
    /// Creates a new parse exception.
    /// </summary>
    public PipelineParseException(string message) : base(message) { }

    /// <summary>
    /// Creates a new parse exception with inner exception.
    /// </summary>
    public PipelineParseException(string message, Exception innerException)
        : base(message, innerException) { }
}
