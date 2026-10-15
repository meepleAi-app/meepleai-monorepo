using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #4384: ricollega a <c>text_chunks</c> le righe di <c>pgvector_embeddings</c> nate senza
    /// <c>source_chunk_id</c>. Le scrivevano così il caricamento da <c>/ingest/pdf</c>, la
    /// reindicizzazione (<c>IndexPdfCommandHandler</c>) e l'import dei dati RAG; dalla stessa consegna
    /// tutti e tre passano da <c>PgVectorEmbeddingEntity.ForChunk</c>, che chiede il collegamento.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La regola di unione è quella della pipeline di Quartz: stesso PDF (<c>vector_documents</c>) e stesso
    /// <c>ChunkIndex</c>. Misurata sul database locale il 2026-10-15: sulle 10.890 righe già collegate dalla
    /// pipeline, <c>source_chunk_id</c> e <c>role_tags</c> coincidono con il passaggio trovato così; le 125
    /// righe scollegate hanno tutte un passaggio corrispondente, e nessun PDF ha due passaggi con lo stesso
    /// indice. Per sicurezza l'aggiornamento salta i casi ambigui (due passaggi sullo stesso indice).
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
