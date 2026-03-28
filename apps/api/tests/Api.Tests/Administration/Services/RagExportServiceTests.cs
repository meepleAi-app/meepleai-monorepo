using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Administration.Services;

/// <summary>
/// Unit tests for RAG export serialization helpers.
/// Covers: manifest round-trip, chunk JSON shape, embedding vector fidelity, JSONL format.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class RagExportServiceTests
{
    // -------------------------------------------------------------------------
    // Manifest round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void ToJsonBytes_Manifest_RoundTrip_PreservesAllFields()
    {
        // Arrange
        var entry = new RagExportManifestEntry(
            PdfDocumentId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GameSlug: "catan",
            GameName: "Catan",
            Path: "snapshots/2026/catan/11111111/",
            Chunks: 42,
            Embeddings: 42,
            Language: "en");

        var manifest = new RagExportManifest(
            ExportVersion: "1.0",
            ExportedAt: new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero),
            TotalDocuments: 1,
            TotalChunks: 42,
            TotalEmbeddings: 42,
            EmbeddingModel: "nomic-embed-text",
            Documents: [entry]);

        // Act
        var bytes = RagExportSerializer.ToJsonBytes(manifest);
        var json = Encoding.UTF8.GetString(bytes);
        var deserialized = JsonSerializer.Deserialize<RagExportManifest>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ExportVersion.Should().Be("1.0");
        deserialized.TotalDocuments.Should().Be(1);
        deserialized.TotalChunks.Should().Be(42);
        deserialized.TotalEmbeddings.Should().Be(42);
        deserialized.EmbeddingModel.Should().Be("nomic-embed-text");
        deserialized.ExportedAt.Should().Be(manifest.ExportedAt);
        deserialized.Documents.Should().HaveCount(1);

        var doc = deserialized.Documents[0];
        doc.PdfDocumentId.Should().Be(entry.PdfDocumentId);
        doc.GameSlug.Should().Be("catan");
        doc.GameName.Should().Be("Catan");
        doc.Path.Should().Be("snapshots/2026/catan/11111111/");
        doc.Chunks.Should().Be(42);
        doc.Language.Should().Be("en");
    }

    // -------------------------------------------------------------------------
    // ChunkLine JSON shape
    // -------------------------------------------------------------------------

    [Fact]
    public void ToJsonBytes_ChunkLine_ContainsExpectedCamelCaseKeys()
    {
        // Arrange
        var chunk = new RagExportChunkLine(
            ChunkIndex: 3,
            PageNumber: 7,
            Content: "Roll the dice to move your robber.",
            CharacterCount: 34);

        // Act
        var bytes = RagExportSerializer.ToJsonBytes(chunk);
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert – key names must be camelCase
        root.TryGetProperty("chunkIndex", out var chunkIndex).Should().BeTrue();
        root.TryGetProperty("pageNumber", out var pageNumber).Should().BeTrue();
        root.TryGetProperty("content", out var content).Should().BeTrue();
        root.TryGetProperty("characterCount", out var characterCount).Should().BeTrue();

        chunkIndex.GetInt32().Should().Be(3);
        pageNumber.GetInt32().Should().Be(7);
        content.GetString().Should().Be("Roll the dice to move your robber.");
        characterCount.GetInt32().Should().Be(34);
    }

    [Fact]
    public void ToJsonBytes_ChunkLine_NullPageNumber_SerializesAsNull()
    {
        // Arrange
        var chunk = new RagExportChunkLine(
            ChunkIndex: 0,
            PageNumber: null,
            Content: "Introduction.",
            CharacterCount: 13);

        // Act
        var bytes = RagExportSerializer.ToJsonBytes(chunk);
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);

        // Assert
        doc.RootElement.GetProperty("pageNumber").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // -------------------------------------------------------------------------
    // EmbeddingLine vector preservation
    // -------------------------------------------------------------------------

    [Fact]
    public void ToJsonBytes_EmbeddingLine_PreservesVectorValues()
    {
        // Arrange
        var vector = new float[] { 0.1f, -0.5f, 0.9999f, float.Epsilon };
        var embedding = new RagExportEmbeddingLine(
            ChunkIndex: 1,
            PageNumber: 2,
            TextContent: "Sample text for embedding.",
            Vector: vector,
            Model: "nomic-embed-text");

        // Act
        var bytes = RagExportSerializer.ToJsonBytes(embedding);
        var json = Encoding.UTF8.GetString(bytes);
        var deserialized = JsonSerializer.Deserialize<RagExportEmbeddingLine>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Vector.Should().HaveCount(vector.Length);
        deserialized.Vector[0].Should().BeApproximately(0.1f, 1e-6f);
        deserialized.Vector[1].Should().BeApproximately(-0.5f, 1e-6f);
        deserialized.Vector[2].Should().BeApproximately(0.9999f, 1e-5f);
        deserialized.Vector[3].Should().BeApproximately(float.Epsilon, 1e-45f);
        deserialized.Model.Should().Be("nomic-embed-text");
        deserialized.TextContent.Should().Be("Sample text for embedding.");
    }

    [Fact]
    public void ToJsonBytes_EmbeddingLine_EmptyVector_RoundTripsCorrectly()
    {
        // Arrange
        var embedding = new RagExportEmbeddingLine(
            ChunkIndex: 0,
            PageNumber: null,
            TextContent: "Empty.",
            Vector: [],
            Model: "test-model");

        // Act
        var bytes = RagExportSerializer.ToJsonBytes(embedding);
        var json = Encoding.UTF8.GetString(bytes);
        var deserialized = JsonSerializer.Deserialize<RagExportEmbeddingLine>(json);

        // Assert
        deserialized!.Vector.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // JSONL format
    // -------------------------------------------------------------------------

    [Fact]
    public void ToJsonlBytes_MultipleItems_EachLineIsValidJson()
    {
        // Arrange
        var items = new[]
        {
            new RagExportChunkLine(0, 1, "First chunk content.", 20),
            new RagExportChunkLine(1, 1, "Second chunk content.", 21),
            new RagExportChunkLine(2, 2, "Third chunk content.", 20)
        };

        // Act
        var bytes = RagExportSerializer.ToJsonlBytes(items);
        var text = Encoding.UTF8.GetString(bytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert – 3 items = 3 lines
        lines.Should().HaveCount(3);

        for (var i = 0; i < lines.Length; i++)
        {
            // Each line must be individually parseable as JSON
            var action = () => JsonDocument.Parse(lines[i]);
            action.Should().NotThrow($"line {i} must be valid JSON");

            using var doc = JsonDocument.Parse(lines[i]);
            doc.RootElement.GetProperty("chunkIndex").GetInt32().Should().Be(i);
        }
    }

    [Fact]
    public void ToJsonlBytes_SingleItem_ProducesOneLineWithTrailingNewline()
    {
        // Arrange
        var items = new[]
        {
            new RagExportChunkLine(0, null, "Only chunk.", 11)
        };

        // Act
        var bytes = RagExportSerializer.ToJsonlBytes(items);
        var text = Encoding.UTF8.GetString(bytes);

        // Assert
        text.Should().EndWith("\n");
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(1);
    }

    [Fact]
    public void ToJsonlBytes_EmptySequence_ProducesEmptyByteArray()
    {
        // Arrange + Act
        var bytes = RagExportSerializer.ToJsonlBytes(Array.Empty<RagExportChunkLine>());

        // Assert
        bytes.Should().BeEmpty();
    }

    [Fact]
    public void ToJsonlBytes_EmbeddingLines_EachLineContainsVector()
    {
        // Arrange
        var items = new[]
        {
            new RagExportEmbeddingLine(0, 1, "Text A", new float[] { 1.0f, 2.0f }, "model-v1"),
            new RagExportEmbeddingLine(1, 2, "Text B", new float[] { 3.0f, 4.0f }, "model-v1")
        };

        // Act
        var bytes = RagExportSerializer.ToJsonlBytes(items);
        var text = Encoding.UTF8.GetString(bytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(2);

        var first = JsonSerializer.Deserialize<RagExportEmbeddingLine>(lines[0])!;
        first.Vector.Should().BeEquivalentTo(new float[] { 1.0f, 2.0f });

        var second = JsonSerializer.Deserialize<RagExportEmbeddingLine>(lines[1])!;
        second.Vector.Should().BeEquivalentTo(new float[] { 3.0f, 4.0f });
    }

    // -------------------------------------------------------------------------
    // Pretty-print verification
    // -------------------------------------------------------------------------

    [Fact]
    public void ToJsonBytes_ProducesPrettyPrintedJson()
    {
        // Arrange
        var manifest = new RagExportManifest(
            ExportVersion: "1.0",
            ExportedAt: DateTimeOffset.UtcNow,
            TotalDocuments: 0,
            TotalChunks: 0,
            TotalEmbeddings: 0,
            EmbeddingModel: "test",
            Documents: []);

        // Act
        var bytes = RagExportSerializer.ToJsonBytes(manifest);
        var json = Encoding.UTF8.GetString(bytes);

        // Assert – pretty-printed JSON contains newlines and indentation
        json.Should().Contain("\n");
        json.Should().Contain("  "); // at least two-space indent
    }

    [Fact]
    public void ToJsonlBytes_LinesAreCompact_NoNewlinesWithinSingleLine()
    {
        // Arrange
        var items = new[]
        {
            new RagExportChunkLine(0, 1, "Some text with spaces.", 22)
        };

        // Act
        var bytes = RagExportSerializer.ToJsonlBytes(items);
        var text = Encoding.UTF8.GetString(bytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert – compact JSON: single line must not itself contain embedded newlines
        lines[0].Should().NotContain("\r");
        // The line should start with '{' and end with '}' (compact object)
        lines[0].Trim().Should().StartWith("{").And.EndWith("}");
    }
}
