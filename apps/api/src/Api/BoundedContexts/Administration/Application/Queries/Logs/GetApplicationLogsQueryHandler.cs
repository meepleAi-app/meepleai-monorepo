using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Logs;

internal sealed class GetApplicationLogsQueryHandler
    : IQueryHandler<GetApplicationLogsQuery, GetApplicationLogsResponse>
{
    private readonly ISeqQueryClient _seqClient;

    public GetApplicationLogsQueryHandler(ISeqQueryClient seqClient)
    {
        _seqClient = seqClient ?? throw new ArgumentNullException(nameof(seqClient));
    }

    public async Task<GetApplicationLogsResponse> Handle(
        GetApplicationLogsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filterParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Source))
            filterParts.Add($"SourceContext like '%{query.Source}%'");
        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            filterParts.Add($"CorrelationId = '{query.CorrelationId}'");

        var searchFilter = !string.IsNullOrWhiteSpace(query.Search) ? query.Search : null;

        string? combinedFilter = null;
        if (filterParts.Count > 0 && searchFilter != null)
            combinedFilter = $"({string.Join(" and ", filterParts)}) and ({searchFilter})";
        else if (filterParts.Count > 0)
            combinedFilter = string.Join(" and ", filterParts);
        else
            combinedFilter = searchFilter;

        var (items, remaining) = await _seqClient.QueryEventsAsync(
            filter: combinedFilter, level: query.Level,
            fromUtc: query.From, toUtc: query.To,
            count: Math.Min(query.Count, 200),
            afterId: query.AfterId, ct: cancellationToken).ConfigureAwait(false);

        var lastId = items.Count > 0 ? items[^1].Id : null;
        return new GetApplicationLogsResponse(items, remaining, lastId);
    }
}
