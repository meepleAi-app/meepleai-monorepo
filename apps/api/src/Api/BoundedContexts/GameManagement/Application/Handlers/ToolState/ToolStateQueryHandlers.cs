using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;
using Api.BoundedContexts.GameManagement.Application.Queries.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
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
/// Returns base toolkit + custom ToolState items for a session.
/// The four base tools (TurnOrder, DiceSet, Whiteboard, Scoreboard) are always returned.
/// Override flags will be wired in #4972.
/// Issue #4969: Base Toolkit Layer.
/// </summary>
internal class GetSessionToolsQueryHandler : IQueryHandler<GetSessionToolsQuery, SessionToolsDto>
{
    private static readonly JsonElement EmptyConfig = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IToolStateRepository _toolStateRepository;

    public GetSessionToolsQueryHandler(
        ILiveSessionRepository sessionRepository,
        IToolStateRepository toolStateRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _toolStateRepository = toolStateRepository ?? throw new ArgumentNullException(nameof(toolStateRepository));
    }

    public async Task<SessionToolsDto> Handle(GetSessionToolsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", query.SessionId.ToString());

        var customTools = await _toolStateRepository
            .GetBySessionIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false);

        return new SessionToolsDto(
            SessionId: query.SessionId,
            ToolkitId: session.ToolkitId,
            BaseTools: BuildBaseToolkit(),
            CustomTools: customTools.Select(ToolStateMapper.ToDto).ToList());
    }

    private static BaseToolkitDto BuildBaseToolkit() => new(
        TurnOrder: new BaseToolDto("turn-order", "Ordine di Turno", BaseToolType.TurnOrder, true, EmptyConfig),
        DiceSet: new BaseToolDto("dice-set", "Set Dadi", BaseToolType.DiceSet, true, EmptyConfig),
        Whiteboard: new BaseToolDto("whiteboard", "Lavagna", BaseToolType.Whiteboard, true, EmptyConfig),
        Scoreboard: new BaseToolDto("scoreboard", "Scoreboard", BaseToolType.Scoreboard, true, EmptyConfig));
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
