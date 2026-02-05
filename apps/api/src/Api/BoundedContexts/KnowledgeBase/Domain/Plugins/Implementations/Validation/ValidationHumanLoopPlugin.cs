// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3423 - Validation Plugins
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Validation;

/// <summary>
/// Human-in-the-loop validation plugin.
/// Queues low-confidence responses for human review.
/// </summary>
[RagPlugin("validation-human-loop-v1",
    Category = PluginCategory.Validation,
    Name = "Human Loop Validation",
    Description = "Queues low-confidence responses for human review",
    Author = "MeepleAI")]
public sealed class ValidationHumanLoopPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "validation-human-loop-v1";

    /// <inheritdoc />
    public override string Name => "Human Loop Validation";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Validation;

    /// <inheritdoc />
    protected override string Description => "Queues low-confidence responses for human review";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["validation", "human-loop", "review", "quality"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["human-review", "queue-management", "auto-approval"];

    // In-memory review queue (production uses persistent storage)
    private static readonly ConcurrentDictionary<string, ReviewItem> _reviewQueue = new(StringComparer.Ordinal);

    public ValidationHumanLoopPlugin(ILogger<ValidationHumanLoopPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var (response, confidence) = ParsePayload(input.Payload);

        if (string.IsNullOrWhiteSpace(response))
        {
            return PluginOutput.Failed(input.ExecutionId, "Response is required for validation", "MISSING_RESPONSE");
        }

        var customConfig = ParseCustomConfig(config);

        var requiresReview = confidence < customConfig.ConfidenceThreshold;
        var passed = !requiresReview;
        string? reviewId = null;
        var queuedForReview = false;

        if (requiresReview)
        {
            // Queue for human review
            reviewId = Guid.NewGuid().ToString("N");
            var reviewItem = new ReviewItem(
                reviewId,
                response,
                confidence,
                customConfig.QueueName,
                DateTimeOffset.UtcNow);

            _reviewQueue[reviewId] = reviewItem;
            queuedForReview = true;

            Logger.LogInformation(
                "Queued for human review: ReviewId={ReviewId}, Confidence={Confidence:F2}, Queue={Queue}",
                reviewId, confidence, customConfig.QueueName);

            // Check for auto-approval timeout
            if (customConfig.AutoApproveAfterMs.HasValue)
            {
                // In production, this would be handled by a background job
                // For now, we simulate immediate auto-approval check
                var alreadyApproved = await CheckAutoApprovalAsync(
                    reviewId,
                    customConfig.AutoApproveAfterMs.Value,
                    cancellationToken).ConfigureAwait(false);

                if (alreadyApproved)
                {
                    passed = true;
                    queuedForReview = false;
                }
            }
        }

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            passed = passed,
            requiresHumanReview = requiresReview,
            queuedForReview = queuedForReview,
            reviewId = reviewId
        }));

        return PluginOutput.Successful(input.ExecutionId, result, confidence);
    }

    private static async Task<bool> CheckAutoApprovalAsync(
        string reviewId,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        // Simulate checking if already approved
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        // In production, check if human has reviewed within timeout
        // For demo, always return false (not yet approved)
        return false;
    }

    /// <summary>
    /// Approves a queued review item.
    /// </summary>
    public static bool ApproveReview(string reviewId)
    {
        if (_reviewQueue.TryRemove(reviewId, out _))
        {
            // In production, mark as approved in persistent storage
            return true;
        }
        return false;
    }

    /// <summary>
    /// Rejects a queued review item.
    /// </summary>
    public static bool RejectReview(string reviewId, string reason)
    {
        if (_reviewQueue.TryRemove(reviewId, out _))
        {
            // In production, mark as rejected with reason
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets pending review items for a queue.
    /// </summary>
    public static IReadOnlyList<ReviewItem> GetPendingReviews(string queueName)
    {
        return _reviewQueue.Values
            .Where(r => string.Equals(r.QueueName, queueName, StringComparison.Ordinal))
            .OrderBy(r => r.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Gets the count of pending reviews.
    /// </summary>
    public static int GetPendingReviewCount(string? queueName = null)
    {
        if (string.IsNullOrEmpty(queueName))
        {
            return _reviewQueue.Count;
        }
        return _reviewQueue.Values.Count(r => string.Equals(r.QueueName, queueName, StringComparison.Ordinal));
    }

    private static (string Response, double Confidence) ParsePayload(JsonDocument payload)
    {
        var response = string.Empty;
        var confidence = 0.5;

        if (payload.RootElement.TryGetProperty("response", out var respElement))
        {
            response = respElement.GetString() ?? string.Empty;
        }

        if (payload.RootElement.TryGetProperty("confidence", out var confElement))
        {
            confidence = confElement.GetDouble();
        }

        return (response, confidence);
    }

    private static HumanLoopConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new HumanLoopConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new HumanLoopConfig
        {
            ConfidenceThreshold = root.TryGetProperty("confidenceThreshold", out var ct) ? ct.GetDouble() : 0.7,
            QueueName = root.TryGetProperty("queueName", out var qn) ? qn.GetString() ?? "default" : "default",
            AutoApproveAfterMs = root.TryGetProperty("autoApproveAfterMs", out var aa) ? aa.GetInt32() : null
        };
    }

    /// <inheritdoc />
    protected override ValidationResult ValidateInputCore(PluginInput input)
    {
        var errors = new List<ValidationError>();

        if (!input.Payload.RootElement.TryGetProperty("response", out var respElement) ||
            string.IsNullOrWhiteSpace(respElement.GetString()))
        {
            errors.Add(new ValidationError
            {
                Message = "Response is required in payload",
                PropertyPath = "payload.response",
                Code = "MISSING_RESPONSE"
            });
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure([.. errors]);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateInputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "response": { "type": "string" },
                "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
            },
            "required": ["response"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateOutputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "passed": { "type": "boolean" },
                "requiresHumanReview": { "type": "boolean" },
                "queuedForReview": { "type": "boolean" },
                "reviewId": { "type": ["string", "null"] }
            },
            "required": ["passed", "requiresHumanReview"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateConfigSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "confidenceThreshold": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "default": 0.7,
                    "description": "Below this → human review"
                },
                "queueName": {
                    "type": "string",
                    "default": "default"
                },
                "autoApproveAfterMs": {
                    "type": "integer",
                    "minimum": 0,
                    "description": "Auto-approve if no review within this time"
                }
            }
        }
        """);
    }

    public sealed record ReviewItem(
        string Id,
        string Response,
        double Confidence,
        string QueueName,
        DateTimeOffset CreatedAt);

    private sealed class HumanLoopConfig
    {
        public double ConfidenceThreshold { get; init; } = 0.7;
        public string QueueName { get; init; } = "default";
        public int? AutoApproveAfterMs { get; init; }
    }
}
