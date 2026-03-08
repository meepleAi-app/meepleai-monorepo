using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Command to accept an AI-generated draft for a specific section.
/// </summary>
internal record AcceptMechanicDraftCommand(
    Guid DraftId,
    string Section,
    string AcceptedDraft)
    : ICommand<MechanicDraftDto>;
