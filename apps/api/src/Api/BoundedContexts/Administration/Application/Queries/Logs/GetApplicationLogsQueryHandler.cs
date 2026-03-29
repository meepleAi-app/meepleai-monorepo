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

        // Build raw Seq property filters (passed directly, not wrapped)
        var rawFilterParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Source))
            rawFilterParts.Add($"SourceContext like '%{query.Source}%'");
        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            rawFilterParts.Add($"CorrelationId = '{query.CorrelationId}'");

        var rawFilter = rawFilterParts.Count > 0 ? string.Join(" and ", rawFilterParts) : null;

        var (items, remaining) = await _seqClient.QueryEventsAsync(
            rawFilter: rawFilter,
            search: !string.IsNullOrWhiteSpace(query.Search) ? query.Search : null,
            level: query.Level,
            fromUtc: query.From, toUtc: query.To,
            count: Math.Min(query.Count, 200),
            afterId: query.AfterId, ct: cancellationToken).ConfigureAwait(false);

        var lastId = items.Count > 0 ? items[^1].Id : null;
        return new GetApplicationLogsResponse(items, remaining, lastId);
    }
}
