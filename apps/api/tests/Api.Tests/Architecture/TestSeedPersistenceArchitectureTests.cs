using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #4054 — il sesto seeder non deve poter reintrodurre il 500.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 Perché questo è un gate e non una riga di documentazione. La correzione di #4054 ha due
/// pezzi: due guardie pre-INSERT (che nominano il campo) e una traduzione dello SQLSTATE in
/// <c>TestSeedPersistence.SaveSeedAsync</c> (che copre tutto il resto). Il secondo pezzo si perde
/// scrivendo <c>_db.SaveChangesAsync</c> invece di <c>_db.SaveSeedAsync</c> — una differenza di
/// sette caratteri, in un file che compila, passa i test del proprio handler e torna a rispondere
/// <c>internal_server_error</c> alla prima collisione. Nessun test del nuovo seeder se ne
/// accorgerebbe: il difetto si manifesta solo sul percorso d'errore, che nessuno scrive per primo.
/// </para>
/// <para>
/// Il raggio è deliberatamente il solo bounded context <c>Testing</c>: altrove
/// <c>SaveChangesAsync</c> è corretto, e la traduzione in 4xx sarebbe sbagliata perché un conflitto
/// su una scrittura di prodotto non è sempre attribuibile al chiamante.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "4054")]
public sealed class TestSeedPersistenceArchitectureTests
{
    /// <summary>
    /// L'unico file autorizzato a chiamare <c>SaveChangesAsync</c>: è quello che la traduce.
    /// </summary>
    private const string TranslatorFileName = "TestSeedPersistence.cs";

    [Fact]
    public void NoSeedHandlerCallsSaveChangesAsyncDirectly()
    {
        var offenders = new List<string>();

        foreach (var path in EnumerateTestingContextSources())
        {
            if (string.Equals(Path.GetFileName(path), TranslatorFileName, StringComparison.Ordinal))
            {
                continue;
            }

            // FindCodeOccurrences salta commenti e letterali: senza quella proprietà questo gate
            // denuncerebbe la propria documentazione, e gli XML doc degli handler che nominano
            // SaveChanges in prosa.
            var source = File.ReadAllText(path);
            if (SourceScanner.FindCodeOccurrences(source, "SaveChangesAsync").Count > 0)
            {
                offenders.Add(Path.GetFileName(path));
            }
        }

        offenders.Should().BeEmpty(
            "i seeder E2E devono persistere via TestSeedPersistence.SaveSeedAsync, che traduce " +
            "unique/foreign-key violation in 409/400 nominando il vincolo. Una SaveChangesAsync " +
            "diretta riporta quelle collisioni al fallback 500, il cui corpo non porta nemmeno il " +
            "nome del vincolo: quello vive in InnerException.Message (#4054)");
    }

    [Fact]
    public void EverySeedHandlerThatWritesGoesThroughTheTranslatingSave()
    {
        var handlersThatWrite = new List<string>();
        var handlersThatSave = new List<string>();

        foreach (var path in EnumerateTestingContextSources())
        {
            var name = Path.GetFileName(path);
            if (!name.EndsWith("CommandHandler.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var source = File.ReadAllText(path);
            // La lista dei seeder si DERIVA dal codice invece di essere elencata qui: un elenco
            // scritto a mano è esattamente ciò che il sesto seeder non aggiornerebbe. «Scrive» =
            // tocca il DbContext E aggiunge qualcosa; le due condizioni insieme perché `.Add(` da
            // solo prende anche una List in memoria, e `_db.` da solo prende i soli lettori (come
            // CleanupTestEntitiesCommandHandler, che cancella con ExecuteDeleteAsync e non salva).
            if (SourceScanner.FindCodeOccurrences(source, "_db.").Count > 0
                && SourceScanner.FindCodeOccurrences(source, ".Add(").Count > 0)
            {
                handlersThatWrite.Add(name);
                if (SourceScanner.FindCodeOccurrences(source, "SaveSeedAsync").Count > 0)
                {
                    handlersThatSave.Add(name);
                }
            }
        }

        handlersThatWrite.Should().NotBeEmpty(
            "se lo scanner non trova più nessun seeder che scrive, il gate sta misurando il vuoto " +
            "e sarebbe verde per il motivo sbagliato");
        handlersThatSave.Should().BeEquivalentTo(
            handlersThatWrite,
            "un handler che aggiunge righe al DbContext deve persisterle via SaveSeedAsync. Se " +
            "questo handler NON scrive davvero sul database (per esempio l'`.Add(` è su una lista " +
            "in memoria), l'euristica di questo gate va resa più precisa qui: non aggiungere " +
            "un'esenzione per nome");
    }

    [Fact]
    public void TheTranslatorStillCoversBothConstraintViolations()
    {
        // Il gate sopra garantisce che tutti passino dalla rete; questo garantisce che la rete
        // abbia ancora le maglie. Senza, cancellare un ramo di Describe lascia i due gate verdi.
        var source = File.ReadAllText(
            Path.Combine(LocateTestingContextRoot(), "Infrastructure", TranslatorFileName));

        // Due asserzioni e non una, perché i due SQLSTATE sono LETTERALI di stringa e
        // FindCodeOccurrences salta proprio i letterali: cercarli con quello non troverebbe nulla
        // e il gate sarebbe rosso sempre. Il VALORE si verifica sul testo esatto della
        // dichiarazione (un commento che nomina 23505 non la contiene); l'USO sugli
        // identificatori, dove lo scanner serve davvero perché ignora i commenti.
        source.Should().Contain(
            "UniqueViolation = \"23505\"", "23505 è lo SQLSTATE del difetto osservato in #4054");
        source.Should().Contain(
            "ForeignKeyViolation = \"23503\"", "23503 è l'altro conflitto prevedibile");

        foreach (var required in
            new[] { "UniqueViolation", "ForeignKeyViolation", "ConflictException", "BadRequestException" })
        {
            SourceScanner.FindCodeOccurrences(source, required).Should().NotBeEmpty(
                "la rete di #4054 traduce unique_violation in un ConflictException (409) e " +
                "foreign_key_violation in un BadRequestException (400): manca «{0}»", required);
        }
    }

    private static IEnumerable<string> EnumerateTestingContextSources() =>
        Directory.EnumerateFiles(LocateTestingContextRoot(), "*.cs", SearchOption.AllDirectories);

    private static string LocateTestingContextRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var root = Path.Combine(
            dir!.FullName, "apps", "api", "src", "Api", "BoundedContexts", "Testing");
        Directory.Exists(root).Should().BeTrue($"the Testing bounded context must exist at {root}");
        return root;
    }
}
