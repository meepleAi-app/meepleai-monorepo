namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4819 — which of the documents an answer cites cannot vouch for the page of their passages (#4782). The one
/// place that asks, so the answers that cite a page (the in-session assistant and its chat, Ask, Explain) do not
/// each carry their own copy of the rule and of what to do when it cannot be asked.
/// </summary>
internal interface ICitedPageTrust
{
    /// <summary>
    /// Of the documents the caller is about to cite, the ones whose page must be left out: a document with several
    /// pages whose passages all claim the same one was indexed without the page of each passage, so every citation of
    /// it would say the same, wrong, page. Better no page than a false one.
    /// </summary>
    /// <param name="documentIds">
    /// The document ids as the citations carry them. Ids that are not a GUID are not documents KnowledgeBase wrote and
    /// cannot be asked about; they are not reported.
    /// </param>
    /// <param name="cancellationToken">A cancellation propagates; it is not read as «cannot be asked».</param>
    /// <returns>
    /// The subset to cite without a page, compared case-insensitively. If the question cannot be answered, every
    /// document named: a reader sent to the wrong page is worse off than one who is not sent anywhere. A cancellation
    /// is not that case and propagates.
    /// </returns>
    Task<IReadOnlySet<string>> WithoutAPageAsync(IEnumerable<string?> documentIds, CancellationToken cancellationToken);
}
