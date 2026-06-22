using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record CreateGameBookCommand(
    GameRef GameRef,
    Guid? OwnerUserId,
    string DisplayName,
    int Roles,
    int ParagraphScheme,
    string Language,
    bool SequentialRead,
    Guid? KbSourceDocId,
    bool PhysicalOnly,
    Guid RequestedBy
) : ICommand<GameBookDto>;
