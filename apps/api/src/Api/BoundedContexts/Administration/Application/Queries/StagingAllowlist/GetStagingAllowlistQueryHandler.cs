using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.StagingAllowlist;

internal sealed class GetStagingAllowlistQueryHandler
    : IRequestHandler<GetStagingAllowlistQuery, IReadOnlyList<StagingAllowlistEntryDto>>
{
    private readonly IStagingAllowlistRepository _repository;

    public GetStagingAllowlistQueryHandler(IStagingAllowlistRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<IReadOnlyList<StagingAllowlistEntryDto>> Handle(
        GetStagingAllowlistQuery request,
        CancellationToken cancellationToken)
    {
        var entries = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return entries
            .OrderByDescending(e => e.AddedAt)
            .Select(e => new StagingAllowlistEntryDto(e.Id, e.Email, e.AddedByUserId, e.AddedAt, e.Note))
            .ToList();
    }
}
