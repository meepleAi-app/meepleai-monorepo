using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.Labels;

/// <summary>
/// Command to delete a custom label.
/// </summary>
internal record DeleteCustomLabelCommand(
    Guid UserId,
    Guid LabelId
) : ICommand<bool>;
