using System.Text.Json;

using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal class GetAiQueryDrillQueryHandler : IQueryHandler<GetAiQueryDrillQuery, AiQueryDrillResult?>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetAiQueryDrillQueryHandler> _logger;

    public GetAiQueryDrillQueryHandler(MeepleAiDbContext db, ILogger<GetAiQueryDrillQueryHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiQueryDrillResult?> Handle(GetAiQueryDrillQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Id == Guid.Empty)
        {
            return null;
        }

        var entity = await _db.AiRequestLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(log => log.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        var chunks = DeserializeOrEmpty<List<RetrievedChunkDto>>(entity.ChunksJson, request.Id, nameof(entity.ChunksJson))
                     ?? new List<RetrievedChunkDto>();

        var breakdown = DeserializeOrEmpty<LatencyBreakdownDto>(entity.BreakdownJson, request.Id, nameof(entity.BreakdownJson));

        return new AiQueryDrillResult
        {
            Request = entity,
            Chunks = chunks,
            Breakdown = breakdown,
        };
    }

    private T? DeserializeOrEmpty<T>(string? json, Guid requestId, string columnName) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
#pragma warning disable CA1031 // Drill is read-only telemetry — corrupted JSON must degrade to fallback, not fail the endpoint.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialize {Column} for AiRequestLog {Id}; surfacing as null.",
                columnName,
                requestId);
            return null;
        }
    }
}
