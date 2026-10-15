using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4384 — una riga di <c>pgvector_embeddings</c> nasce solo da <c>PgVectorEmbeddingEntity.ForChunk</c>.
/// </summary>
/// <remarks>
/// <para>
/// La regola «collega la riga al suo <c>text_chunks</c>» era scritta in quattro posti e rispettata in
/// uno solo: la pipeline di Quartz. Il caricamento da <c>/ingest/pdf</c>, la reindicizzazione
/// (<c>IndexPdfCommandHandler</c>) e l'import dei dati RAG costruivano le righe a mano, e lasciavano
/// <c>source_chunk_id</c> nullo. Nessun test se ne accorgeva: una riga scollegata si salva, si trova e
/// si cita, e perde in silenzio il boost di heading e la localizzazione della citazione.
/// </para>
/// <para>
/// <c>ForChunk</c> chiede il collegamento come argomento obbligatorio. Questo gate impedisce di
/// aggirarlo con un <c>new PgVectorEmbeddingEntity { … }</c> in un quinto scrittore. La pipeline di
/// Quartz scrive per un'altra via (l'entità di dominio <c>Embedding</c> e il COPY dell'adattatore
/// pgvector) e lì il collegamento c'è già, protetto da <c>PdfProcessingPipelineRoleTagsTests</c>.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4384")]
public sealed class PgVectorEmbeddingWritersArchitectureTests
{
    private const string Construction = "new PgVectorEmbeddingEntity";

    [Fact]
    public void NoSourceBuildsAPgVectorRowOutsideForChunk()
    {
        var offenders = EnumerateApiSources()
            .Where(path => SourceScanner.FindCodeOccurrences(File.ReadAllText(path), Construction).Count > 0)
            .Select(path => Path.GetRelativePath(LocateApiSourceRoot(), path))
            .ToList();

        offenders.Should().BeEmpty(
            "ogni riga di pgvector_embeddings va costruita con PgVectorEmbeddingEntity.ForChunk, che chiede " +
            "a quale text_chunks appartiene: una riga senza source_chunk_id perde boost di heading e " +
            "localizzazione della citazione, in silenzio (#4384)");
    }

    [Fact]
    public void TheScanFindsTheWritersThatUseForChunk()
    {
        // Contro un gate vuoto: se la scansione non leggesse i sorgenti giusti, il test sopra
        // passerebbe per niente.
        var writers = EnumerateApiSources()
            .Where(path => SourceScanner.FindCodeOccurrences(File.ReadAllText(path), "PgVectorEmbeddingEntity.ForChunk").Count > 0)
            .Select(Path.GetFileName)
            .ToList();

        writers.Should().Contain(
            ["UploadPdfCommandHandler.Processing.cs", "IndexPdfCommandHandler.cs", "ImportRagDataCommandHandler.cs"]);
    }

    private static IEnumerable<string> EnumerateApiSources()
    {
        var root = LocateApiSourceRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relative = Path.GetRelativePath(root, path);
                return !relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            });
    }

    private static string LocateApiSourceRoot()
    {
        // In un worktree .git è un file, non una cartella: si accettano entrambi.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !Directory.Exists(Path.Combine(dir.FullName, ".git"))
               && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("il binario dei test deve stare dentro il repository");
        var root = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(root).Should().BeTrue($"i sorgenti dell'API devono stare in {root}");
        return root;
    }
}
