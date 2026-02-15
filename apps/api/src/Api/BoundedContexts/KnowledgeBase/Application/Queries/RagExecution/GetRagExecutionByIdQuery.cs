using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;

/// <summary>
/// Get a single RAG execution with full detail including trace.
/// Issue #4458: RAG Execution History
/// </summary>
internal record GetRagExecutionByIdQuery(Guid Id) : IQuery<RagExecutionDetailDto?>;

internal record RagExecutionDetailDto(
    Guid Id,
    string Query,
    Guid? AgentDefinitionId,
    string? AgentName,
    string Strategy,
    string? Model,
    string? Provider,
    Guid? GameId,
    bool IsPlayground,
    int TotalLatencyMs,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal TotalCost,
    double? Confidence,
    bool CacheHit,
    string Status,
    string? ErrorMessage,
    string? ExecutionTrace,
    DateTime CreatedAt);

internal sealed class GetRagExecutionByIdQueryHandler
    : IQueryHandler<GetRagExecutionByIdQuery, RagExecutionDetailDto?>
{
    private readonly IRagExecutionRepository _repository;

    public GetRagExecutionByIdQueryHandler(IRagExecutionRepository repository)
    {
        _repository = repository;
    }

    public async Task<RagExecutionDetailDto?> Handle(
        GetRagExecutionByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return null;

        return new RagExecutionDetailDto(
            entity.Id,
            entity.Query,
            entity.AgentDefinitionId,
            entity.AgentName,
            entity.Strategy,
            entity.Model,
            entity.Provider,
            entity.GameId,
            entity.IsPlayground,
            entity.TotalLatencyMs,
            entity.PromptTokens,
            entity.CompletionTokens,
            entity.TotalTokens,
            entity.TotalCost,
            entity.Confidence,
            entity.CacheHit,
            entity.Status,
            entity.ErrorMessage,
            entity.ExecutionTrace,
            entity.CreatedAt);
    }
}
