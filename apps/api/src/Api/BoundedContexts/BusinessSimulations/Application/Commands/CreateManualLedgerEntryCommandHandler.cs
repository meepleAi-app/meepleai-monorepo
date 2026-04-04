using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Handler for creating manual ledger entries (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed class CreateManualLedgerEntryCommandHandler
    : ICommandHandler<CreateManualLedgerEntryCommand, Guid>
{
    private readonly ILedgerEntryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateManualLedgerEntryCommandHandler> _logger;

    public CreateManualLedgerEntryCommandHandler(
        ILedgerEntryRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateManualLedgerEntryCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        CreateManualLedgerEntryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entry = LedgerEntry.CreateManualEntry(
            date: command.Date,
            type: command.Type,
            category: command.Category,
            amount: command.Amount,
            createdByUserId: command.CreatedByUserId,
            currency: command.Currency,
            description: command.Description);

        await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created manual ledger entry {EntryId}: {Type} {Amount} {Currency} ({Category}) by user {UserId}",
            entry.Id, command.Type, command.Amount, command.Currency, command.Category, command.CreatedByUserId);

        return entry.Id;
    }
}
