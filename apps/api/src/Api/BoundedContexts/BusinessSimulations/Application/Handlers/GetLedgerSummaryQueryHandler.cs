using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Handler for getting ledger summary (income vs expense) (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed class GetLedgerSummaryQueryHandler
    : IQueryHandler<GetLedgerSummaryQuery, LedgerSummaryDto>
{
    private readonly ILedgerEntryRepository _repository;

    public GetLedgerSummaryQueryHandler(ILedgerEntryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<LedgerSummaryDto> Handle(
        GetLedgerSummaryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (totalIncome, totalExpense) = await _repository
            .GetSummaryByDateRangeAsync(query.DateFrom, query.DateTo, cancellationToken)
            .ConfigureAwait(false);

        return new LedgerSummaryDto(
            TotalIncome: totalIncome,
            TotalExpense: totalExpense,
            NetBalance: totalIncome - totalExpense,
            From: query.DateFrom,
            To: query.DateTo);
    }
}
