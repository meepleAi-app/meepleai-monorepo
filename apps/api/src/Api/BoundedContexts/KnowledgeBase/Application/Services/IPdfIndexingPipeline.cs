namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Cross-BC contract for marking a PDF as "indexed in the vector store".
///
/// Closes the catena di rottura described in epic #2242:
/// DocumentProcessing handlers used to create <c>VectorDocumentEntity</c>
/// directly via EF, bypassing the <c>VectorDocument</c> domain aggregate
/// whose constructor raises <c>VectorDocumentIndexedEvent</c>. The
/// downstream <c>VectorDocumentIndexedForKbFlagHandler</c> never ran,
/// leaving <c>shared_games.has_knowledge_base</c> stuck on <c>false</c>
/// even after a successful indexing run.
///
/// Implementations MUST:
/// <list type="bullet">
///   <item>Build the domain aggregate via <see cref="Domain.Entities.VectorDocument.Create"/>
///         (or update an existing aggregate) so the event is collected.</item>
///   <item>Persist the EF entity with the full field set
///         (TotalCharacters, IndexingStatus, IndexedAt, …) that the domain
///         aggregate does not yet model.</item>
///   <item>Publish collected domain events via <c>IMediator</c> AFTER
///         <c>SaveChanges</c> so projection handlers see committed data.</item>
/// </list>
/// </summary>
public interface IPdfIndexingPipeline
{
    /// <summary>
    /// Marks a freshly indexed (or re-indexed) PDF as complete.
    /// </summary>
    /// <param name="pdfDocumentId">Source PDF id.</param>
    /// <param name="gameId">
    /// Private/legacy <c>games.Id</c> FK target (NOT <c>shared_games.id</c>).
    /// May be null for shared-only PDFs; the resolver in DocumentProcessing
    /// supplies it.
    /// </param>
    /// <param name="sharedGameId">
    /// <c>shared_games.id</c>. Set when the PDF is attached to a shared game
    /// so the <c>VectorDocumentIndexedForKbFlagHandler</c> can flip the
    /// <c>has_knowledge_base</c> flag.
    /// </param>
    /// <param name="chunkCount">Number of indexed chunks.</param>
    /// <param name="totalCharacters">Character count from the extracted text.</param>
    /// <param name="language">Detected/declared language (ISO-639-1 or BCP-47).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexAsync(
        Guid pdfDocumentId,
        Guid? gameId,
        Guid? sharedGameId,
        int chunkCount,
        int totalCharacters,
        string language,
        CancellationToken cancellationToken = default);
}
