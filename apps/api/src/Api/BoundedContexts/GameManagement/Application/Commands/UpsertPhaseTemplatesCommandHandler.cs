using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal sealed class UpsertPhaseTemplatesCommandHandler
    : ICommandHandler<UpsertPhaseTemplatesCommand, IReadOnlyList<PhaseTemplateDto>>
{
    private readonly IGamePhaseTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpsertPhaseTemplatesCommandHandler(
        IGamePhaseTemplateRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<IReadOnlyList<PhaseTemplateDto>> Handle(UpsertPhaseTemplatesCommand command, CancellationToken cancellationToken)
    {
        // Full replace strategy: delete all, then insert new
        await _repository.DeleteByGameIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);

        var templates = command.Templates
            .Select(t => GamePhaseTemplate.Create(
                command.GameId,
                t.PhaseName,
                t.PhaseOrder,
                command.EditorUserId,
                t.Description))
            .ToList();

        await _repository.AddRangeAsync(templates, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return templates
            .Select(t => new PhaseTemplateDto(t.Id, t.PhaseName, t.PhaseOrder, t.Description))
            .ToList()
            .AsReadOnly();
    }
}
