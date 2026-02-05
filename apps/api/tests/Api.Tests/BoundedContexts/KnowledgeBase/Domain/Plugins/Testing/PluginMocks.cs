// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3430 - Plugin Testing Framework
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;

/// <summary>
/// Static factory class for creating mock plugin inputs and outputs.
/// Provides test data generators for various plugin testing scenarios.
/// </summary>
public static class PluginMocks
{
    #region Input Generators

    /// <summary>
    /// Creates a valid plugin input with minimal data.
    /// </summary>
    public static PluginInput CreateValidInput()
    {
        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse("{}")
        };
    }

    /// <summary>
    /// Creates a valid plugin input with a specific execution ID.
    /// </summary>
    public static PluginInput CreateValidInput(Guid executionId)
    {
        return new PluginInput
        {
            ExecutionId = executionId,
            Payload = JsonDocument.Parse("{}")
        };
    }

    /// <summary>
    /// Creates a plugin input with custom JSON payload.
    /// </summary>
    public static PluginInput CreateInputWithPayload(string jsonPayload)
    {
        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse(jsonPayload)
        };
    }

    /// <summary>
    /// Creates a plugin input with a query payload (common for RAG plugins).
    /// </summary>
    public static PluginInput CreateQueryInput(string query, Guid? gameId = null, Guid? userId = null)
    {
        var payload = new
        {
            query,
            timestamp = DateTimeOffset.UtcNow.ToString("O")
        };

        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse(JsonSerializer.Serialize(payload)),
            GameId = gameId,
            UserId = userId
        };
    }

    /// <summary>
    /// Creates a plugin input with pipeline context from previous nodes.
    /// </summary>
    public static PluginInput CreateInputWithContext(
        string jsonPayload,
        IReadOnlyDictionary<string, JsonDocument> pipelineContext)
    {
        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse(jsonPayload),
            PipelineContext = pipelineContext
        };
    }

    /// <summary>
    /// Creates a plugin input with retrieval results context.
    /// </summary>
    public static PluginInput CreateRetrievalContextInput(
        string query,
        IEnumerable<RetrievedDocument> documents)
    {
        var payload = new { query };
        var context = new Dictionary<string, JsonDocument>(StringComparer.Ordinal)
        {
            ["retrieval"] = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                documents = documents.Select(d => new
                {
                    d.Id,
                    d.Content,
                    d.Score,
                    d.Metadata
                })
            }))
        };

        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse(JsonSerializer.Serialize(payload)),
            PipelineContext = context
        };
    }

    /// <summary>
    /// Creates an invalid plugin input with empty execution ID.
    /// </summary>
    public static PluginInput CreateInvalidInput_EmptyExecutionId()
    {
        return new PluginInput
        {
            ExecutionId = Guid.Empty,
            Payload = JsonDocument.Parse("{}")
        };
    }

    #endregion

    #region Output Generators

    /// <summary>
    /// Creates a successful plugin output with minimal data.
    /// </summary>
    public static PluginOutput CreateSuccessfulOutput(Guid executionId)
    {
        return PluginOutput.Successful(
            executionId,
            JsonDocument.Parse("""{"success": true}"""));
    }

    /// <summary>
    /// Creates a successful plugin output with custom result.
    /// </summary>
    public static PluginOutput CreateSuccessfulOutput(Guid executionId, string jsonResult)
    {
        return PluginOutput.Successful(
            executionId,
            JsonDocument.Parse(jsonResult));
    }

    /// <summary>
    /// Creates a successful plugin output with confidence score.
    /// </summary>
    public static PluginOutput CreateSuccessfulOutput(Guid executionId, double confidence)
    {
        return PluginOutput.Successful(
            executionId,
            JsonDocument.Parse("""{"success": true}"""),
            confidence);
    }

    /// <summary>
    /// Creates a failed plugin output.
    /// </summary>
    public static PluginOutput CreateFailedOutput(Guid executionId, string errorMessage, string? errorCode = null)
    {
        return PluginOutput.Failed(executionId, errorMessage, errorCode);
    }

    /// <summary>
    /// Creates a routing decision output.
    /// </summary>
    public static PluginOutput CreateRoutingOutput(
        Guid executionId,
        string routeDecision,
        double confidence,
        string? reasoning = null)
    {
        var result = new
        {
            route = routeDecision,
            confidence,
            reasoning
        };

        return new PluginOutput
        {
            ExecutionId = executionId,
            Success = true,
            Result = JsonDocument.Parse(JsonSerializer.Serialize(result)),
            Confidence = confidence
        };
    }

    /// <summary>
    /// Creates a retrieval result output.
    /// </summary>
    public static PluginOutput CreateRetrievalOutput(
        Guid executionId,
        IEnumerable<RetrievedDocument> documents,
        double? confidence = null)
    {
        var result = new
        {
            documents = documents.Select(d => new
            {
                d.Id,
                d.Content,
                d.Score,
                d.Metadata
            }).ToList(),
            totalCount = documents.Count()
        };

        return new PluginOutput
        {
            ExecutionId = executionId,
            Success = true,
            Result = JsonDocument.Parse(JsonSerializer.Serialize(result)),
            Confidence = confidence,
            Metrics = new PluginExecutionMetrics { ItemsProcessed = documents.Count() }
        };
    }

    /// <summary>
    /// Creates a generation result output.
    /// </summary>
    public static PluginOutput CreateGenerationOutput(
        Guid executionId,
        string generatedText,
        int inputTokens = 100,
        int outputTokens = 50,
        double? confidence = null)
    {
        var result = new
        {
            text = generatedText,
            model = "test-model",
            finishReason = "stop"
        };

        return new PluginOutput
        {
            ExecutionId = executionId,
            Success = true,
            Result = JsonDocument.Parse(JsonSerializer.Serialize(result)),
            Confidence = confidence,
            Metrics = new PluginExecutionMetrics
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            }
        };
    }

    #endregion

    #region Config Generators

    /// <summary>
    /// Creates a default plugin configuration.
    /// </summary>
    public static PluginConfig CreateDefaultConfig()
    {
        return PluginConfig.Default();
    }

    /// <summary>
    /// Creates a configuration with custom timeout.
    /// </summary>
    public static PluginConfig CreateConfigWithTimeout(int timeoutMs)
    {
        return new PluginConfig { TimeoutMs = timeoutMs };
    }

    /// <summary>
    /// Creates a configuration with retry settings.
    /// </summary>
    public static PluginConfig CreateConfigWithRetries(int maxRetries, int backoffMs = 1000)
    {
        return new PluginConfig
        {
            MaxRetries = maxRetries,
            RetryBackoffMs = backoffMs,
            ExponentialBackoff = true
        };
    }

    /// <summary>
    /// Creates a configuration with custom JSON settings.
    /// </summary>
    public static PluginConfig CreateConfigWithCustom(string customConfigJson)
    {
        return new PluginConfig
        {
            CustomConfig = JsonDocument.Parse(customConfigJson)
        };
    }

    /// <summary>
    /// Creates an invalid configuration with negative timeout.
    /// </summary>
    public static PluginConfig CreateInvalidConfig_NegativeTimeout()
    {
        return new PluginConfig { TimeoutMs = -1 };
    }

    /// <summary>
    /// Creates an invalid configuration with negative retries.
    /// </summary>
    public static PluginConfig CreateInvalidConfig_NegativeRetries()
    {
        return new PluginConfig { MaxRetries = -1 };
    }

    #endregion

    #region Health Check Generators

    /// <summary>
    /// Creates a healthy health check result.
    /// </summary>
    public static HealthCheckResult CreateHealthyResult(double durationMs = 5)
    {
        return HealthCheckResult.Healthy() with { CheckDurationMs = durationMs };
    }

    /// <summary>
    /// Creates a degraded health check result.
    /// </summary>
    public static HealthCheckResult CreateDegradedResult(string message, double durationMs = 10)
    {
        return HealthCheckResult.Degraded(message) with { CheckDurationMs = durationMs };
    }

    /// <summary>
    /// Creates an unhealthy health check result.
    /// </summary>
    public static HealthCheckResult CreateUnhealthyResult(string message, double durationMs = 15)
    {
        return HealthCheckResult.Unhealthy(message) with { CheckDurationMs = durationMs };
    }

    #endregion

    #region Validation Generators

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult CreateValidationSuccess()
    {
        return ValidationResult.Success();
    }

    /// <summary>
    /// Creates a validation result with warnings.
    /// </summary>
    public static ValidationResult CreateValidationWithWarnings(params string[] warnings)
    {
        return ValidationResult.SuccessWithWarnings(
            warnings.Select(w => new ValidationWarning { Message = w }).ToArray());
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult CreateValidationFailure(params (string message, string? code)[] errors)
    {
        return ValidationResult.Failure(
            errors.Select(e => new ValidationError { Message = e.message, Code = e.code }).ToArray());
    }

    #endregion

    #region Test Data Helpers

    /// <summary>
    /// Creates a sample retrieved document for testing.
    /// </summary>
    public static RetrievedDocument CreateRetrievedDocument(
        string? id = null,
        string? content = null,
        double score = 0.9,
        Dictionary<string, object>? metadata = null)
    {
        return new RetrievedDocument
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Content = content ?? "Sample document content for testing purposes.",
            Score = score,
            Metadata = metadata ?? new Dictionary<string, object>
            {
                ["source"] = "test",
                ["page"] = 1
            }
        };
    }

    /// <summary>
    /// Creates multiple sample retrieved documents.
    /// </summary>
    public static IReadOnlyList<RetrievedDocument> CreateRetrievedDocuments(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => CreateRetrievedDocument(
                id: $"doc-{i}",
                content: $"Document {i} content for testing.",
                score: 1.0 - (i * 0.1)))
            .ToList();
    }

    /// <summary>
    /// Creates a sample game rules query input.
    /// </summary>
    public static PluginInput CreateGameRulesQueryInput(Guid gameId, string question)
    {
        var payload = new
        {
            query = question,
            queryType = "rules",
            gameId = gameId.ToString()
        };

        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse(JsonSerializer.Serialize(payload)),
            GameId = gameId
        };
    }

    /// <summary>
    /// Creates a sample strategy query input.
    /// </summary>
    public static PluginInput CreateStrategyQueryInput(Guid gameId, string question)
    {
        var payload = new
        {
            query = question,
            queryType = "strategy",
            gameId = gameId.ToString()
        };

        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse(JsonSerializer.Serialize(payload)),
            GameId = gameId
        };
    }

    #endregion
}

/// <summary>
/// Represents a retrieved document for testing retrieval plugins.
/// </summary>
public sealed record RetrievedDocument
{
    /// <summary>
    /// Document identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Document content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Relevance score (0.0 to 1.0).
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
