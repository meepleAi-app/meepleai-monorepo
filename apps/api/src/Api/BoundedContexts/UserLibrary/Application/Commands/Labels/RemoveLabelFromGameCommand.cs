using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.Labels;

/// <summary>
/// Command to remove a label from a game in the user's library.
/// </summary>
internal record RemoveLabelFromGameCommand(
    Guid UserId,
    Guid GameId,
    Guid LabelId
) : ICommand<bool>;
