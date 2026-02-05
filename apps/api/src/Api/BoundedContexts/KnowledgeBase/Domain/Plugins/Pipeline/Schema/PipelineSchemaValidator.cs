// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3416 - Pipeline Definition Schema
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Schema;

/// <summary>
/// Validates pipeline definitions against the schema.
/// Provides detailed error messages for invalid configurations.
/// </summary>
public sealed class PipelineSchemaValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Validates a JSON pipeline definition.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    public ValidationResult ValidateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ValidationResult.Failure(new ValidationError
            {
                Message = "Pipeline JSON cannot be empty",
                PropertyPath = "",
                Code = "EMPTY_JSON"
            });
        }

        try
        {
            var definition = JsonSerializer.Deserialize<PipelineDefinition>(json, JsonOptions);
            if (definition == null)
            {
                return ValidationResult.Failure(new ValidationError
                {
                    Message = "Failed to parse pipeline definition",
                    PropertyPath = "",
                    Code = "PARSE_ERROR"
                });
            }

            return ValidateDefinition(definition);
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure(new ValidationError
            {
                Message = $"Invalid JSON: {ex.Message}",
                PropertyPath = ex.Path ?? "",
                Code = "JSON_ERROR"
            });
        }
    }

    /// <summary>
    /// Validates a pipeline definition object.
    /// </summary>
    /// <param name="definition">The pipeline definition to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    public ValidationResult ValidateDefinition(PipelineDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Schema version validation
        ValidateSchemaVersion(definition, errors);

        // Required fields validation
        ValidateRequiredFields(definition, errors);

        // Nodes validation
        ValidateNodes(definition, errors, warnings);

        // Edges validation
        ValidateEdges(definition, errors, warnings);

        // Entry/exit point validation
        ValidateEntryExitPoints(definition, errors);

        // Global settings validation
        ValidateGlobalSettings(definition, errors, warnings);

        if (errors.Count > 0)
        {
            return ValidationResult.Failure([.. errors]);
        }

        return warnings.Count > 0
            ? ValidationResult.SuccessWithWarnings([.. warnings])
            : ValidationResult.Success();
    }

    private static void ValidateSchemaVersion(PipelineDefinition definition, List<ValidationError> errors)
    {
        // Schema version should be "1.0" or higher
        if (!string.IsNullOrWhiteSpace(definition.SchemaVersion))
        {
            if (!Version.TryParse(definition.SchemaVersion, out var version))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Invalid schema version format: {definition.SchemaVersion}. Expected semantic version (e.g., '1.0').",
                    PropertyPath = "schemaVersion",
                    Code = "INVALID_SCHEMA_VERSION"
                });
            }
            else if (version.Major < 1)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Unsupported schema version: {definition.SchemaVersion}. Minimum supported: 1.0",
                    PropertyPath = "schemaVersion",
                    Code = "UNSUPPORTED_SCHEMA_VERSION"
                });
            }
        }
    }

    private static void ValidateRequiredFields(PipelineDefinition definition, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            errors.Add(new ValidationError
            {
                Message = "Pipeline ID is required",
                PropertyPath = "id",
                Code = "MISSING_ID"
            });
        }
        else if (!IsValidIdentifier(definition.Id))
        {
            errors.Add(new ValidationError
            {
                Message = $"Invalid pipeline ID format: {definition.Id}. Must be alphanumeric with hyphens.",
                PropertyPath = "id",
                Code = "INVALID_ID_FORMAT"
            });
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add(new ValidationError
            {
                Message = "Pipeline name is required",
                PropertyPath = "name",
                Code = "MISSING_NAME"
            });
        }
    }

    private static void ValidateNodes(PipelineDefinition definition, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (definition.Nodes == null || definition.Nodes.Count == 0)
        {
            errors.Add(new ValidationError
            {
                Message = "Pipeline must have at least one node",
                PropertyPath = "nodes",
                Code = "NO_NODES"
            });
            return;
        }

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < definition.Nodes.Count; i++)
        {
            var node = definition.Nodes[i];
            var nodePath = $"nodes[{i}]";

            // Validate node ID
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Node at index {i} is missing ID",
                    PropertyPath = $"{nodePath}.id",
                    Code = "MISSING_NODE_ID"
                });
            }
            else
            {
                if (!IsValidIdentifier(node.Id))
                {
                    errors.Add(new ValidationError
                    {
                        Message = $"Invalid node ID format: {node.Id}",
                        PropertyPath = $"{nodePath}.id",
                        Code = "INVALID_NODE_ID_FORMAT"
                    });
                }

                if (!nodeIds.Add(node.Id))
                {
                    errors.Add(new ValidationError
                    {
                        Message = $"Duplicate node ID: {node.Id}",
                        PropertyPath = $"{nodePath}.id",
                        Code = "DUPLICATE_NODE_ID"
                    });
                }
            }

            // Validate plugin ID
            if (string.IsNullOrWhiteSpace(node.PluginId))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Node '{node.Id}' is missing plugin ID",
                    PropertyPath = $"{nodePath}.pluginId",
                    Code = "MISSING_PLUGIN_ID"
                });
            }

            // Validate timeout
            if (node.TimeoutMs < 0)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Node '{node.Id}' has invalid timeout: {node.TimeoutMs}",
                    PropertyPath = $"{nodePath}.timeoutMs",
                    Code = "INVALID_TIMEOUT"
                });
            }
            else if (node.TimeoutMs > 0 && node.TimeoutMs < 100)
            {
                warnings.Add(new ValidationWarning
                {
                    Message = $"Node '{node.Id}' has very short timeout ({node.TimeoutMs}ms)",
                    PropertyPath = $"{nodePath}.timeoutMs",
                    Code = "SHORT_TIMEOUT"
                });
            }

            // Validate retry config
            if (node.Retry != null)
            {
                ValidateRetryConfig(node.Retry, $"{nodePath}.retry", node.Id, errors, warnings);
            }
        }
    }

    private static void ValidateRetryConfig(
        RetryConfig retry,
        string path,
        string nodeId,
        List<ValidationError> errors,
        List<ValidationWarning> warnings)
    {
        if (retry.MaxAttempts < 0)
        {
            errors.Add(new ValidationError
            {
                Message = $"Node '{nodeId}' has invalid max attempts: {retry.MaxAttempts}",
                PropertyPath = $"{path}.maxAttempts",
                Code = "INVALID_MAX_ATTEMPTS"
            });
        }
        else if (retry.MaxAttempts > 10)
        {
            warnings.Add(new ValidationWarning
            {
                Message = $"Node '{nodeId}' has high max attempts ({retry.MaxAttempts}). Consider if this is appropriate.",
                PropertyPath = $"{path}.maxAttempts",
                Code = "HIGH_MAX_ATTEMPTS"
            });
        }

        if (retry.BackoffMs < 0)
        {
            errors.Add(new ValidationError
            {
                Message = $"Node '{nodeId}' has invalid backoff: {retry.BackoffMs}",
                PropertyPath = $"{path}.backoffMs",
                Code = "INVALID_BACKOFF"
            });
        }
    }

    private static void ValidateEdges(PipelineDefinition definition, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (definition.Edges == null)
        {
            return;
        }

        // Skip edge validation if nodes are missing - ValidateNodes already reports this error
        if (definition.Nodes == null || definition.Nodes.Count == 0)
        {
            return;
        }

        var nodeIds = definition.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        var edgeSet = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < definition.Edges.Count; i++)
        {
            var edge = definition.Edges[i];
            var edgePath = $"edges[{i}]";

            // Validate from node
            if (string.IsNullOrWhiteSpace(edge.From))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Edge at index {i} is missing 'from' node",
                    PropertyPath = $"{edgePath}.from",
                    Code = "MISSING_EDGE_FROM"
                });
            }
            else if (!nodeIds.Contains(edge.From))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Edge references non-existent source node: {edge.From}",
                    PropertyPath = $"{edgePath}.from",
                    Code = "INVALID_EDGE_FROM"
                });
            }

            // Validate to node
            if (string.IsNullOrWhiteSpace(edge.To))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Edge at index {i} is missing 'to' node",
                    PropertyPath = $"{edgePath}.to",
                    Code = "MISSING_EDGE_TO"
                });
            }
            else if (!nodeIds.Contains(edge.To))
            {
                errors.Add(new ValidationError
                {
                    Message = $"Edge references non-existent target node: {edge.To}",
                    PropertyPath = $"{edgePath}.to",
                    Code = "INVALID_EDGE_TO"
                });
            }

            // Check for duplicate edges
            var edgeKey = $"{edge.From}->{edge.To}";
            if (!edgeSet.Add(edgeKey))
            {
                warnings.Add(new ValidationWarning
                {
                    Message = $"Duplicate edge: {edgeKey}",
                    PropertyPath = edgePath,
                    Code = "DUPLICATE_EDGE"
                });
            }

            // Validate condition expression
            if (!string.IsNullOrWhiteSpace(edge.Condition))
            {
                ValidateConditionExpression(edge.Condition, $"{edgePath}.condition", errors, warnings);
            }

            // Validate priority
            if (edge.Priority < 0)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Edge {edgeKey} has invalid priority: {edge.Priority}",
                    PropertyPath = $"{edgePath}.priority",
                    Code = "INVALID_PRIORITY"
                });
            }
        }
    }

    private static void ValidateConditionExpression(
        string condition,
        string path,
        List<ValidationError> errors,
        List<ValidationWarning> warnings)
    {
        var trimmed = condition.Trim().ToLowerInvariant();

        // Keywords are always valid
        if (trimmed is "always" or "never" or "true" or "false")
        {
            return;
        }

        // Check for basic comparison operators
        var operators = new[] { ">=", "<=", "!=", "==", ">", "<" };
        var hasOperator = operators.Any(op => condition.Contains(op, StringComparison.Ordinal));

        if (!hasOperator)
        {
            warnings.Add(new ValidationWarning
            {
                Message = $"Condition expression may be invalid: {condition}. Expected comparison or keyword.",
                PropertyPath = path,
                Code = "UNUSUAL_CONDITION"
            });
        }

        // Check for common syntax issues
        if (condition.Contains("===", StringComparison.Ordinal))
        {
            warnings.Add(new ValidationWarning
            {
                Message = "Condition uses '===' which is not supported. Use '==' instead.",
                PropertyPath = path,
                Code = "UNSUPPORTED_OPERATOR"
            });
        }
    }

    private static void ValidateEntryExitPoints(PipelineDefinition definition, List<ValidationError> errors)
    {
        var nodeIds = definition.Nodes?.Select(n => n.Id).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        // Validate entry point
        if (string.IsNullOrWhiteSpace(definition.EntryPoint))
        {
            errors.Add(new ValidationError
            {
                Message = "Entry point is required",
                PropertyPath = "entryPoint",
                Code = "MISSING_ENTRY_POINT"
            });
        }
        else if (!nodeIds.Contains(definition.EntryPoint))
        {
            errors.Add(new ValidationError
            {
                Message = $"Entry point references non-existent node: {definition.EntryPoint}",
                PropertyPath = "entryPoint",
                Code = "INVALID_ENTRY_POINT"
            });
        }

        // Validate exit points
        if (definition.ExitPoints == null || definition.ExitPoints.Count == 0)
        {
            errors.Add(new ValidationError
            {
                Message = "At least one exit point is required",
                PropertyPath = "exitPoints",
                Code = "NO_EXIT_POINTS"
            });
        }
        else
        {
            for (var i = 0; i < definition.ExitPoints.Count; i++)
            {
                var exitPoint = definition.ExitPoints[i];
                if (!nodeIds.Contains(exitPoint))
                {
                    errors.Add(new ValidationError
                    {
                        Message = $"Exit point references non-existent node: {exitPoint}",
                        PropertyPath = $"exitPoints[{i}]",
                        Code = "INVALID_EXIT_POINT"
                    });
                }
            }
        }
    }

    private static void ValidateGlobalSettings(
        PipelineDefinition definition,
        List<ValidationError> errors,
        List<ValidationWarning> warnings)
    {
        // Validate global timeout
        if (definition.GlobalTimeoutMs < 0)
        {
            errors.Add(new ValidationError
            {
                Message = $"Invalid global timeout: {definition.GlobalTimeoutMs}",
                PropertyPath = "globalTimeoutMs",
                Code = "INVALID_GLOBAL_TIMEOUT"
            });
        }
        else if (definition.GlobalTimeoutMs > 0 && definition.GlobalTimeoutMs < 1000)
        {
            warnings.Add(new ValidationWarning
            {
                Message = $"Global timeout is very short ({definition.GlobalTimeoutMs}ms)",
                PropertyPath = "globalTimeoutMs",
                Code = "SHORT_GLOBAL_TIMEOUT"
            });
        }

        // Validate max parallelism
        if (definition.MaxParallelism < 1)
        {
            errors.Add(new ValidationError
            {
                Message = $"Max parallelism must be at least 1: {definition.MaxParallelism}",
                PropertyPath = "maxParallelism",
                Code = "INVALID_MAX_PARALLELISM"
            });
        }
        else if (definition.MaxParallelism > 100)
        {
            warnings.Add(new ValidationWarning
            {
                Message = $"Max parallelism is very high ({definition.MaxParallelism}). Consider resource constraints.",
                PropertyPath = "maxParallelism",
                Code = "HIGH_MAX_PARALLELISM"
            });
        }
    }

    private static bool IsValidIdentifier(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        // Allow alphanumeric, hyphens, and underscores
        foreach (var c in id)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
