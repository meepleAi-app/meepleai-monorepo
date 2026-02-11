using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Handler for updating ledger entries - Level 1 edit (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed class UpdateLedgerEntryCommandHandler : ICommandHandler<UpdateLedgerEntryCommand>
{
    private readonly ILedgerEntryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateLedgerEntryCommandHandler> _logger;

    public UpdateLedgerEntryCommandHandler(
        ILedgerEntryRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateLedgerEntryCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(UpdateLedgerEntryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entry = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (entry is null)
            throw new NotFoundException("LedgerEntry", command.Id.ToString());

        if (command.Description is not null)
            entry.UpdateDescription(command.Description);

        if (command.Category.HasValue)
            entry.UpdateCategory(command.Category.Value);

        if (command.Metadata is not null)
            entry.UpdateMetadata(command.Metadata);

        await _repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated ledger entry {EntryId}", command.Id);
    }
}
