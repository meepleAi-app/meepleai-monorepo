using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Answers <see cref="GetDocumentsWithUnreliablePageNumbersQuery"/>: a document is unreliable when it is known to have
/// more than one page and the page its passages claim most often holds more than half of them.
/// </summary>
/// <remarks>
/// <para>
/// Why «more than half» and not «all on one page»: a document indexed without the page of each passage claims page 1
/// for every passage, but a later step can add a few passages with a true page (the tables extracted afterwards are
/// written with the page of their region). One of them would be enough for a «two distinct pages» rule to believe the
/// forty body passages that still say page 1. The same weakness is in the structured path: a section takes the page of
/// its first element and every passage of it inherits that page, so two headings on pages 1 and 12 give two distinct
/// pages and eleven pages that all say «p. 1». Concentration sees both.
/// </para>
/// <para>
/// The price is a document that is truly front-loaded (a long first section) losing its pages: under «better no page
/// than a false one» that is the right side to err on, and its passages inherit the start page of the section anyway.
/// </para>
/// <para>
/// A page number of 0 or less is no page: the indexing paths write <c>chunk.Page</c> as it comes (0 when unset), and
/// the citation clamps it to 1, so counting a 0 as a page would believe a document whose passages say 1. A document
/// whose page count is unknown, or is one, is given the benefit of the doubt: with one page there is nothing to
/// confuse, and without the count there is nothing to say the numbers are wrong. A document with several pages and no
/// passage carrying a page is unreliable.
/// </para>
/// </remarks>
internal sealed class GetDocumentsWithUnreliablePageNumbersQueryHandler
    : IRequestHandler<GetDocumentsWithUnreliablePageNumbersQuery, IReadOnlyList<Guid>>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetDocumentsWithUnreliablePageNumbersQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<Guid>> Handle(
        GetDocumentsWithUnreliablePageNumbersQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asked = request.DocumentIds.Distinct().ToList();
        if (asked.Count == 0)
        {
            return [];
        }

        var withSeveralPages = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(d => asked.Contains(d.Id) && d.PageCount > 1)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (withSeveralPages.Count == 0)
        {
            return [];
        }

        // One row per (document, page): how many passages claim it. A plain GROUP BY, summed up below.
        var claims = await _dbContext.TextChunks
            .AsNoTracking()
            .Where(c => withSeveralPages.Contains(c.PdfDocumentId) && c.PageNumber > 0)
            .GroupBy(c => new { c.PdfDocumentId, c.PageNumber })
            .Select(g => new { g.Key.PdfDocumentId, Passages = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var believed = claims
            .GroupBy(c => c.PdfDocumentId)
            .Where(g => g.Max(c => c.Passages) * 2 <= g.Sum(c => c.Passages))
            .Select(g => g.Key)
            .ToHashSet();

        return withSeveralPages.Where(id => !believed.Contains(id)).ToList();
    }
}
