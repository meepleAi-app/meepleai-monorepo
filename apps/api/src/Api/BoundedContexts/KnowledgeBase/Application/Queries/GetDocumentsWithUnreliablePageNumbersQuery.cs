using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #4782 — which of these documents cannot say on which page a passage is.
/// </summary>
/// <remarks>
/// A document that has several pages and whose passages all claim the same one (or none) was indexed without the
/// page of each passage: <c>ExtractedDocumentFactory</c> gives a document it has no structure for a single section on
/// page 1, and every passage inherits it. The page of such a passage is not a fact, and a source that says «p. 1» for
/// every question sends the reader to the wrong place. The ask is for the documents the caller cites, so what comes
/// back is a subset of <see cref="DocumentIds"/>.
/// </remarks>
/// <param name="DocumentIds">The documents whose pages the caller is about to show.</param>
public sealed record GetDocumentsWithUnreliablePageNumbersQuery(IReadOnlyCollection<Guid> DocumentIds)
    : IRequest<IReadOnlyList<Guid>>;
