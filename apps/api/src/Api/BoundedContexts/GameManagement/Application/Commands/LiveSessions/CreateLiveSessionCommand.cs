using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to create a new live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record CreateLiveSessionCommand(
    Guid UserId,
    string GameName,
    Guid? GameId = null,
    PlayRecordVisibility Visibility = PlayRecordVisibility.Private,
    Guid? GroupId = null,
    List<string>? ScoringDimensions = null,
    Dictionary<string, string>? DimensionUnits = null,
    AgentSessionMode AgentMode = AgentSessionMode.None
) : ICommand<Guid>;
