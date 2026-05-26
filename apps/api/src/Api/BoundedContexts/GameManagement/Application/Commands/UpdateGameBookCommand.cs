using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record UpdateGameBookCommand(
    Guid BookId,
    string DisplayName,
    int Roles,
    Guid RequestedBy
) : ICommand<GameBookDto>;
