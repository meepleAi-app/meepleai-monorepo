using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4440 — né l'API né i suoi test usano iText. iText Core gratuito è sotto AGPLv3 e il prodotto è proprietario
/// (ADR-003b scartò <c>pymupdf4llm</c> per lo stesso motivo). La dipendenza è uscita perché i percorsi che la
/// usavano non avevano lettori; questo gate impedisce che rientri senza che qualcuno lo decida. Non è un parere
/// legale: dice solo che oggi il codice non la nomina.
/// </summary>
/// <remarks>
/// Limite, ammesso: vede i riferimenti ai pacchetti in ogni file di progetto sotto <c>apps/api</c> e ogni spazio dei
/// nomi <c>iText.</c> nel CODICE (un commento o una stringa che lo nomina, come questo, non conta: è la proprietà di
/// <see cref="SourceScanner"/> che rende utilizzabile un gate così). Una libreria AGPL che arrivasse come dipendenza
/// transitiva di un'altra non si vede.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4440")]
public sealed class NoAgplPdfLibraryArchitectureTests
{
    /// <summary>
    /// Ogni <c>iText.</c> nel codice, di qualunque sotto-spazio e anche dopo <c>using static</c> o <c>global::</c>: l'elenco
    /// per sotto-spazio (Kernel, Layout, IO, Forms) lasciava fuori <c>iText.Commons.</c>, <c>iText.Pdfa.</c> e
    /// <c>using static iText.…</c> (#4930). Un identificatore che finisce in «iText» (<c>ApiText.</c>) non conta.
    /// </summary>
    private static List<int> FindITextNamespaces(string text) =>
        SourceScanner.FindCodeOccurrences(text, "iText.")
            .Where(offset => offset == 0 || !(char.IsLetterOrDigit(text[offset - 1]) || text[offset - 1] == '_'))
            .ToList();

    [Fact]
    public void NoSourceFileOfTheApiOrItsTestsNamesAnITextNamespace()
    {
        var root = LocateRepoRoot();
        var offenders = new List<string>();
        foreach (var folder in new[] { Path.Combine(root, "apps", "api", "src"), Path.Combine(root, "apps", "api", "tests") })
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Where(p => !IsBuildOutput(p)))
            {
                var text = File.ReadAllText(path);
                offenders.AddRange(FindITextNamespaces(text)
                    .Select(offset => $"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{LineOf(text, offset)}"));
            }
        }

        offenders.Should().BeEmpty(
            "iText is AGPLv3 and this product is proprietary (#4440): extract with Docnet or the Unstructured service instead. Found:\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void NoProjectFileUnderTheApiReferencesAnITextPackage()
    {
        var root = LocateRepoRoot();
        var projectFiles = Directory.EnumerateFiles(Path.Combine(root, "apps", "api"), "*.*", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p)
                && (p.EndsWith(".csproj", StringComparison.Ordinal)
                    || p.EndsWith(".props", StringComparison.Ordinal)
                    || p.EndsWith(".targets", StringComparison.Ordinal)))
            .ToList();

        projectFiles.Should().Contain(p => p.EndsWith("Api.csproj", StringComparison.Ordinal))
            .And.Contain(p => p.EndsWith("Api.Tests.csproj", StringComparison.Ordinal), "the scan must reach the two main projects");

        foreach (var file in projectFiles)
        {
            File.ReadAllText(file).Should().NotContainEquivalentOf(
                "Include=\"itext",
                $"{Path.GetFileName(file)} must not reference an iText package (AGPLv3, #4440)");
        }
    }

    [Fact]
    public void TheScanSeesANamespaceInCodeAndOnlyThere()
    {
        SourceScanner.FindCodeOccurrences("using iText.Kernel.Pdf;", "using iText.").Should().ContainSingle();
        SourceScanner.FindCodeOccurrences("var r = new iText.Kernel.Pdf.PdfReader(p);", "iText.Kernel.").Should().ContainSingle();
        SourceScanner.FindCodeOccurrences("// replaced using iText.Kernel.Pdf;", "using iText.").Should().BeEmpty();
        SourceScanner.FindCodeOccurrences("/// <see cref=\"iText.Kernel.Pdf.PdfReader\"/>", "iText.Kernel.").Should().BeEmpty();
        SourceScanner.FindCodeOccurrences("var s = \"using iText.Kernel;\";", "using iText.").Should().BeEmpty();
    }

    [Theory]
    [InlineData("using iText.Kernel.Pdf;", 1)]
    [InlineData("using static iText.Kernel.Pdf.PdfName;", 1)]
    [InlineData("using iText.Pdfa;", 1)]
    [InlineData("var u = iText.Commons.Utils.Foo.Bar();", 1)]
    [InlineData("var u = global::iText.Layout.Document.Create();", 1)]
    [InlineData("var t = ApiText.Length + multi_iText.Count;", 0)]
    [InlineData("// using iText.Kernel.Pdf;", 0)]
    [InlineData("var s = \"iText.Kernel.\";", 0)]
    public void TheGateSeesEveryITextNamespaceAndNoIdentifierThatEndsInIt(string code, int expected) =>
        FindITextNamespaces(code).Should().HaveCount(expected);

    [Fact]
    public void TheGateReallyLooksAtTheApi()
    {
        var root = LocateRepoRoot();

        Directory.EnumerateFiles(Path.Combine(root, "apps", "api", "src"), "*.cs", SearchOption.AllDirectories)
            .Count(p => !IsBuildOutput(p))
            .Should().BeGreaterThan(500, "the scan must cover the whole API, not an empty folder");
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static int LineOf(string text, int offset) => text.AsSpan(0, offset).Count('\n') + 1;

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // In a git worktree `.git` is a file, not a directory: accepting both stops the climb at the right root
        // instead of going on to the main checkout.
        while (dir is not null)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                break;
            }

            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the tests run inside the repository");
        return dir!.FullName;
    }
}
