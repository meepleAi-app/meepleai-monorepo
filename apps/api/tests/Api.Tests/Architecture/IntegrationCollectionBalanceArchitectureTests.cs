using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #3742 — tiene separato l'asse del parallelismo da quello dello sharding.
///
/// <para>
/// xUnit parallelizza FRA collection e serializza DENTRO una collection. Le quattro
/// <c>Integration-Group{A..D}</c> erano state assegnate per bounded context, e i filtri di shard di
/// <c>dev-async.yml</c> tagliano sugli stessi nomi: i due assi coincidevano. Lo shard Games
/// (SharedGameCatalog + GameManagement + Administration) finiva così tutto dentro GroupC e GroupD,
/// con GroupA e GroupB vuote — 2 thread su 4 per l'intera run, e una catena seriale di 113 classi
/// che sfondava il TestSessionTimeout.
/// </para>
/// <para>
/// Il commento della fixture dichiarava «~39-42 classi per gruppo» mentre GroupC era arrivata a 157.
/// La deriva è passata inosservata per mesi perché niente la misurava: un commento non è un guard.
/// </para>
/// <para>
/// Sull'alternativa sanzionata quando due classi devono davvero escludersi a vicenda: la regola
/// dell'hash non ammette eccezioni, quindi co-locarle nello stesso gruppo per farle serializzare è
/// vietato dal guard (<see cref="EveryIntegrationClass_IsInTheGroupItsHashDictates"/>). Il caso
/// concreto è <c>AdminProviderEndpointsIntegrationTests</c> e <c>GameNightTokenRateLimitTests</c>:
/// entrambe mutano <c>DISABLE_RATE_LIMITING</c> / <c>RateLimiting__Enabled</c> via
/// <c>Environment.SetEnvironmentVariable</c>, che sono globali di PROCESSO — per la durata del
/// test il rate limiting è attivo per ogni host costruito ovunque nel processo, e il
/// salva-e-ripristina è una lost update se le due classi girano insieme. Non correggerle è fuori
/// scope qui; la via sanzionata per chi deve risolvere un caso simile è un
/// <see cref="SemaphoreSlim"/> statico condiviso attorno alla mutazione, oppure spostare il flag
/// nella configurazione per-host invece che nell'ambiente di processo — non co-locare le classi.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public sealed class IntegrationCollectionBalanceArchitectureTests
{
    private const int GroupCount = 4;

    private static readonly string[] GroupNames =
    {
        "Integration-GroupA",
        "Integration-GroupB",
        "Integration-GroupC",
        "Integration-GroupD",
    };

    /// <summary>
    /// Token dello shard KnowledgeBase. Devono restare allineati al <c>filter_extra</c> della
    /// matrice in <c>.github/workflows/dev-async.yml</c> e <c>.github/workflows/ci.yml</c>.
    /// La duplicazione è deliberata: parsare il YAML legherebbe un test unitario al formato di un
    /// file di CI. Va annotata da entrambi i lati, perché una divergenza fra i due elenchi rende
    /// questo guard cieco proprio sullo shard che è cambiato.
    /// </summary>
    private static readonly string[] KnowledgeBaseTokens =
    {
        "KnowledgeBase", "DocumentProcessing", "Authentication",
    };

    /// <summary>Token dello shard Games. Stessa nota di <see cref="KnowledgeBaseTokens"/>.</summary>
    private static readonly string[] GamesTokens =
    {
        "SharedGameCatalog", "GameManagement", "Administration",
    };

    /// <summary>
    /// Gruppo dedotto dal nome pienamente qualificato: SHA-256(UTF-8(FQN)), primi 4 byte
    /// big-endian, mod 4. La scelta dell'hash non è estetica: <c>string.GetHashCode()</c> in .NET è
    /// randomizzato per processo, quindi darebbe una ripartizione diversa a ogni esecuzione dello
    /// script di riassegnazione e un diff illeggibile a ogni rerun.
    /// </summary>
    public static string GroupFor(string fullyQualifiedName)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(fullyQualifiedName));
        var bucket = BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(0, 4)) % GroupCount;
        return GroupNames[bucket];
    }

    /// <summary>
    /// Le classi annidate sono escluse per scelta, non per svista: <see cref="Type.FullName"/> le
    /// identifica come <c>Outer+Inner</c>, non <c>Namespace.Inner</c>. Lo script Python che applica
    /// la riassegnazione in blocco è testuale — non un parser C# — e non può ricostruire in modo
    /// affidabile la catena dei tipi contenitori (region, classi parziali, annidamento multiplo). Se
    /// questo filtro cambiasse per includerle, lo script dovrebbe cambiare di pari passo: altrimenti
    /// una classe annidata potrebbe finire riassegnata su un FQN sintetico che nessuna riflessione
    /// produrrebbe mai — la stessa deriva silenziosa che questa regola chiude sull'asse shard/gruppo.
    /// </summary>
    private static IReadOnlyList<(string Fqn, string Group)> IntegrationClasses() =>
        typeof(IntegrationCollectionBalanceArchitectureTests).Assembly
            .GetTypes()
            .Where(t => !t.IsNested)
            .Select(t => new
            {
                Fqn = t.FullName ?? string.Empty,
                Group = t.GetCustomAttribute<CollectionAttribute>()?.Name,
            })
            .Where(x => x.Group is not null
                        && x.Group.StartsWith("Integration-Group", StringComparison.Ordinal))
            .Select(x => (x.Fqn, Group: x.Group!))
            .OrderBy(x => x.Fqn, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Recupera le classi di integrazione e garantisce che la riflessione le abbia trovate. Se
    /// <see cref="IntegrationClasses"/> tornasse vuota — riflessione rotta, forma dell'attributo
    /// cambiata, scope dell'assembly diverso — ogni fact che filtra quella sequenza passerebbe a
    /// vuoto: zero segnale proprio quando il meccanismo su cui si regge si è rotto. Usata da tutti
    /// e tre i fact, non da uno soltanto: il guard è sul recupero, non su un singolo assert locale.
    /// </summary>
    private static IReadOnlyList<(string Fqn, string Group)> IntegrationClassesOrFail()
    {
        var classes = IntegrationClasses();
        classes.Should().HaveCountGreaterThan(
            100,
            "se la riflessione non trova le classi di integrazione, ogni fact di questa classe " +
            "filtra una sequenza vuota e passa a vuoto: sarebbe verde proprio quando il meccanismo " +
            "su cui si regge si è rotto");
        return classes;
    }

    private static bool InKnowledgeBaseShard(string fqn) =>
        KnowledgeBaseTokens.Any(t => fqn.Contains(t, StringComparison.Ordinal));

    private static bool InGamesShard(string fqn) =>
        GamesTokens.Any(t => fqn.Contains(t, StringComparison.Ordinal));

    // Lo shard Core è il complemento: nel workflow è una catena di sei `FullyQualifiedName!~`.
    private static bool InCoreShard(string fqn) =>
        !InKnowledgeBaseShard(fqn) && !InGamesShard(fqn);

    [Fact]
    public void EveryShard_SeesAllFourCollectionGroups()
    {
        var classes = IntegrationClassesOrFail();
        var shards = new (string Name, Func<string, bool> Predicate)[]
        {
            ("KnowledgeBase", InKnowledgeBaseShard),
            ("Games", InGamesShard),
            ("Core", InCoreShard),
        };

        var starved = new List<string>();
        foreach (var (name, predicate) in shards)
        {
            var groups = classes
                .Where(c => predicate(c.Fqn))
                .Select(c => c.Group)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (groups.Count < GroupCount)
            {
                var missing = GroupNames.Except(groups, StringComparer.Ordinal);
                starved.Add($"{name} (mancano: {string.Join(", ", missing)})");
            }
        }

        starved.Should().BeEmpty(
            "uno shard che non contiene tutti e {0} i gruppi gira con meno thread di quelli " +
            "concessi, perché xUnit parallelizza solo FRA collection. È il difetto del 2026-08-17: " +
            "lo shard Games aveva GroupA e GroupB vuote, usava 2 thread su 4 e sfondava il " +
            "TestSessionTimeout. Shard affamati: {1}",
            GroupCount,
            string.Join(" · ", starved));
    }

    [Fact]
    public void EveryIntegrationClass_IsInTheGroupItsHashDictates()
    {
        var misplaced = IntegrationClassesOrFail()
            .Where(c => !string.Equals(c.Group, GroupFor(c.Fqn), StringComparison.Ordinal))
            .Select(c => $"{c.Fqn}: sta in {c.Group}, deve stare in {GroupFor(c.Fqn)}")
            .ToList();

        misplaced.Should().BeEmpty(
            "il gruppo si deriva da SHA-256(FQN) mod {0}, non dal bounded context: assegnarlo per " +
            "dominio riallinea l'asse del parallelismo a quello dello sharding, che è esattamente " +
            "ciò che ha rotto il gate. {1} classi fuori posto; prime 10: {2}",
            GroupCount,
            misplaced.Count,
            string.Join(" · ", misplaced.Take(10)));
    }

    /// <summary>
    /// La banda è volutamente larga (18-32%, non 20-30%): l'aggregato per gruppo non è una
    /// variabile libera, è funzione di <see cref="GroupFor"/> e dell'insieme delle classi. A
    /// 20-30% GroupA stava già al 29,46% (109/370) — mezzo punto sotto il tetto — e sarebbero
    /// bastate quattro classi nuove finite in A per sforare una PR che aveva solo aggiunto due
    /// test. Il rimedio proporzionato quando questo fallisce NON è spostare a mano un singolo
    /// file: <see cref="EveryIntegrationClass_IsInTheGroupItsHashDictates"/> lo impedisce, perché
    /// il gruppo si deriva dall'hash, non si assegna. Un fallimento qui significa rivedere la
    /// funzione di ripartizione stessa (<see cref="GroupFor"/> in C# *e* la sua copia in
    /// <c>infra/scripts/assign-integration-collections.py</c>) e rifare lo sweep di ~370 file — la
    /// banda esiste per rilevare un collasso vero della ripartizione, non per essere inseguita a
    /// ogni aggiunta di test.
    /// </summary>
    [Fact]
    public void Groups_HoldBetween18And32PercentOfTheClasses()
    {
        var classes = IntegrationClassesOrFail();

        var offBalance = GroupNames
            .Select(g => new
            {
                Group = g,
                Share = (double)classes.Count(c => string.Equals(c.Group, g, StringComparison.Ordinal))
                        / classes.Count,
            })
            .Where(x => x.Share < 0.18 || x.Share > 0.32)
            .Select(x => $"{x.Group}: {x.Share:P1}")
            .ToList();

        offBalance.Should().BeEmpty(
            "con {0} gruppi la quota attesa è il 25%; fuori dalla banda 18-32% il gruppo più " +
            "grosso torna a essere il collo di bottiglia seriale dello shard che lo contiene. " +
            "Spostare un singolo file NON è una via d'uscita — il fact sull'hash lo impedisce — " +
            "quindi un fallimento qui va rivisto come un collasso della funzione di ripartizione " +
            "(GroupFor, C# e Python), non inseguito file per file. Sbilanciati: {1}",
            GroupCount,
            string.Join(" · ", offBalance));
    }

    /// <summary>
    /// <see cref="EveryShard_SeesAllFourCollectionGroups"/> passerebbe anche con A=1, B=1, C=1,
    /// D=157: verifica solo che i 4 gruppi siano non vuoti, non quanto pesano. Il wall-clock di
    /// uno shard è governato dalla catena seriale PIÙ LUNGA fra i suoi gruppi — xUnit parallelizza
    /// fra collection ma serializza dentro una collection — quindi è il gruppo più numeroso, non
    /// la loro presenza, a decidere se lo shard sfora il TestSessionTimeout.
    /// </summary>
    [Fact]
    public void EveryShard_LongestChainStaysWithinOneAndAHalfTimesTheIdealShare()
    {
        var classes = IntegrationClassesOrFail();
        var shards = new (string Name, Func<string, bool> Predicate)[]
        {
            ("KnowledgeBase", InKnowledgeBaseShard),
            ("Games", InGamesShard),
            ("Core", InCoreShard),
        };

        var overloaded = new List<string>();
        foreach (var (name, predicate) in shards)
        {
            var shardClasses = classes.Where(c => predicate(c.Fqn)).ToList();
            if (shardClasses.Count == 0)
            {
                // EveryShard_SeesAllFourCollectionGroups già segnala uno shard vuoto: qui
                // dividere per zero non aggiungerebbe segnale, solo un'eccezione fuori posto.
                continue;
            }

            var idealShare = shardClasses.Count / (double)GroupCount;
            var longestChainSize = shardClasses
                .GroupBy(c => c.Group, StringComparer.Ordinal)
                .Max(g => g.Count());
            var ratio = longestChainSize / idealShare;

            if (ratio > 1.5)
            {
                overloaded.Add(
                    $"{name}: catena più lunga {longestChainSize}/{shardClasses.Count} " +
                    $"({ratio:F2}x l'ideale del 25%)");
            }
        }

        overloaded.Should().BeEmpty(
            "la presenza dei {0} gruppi non basta: xUnit serializza DENTRO una collection, quindi " +
            "il wall-clock di uno shard è governato dalla sua catena seriale più lunga, non dal " +
            "numero di gruppi non vuoti. Uno shard con A=1,B=1,C=1,D=157 passerebbe " +
            "EveryShard_SeesAllFourCollectionGroups pur girando in pratica su un thread solo. La " +
            "soglia è 1,5x la quota ideale del 25% (cioè 37,5%): oggi Core è il caso più vicino al " +
            "limite, a 1,39x. Fuori soglia: {1}",
            GroupCount,
            string.Join(" · ", overloaded));
    }
}
