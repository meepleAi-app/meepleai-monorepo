using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Answers <see cref="GetDocumentsWithUnreliablePageNumbersQuery"/>: a document is unreliable when it is known to have
/// more than one page and the pages its passages carry do not tell two of them apart (fewer than two distinct page
/// numbers).
/// </summary>
/// <remarks>
/// A document whose page count is unknown, or is one, is given the benefit of the doubt: with one page there is
/// nothing to confuse, and without the count there is nothing to say the numbers are wrong. A document with several
/// pages and no passage carrying a page number is unreliable: nothing vouches for any page.
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

        var tellingPagesApart = await _dbContext.TextChunks
            .AsNoTracking()
            .Where(c => withSeveralPages.Contains(c.PdfDocumentId) && c.PageNumber != null)
            .GroupBy(c => c.PdfDocumentId)
            .Select(g => new { DocumentId = g.Key, Pages = g.Select(c => c.PageNumber).Distinct().Count() })
            .Where(x => x.Pages > 1)
            .Select(x => x.DocumentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return withSeveralPages.Except(tellingPagesApart).ToList();
    }
}
