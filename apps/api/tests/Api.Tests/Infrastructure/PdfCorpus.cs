namespace Api.Tests.Infrastructure;

/// <summary>
/// Risolve i percorsi del corpus PDF di test dalla <b>radice del repository</b>.
/// </summary>
/// <remarks>
/// <para>
/// #4040 — esiste perché il manifest `TestData/pdf-corpus/gold-standards.json` dichiara percorsi
/// della forma <c>../../../../data/rulebook/&lt;file&gt;.pdf</c>, e una risalita a <b>conteggio
/// fisso</b> non può essere corretta: dipende da quanti livelli separano la working directory dei
/// test dalla radice. Misurato, i test girano in
/// <c>apps/api/tests/Api.Tests/bin/Debug/net9.0</c>, dove quattro livelli su portano a
/// <c>apps/api/tests/</c> — che non contiene <c>data/</c>. La radice sta <b>sette</b> livelli su.
/// </para>
/// <para>
/// 🔴 La conseguenza era peggiore del percorso sbagliato: i sette siti
/// <c>if (!File.Exists(standard.RelativePath)) Assert.Skip($"PDF not found: …")</c> saltavano
/// <b>sempre</b>, in ogni ambiente, e il motivo chiudeva l'indagine — «PDF not found» si legge come
/// «manca il file», non come «il percorso non punta dove credi». I PDF sono committati in
/// <c>data/rulebook/</c> e c'erano tutti. È il caso MinIO (#3978) con un'altra causa: un dato
/// <b>presente</b> letto come <b>assente</b>.
/// </para>
/// <para>
/// La risoluzione normalizza: scarta i segmenti <c>../</c> iniziali, che nel manifest esprimono
/// «risali alla radice», e combina il resto con la radice trovata risalendo fino a <c>.git</c>.
/// Così il manifest resta leggibile e il percorso non dipende dalla profondità di chi lo legge.
/// </para>
/// </remarks>
internal static class PdfCorpus
{
    /// <summary>Il percorso del manifest, relativo alla working directory dei test.</summary>
    /// <remarks>
    /// Questo <i>sì</i> è relativo alla cwd, e correttamente: il file è copiato nell'output del
    /// progetto di test, quindi sta accanto all'assembly.
    /// </remarks>
    internal const string ManifestPath = "TestData/pdf-corpus/gold-standards.json";

    private static readonly Lazy<string> Root = new(FindRepositoryRoot);

    /// <summary>La radice del repository, trovata risalendo fino alla cartella che contiene <c>.git</c>.</summary>
    internal static string RepositoryRoot => Root.Value;

    /// <summary>
    /// Il percorso assoluto di una voce del manifest.
    /// </summary>
    /// <param name="manifestRelativePath">
    /// Il valore di <c>relativePath</c> come sta nel manifest, con o senza segmenti <c>../</c>.
    /// </param>
    internal static string Resolve(string manifestRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestRelativePath);

        var normalized = manifestRelativePath.Replace('\\', '/');
        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .SkipWhile(s => s is ".." or ".")
            .ToArray();

        if (segments.Length == 0)
        {
            throw new ArgumentException(
                $"il percorso '{manifestRelativePath}' e' composto solo da segmenti di risalita: "
                + "non nomina alcun file",
                nameof(manifestRelativePath));
        }

        return Path.GetFullPath(Path.Combine(new[] { RepositoryRoot }.Concat(segments).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException(
                $"radice del repository non trovata risalendo da {AppContext.BaseDirectory}: "
                + "i test devono girare dentro il repository perche' il corpus PDF e' committato");
    }
}
