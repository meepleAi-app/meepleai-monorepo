using System.Text.Json;

using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal class LogAiRequestCommandHandler : ICommandHandler<LogAiRequestCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LogAiRequestCommandHandler> _logger;
    private readonly QualityMetrics _qualityMetrics;

    public LogAiRequestCommandHandler(
        MeepleAiDbContext db,
        ILogger<LogAiRequestCommandHandler> logger,
        QualityMetrics qualityMetrics,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _qualityMetrics = qualityMetrics ?? throw new ArgumentNullException(nameof(qualityMetrics));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task Handle(LogAiRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
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
                IsLowQuality = command.QualityScores?.IsLowQuality ?? false,
                // #1728: Serialize drill payload into jsonb columns. Null when
                // the caller did not pass chunks/breakdown — surfaces as
                // "limited drill" / "breakdown unavailable" on the FE.
                ChunksJson = command.Chunks is { Count: > 0 }
                    ? JsonSerializer.Serialize(command.Chunks, JsonOptions)
                    : null,
                BreakdownJson = command.Breakdown is not null
                    ? JsonSerializer.Serialize(command.Breakdown, JsonOptions)
                    : null,
            };

            // #3817: gli stessi punteggi che finiscono in colonna vanno anche in metrica.
            // Prima di SaveChanges di proposito: un guasto del DB non deve far perdere la
            // misura, che non dipende dalla persistenza. Solo il percorso QA passa oggi
            // QualityScores; per gli altri endpoint resta null e non emettiamo nulla —
            // una serie assente e' informativa, una a zero mentirebbe (#3814).
            if (command.QualityScores is not null)
            {
                var (agentType, operation) = SplitEndpoint(command.Endpoint);
                _qualityMetrics.RecordQualityScores(command.QualityScores, agentType, operation);
            }

            _db.AiRequestLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// #3817: l'Endpoint del log ("qa", "qa-stream", "setup-stream", "explain") mescola due cose
    /// che la metrica tiene distinte — quale agente ha risposto e con quale modalita'. Il suffisso
    /// "-stream" e' l'unica forma composta in uso, quindi lo separiamo e il resto e' l'agente.
    /// </summary>
    private static (string AgentType, string Operation) SplitEndpoint(string endpoint)
    {
        const string StreamSuffix = "-stream";

        return endpoint.EndsWith(StreamSuffix, StringComparison.Ordinal)
            ? (endpoint[..^StreamSuffix.Length], "stream")
            : (endpoint, "answer");
    }
}
