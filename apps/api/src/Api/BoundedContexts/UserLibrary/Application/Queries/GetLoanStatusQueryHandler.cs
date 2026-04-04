using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

internal class GetLoanStatusQueryHandler : IQueryHandler<GetLoanStatusQuery, LoanStatusDto?>
{
    private readonly IUserLibraryRepository _repository;

    public GetLoanStatusQueryHandler(IUserLibraryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<LoanStatusDto?> Handle(GetLoanStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entry = await _repository.GetByUserAndGameAsync(query.UserId, query.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null) return null;

        var isOnLoan = entry.CurrentState.Value == GameStateType.InPrestito;

        return new LoanStatusDto(
            IsOnLoan: isOnLoan,
            BorrowerInfo: entry.CurrentState.StateNotes,
            LoanedSince: isOnLoan ? entry.CurrentState.ChangedAt : null
        );
    }
}
