using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record SoftDeleteGameBookCommand(Guid BookId, Guid RequestedBy)
    : ICommand<Unit>;
