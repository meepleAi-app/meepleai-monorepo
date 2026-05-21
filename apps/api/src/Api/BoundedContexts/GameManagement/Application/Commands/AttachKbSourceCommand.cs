using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record AttachKbSourceCommand(Guid BookId, Guid PdfDocId, Guid RequestedBy)
    : ICommand<GameBookDto>;
