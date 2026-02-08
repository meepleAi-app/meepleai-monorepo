using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

/// <summary>
/// Command to create a new play record.
/// Supports both catalog games and free-form games.
/// Issue #3889: CQRS commands for play records.
/// </summary>
internal record CreatePlayRecordCommand(
    Guid UserId,
    Guid? GameId,
    string GameName,
    DateTime SessionDate,
    PlayRecordVisibility Visibility,
    Guid? GroupId = null,
    List<string>? ScoringDimensions = null,
    Dictionary<string, string>? DimensionUnits = null
) : ICommand<Guid>;
