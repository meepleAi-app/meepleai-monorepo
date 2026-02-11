using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Handler for deleting ledger entries - Level 2 (Issue #3722: Manual Ledger CRUD)
/// Only manual entries can be deleted. Auto entries are protected.
/// </summary>
internal sealed class DeleteLedgerEntryCommandHandler : ICommandHandler<DeleteLedgerEntryCommand>
{
    private readonly ILedgerEntryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteLedgerEntryCommandHandler> _logger;

    public DeleteLedgerEntryCommandHandler(
        ILedgerEntryRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteLedgerEntryCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DeleteLedgerEntryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entry = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (entry is null)
            throw new NotFoundException("LedgerEntry", command.Id.ToString());

        if (entry.Source != LedgerEntrySource.Manual)
            throw new DomainException("Only manual ledger entries can be deleted. Auto-generated entries are protected.");

        await _repository.DeleteAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Deleted manual ledger entry {EntryId}: {Type} {Amount} {Currency}",
            entry.Id, entry.Type, entry.Amount.Amount, entry.Amount.Currency);
    }
}
