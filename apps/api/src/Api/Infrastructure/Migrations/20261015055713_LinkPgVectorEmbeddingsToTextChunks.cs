using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #4384: ricollega a <c>text_chunks</c> le righe di <c>pgvector_embeddings</c> nate senza
    /// <c>source_chunk_id</c>. Le scrivevano così il caricamento da <c>/ingest/pdf</c>, la
    /// reindicizzazione (<c>IndexPdfCommandHandler</c>) e l'import dei dati RAG; dalla stessa consegna
    /// passano tutti da <c>PgVectorEmbeddingEntity.ForChunk</c>, che chiede il collegamento.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La regola di unione è quella della pipeline di Quartz, più una verifica: stesso PDF
    /// (<c>vector_documents</c>), stesso <c>ChunkIndex</c> e <b>stesso testo</b>. Il testo esclude una riga
    /// rimasta accanto ai passaggi di un'altra suddivisione (una reindicizzazione che aveva salvato i vettori
    /// e perso il salvataggio finale dei passaggi): lì lo stesso indice è un altro passaggio. Ogni scrittore
    /// salva lo stesso testo nelle due tabelle. Misurato sul database locale il 2026-10-15: le coppie trovate
    /// per indice hanno tutte lo stesso testo, comprese quelle già collegate dalla pipeline, e nessun PDF ha
    /// due passaggi sullo stesso indice. L'aggiornamento salta comunque i casi ambigui.
    /// </para>
    /// <para>
    /// Tocca solo righe con <c>source_chunk_id</c> nullo: si può rieseguire, e non cambia le righe già
    /// collegate. Copia anche <c>role_tags</c> dal passaggio, come fa la pipeline. Le righe senza un passaggio
    /// corrispondente restano come sono. I PDF del caricamento a pezzi non hanno righe vettoriali affatto
    /// (#4395): lì serve una reindicizzazione, non questo aggiornamento.
    /// </para>
    /// </remarks>
    public partial class LinkPgVectorEmbeddingsToTextChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE pgvector_embeddings AS e
                SET source_chunk_id = tc."Id",
                    role_tags = tc.role_tags
                FROM vector_documents AS vd
                JOIN text_chunks AS tc ON tc."PdfDocumentId" = vd."PdfDocumentId"
                WHERE vd."Id" = e.vector_document_id
                  AND tc."ChunkIndex" = e.chunk_index
                  AND tc."Content" = e.text_content
                  AND e.source_chunk_id IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM text_chunks AS other
                      WHERE other."PdfDocumentId" = tc."PdfDocumentId"
                        AND other."ChunkIndex" = tc."ChunkIndex"
                        AND other."Id" <> tc."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Niente da annullare: un collegamento giusto non va tolto, e dopo l'aggiornamento non si sa
            // più quali righe erano scollegate. Il codice della versione precedente legge le righe
            // collegate senza problemi (la pipeline di Quartz le ha sempre scritte così).
        }
    }
}
