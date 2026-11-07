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
/// Limite, ammesso: vede i riferimenti ai pacchetti dei due progetti e gli spazi dei nomi nel CODICE (un commento o
/// una stringa che li nomina, come questo, non conta: è la proprietà di <see cref="SourceScanner"/> che rende
/// utilizzabile un gate così). Una libreria AGPL che arrivasse come dipendenza transitiva di un'altra non si vede.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4440")]
public sealed class NoAgplPdfLibraryArchitectureTests
{
    private static readonly string[] ForbiddenInCode =
    [
        "using iText.",
        "iText.Kernel.",
        "iText.Layout.",
        "iText.IO.",
        "iText.Forms.",
    ];

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
                foreach (var needle in ForbiddenInCode)
                {
                    offenders.AddRange(SourceScanner.FindCodeOccurrences(text, needle)
                        .Select(offset => $"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{LineOf(text, offset)} ({needle})"));
                }
            }
        }

        offenders.Should().BeEmpty(
            "iText is AGPLv3 and this product is proprietary (#4440): extract with Docnet or the Unstructured service instead. Found:\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void NeitherProjectReferencesAnITextPackage()
    {
        var root = LocateRepoRoot();
        foreach (var project in new[]
        {
            Path.Combine(root, "apps", "api", "src", "Api", "Api.csproj"),
            Path.Combine(root, "apps", "api", "tests", "Api.Tests", "Api.Tests.csproj"),
        })
        {
            File.Exists(project).Should().BeTrue($"the project file must be at {project}");
            File.ReadAllText(project).Should().NotContainEquivalentOf(
                "Include=\"itext",
                $"{Path.GetFileName(project)} must not reference an iText package (AGPLv3, #4440)");
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
