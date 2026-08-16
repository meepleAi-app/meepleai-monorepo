using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Issue #3688 — inventario dei write-path delle entità che dichiarano un concurrency token.
///
/// <para>
/// <b>Perché esiste.</b> Un'entità con token <c>xmin</c> può essere persistita in modo corretto o
/// rotto, e la differenza non si vede né dal modello né dai test: <b>EF InMemory non ha la colonna
/// di sistema <c>xmin</c></b>, quindi entrambi i casi passano. #3670 ha trovato
/// <c>GameToolkitRepository</c> rotto in produzione su ~21 call site — rinominare un toolkit,
/// aggiungere un dice tool, salvare un preset — con l'intera suite verde.
/// </para>
/// <para>
/// <b>Il difetto.</b> <c>MapToPersistence</c> + <c>DbSet.Update(entity)</c> su un'istanza
/// <b>detached</b> il cui token vale 0 emette <c>UPDATE … WHERE xmin = 0</c>. <c>xmin</c> non è mai
/// 0 per una tupla viva, quindi zero righe, <c>DbUpdateConcurrencyException</c>, 500 — su
/// <b>ogni</b> scrittura, non su una race.
/// </para>
/// <para>
/// <b>Le tre tecniche che lo evitano</b>, tutte in uso nel repository e tutte legittime:
/// <list type="bullet">
///   <item><see cref="WritePathTechnique.OriginalValue"/> — <c>entry.Property(e =&gt; e.Xmin)
///   .OriginalValue = aggregate.Xmin</c> prima di salvare (<c>AlertChannelRepository:78</c>).</item>
///   <item><see cref="WritePathTechnique.TokenRoundTrip"/> — il mapper riporta il token letto
///   nell'entità mappata, così <c>Update()</c> su detached parte dal valore giusto
///   (<c>PlayRecordRepository:338</c>, con commento).</item>
///   <item><see cref="WritePathTechnique.TrackedMutation"/> — si carica l'entità tracked e la si
///   muta, così EF conserva l'original value caricato
///   (<c>MechanicGoldenClaimRepository:72</c>).</item>
/// </list>
/// </para>
/// <para>
/// <b>Che cosa protegge questo test.</b> Non prova che un write-path sia corretto — quello si
/// dimostra solo su Postgres, e il modello è
/// <c>GameToolkitRepositoryPostgresConcurrencyTests</c> (#3670). Protegge dall'altra cosa: che una
/// <b>nuova</b> entità con token entri nel modello senza che nessuno abbia deciso, e scritto, come
/// viene persistita. Un test di convenzione sull'inventario costa una riga per entità; un test di
/// concorrenza Postgres per ogni repository sano costerebbe Testcontainers su tutto l'inventario e
/// sarebbe la prima cosa a essere tagliata.
/// </para>
/// <para>
/// <b>Se questo test fallisce</b> hai aggiunto (o rimosso) un concurrency token. Leggi il
/// write-path del repository che scrive quell'entità, stabilisci quale delle tre tecniche usa —
/// se non ne usa nessuna, è rotto e lo scoprirai solo in produzione — e aggiungi la riga qui.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Issue", "3688")]
public sealed class ConcurrencyTokenWritePathInventoryTests
{
    internal enum WritePathTechnique
    {
        /// <summary>Il repository assegna esplicitamente <c>OriginalValue</c> al token.</summary>
        OriginalValue,

        /// <summary>Il mapper riporta il token letto nell'entità mappata prima di <c>Update()</c>.</summary>
        TokenRoundTrip,

        /// <summary>Il repository carica l'entità tracked e la muta, senza <c>Update()</c> su detached.</summary>
        TrackedMutation,
    }

    /// <summary>
    /// Ogni entità con token <c>xmin</c> (<see cref="uint"/>), con il modo in cui il suo write-path
    /// preserva il token. La chiave è il nome CLR dell'entità.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (WritePathTechnique Technique, string WritePath)> DeclaredWritePaths =
        new Dictionary<string, (WritePathTechnique, string)>(StringComparer.Ordinal)
        {
            ["AlertChannelEntity"] = (WritePathTechnique.OriginalValue, "AlertChannelRepository:78 — il pattern di riferimento"),
            ["AppBudgetEntity"] = (WritePathTechnique.OriginalValue, "AppBudgetRepository:66 (#2690)"),
            ["GameNightEventEntity"] = (WritePathTechnique.OriginalValue, "GameNightEventRepository"),
            ["GameToolkitEntity"] = (WritePathTechnique.OriginalValue, "GameToolkitRepository (#3670 — qui il difetto è stato trovato)"),
            ["LiveGameSessionEntity"] = (WritePathTechnique.OriginalValue, "LiveSessionRepository (ADR-060)"),
            ["SharedGameEntity"] = (WritePathTechnique.OriginalValue, "SharedGameRepository"),

            ["CertificationThresholdsConfigEntity"] = (WritePathTechnique.TokenRoundTrip, "CertificationThresholdsConfigRepository:87 — Xmin = config.XminVersion"),
            ["GameBook"] = (WritePathTechnique.TokenRoundTrip, "GameBookRepository:58 — l'aggregato è l'entità EF e trasporta il token letto"),
            ["GameNightPlaylistEntity"] = (WritePathTechnique.TokenRoundTrip, "GameNightPlaylistRepository:180 (#2306)"),
            ["MechanicAnalysisEntity"] = (WritePathTechnique.TokenRoundTrip, "MechanicAnalysisRepository:391, con Xmin escluso da IsModified"),
            ["MechanicCardEntity"] = (WritePathTechnique.TokenRoundTrip, "MechanicCardRepository:159"),
            ["PdfDocumentEntity"] = (WritePathTechnique.TokenRoundTrip, "PdfDocumentRepository:338 (#3694) — il commento a :333 spiega il difetto"),
            ["PlayRecordEntity"] = (WritePathTechnique.TokenRoundTrip, "PlayRecordRepository:338 — «round-trip for detached Update (ADR-060)»"),
            ["ShareRequestEntity"] = (WritePathTechnique.TokenRoundTrip, "ShareRequestRepository:259 (#3698)"),
            ["SharedGameTranslationEntity"] = (WritePathTechnique.TokenRoundTrip, "SharedGameTranslationRepository:54-58 — Attach + Modified con il token nell'entità"),
            ["ToolkitVersionEntity"] = (WritePathTechnique.TokenRoundTrip, "ToolkitVersionRepository:173 (#3688 → #3704)"),

            ["BggTosHashEntity"] = (WritePathTechnique.TrackedMutation, "BggTosWatcherJob:129-169 — riga singleton caricata AsTracking e mutata (#3651 lotto 4)"),
            ["DomainEventOutboxEntity"] = (WritePathTechnique.TrackedMutation, "DomainEventOutboxProcessor:118 — righe tracked mutate via MarkSent/MarkRetry/MarkFailed (#1535)"),
            ["GameCoverAssignmentEntity"] = (WritePathTechnique.TrackedMutation, "SharedGameRepository:244 — riconciliazione su set AsTracking"),
            ["MechanicGoldenClaimEntity"] = (WritePathTechnique.TrackedMutation, "MechanicGoldenClaimRepository:72 — il commento descrive per esteso il difetto evitato"),
            ["ProviderCredential"] = (WritePathTechnique.TrackedMutation, "ProviderCredentialRepository (#3683)"),
        };

    /// <summary>
    /// Entità il cui concurrency token è ancora <c>byte[]</c> su una colonna <c>bytea</c> — #3651.
    ///
    /// <para>
    /// Su queste il token è dichiarato ma <b>inerte</b>: Postgres non popola una <c>bytea</c>, il
    /// valore resta <c>null</c> e non cambia mai fra un update e l'altro, quindi nessun conflitto
    /// viene mai rilevato (last-write-wins silenzioso). Non hanno un write-path da classificare
    /// perché non c'è nulla da preservare: la loro voce è un debito, non una tecnica.
    /// </para>
    /// <para>
    /// Quando #3651 converte una di queste a <c>uint Xmin</c>, questo test costringe a spostarne la
    /// riga in <see cref="DeclaredWritePaths"/> — cioè a stabilire come il write-path preserva il
    /// token appena acceso. È il punto in cui i due difetti si toccano: convertire senza guardare
    /// il write-path sposta dal guasto di #3651 a quello di #3688.
    /// </para>
    /// </summary>
    private static readonly IReadOnlySet<string> PendingByteaConversions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "AbTestSession",
            "CatalogSeedDraftEntity",
            "GameSessionStateEntity",
            // Non censita in #3651, trovata da questo test. `KbQualityBudgetCounterEntityConfiguration:23-25`
            // usa `.IsRowVersion()` su un `byte[]?` e il commento dichiara «optimistic concurrency via
            // Postgres xmin» citando la convenzione di PdfDocumentEntity (#1802). È la stessa
            // affermazione falsa che #3651 documenta: su Npgsql `IsRowVersion()` su byte[] NON mappa a
            // `xmin`, serve la configurazione esplicita. Il token è quindi inerte come gli altri.
            "KbQualityBudgetCounter",
            "PhotoBatchUpload",
            "ProposalMigrationEntity",
            "RuleSpecEntity",
            "SessionEntity",
            "UserLibraryEntryEntity",
        };

    private static MeepleAiDbContext CreateModelOnlyContext()
    {
        // Nessuna connessione viene aperta: costruire il modello non tocca il database. Il provider
        // è Npgsql (non InMemory) di proposito — è il provider che mappa `xmin` a un concurrency
        // token, ed è esattamente la configurazione che questo inventario deve osservare.
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=model_only;Username=none;Password=none",
                o => o.UseVector()) // #3547: richiesto dal modello
            .Options;

        return new MeepleAiDbContext(options, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
    }

    private static IReadOnlyList<(string EntityName, Type TokenType)> DiscoverConcurrencyTokens()
    {
        using var context = CreateModelOnlyContext();

        return context.Model.GetEntityTypes()
            .Select(entityType => new
            {
                Name = entityType.ClrType.Name,
                Token = entityType.GetProperties().FirstOrDefault(p => p.IsConcurrencyToken),
            })
            .Where(x => x.Token is not null)
            .Select(x => (x.Name, TokenType: x.Token!.ClrType))
            .DistinctBy(x => x.Name, StringComparer.Ordinal)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void EveryXminTokenDeclaresHowItsWritePathPreservesIt()
    {
        var undeclared = DiscoverConcurrencyTokens()
            .Where(x => x.TokenType == typeof(uint))
            .Select(x => x.EntityName)
            .Where(name => !DeclaredWritePaths.ContainsKey(name))
            .ToList();

        undeclared.Should().BeEmpty(
            "ogni entità con token xmin deve dichiarare come il suo write-path lo preserva "
            + "(#3688): OriginalValue, TokenRoundTrip o TrackedMutation. Non dichiarate: {0}. "
            + "Leggi il repository che scrive l'entità: se fa MapToPersistence + Update() su "
            + "detached senza riportare il token, OGNI scrittura fallirà su Postgres e la suite "
            + "resterà verde, perché InMemory non ha la colonna di sistema xmin.",
            string.Join(", ", undeclared));
    }

    [Fact]
    public void EveryByteaTokenIsAccountedForAsPendingConversion()
    {
        var unaccounted = DiscoverConcurrencyTokens()
            .Where(x => x.TokenType == typeof(byte[]))
            .Select(x => x.EntityName)
            .Where(name => !PendingByteaConversions.Contains(name))
            .ToList();

        unaccounted.Should().BeEmpty(
            "un concurrency token byte[] su colonna bytea non protegge nulla — Postgres non la "
            + "popola mai (#3651). Una nuova entità non può nascere così: usa uint Xmin sulla "
            + "colonna di sistema, come le altre. Non contabilizzate: {0}",
            string.Join(", ", unaccounted));
    }

    [Fact]
    public void TheInventoryDoesNotOutliveTheModel()
    {
        var discovered = DiscoverConcurrencyTokens();
        var xminEntities = discovered.Where(x => x.TokenType == typeof(uint)).Select(x => x.EntityName).ToHashSet(StringComparer.Ordinal);
        var byteaEntities = discovered.Where(x => x.TokenType == typeof(byte[])).Select(x => x.EntityName).ToHashSet(StringComparer.Ordinal);

        var staleWritePaths = DeclaredWritePaths.Keys.Where(name => !xminEntities.Contains(name)).ToList();
        var stalePending = PendingByteaConversions.Where(name => !byteaEntities.Contains(name)).ToList();

        staleWritePaths.Should().BeEmpty(
            "una voce di DeclaredWritePaths che non corrisponde più a un'entità con token xmin è "
            + "rumore: o il token è stato rimosso (togli la riga), o l'entità è stata rinominata "
            + "(aggiorna la riga). Voci stale: {0}",
            string.Join(", ", staleWritePaths));

        stalePending.Should().BeEmpty(
            "una voce di PendingByteaConversions che non ha più un token bytea è stata convertita "
            + "da #3651: spostala in DeclaredWritePaths dichiarando come il write-path preserva il "
            + "token appena acceso — è il passaggio in cui il guasto di #3651 può diventare quello "
            + "di #3688. Voci stale: {0}",
            string.Join(", ", stalePending));
    }

    [Fact]
    public void NoConcurrencyTokenUsesAnUnexpectedClrType()
    {
        var unexpected = DiscoverConcurrencyTokens()
            .Where(x => x.TokenType != typeof(uint) && x.TokenType != typeof(byte[]))
            .Select(x => $"{x.EntityName} ({x.TokenType.Name})")
            .ToList();

        unexpected.Should().BeEmpty(
            "questo inventario conosce due forme di token: uint (xmin, quella corretta) e byte[] "
            + "(bytea, inerte, in via di conversione da #3651). Una terza forma non sarebbe "
            + "classificata da nessuno dei due test sopra e passerebbe inosservata: {0}",
            string.Join(", ", unexpected));
    }
}
