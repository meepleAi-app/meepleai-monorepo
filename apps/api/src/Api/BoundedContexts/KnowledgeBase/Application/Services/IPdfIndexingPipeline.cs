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
    /// First step of an indexing run: makes the vector document row exist, WITHOUT declaring an index.
    /// </summary>
    /// <remarks>
    /// <para>
    /// #4609 — why this is a separate step. The text chunks and the vector rows hang off the vector
    /// document, so it has to exist before they are saved. But <see cref="IndexAsync"/> writes it
    /// <c>completed</c> and publishes <c>VectorDocumentIndexedEvent</c>, which raises
    /// <c>shared_games.has_knowledge_base</c>; calling it first left a "completed" index with no vector rows,
    /// and a game claiming a knowledge base, whenever a save after it failed or was cancelled.
    /// </para>
    /// <para>
    /// The order for a caller that writes the chunks itself is: <c>BeginIndexingAsync</c>, then the saves,
    /// then <see cref="IndexAsync"/> LAST. A caller that already creates the row in "processing" (the
    /// re-index command) goes straight to <see cref="IndexAsync"/>.
    /// </para>
    /// <para>
    /// A new row is created in "processing". A row that is "failed" or "processing" goes (back) to
    /// "processing" with its error cleared. A row that is already "completed" is left completed: a re-run of
    /// the pipeline rewrites the chunks of a document that is already indexed, and must neither take its
    /// index away meanwhile nor announce it a second time. Publishes nothing. A concurrency conflict is
    /// logged and swallowed, as in <see cref="IndexAsync"/>: the caller finds the row missing.
    /// </para>
    /// </remarks>
    /// <param name="pdfDocumentId">Source PDF id.</param>
    /// <param name="gameId">Same meaning as in <see cref="IndexAsync"/>.</param>
    /// <param name="sharedGameId">Same meaning as in <see cref="IndexAsync"/>.</param>
    /// <param name="chunkCount">Number of chunks that will be indexed.</param>
    /// <param name="totalCharacters">Character count from the extracted text.</param>
    /// <param name="language">Detected/declared language (ISO-639-1 or BCP-47).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BeginIndexingAsync(
        Guid pdfDocumentId,
        Guid? gameId,
        Guid? sharedGameId,
        int chunkCount,
        int totalCharacters,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a freshly indexed (or re-indexed) PDF as complete. Call it AFTER the chunks and the vector rows
    /// are saved (#4609); a caller that has to create the row first uses <see cref="BeginIndexingAsync"/>.
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

    /// <summary>
    /// Third step, on the failure path: a PDF that failed after <see cref="BeginIndexingAsync"/> must not leave
    /// its vector document in "processing".
    /// </summary>
    /// <remarks>
    /// <para>
    /// #4609 follow-up. <see cref="BeginIndexingAsync"/> creates the row in "processing" and
    /// <see cref="IndexAsync"/> completes it; when the PDF fails in between (a save fails, the user cancels) nobody
    /// completes it, and the game's indexing status would read "processing" for ever (the polling never ends, the
    /// documents list shows the PDF as being processed). This marks the row "failed" with the error, like the
    /// re-index command does.
    /// </para>
    /// <para>
    /// A row that is already "completed" is left as it is (this is not the compensation of "completed" that the
    /// 2026-10-25 decision ruled out), and a PDF with no vector document is a no-op. That does not promise the index
    /// is intact: the re-index path deletes the embeddings before it inserts them again, so a failed re-index can
    /// leave a "completed" row without embeddings. The row is judged by the database's values, not by whatever the
    /// change tracker holds after the save that failed. Best effort: it is called from failure handlers, so it never
    /// throws; a failure to write is logged.
    /// </para>
    /// </remarks>
    /// <param name="pdfDocumentId">Source PDF id.</param>
    /// <param name="errorMessage">
    /// Why the PDF failed; stored (truncated) as the vector document's error, which the indexing-status endpoint
    /// serves to users: pass text that is safe to show (a category, a fixed phrase), not an exception message.
    /// </param>
    /// <param name="cancellationToken">Cancellation token. Failure handlers pass <c>CancellationToken.None</c>.</param>
    Task MarkIndexingFailedAsync(
        Guid pdfDocumentId,
        string errorMessage,
        CancellationToken cancellationToken = default);
}
