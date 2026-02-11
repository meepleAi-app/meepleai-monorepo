using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Handler for getting a single ledger entry by ID (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed class GetLedgerEntryByIdQueryHandler
    : IQueryHandler<GetLedgerEntryByIdQuery, LedgerEntryDto>
{
    private readonly ILedgerEntryRepository _repository;

    public GetLedgerEntryByIdQueryHandler(ILedgerEntryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<LedgerEntryDto> Handle(
        GetLedgerEntryByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entry = await _repository.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        if (entry is null)
            throw new NotFoundException("LedgerEntry", query.Id.ToString());

        return LedgerEntryDto.FromEntity(entry);
    }
}
