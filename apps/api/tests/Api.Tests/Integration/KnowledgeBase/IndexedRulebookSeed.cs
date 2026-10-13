using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Il manuale indicizzato di un gioco del catalogo, come lo lascia l'indicizzazione. Lo usano i
/// test che fanno una domanda via HTTP e hanno bisogno che il recupero trovi qualcosa.
/// </summary>
internal static class IndexedRulebookSeed
{
    /// <summary>
    /// Tre chunk del manuale, tutti sopra la soglia del braccio vettoriale (coseno 1, 0,8 e 0,7) e
    /// con parole della domanda, così che il pool di candidati e il top-K finale si vedano.
    /// </summary>
    public static async Task SeedAsync(MeepleAiDbContext db, Guid gameId, Guid uploaderId, CancellationToken ct)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rulebook.pdf",
            FilePath = "/tmp/rulebook.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = uploaderId,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = gameId,
        });

        var vectorDocumentId = Guid.NewGuid();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = gameId,
            PdfDocumentId = pdfId,
            ChunkCount = 3,
            TotalCharacters = 300,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = QueryVectorEmbeddings.Dimensions,
        });

        var chunks = new[]
        {
            (Page: 3, Text: "At setup each player draws five cards and keeps them hidden.", Vector: QueryVectorEmbeddings.Axis(1f, 0f)),
            (Page: 4, Text: "Each player draws one card at the start of every turn.", Vector: QueryVectorEmbeddings.Axis(0.8f, 0.6f)),
            (Page: 9, Text: "A player who has no cards left draws two cards at setup of the next round.", Vector: QueryVectorEmbeddings.Axis(0.7f, 0.714f)),
        };

        var entities = chunks.Select((c, i) => new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            PdfDocumentId = pdfId,
            Content = c.Text,
            ChunkIndex = i,
            PageNumber = c.Page,
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
            command.Parameters.AddWithValue("@gameId", gameId);
            command.Parameters.AddWithValue("@text", entities[i].Content);
            command.Parameters.AddWithValue("@vec", new Pgvector.Vector(chunks[i].Vector));
            command.Parameters.AddWithValue("@chunkIndex", i);
            command.Parameters.AddWithValue("@pageNumber", chunks[i].Page);
            command.Parameters.AddWithValue("@sourceChunkId", entities[i].Id);
            await command.ExecuteNonQueryAsync(ct);
        }
    }
}
