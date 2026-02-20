using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;
using Api.BoundedContexts.GameManagement.Application.Queries.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.ToolState;

/// <summary>
/// Gets all tool states for a session.
/// Issue #4754: ToolState Entity + Toolkit ↔ Session Integration.
/// </summary>
internal class GetToolStatesQueryHandler : IQueryHandler<GetToolStatesQuery, IReadOnlyList<ToolStateDto>>
{
    private readonly IToolStateRepository _toolStateRepository;

    public GetToolStatesQueryHandler(IToolStateRepository toolStateRepository)
    {
        _toolStateRepository = toolStateRepository ?? throw new ArgumentNullException(nameof(toolStateRepository));
    }

    public async Task<IReadOnlyList<ToolStateDto>> Handle(GetToolStatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var toolStates = await _toolStateRepository.GetBySessionIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false);

        return toolStates.Select(ToolStateMapper.ToDto).ToList();
    }
}

/// <summary>
/// Gets a specific tool state by session and tool name.
/// Issue #4754: ToolState Entity + Toolkit ↔ Session Integration.
/// </summary>
internal class GetToolStateQueryHandler : IQueryHandler<GetToolStateQuery, ToolStateDto?>
{
    private readonly IToolStateRepository _toolStateRepository;

    public GetToolStateQueryHandler(IToolStateRepository toolStateRepository)
    {
        _toolStateRepository = toolStateRepository ?? throw new ArgumentNullException(nameof(toolStateRepository));
    }

    public async Task<ToolStateDto?> Handle(GetToolStateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var toolState = await _toolStateRepository
            .GetBySessionAndToolNameAsync(query.SessionId, query.ToolName, cancellationToken)
            .ConfigureAwait(false);

        return toolState != null ? ToolStateMapper.ToDto(toolState) : null;
    }
}
