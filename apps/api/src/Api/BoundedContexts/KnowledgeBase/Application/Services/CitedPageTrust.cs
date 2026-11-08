using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4819 — <see cref="ICitedPageTrust"/> on top of <see cref="GetDocumentsWithUnreliablePageNumbersQuery"/> (#4782),
/// the query the rule dispute already asks for its own references.
/// </summary>
internal sealed class CitedPageTrust : ICitedPageTrust
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly IMediator _mediator;
    private readonly ILogger<CitedPageTrust> _logger;

    public CitedPageTrust(IMediator mediator, ILogger<CitedPageTrust> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlySet<string>> WithoutAPageAsync(
        IEnumerable<string?> documentIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        var named = documentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var asked = named
            .Select(id => Guid.TryParse(id, out var guid) ? guid : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        if (asked.Count == 0)
        {
            return None;
        }

        try
        {
            var unreliable = (await _mediator
                    .Send(new GetDocumentsWithUnreliablePageNumbersQuery(asked), cancellationToken)
                    .ConfigureAwait(false))
                .ToHashSet();
            return named
                .Where(id => Guid.TryParse(id, out var guid) && unreliable.Contains(guid))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // The pages are an extra: failing the answer for them would be the worse outcome
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(
                ex,
                "Could not tell whether the pages of {Count} cited document(s) can be believed; they are cited without a page",
                asked.Count);
            return named.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
