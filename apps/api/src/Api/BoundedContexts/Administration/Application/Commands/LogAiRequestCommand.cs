using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to log an AI request for telemetry and analytics.
/// This command should never fail - it's a fire-and-forget telemetry operation.
/// </summary>
internal record LogAiRequestCommand(
    string? UserId,
    string? GameId,
    string Endpoint,
    string? Query,
    string? ResponseSnippet,
    int LatencyMs,
    int? TokenCount = null,
    double? Confidence = null,
    string Status = "Success",
    string? ErrorMessage = null,
    string? IpAddress = null,
    string? UserAgent = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    string? Model = null,
    string? FinishReason = null,
    string? ApiKeyId = null,
    QualityScores? QualityScores = null
) : ICommand;
