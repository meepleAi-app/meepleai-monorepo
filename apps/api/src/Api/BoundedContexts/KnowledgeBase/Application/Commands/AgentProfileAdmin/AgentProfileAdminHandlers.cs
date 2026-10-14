using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

#pragma warning disable MA0048 // File name must match type name - the handlers of the admin agent page
namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentProfileAdmin;

internal sealed class SaveAgentProfileDraftCommandHandler : ICommandHandler<SaveAgentProfileDraftCommand, AgentProfileAdminDto>
{
    private readonly IAgentProfileRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveAgentProfileDraftCommandHandler(IAgentProfileRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AgentProfileAdminDto> Handle(SaveAgentProfileDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await AgentProfileAdminLoader.LoadAsync(_repository, cancellationToken).ConfigureAwait(false);

        // Le sezioni che la pagina non modifica vengono dalla bozza, o dalla versione pubblicata se la
        // bozza non c'è ancora: in sola lettura fino alla fetta 7 (#4170), decisione del 2026-10-12.
        var kept = (profile.Draft ?? profile.Published).Content;
        var content = AgentProfileContent.Create(
            command.Name,
            command.Description ?? string.Empty,
            command.Persona ?? string.Empty,
            command.PrimaryModelId,
            command.FallbackModelId,
            command.Temperature,
            command.MaxResponseTokens,
            command.CitationStyle,
            command.SystemPrompts,
            command.FallbackLanguage,
            kept.CandidatePoolSize,
            kept.FinalTopK,
            kept.MinScore,
            kept.VectorWeight,
            kept.KeywordWeight,
            kept.RerankerEnabled,
            kept.AllowedCategories.ToList(),
            kept.SessionAssistant);

        if (profile.Draft is null)
        {
            profile.CreateDraft(content);
        }
        else
        {
            profile.UpdateDraft(content);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AgentProfileAdminDto.From(profile);
    }
}

internal sealed class PublishAgentProfileDraftCommandHandler : ICommandHandler<PublishAgentProfileDraftCommand, AgentProfileAdminDto>
{
    private readonly IAgentProfileRepository _repository;
    private readonly IAgentProfileModelCatalogReader _catalog;
    private readonly IAgentReserveModelProvider _reserveModels;
    private readonly IUnitOfWork _unitOfWork;

    public PublishAgentProfileDraftCommandHandler(
        IAgentProfileRepository repository,
        IAgentProfileModelCatalogReader catalog,
        IAgentReserveModelProvider reserveModels,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _reserveModels = reserveModels ?? throw new ArgumentNullException(nameof(reserveModels));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AgentProfileAdminDto> Handle(PublishAgentProfileDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await AgentProfileAdminLoader.LoadAsync(_repository, cancellationToken).ConfigureAwait(false);
        var catalog = await _catalog.ReadAsync(cancellationToken).ConfigureAwait(false);
        profile.PublishDraft(catalog);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // La riserva è l'unico campo del profilo tenuto in cache (un minuto, dal gateway in degrado).
        _reserveModels.Invalidate();
        return AgentProfileAdminDto.From(profile);
    }
}

internal sealed class RollbackAgentProfileCommandHandler : ICommandHandler<RollbackAgentProfileCommand, AgentProfileAdminDto>
{
    private readonly IAgentProfileRepository _repository;
    private readonly IAgentProfileModelCatalogReader _catalog;
    private readonly IAgentReserveModelProvider _reserveModels;
    private readonly IUnitOfWork _unitOfWork;

    public RollbackAgentProfileCommandHandler(
        IAgentProfileRepository repository,
        IAgentProfileModelCatalogReader catalog,
        IAgentReserveModelProvider reserveModels,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _reserveModels = reserveModels ?? throw new ArgumentNullException(nameof(reserveModels));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AgentProfileAdminDto> Handle(RollbackAgentProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await AgentProfileAdminLoader.LoadAsync(_repository, cancellationToken).ConfigureAwait(false);
        var catalog = await _catalog.ReadAsync(cancellationToken).ConfigureAwait(false);
        profile.RollbackTo(command.VersionNumber, catalog);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _reserveModels.Invalidate();
        return AgentProfileAdminDto.From(profile);
    }
}

internal static class AgentProfileAdminLoader
{
    /// <summary>Il profilo, tracciato. Solo un database senza le migrazioni non ce l'ha.</summary>
    public static async Task<AgentProfile> LoadAsync(IAgentProfileRepository repository, CancellationToken cancellationToken) =>
        await repository.GetAsync(cancellationToken).ConfigureAwait(false)
        ?? throw new NotFoundException("AgentProfile", "system");
}
