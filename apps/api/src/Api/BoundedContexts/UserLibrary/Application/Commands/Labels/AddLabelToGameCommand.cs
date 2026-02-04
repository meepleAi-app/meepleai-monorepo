using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.Labels;

/// <summary>
/// Command to add a label to a game in the user's library.
/// </summary>
internal record AddLabelToGameCommand(
    Guid UserId,
    Guid GameId,
    Guid LabelId
) : ICommand<LabelDto>;
