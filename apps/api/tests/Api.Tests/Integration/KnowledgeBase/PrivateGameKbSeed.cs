using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// #4244 — il PDF di un gioco privato come lo lascia l'indicizzazione: il gioco nella colonna
/// <c>PrivateGameId</c> di <c>text_chunks</c> e <c>vector_documents</c> (con <c>GameId</c> null), e
/// nel <c>game_id</c> unico degli embedding.
/// </summary>
internal static class PrivateGameKbSeed
{
    public static async Task<Guid> IndexPdfAsync(
        MeepleAiDbContext db,
        Guid uploaderId,
        Guid privateGameId,
        string processingState,
        CancellationToken ct,
        params (string Text, float[] Vector)[] chunks)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = $"private-{pdfId:N}.pdf",
            FilePath = $"/tmp/private-{pdfId:N}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = uploaderId,
            ProcessingState = processingState,
            ProcessedAt = string.Equals(processingState, "Ready", StringComparison.Ordinal) ? DateTime.UtcNow : null,
            PrivateGameId = privateGameId,
        });
        await db.SaveChangesAsync(ct);

        if (chunks.Length == 0)
        {
            return pdfId;
        }

        var vectorDocumentId = Guid.NewGuid();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = null,
            PrivateGameId = privateGameId,
            PdfDocumentId = pdfId,
            ChunkCount = chunks.Length,
            TotalCharacters = chunks.Sum(c => c.Text.Length),
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = QueryVectorEmbeddings.Dimensions,
        });

        var entities = chunks.Select((c, i) => new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            GameId = null,
            PrivateGameId = privateGameId,
            PdfDocumentId = pdfId,
            Content = c.Text,
            ChunkIndex = i,
            PageNumber = i + 1,
            CharacterCount = c.Text.Length,
        }).ToList();
        db.TextChunks.AddRange(entities);
        await db.SaveChangesAsync(ct);

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        for (var i = 0; i < entities.Count; i++)
        {
            await using var command = (NpgsqlCommand)connection.CreateCommand();
            command.CommandText = """
                INSERT INTO pgvector_embeddings
                    (id, vector_document_id, game_id, text_content, vector, model,
                     chunk_index, page_number, created_at, lang, source_chunk_id, is_translation, role_tags)
                VALUES
                    (@id, @vdId, @gameId, @text, @vec, 'test-model',
                     @chunkIndex, @pageNumber, NOW(), 'en', @sourceChunkId, false, 0)
                """;
            command.Parameters.AddWithValue("@id", Guid.NewGuid());
            command.Parameters.AddWithValue("@vdId", vectorDocumentId);
            command.Parameters.AddWithValue("@gameId", privateGameId);
            command.Parameters.AddWithValue("@text", entities[i].Content);
            command.Parameters.AddWithValue("@vec", new Pgvector.Vector(chunks[i].Vector));
            command.Parameters.AddWithValue("@chunkIndex", i);
            command.Parameters.AddWithValue("@pageNumber", i + 1);
            command.Parameters.AddWithValue("@sourceChunkId", entities[i].Id);
            await command.ExecuteNonQueryAsync(ct);
        }

        return pdfId;
    }
}
