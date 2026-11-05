using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4782: the page of a chunk that nobody measured is stored as <c>NULL</c>, and nothing reads that <c>NULL</c> as a
/// real page. Two halves, both on the source (the pattern of <c>ConstantTimeComparisonArchitectureTests</c>), because
/// the property is a plain <c>int?</c> and the compiler forces every reader to handle the null but not to handle it
/// <i>truthfully</i>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Writers.</b> Every code path that builds a <c>TextChunkEntity</c> from a chunk gives it
/// <c>ChunkPage.ForStorage(chunk.Page)</c>: a <c>0</c> (unknown) becomes <c>NULL</c> and is never stored as a page, and a
/// new writer cannot copy <c>chunk.Page</c> back in and re-open the defect. The writers that take their page from
/// somewhere else are listed, each with the reason.
/// </para>
/// <para>
/// <b>Readers.</b> A reader that writes <c>PageNumber ?? 0</c> keeps «no page» as «no page» (0 is the convention of
/// <c>GetDocumentsWithUnreliablePageNumbersQueryHandler</c>: «a page number of 0 or less is no page»). A reader that
/// writes <c>PageNumber ?? 1</c> turns «unknown» into a page that looks measured, which is the very defect. It is only
/// acceptable where the target type cannot say «unknown» at all, and each such site is listed with the type that
/// forces it.
/// </para>
/// <para>
/// <b>What this does not see.</b> A default written some other way (a <c>GetValueOrDefault(1)</c>, a ternary), a page
/// read from the vector table (<c>pgvector_embeddings.page_number</c> is never unknown: it is clamped to 1 on write, so
/// the citations built from vector hits still say a page; they are protected by the per-document rule of
/// <c>ICitedPageTrust</c>, not by this column), and the way a reader <i>presents</i> a page of 0.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4782")]
public sealed class ChunkPageStorageArchitectureTests
{
    /// <summary>
    /// Files that build a <c>TextChunkEntity</c> and set its page from something other than a chunk's page.
    /// </summary>
    private static readonly Dictionary<string, string> WritersWithAnotherSource = new(StringComparer.Ordinal)
    {
        ["BoundedContexts/DocumentProcessing/Infrastructure/Services/TableChunkIndexer.cs"] =
            "la pagina è quella della regione della tabella, misurata dall'estrattore delle tabelle; non è la pagina di un chunk",
        ["BoundedContexts/Administration/Application/Commands/ImportRagData/ImportRagDataCommandHandler.cs"] =
            "ripristina una riga esportata: copia il suo `int?` così com'è, quindi una pagina ignota resta null",
    };

    /// <summary>
    /// Files where an unknown page of a chunk becomes a real page (`?? 1`), with the type that cannot say «unknown».
    /// </summary>
    private static readonly Dictionary<string, string> ReadersThatNeedARealPage = new(StringComparer.Ordinal)
    {
        ["BoundedContexts/Administration/Application/Services/VectorReembeddingService.cs"] =
            "la riga vettoriale (`pgvector_embeddings.page_number`, `Embedding`) non ha «ignota»: si porta a 1 come fa PdfChunkPersistence",
        ["BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs"] =
            "il `SearchResult` di dominio rifiuta una pagina < 1 (ArgumentException): non può portare «ignota»; la citazione passa da ICitedPageTrust",
        ["BoundedContexts/Administration/Application/Commands/ImportRagData/ImportRagDataCommandHandler.cs"] =
            "la riga vettoriale importata non ha «ignota»: `Math.Max(1, …)` come nella scrittura normale",
    };

    private static readonly Regex PageAssignment = new(
        @"\bPageNumber\s*=(?!=)\s*(?<expr>[^,;\r\n]+)", RegexOptions.Compiled);

    private static readonly Regex DefaultToARealPage = new(
        @"\bPageNumber\s*\?\?\s*[1-9][0-9]*", RegexOptions.Compiled);

    [Fact]
    public void EveryTextChunkWriter_StoresThePageOfAChunkThroughChunkPageForStorage()
    {
        var apiSrc = LocateApiSrc();
        var offenders = new List<string>();

        foreach (var (relative, text) in SourceFiles(apiSrc))
        {
            if (!text.Contains("new TextChunkEntity", StringComparison.Ordinal)
                || WritersWithAnotherSource.ContainsKey(relative))
            {
                continue;
            }

            foreach (Match match in PageAssignment.Matches(text))
            {
                var expression = match.Groups["expr"].Value.Trim();
                if (!expression.Contains("ChunkPage.ForStorage(", StringComparison.Ordinal))
                {
                    offenders.Add($"{relative}  PageNumber = {expression}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "una pagina `0` è «non misurata»: salvata così com'è, i lettori la prenderebbero per una pagina (`p. 0`), e "
            + "copiata come 1 è la pagina falsa di #4782. Il valore per la colonna è `ChunkPage.ForStorage(chunk.Page)`: "
            + "0 diventa NULL. Se la pagina non è quella di un chunk, registra il file in "
            + $"{nameof(WritersWithAnotherSource)} con il motivo. Scrittori fuori regola:\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void NoReaderTurnsAnUnknownPageIntoARealOne_ExceptWhereTheTargetTypeCannotSayUnknown()
    {
        var apiSrc = LocateApiSrc();
        var offenders = new List<string>();

        foreach (var (relative, text) in SourceFiles(apiSrc))
        {
            if (DefaultToARealPage.IsMatch(text) && !ReadersThatNeedARealPage.ContainsKey(relative))
            {
                var line = text.Split('\n').First(l => DefaultToARealPage.IsMatch(l)).Trim();
                offenders.Add($"{relative}  {line}");
            }
        }

        offenders.Should().BeEmpty(
            "`PageNumber ?? 1` trasforma «pagina ignota» in una pagina che sembra misurata: è il difetto di #4782 spostato "
            + "nel lettore. `?? 0` va bene (0 è «nessuna pagina»). Se il tipo di destinazione non può dire «ignota» (rifiuta "
            + $"una pagina < 1), registra il file in {nameof(ReadersThatNeedARealPage)} con il tipo che lo impone. "
            + "Lettori fuori regola:\n" + string.Join('\n', offenders));
    }

    /// <summary>
    /// Il numero di pagine del PDF è ciò che distingue un documento piatto di UNA pagina (pagina 1, vera) da uno di
    /// dodici senza mappa (nessuna pagina). Tre vie di indicizzazione chiamano <c>HeadingAwareChunker.BuildAsync</c>, e
    /// solo una ha un test che la esegue: se un'altra smettesse di passarlo, ogni suo documento piatto perderebbe la
    /// pagina 1 e nessun test se ne accorgerebbe.
    /// </summary>
    [Fact]
    public void EveryHeadingAwareChunkerCall_PassesThePageCountOfTheDocument()
    {
        var apiSrc = LocateApiSrc();
        var calls = new List<(string Relative, string Call)>();
        const string Marker = "HeadingAwareChunker.BuildAsync(";

        foreach (var (relative, text) in SourceFiles(apiSrc))
        {
            var from = 0;
            while ((from = text.IndexOf(Marker, from, StringComparison.Ordinal)) >= 0)
            {
                var end = text.IndexOf(';', from);
                calls.Add((relative, text[from..end]));
                from = end;
            }
        }

        calls.Should().NotBeEmpty("la scansione deve trovare le vie di indicizzazione, o non protegge nulla");
        var withoutTheCount = calls
            .Where(c => !c.Call.Contains("documentPageCount:", StringComparison.Ordinal))
            .Select(c => c.Relative)
            .ToList();

        withoutTheCount.Should().BeEmpty(
            "senza `documentPageCount:` un documento piatto di una sola pagina riceve «pagina ignota» invece di pagina 1 "
            + "(ExtractedDocumentFactory). Passa il numero di pagine del PDF. Chiamanti che non lo passano:" + "\n"
            + string.Join('\n', withoutTheCount));
    }

    [Fact]
    public void EveryRegisteredFile_IsStillNeeded()
    {
        var apiSrc = LocateApiSrc();
        var stale = new List<string>();

        foreach (var relative in WritersWithAnotherSource.Keys)
        {
            var text = ReadRegistered(apiSrc, relative);
            if (!text.Contains("new TextChunkEntity", StringComparison.Ordinal))
            {
                stale.Add($"{nameof(WritersWithAnotherSource)}: {relative} non costruisce più un TextChunkEntity");
            }
        }

        foreach (var relative in ReadersThatNeedARealPage.Keys)
        {
            var text = ReadRegistered(apiSrc, relative);
            if (!DefaultToARealPage.IsMatch(text))
            {
                stale.Add($"{nameof(ReadersThatNeedARealPage)}: {relative} non ha più un `PageNumber ?? n`");
            }
        }

        stale.Should().BeEmpty(
            "una voce che non serve più è rumore, e nasconde la regola: toglila. Voci non più necessarie:\n"
            + string.Join('\n', stale));
    }

    private static string ReadRegistered(string apiSrc, string relative)
    {
        var path = Path.Combine(apiSrc, relative.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue(
            $"il file registrato deve esistere: {relative}. Se è stato spostato, aggiorna la voce invece di toglierla.");
        return File.ReadAllText(path);
    }

    private static IEnumerable<(string Relative, string Text)> SourceFiles(string apiSrc)
    {
        foreach (var path in Directory.EnumerateFiles(apiSrc, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(apiSrc, path).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal)
                || relative.StartsWith("obj/", StringComparison.Ordinal)
                || relative.StartsWith("Infrastructure/Migrations/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (relative, File.ReadAllText(path));
        }
    }

    private static string LocateApiSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // In un git worktree `.git` è un file, non una directory: accettare entrambi.
        while (dir is not null)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                break;
            }

            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}
