using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

public class LogAiRequestCommandHandler : ICommandHandler<LogAiRequestCommand>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LogAiRequestCommandHandler> _logger;

    public LogAiRequestCommandHandler(
        MeepleAiDbContext db,
        ILogger<LogAiRequestCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task Handle(LogAiRequestCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var log = new AiRequestLogEntity
            {
                UserId = command.UserId != null && Guid.TryParse(command.UserId, out var userGuid) ? userGuid : null,
                ApiKeyId = command.ApiKeyId != null && Guid.TryParse(command.ApiKeyId, out var apiKeyGuid) ? apiKeyGuid : null,
                GameId = command.GameId != null && Guid.TryParse(command.GameId, out var gameGuid) ? gameGuid : null,
                Endpoint = command.Endpoint,
                Query = command.Query,
                ResponseSnippet = command.ResponseSnippet,
                LatencyMs = command.LatencyMs,
                TokenCount = command.TokenCount ?? 0,
                PromptTokens = command.PromptTokens ?? 0,
                CompletionTokens = command.CompletionTokens ?? 0,
                Confidence = command.Confidence,
                Status = command.Status,
                ErrorMessage = command.ErrorMessage,
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent,
                Model = command.Model,
                FinishReason = command.FinishReason,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                // AI-11: Store quality scores if provided
                RagConfidence = command.QualityScores?.RagConfidence,
                LlmConfidence = command.QualityScores?.LlmConfidence,
                CitationQuality = command.QualityScores?.CitationQuality,
                OverallConfidence = command.QualityScores?.OverallConfidence,
                IsLowQuality = command.QualityScores?.IsLowQuality ?? false
            };

            _db.AiRequestLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // RESILIENCE PATTERN: AI request logging must never fail AI operations
            // Rationale: AI request logging is telemetry - failing an AI query because we
            // cannot log the request violates fail-safe principles. The AI operation succeeded
            // and we must return results to the user, even if we cannot track metrics.
            // Context: Logging failures are typically DB-related (connection loss, disk full)
            _logger.LogError(ex, "Failed to log AI request for endpoint {Endpoint}", command.Endpoint);
        }
    }
}
