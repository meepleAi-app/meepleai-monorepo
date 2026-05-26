using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record DetachKbSourceCommand(Guid BookId, Guid RequestedBy)
    : ICommand<GameBookDto>;
