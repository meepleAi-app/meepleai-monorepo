using System.Text.RegularExpressions;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4138: il frontend filtra le opzioni della tendina dei modelli con una copia dei prefissi di
/// <see cref="LlmModelRouting.BareCloudProviderPrefixes"/> (<c>apps/web/src/lib/llm-model-routing.ts</c>),
/// così non offre mai un modello che il backend respinge con <c>422 "Model is not routable"</c>.
/// <para>
/// Quella copia era tenuta allineata a mano, solo con un commento. Se il backend aggiunge un prefisso
/// e il frontend no, la tendina torna a offrire un'opzione che risponde sempre 422. Se invece il
/// frontend ne ha uno in più, nasconde un modello che funziona. Questo test legge l'array dal
/// sorgente TypeScript e lo confronta con quello C#.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4138")]
public sealed partial class FrontendModelRoutingMirrorTests
{
    private const string FrontendFile = "apps/web/src/lib/llm-model-routing.ts";

    [Fact]
    public void FrontendPrefixes_AreExactlyTheBackendOnes()
    {
        var frontend = ReadFrontendPrefixes();

        frontend.Should().Equal(
            LlmModelRouting.BareCloudProviderPrefixes,
            $"{FrontendFile} deve elencare gli stessi prefissi di LlmModelRouting.BareCloudProviderPrefixes, "
            + "nello stesso ordine: una divergenza è un'opzione offerta che risponde 422, oppure un modello "
            + "instradabile nascosto");
    }

    private static List<string> ReadFrontendPrefixes()
    {
        var path = Path.Combine(LocateRepoRoot(), FrontendFile.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"il mirror frontend deve esistere in {FrontendFile}");

        var source = File.ReadAllText(path);
        var array = PrefixArrayRegex().Match(source);
        array.Success.Should().BeTrue(
            $"{FrontendFile} deve dichiarare `export const BARE_CLOUD_PROVIDER_PREFIXES = [ ... ] as const`; "
            + "se la forma è cambiata, aggiorna questo test invece di toglierlo");

        var prefixes = StringLiteralRegex().Matches(array.Groups["items"].Value)
            .Select(m => m.Groups["value"].Value)
            .ToList();
        prefixes.Should().NotBeEmpty("un array letto vuoto vorrebbe dire che il test non ha confrontato niente");
        return prefixes;
    }

    /// <summary>
    /// Risale fino alla radice del monorepo riconoscendola dai sorgenti, non da <c>.git</c>, che in un
    /// worktree è un file.
    /// </summary>
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, FrontendFile.Replace('/', Path.DirectorySeparatorChar))))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        return dir!.FullName;
    }

    [GeneratedRegex(@"export\s+const\s+BARE_CLOUD_PROVIDER_PREFIXES\s*=\s*\[(?<items>[^\]]*)\]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrefixArrayRegex();

    [GeneratedRegex(@"'(?<value>[^']*)'|""(?<value>[^""]*)""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StringLiteralRegex();
}
