using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.Labels;

/// <summary>
/// Command to create a custom label for a user.
/// </summary>
internal record CreateCustomLabelCommand(
    Guid UserId,
    string Name,
    string Color
) : ICommand<LabelDto>;
