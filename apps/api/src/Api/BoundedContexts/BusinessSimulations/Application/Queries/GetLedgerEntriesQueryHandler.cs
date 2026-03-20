using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Handler for getting paginated and filtered ledger entries (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed class GetLedgerEntriesQueryHandler
    : IQueryHandler<GetLedgerEntriesQuery, LedgerEntriesResponseDto>
{
    private readonly ILedgerEntryRepository _repository;

    public GetLedgerEntriesQueryHandler(ILedgerEntryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<LedgerEntriesResponseDto> Handle(
        GetLedgerEntriesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (entries, total) = await _repository.GetFilteredAsync(
            type: query.Type,
            category: query.Category,
            source: query.Source,
            dateFrom: query.DateFrom,
            dateTo: query.DateTo,
            page: query.Page,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var dtos = entries.Select(LedgerEntryDto.FromEntity).ToList();

        return new LedgerEntriesResponseDto(dtos, total, query.Page, query.PageSize);
    }
}
