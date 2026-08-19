# Dev Async Integration Gate — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** far sì che il gate `Backend Integration` di `dev-async` produca, su `main-dev`, un esito che misuri la selezione **intera** e che non venga cancellato prima di arrivarci.

**Architecture:** tre PR sequenziali. PR1 ripara il segnale e costruisce lo strumento di misura (`.trx` come artifact, guard sul troncamento), così PR2 e PR3 sono verificabili invece che plausibili. PR2 rende l'appartenenza alle collection xUnit ortogonale ai filtri di shard, che oggi coincidono e collassano il parallelismo. PR3 sostituisce la ricostruzione dello schema per classe con un clone di database-modello.

**Tech Stack:** GitHub Actions (bash), .NET 9, xUnit v3.2.1, FluentAssertions, Npgsql, PostgreSQL 16 (pgvector), Python 3 (script one-shot di riassegnazione).

**Spec:** [`docs/for-developers/specs/2026-08-17-dev-async-integration-gate-design.md`](../../for-developers/specs/2026-08-17-dev-async-integration-gate-design.md)

## Global Constraints

- **Non modificare** `TestSessionTimeout` (resta `4500000` in `apps/api/tests/Api.Tests/integration.runsettings`), `maxParallelThreads` (resta `4` in `apps/api/tests/Api.Tests/xunit.runner.json`), né la matrice `shard` di `dev-async.yml` e `ci.yml`. Se dopo PR3 uno shard resta oltre budget, è una decisione separata da prendere con numeri veri.
- **Il colore del job non è un metro.** Il gate è rosso per progetto finché [#3633](https://github.com/meepleAi-app/meepleai-monorepo/issues/3633) è aperta. Il confronto valido è il `Failed:` **per shard** contro la baseline: **Core 9 · KnowledgeBase 4 · Games 3**. Un aumento va attribuito a un test nominato, mai accettato in blocco.
- **Baseline di riferimento** (run [`32026945696`](https://github.com/meepleAi-app/meepleai-monorepo/actions/runs/32026945696), sha `a3d9df5c`):

  | shard | Failed | Passed | Total | sessione |
  |---|---|---|---|---|
  | KnowledgeBase | 4 | 900 | 926 | 54m26s (completo) |
  | Games | 3 | 731 | 743 | 74m (**troncato**) |
  | Core | 9 | 771 | 825 | 74m (**troncato**) |

- **Branch**: partire SEMPRE da `main-dev` pulito. Prima di `git checkout -b`: `git branch --show-current` deve stampare `main-dev`, `git status` deve essere pulito, `git pull --ff-only` deve riuscire. PR verso `main-dev`.
- ⚠️ **Al 2026-08-17 il checkout principale è occupato** da `feature/issue-3737-e5-query-prefix` con 21 file modificati di un'altra sessione. Non fare stash, non cambiare branch, non committare finché non è libero — oppure lavorare in un worktree dedicato (`superpowers:using-git-worktrees`).
- **Commit**: `feat|fix|docs|refactor|test|chore(scope): descrizione`. Il subject deve stare in **72 caratteri**: l'hook `commit-msg` rifiuta oltre, e se il commit gira in background il fallimento resta invisibile — verificare sempre con `git log --oneline -1`.
- **Mai `// TODO` nel C#**: SonarAnalyzer S1135 fa fallire la build. Usare `// Follow-up:`.
- **Non lanciare `dotnet format` senza `--include`** sui file toccati: senza filtro applica i fix degli analyzer e S1144 rimuove i costruttori usati solo via reflection/DI.
- La spec vieta di dedurre un esito dai log a `--verbosity minimal`: i test **passati** non compaiono, quindi l'assenza di un test dal log non prova che non sia girato.

---

## File Structure

| File | Responsabilità | Task |
|---|---|---|
| `.github/workflows/dev-async.yml` | trigger, concurrency, matrice, esecuzione e guard del gate | 1, 2 |
| `apps/api/tests/Api.Tests/Architecture/IntegrationCollectionBalanceArchitectureTests.cs` | **nuovo** — impone la regola di assegnazione delle collection e l'invariante «ogni shard vede 4 gruppi» | 3 |
| `infra/scripts/assign-integration-collections.py` | **nuovo** — applica una volta la riassegnazione; l'autorità resta il test C# | 4 |
| ~370 file `apps/api/tests/Api.Tests/**/*.cs` | solo la stringa dentro `[Collection("Integration-Group?")]` | 4 |
| `apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs` | costruzione del modello, clone dei database isolati, commento della regola | 4, 5, 6 |
| `apps/api/tests/Api.Tests/Infrastructure/IsolatedDatabaseTemplateTests.cs` | **nuovo** — pinna il comportamento del clone e la sigillatura del modello | 5 |

---

# PR1 — riavere un segnale

Branch: `feature/issue-XXXX-dev-async-signal` (sostituire `XXXX` col numero della issue creata per questo lavoro; #3633 resta al triage dei fallimenti).

## Task 1: la run non viene più falciata

**Files:**
- Modify: `.github/workflows/dev-async.yml:25-27`

**Interfaces:**
- Consumes: niente.
- Produces: il numero di issue che tutti i branch e i titoli di PR successivi usano al posto di `XXXX`.

- [ ] **Step 0: Aprire la issue e fissare il numero**

#3633 resta al triage dei 16 fallimenti: sovrapporre i due lavori riporterebbe la confusione che quella issue si è presa la briga di evitare («Due problemi distinti, da non confondere»).

```bash
gh issue create --title "ci: il gate di integrazione di dev-async non misura la selezione intera" --body "$(cat <<'EOF'
Residuo di #3633 (problema 2) più un difetto nuovo sulla concurrency.

Due cause distinte, entrambe misurate:

1. **Gli assi coincidono.** Le collection `Integration-Group{A..D}` sono assegnate per bounded
   context, e i filtri di shard di `dev-async.yml` tagliano sugli stessi nomi. xUnit parallelizza
   FRA collection: lo shard Games ha GroupA e GroupB vuote, gira su 2 thread su 4 e ha una catena
   seriale di 113 classi. È lo shard che sfora per primo il `TestSessionTimeout`.
2. **Ogni classe ricostruisce lo schema.** 362 classi × (`CREATE DATABASE` + 18 migration, 250
   tabelle e 727 indici) = 5,1-7,4 s misurati ciascuna, contro i 135-159 ms di un
   `CREATE DATABASE ... TEMPLATE`.

Più un terzo problema indipendente: `cancel-in-progress: true` su un gate da ~83 minuti, su un
branch che riceve merge ogni 40-70. Il 2026-08-17, nove run su quindici erano `cancelled`.

Design: `docs/for-developers/specs/2026-08-17-dev-async-integration-gate-design.md`
Piano: `docs/superpowers/plans/2026-08-17-dev-async-integration-gate.md`
EOF
)"
```

Annotare il numero restituito e sostituirlo a `XXXX` in **tutti** i nomi di branch e titoli di PR di questo piano.

- [ ] **Step 1: Sostituire il blocco `concurrency`**

Il blocco attuale è:

```yaml
concurrency:
  group: dev-async-${{ github.ref }}
  cancel-in-progress: true
```

Sostituirlo con:

```yaml
# `cancel-in-progress: false` NON accoda una fila. A gruppo occupato GitHub mette la nuova run in
# pending e cancella la pending PRECEDENTE: la run in volo arriva quindi sempre in fondo, e quando
# lo slot si libera parte l'ultimo sha. È un debounce, non un backlog.
#
# Con `true`, un gate da ~83 minuti su un branch che riceve merge ogni 40-70 non aveva finestra: il
# 2026-08-17 nove run su quindici erano `cancelled`, e lo shard Games — l'ultimo a chiudere — non
# arrivava quasi mai in fondo. Un `cancelled` si legge come «annullato», non come «fallito»:
# `dev-auto-revert` lo classifica `green-pending` e non fa nulla, quindi su main-dev non esisteva
# alcun esito, né verde né rosso. È lo stesso difetto di #3629 su un altro asse.
concurrency:
  group: dev-async-${{ github.ref }}
  cancel-in-progress: false
```

- [ ] **Step 2: Verificare che il YAML resti valido**

Run:

```bash
python -c "import yaml,sys; d=yaml.safe_load(open('.github/workflows/dev-async.yml',encoding='utf-8')); print(d['concurrency'])"
```

Expected: `{'group': 'dev-async-${{ github.ref }}', 'cancel-in-progress': False}`

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/dev-async.yml
git commit -m "fix(ci): dev-async non cancella piu la run in volo"
git log --oneline -1
```

- [ ] **Step 4: Verifica sul campo (dopo il merge)**

Run:

```bash
gh run list --workflow=dev-async.yml --limit 10 \
  --json databaseId,headSha,conclusion,createdAt,updatedAt \
  --jq '.[] | "\(.databaseId) \(.headSha[0:8]) \(.conclusion) \(.createdAt) -> \(.updatedAt)"'
```

Expected: nessuna run `cancelled` fra quelle avviate da `push` dopo il merge. Se ne compare una, controllare che non sia una cancellazione manuale (`gh run view <id> --json event,conclusion`).

---

## Task 2: `.trx` scaricabile, troncamento nominato, riepilogo per shard

**Files:**
- Modify: `.github/workflows/dev-async.yml` — step `Integration tests` (righe 180-217), più due step nuovi in coda al job.

**Interfaces:**
- Consumes: niente.
- Produces: per ogni shard un artifact `integration-results-<shard>` contenente `integration-test-results.trx` (durate per-test) e `integration-<shard>.log`. **Task 4 e Task 6 misurano su questo artifact**: senza, le loro verifiche non sono eseguibili.

- [ ] **Step 1: Incanalare l'output dei test in un log preservando l'exit code**

Nello step `Integration tests`, sostituire il blocco `run:` finale. Il comando `dotnet test` e i suoi argomenti **non cambiano**: si aggiungono solo `set -o pipefail` e il `tee`.

```yaml
        run: |
          # `set -o pipefail` è obbligatorio: senza, l'exit code che arriva a GitHub è quello di
          # `tee` (sempre 0) e un `Test Run Aborted.` passerebbe per successo.
          set -o pipefail
          # Target esplicito del .csproj (PR #1505): `dotnet test` sulla solution invocherebbe anche
          # Api.Analyzers.Tests.dll, che non ha test Category=Integration, e vstest tratta il
          # no-match su un assembly non vuoto come exit 1.
          #
          # Stesse esclusioni per nome di ci.yml, così i due gate misurano lo stesso insieme: una
          # divergenza qui riporterebbe il difetto di #3622 (test che nessun gate esegue) su un
          # altro asse.
          #
          # NESSUN override di TestSessionTimeout: valgono i 75 min del runsettings condiviso (#3634).
          dotnet test tests/Api.Tests/Api.Tests.csproj \
            --filter "Category=Integration&FullyQualifiedName!~UploadPdfMidPhaseCancellationTests&FullyQualifiedName!~FrontendSdk&FullyQualifiedName!~ArbitroAgent${{ matrix.shard.filter_extra }}" \
            --settings tests/Api.Tests/integration.runsettings \
            --no-build \
            --configuration Release \
            --logger "console;verbosity=minimal" \
            --blame-hang-timeout 5min \
            2>&1 | tee "integration-${{ matrix.shard.name }}.log"
```

- [ ] **Step 2: Aggiungere il guard, in coda al job**

Aggiungere dopo lo step `Integration tests`:

```yaml
      # #3632/#3633 — il verde va misurato, non dichiarato, e il troncamento va NOMINATO.
      #
      # `Aborting test run: test run timeout` esce già 134 e fa fallire lo step, quindi questo guard
      # non introduce un rosso nuovo: rende distinguibile «la selezione non è stata misurata per
      # intero» da «alcuni test sono falliti». Sono due fatti diversi e finora erano lo stesso colore.
      #
      # `!cancelled()` e non `always()`: su una cancellazione non c'è niente da misurare.
      - name: 'Guard: la selezione e stata misurata per intero (#3633)'
        if: ${{ !cancelled() }}
        working-directory: apps/api
        env:
          SHARD: ${{ matrix.shard.name }}
        run: |
          set -uo pipefail
          status=0
          log="integration-${SHARD}.log"

          if [ ! -f "$log" ]; then
            echo "::error::Shard ${SHARD}: log assente, lo step dei test non e partito."
            exit 1
          fi

          mapfile -t trx_files < <(find . -name 'integration-test-results.trx' -print)
          if [ ${#trx_files[@]} -eq 0 ]; then
            echo "::error::Shard ${SHARD}: nessun .trx, nessun test eseguito."
            status=1
          fi

          executed=0
          for f in "${trx_files[@]}"; do
            n=$(sed -n 's/.*<Counters[^>]* executed="\([0-9]\{1,\}\)".*/\1/p' "$f" | head -1)
            if ! [[ "${n:-}" =~ ^[0-9]+$ ]]; then
              echo "::error::$f: counter 'executed' illeggibile (trx malformato?)."
              status=1
              continue
            fi
            echo "  $f -> executed=$n"
            executed=$((executed + n))
          done

          # Soglia bassa e deliberata: distingue «e girato» da «zero», non pinna un numero.
          if [ "$executed" -lt 100 ]; then
            echo "::error::Shard ${SHARD}: solo $executed test eseguiti, sarebbe stato verde senza eseguire nulla (#3632)."
            status=1
          fi

          # `|| true`: sotto `set -e` + `pipefail`, un grep senza match esce 1 e l'assegnazione
          # ucciderebbe lo script prima di scrivere il summary e prima del controllo sul
          # troncamento. Il fallback `${summary:-...}` qui sotto sarebbe altrimenti irraggiungibile:
          # serve per i log senza trailer Passed!/Failed! (test host crashato, dotnet test mai
          # partito, dump di --blame-hang), non per il troncamento — una run troncata stampa
          # comunque il trailer dopo `Test Run Aborted.`.
          summary=$(grep -aE '^(Passed|Failed)!  -' "$log" | tail -1 || true)
          {
            echo "### Backend Integration — ${SHARD}"
            echo
            echo '```'
            echo "${summary:-(riepilogo non trovato nel log)}"
            echo '```'
            echo
            echo "Baseline attesa per shard: Core 9 · KnowledgeBase 4 · Games 3 (#3633)."
          } >> "$GITHUB_STEP_SUMMARY"

          if grep -qa 'Aborting test run: test run timeout' "$log"; then
            echo "::error::Shard ${SHARD} TRONCATO: il TestSessionTimeout e scattato. La selezione NON e stata misurata per intero: i conteggi qui sopra sono parziali e non confrontabili con una baseline completa (#3633)."
            {
              echo
              echo "> ⚠️ **Shard troncato** — sessione abortita sul TestSessionTimeout. Conteggi parziali."
            } >> "$GITHUB_STEP_SUMMARY"
            status=1
          fi

          exit $status
```

- [ ] **Step 3: Aggiungere l'upload dell'artifact, come ultimo step del job**

```yaml
      # Le durate per-test del .trx sono l'unico modo di attribuire un guadagno o una regressione a
      # una causa. Il runsettings il .trx lo produce già (integration-test-results.trx): finora
      # nessuno lo raccoglieva, quindi ogni ipotesi sulle prestazioni della suite restava tale.
      - name: Upload test results
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@v7
        with:
          name: integration-results-${{ matrix.shard.name }}
          path: |
            apps/api/**/integration-test-results.trx
            apps/api/integration-*.log
          retention-days: 14
          if-no-files-found: warn
```

- [ ] **Step 4: Verificare che il YAML resti valido e che gli step siano nell'ordine giusto**

Run:

```bash
python -c "
import yaml
d=yaml.safe_load(open('.github/workflows/dev-async.yml',encoding='utf-8'))
for s in d['jobs']['backend-integration']['steps']:
    print(s.get('name') or s.get('uses'))
"
```

Expected: gli ultimi tre nomi, in quest'ordine — `Integration tests`, `Guard: la selezione e stata misurata per intero (#3633)`, `Upload test results`.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/dev-async.yml
git commit -m "ci(dev-async): pubblica il trx e nomina il troncamento"
git log --oneline -1
```

- [ ] **Step 6: Verifica sul campo — dispatch e lettura dell'artifact**

```bash
gh workflow run dev-async.yml --ref feature/issue-XXXX-dev-async-signal
sleep 60 && gh run list --workflow=dev-async.yml --limit 1 --json databaseId --jq '.[0].databaseId'
```

A run finita, con `<id>` quello stampato sopra:

```bash
gh run download <id> --dir /tmp/devasync
ls /tmp/devasync/*/
```

Expected: tre cartelle `integration-results-{KnowledgeBase,Games,Core}`, ognuna con un `.trx` e un `.log`. Nella pagina della run, gli shard troncati devono mostrare l'annotazione `Shard ... TRONCATO` — e a questo punto del piano **Games e Core devono ancora essere troncati**: è PR2 che li fa rientrare, e vederli rossi qui conferma che il guard funziona.

- [ ] **Step 7: PR verso `main-dev`**

```bash
git push -u origin feature/issue-XXXX-dev-async-signal
gh pr create --base main-dev \
  --title "ci(dev-async): riavere un esito sul gate di integrazione (#XXXX)" \
  --body "$(cat <<'EOF'
Tre cambi, tutti dentro `dev-async.yml`.

**`cancel-in-progress: false`** — GitHub non accoda una fila: a gruppo occupato mette la nuova run
in pending e cancella la pending *precedente*. La run in volo arriva quindi sempre in fondo, e
quando lo slot si libera parte l'ultimo sha. Con `true`, un gate da ~83 minuti su un branch che
riceve merge ogni 40-70 non aveva finestra: il 2026-08-17, nove run su quindici erano `cancelled`,
e un `cancelled` `dev-auto-revert` lo classifica `green-pending` — cioè nessun esito, né verde né
rosso.

**`.trx` come artifact** — il runsettings lo produce già, nessuno lo raccoglieva. Senza le durate
per-test, ogni ipotesi sulle prestazioni della suite resta tale. Le due PR successive si misurano
su questo.

**Guard sul troncamento** — `Aborting test run` esce già 134, quindi qui non nasce un rosso nuovo:
diventa distinguibile «la selezione non è stata misurata per intero» da «alcuni test sono falliti».
Erano due fatti diversi con lo stesso colore.

**Atteso in questa PR**: Games e Core ancora troncati, ora però *nominati*. È PR2 che li fa
rientrare.
EOF
)"
```

---

# PR2 — collection ortogonali all'asse dello sharding

Branch: `feature/issue-XXXX-integration-collection-balance`, creato da `main-dev` **dopo** il merge di PR1.

## Task 3: il guard test, che deve fallire sulla realtà attuale

**Files:**
- Create: `apps/api/tests/Api.Tests/Architecture/IntegrationCollectionBalanceArchitectureTests.cs`

**Interfaces:**
- Consumes: niente.
- Produces: `public static string GroupFor(string fullyQualifiedName)` — la regola di assegnazione. Task 4 la replica in Python e questo test è l'autorità che verifica la replica.

Il modello da seguire è `apps/api/tests/Api.Tests/Architecture/TestCategoryGateArchitectureTests.cs`: stesso namespace, stesso `[Trait("Category", "Unit")]`, stessa pratica di duplicare i filtri del workflow come costanti annotate.

⚠️ La classe **non deve** avere un attributo `[Collection(...)]`: è un test unitario, non tocca Docker. Un test unitario dentro una collection di integrazione fallisce sul gate `Backend Fast`, che gira senza Docker.

- [ ] **Step 1: Scrivere il test**

```csharp
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
/// Issue #3633 — tiene separato l'asse del parallelismo da quello dello sharding.
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
        var classes = IntegrationClasses();
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
        var misplaced = IntegrationClasses()
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

    [Fact]
    public void Groups_HoldBetween20And30PercentOfTheClasses()
    {
        var classes = IntegrationClasses();
        classes.Should().HaveCountGreaterThan(
            100,
            "se la riflessione non trova le classi di integrazione, gli altri due test di questa " +
            "classe passano a vuoto");

        var offBalance = GroupNames
            .Select(g => new
            {
                Group = g,
                Share = (double)classes.Count(c => string.Equals(c.Group, g, StringComparison.Ordinal))
                        / classes.Count,
            })
            .Where(x => x.Share < 0.20 || x.Share > 0.30)
            .Select(x => $"{x.Group}: {x.Share:P1}")
            .ToList();

        offBalance.Should().BeEmpty(
            "con {0} gruppi la quota attesa è il 25%; fuori dalla banda 20-30% il gruppo più " +
            "grosso torna a essere il collo di bottiglia seriale dello shard che lo contiene. Se " +
            "questo fallisce dopo un'aggiunta massiccia di test, va rivista la funzione di " +
            "ripartizione, non il singolo file. Sbilanciati: {1}",
            GroupCount,
            string.Join(" · ", offBalance));
    }
}
```

- [ ] **Step 2: Eseguire il test e verificare che fallisca sulla realtà attuale**

Run:

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~IntegrationCollectionBalanceArchitectureTests" \
  --logger "console;verbosity=normal"
```

Expected: **2 falliti su 3**.
- `EveryShard_SeesAllFourCollectionGroups` → FAIL, `Games (mancano: Integration-GroupA, Integration-GroupB)`.
- `EveryIntegrationClass_IsInTheGroupItsHashDictates` → FAIL, con un conteggio di classi fuori posto nell'ordine delle centinaia.
- `Groups_HoldBetween20And30PercentOfTheClasses` → FAIL (oggi la ripartizione è 74/42/157/97 su 370, cioè 20,0% / 11,4% / 42,4% / 26,2%: fuori banda su GroupB e GroupC).

Se invece **compila male** su `GetCustomAttribute<CollectionAttribute>()?.Name`, l'API di xUnit v3 differisce: leggere l'argomento del costruttore via `CustomAttributeData` — `t.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "CollectionAttribute")?.ConstructorArguments[0].Value as string` — e proseguire.

- [ ] **Step 3: Commit del test rosso**

```bash
git add apps/api/tests/Api.Tests/Architecture/IntegrationCollectionBalanceArchitectureTests.cs
git commit -m "test(ci): pinna la regola di assegnazione delle collection"
git log --oneline -1
```

---

## Task 4: applicare la riassegnazione

**Files:**
- Create: `infra/scripts/assign-integration-collections.py`
- Modify: ~370 file sotto `apps/api/tests/Api.Tests/` (solo la stringa dentro `[Collection(...)]`)
- Modify: `apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs:1202-1215` (commento)

**Interfaces:**
- Consumes: `IntegrationCollectionBalanceArchitectureTests.GroupFor` (Task 3) — lo script ne è la replica in Python, il test C# resta l'autorità.
- Produces: un'assegnazione delle collection su cui Task 5 e Task 6 si appoggiano (una classe di test nuova va nel gruppo che il suo hash impone).

- [ ] **Step 1: Scrivere lo script**

```python
#!/usr/bin/env python3
"""Riassegna [Collection("Integration-Group{A..D}")] per hash dell'FQN.

Issue #3633. Le collection erano assegnate per bounded context, cioè sullo stesso asse dei filtri di
shard di dev-async.yml: ogni shard finiva per contenere solo un paio di gruppi e girava con meno
thread di quelli concessi. Qui l'assegnazione diventa ortogonale al dominio.

Questo script si esegue UNA VOLTA. L'autorità permanente è il test C#
Api.Tests.Architecture.IntegrationCollectionBalanceArchitectureTests, che verifica la stessa regola
a ogni build: se le due implementazioni divergono, il test fallisce — ed è il comportamento voluto.

Uso:
    python infra/scripts/assign-integration-collections.py           # report, non scrive
    python infra/scripts/assign-integration-collections.py --apply   # riscrive i file
"""

import argparse
import collections
import hashlib
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2] / "apps" / "api" / "tests" / "Api.Tests"
GROUPS = [
    "Integration-GroupA",
    "Integration-GroupB",
    "Integration-GroupC",
    "Integration-GroupD",
]

ATTR = re.compile(r'\[Collection\("(Integration-Group[A-D])"\)\]')
NAMESPACE = re.compile(r"^\s*namespace\s+([\w\.]+)", re.M)
CLASS = re.compile(
    r"\b(?:public\s+|internal\s+|sealed\s+|abstract\s+|partial\s+|static\s+)*class\s+(\w+)"
)
SKIP_PARTS = {"obj", "bin", "TestResults"}


def group_for(fqn: str) -> str:
    """SHA-256(UTF-8(fqn)) -> primi 4 byte big-endian -> mod 4.

    Deve restare identica a IntegrationCollectionBalanceArchitectureTests.GroupFor.
    """
    digest = hashlib.sha256(fqn.encode("utf-8")).digest()
    return GROUPS[int.from_bytes(digest[:4], "big") % len(GROUPS)]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="riscrive i file invece di elencare")
    args = parser.parse_args()

    counts = collections.Counter()
    moved = 0
    scanned = 0

    for path in sorted(ROOT.rglob("*.cs")):
        if SKIP_PARTS & set(path.parts):
            continue

        # newline="" preserva i terminatori di riga esistenti: senza, su Windows l'intero file
        # verrebbe riscritto e il diff sarebbe illeggibile invece che di una riga per classe.
        with open(path, encoding="utf-8", newline="") as handle:
            text = handle.read()

        matches = list(ATTR.finditer(text))
        if not matches:
            continue

        namespace = NAMESPACE.search(text)
        if not namespace:
            print(f"ERRORE: namespace non risolto in {path}", file=sys.stderr)
            return 2

        # Si applica dal fondo verso l'inizio, così gli offset dei match precedenti restano validi.
        edits = []
        for match in matches:
            declaration = CLASS.search(text, match.end())
            if not declaration:
                print(f"ERRORE: classe non risolta dopo {match.group(0)} in {path}", file=sys.stderr)
                return 2

            fqn = f"{namespace.group(1)}.{declaration.group(1)}"
            want = group_for(fqn)
            counts[want] += 1
            scanned += 1
            if match.group(1) != want:
                moved += 1
                edits.append((match.start(), match.end(), f'[Collection("{want}")]'))

        if edits and args.apply:
            for start, end, replacement in reversed(edits):
                text = text[:start] + replacement + text[end:]
            with open(path, "w", encoding="utf-8", newline="") as handle:
                handle.write(text)

    print(f"classi con collection di integrazione: {scanned}")
    for group in GROUPS:
        share = counts[group] / scanned * 100 if scanned else 0
        print(f"  {group}: {counts[group]:>4}  ({share:.1f}%)")
    print(f"da spostare: {moved}")
    print("APPLICATO" if args.apply else "(report soltanto: rilanciare con --apply)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Eseguire in sola lettura e controllare la ripartizione**

Run:

```bash
python infra/scripts/assign-integration-collections.py
```

Expected: `classi con collection di integrazione: 370` e quattro quote **tutte fra il 20% e il 30%**. Se una cade fuori banda, fermarsi: il terzo test di Task 3 fallirebbe comunque, e la risposta è cambiare la regola di ripartizione, non un singolo file.

- [ ] **Step 3: Applicare**

```bash
python infra/scripts/assign-integration-collections.py --apply
git diff --stat | tail -3
```

Expected: circa 280 file modificati, **una sola riga cambiata per classe**. Se `git diff --stat` mostra file con decine di righe cambiate, i terminatori di riga sono stati riscritti: annullare con `git checkout -- apps/api/tests/` e correggere la gestione di `newline=""`.

- [ ] **Step 4: NON lanciare `dotnet format` su questi file**

Il pre-commit salta la formattazione backend sui branch `feature/*`, ed è quello che vogliamo qui: `dotnet format` senza `--include` applica i fix degli analyzer, e S1144 rimuove i costruttori usati solo via reflection/DI. Il diff di questo task cambia una stringa dentro un attributo: non c'è niente da formattare.

- [ ] **Step 5: Aggiornare il commento della fixture**

In `apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs`, sostituire le righe del blocco `<summary>` che elencano i gruppi per dominio:

```csharp
/// Group A: KnowledgeBase + DocumentProcessing (~41 classes)
/// Group B: Authentication + Integration root tests (~42 classes)
/// Group C: SharedGameCatalog + GameManagement + UserLibrary + SessionTracking (~39 classes)
/// Group D: Administration + SystemConfiguration + misc (~42 classes)
```

con:

```csharp
/// Issue #3633: l'appartenenza NON si sceglie per dominio. Assegnare i gruppi per bounded context
/// li allineava ai filtri di shard di dev-async.yml, che tagliano sugli stessi nomi: lo shard Games
/// finiva tutto in GroupC + GroupD, con GroupA e GroupB vuote, e girava su 2 thread invece di 4.
///
/// Il gruppo si deriva da SHA-256(FQN della classe), primi 4 byte big-endian, mod 4. La regola è
/// imposta da Api.Tests.Architecture.IntegrationCollectionBalanceArchitectureTests, che dice anche
/// in quale gruppo va una classe nuova. Per un'assegnazione in blocco:
/// `python infra/scripts/assign-integration-collections.py --apply`.
```

- [ ] **Step 6: Annotare il workflow verso il test**

La spec richiede che la duplicazione dei token sia annotata **da entrambi i lati**: una divergenza fra i due elenchi rende il guard cieco proprio sullo shard che è cambiato, e chi modifica il workflow non ha modo di saperlo.

In `.github/workflows/dev-async.yml`, sopra `shard:` nella matrice, aggiungere in coda al commento esistente:

```yaml
        # #3633: i token qui sotto sono duplicati come costanti in
        # Api.Tests.Architecture.IntegrationCollectionBalanceArchitectureTests, che verifica che
        # ogni shard contenga tutte e 4 le collection xUnit. Se cambi questo elenco, aggiorna anche
        # quello: divergendo, il guard smette di guardare proprio lo shard che hai toccato.
```

Lo stesso commento va aggiunto alla matrice gemella di `.github/workflows/ci.yml`, che usa gli stessi `filter_extra`.

- [ ] **Step 7: Il guard test deve passare**

Run:

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~IntegrationCollectionBalanceArchitectureTests" \
  --logger "console;verbosity=normal"
```

Expected: **3 passati, 0 falliti**.

- [ ] **Step 8: La suite unitaria non deve regredire**

Run:

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "Category=Unit" --logger "console;verbosity=minimal"
```

Expected: stesso conteggio di `Failed:` del merge-base. Prenderlo prima con `git stash && dotnet test … && git stash pop` se non è già noto: un confronto senza riferimento non è un confronto.

- [ ] **Step 9: Commit**

```bash
git add infra/scripts/assign-integration-collections.py apps/api/tests/Api.Tests \
        .github/workflows/dev-async.yml .github/workflows/ci.yml
git commit -m "test(ci): collection ortogonali ai filtri di shard (#3633)"
git log --oneline -1
```

- [ ] **Step 10: Verifica sul campo — è questo il passo che chiude il sintomo**

```bash
git push -u origin feature/issue-XXXX-integration-collection-balance
gh workflow run dev-async.yml --ref feature/issue-XXXX-integration-collection-balance
```

A run finita — l'id si ottiene con `gh run list --workflow=dev-async.yml --branch feature/issue-XXXX-integration-collection-balance --limit 1 --json databaseId --jq '.[0].databaseId'` — per ogni shard:

```bash
gh run download <id> --dir /tmp/devasync-pr2
grep -h 'Aborting test run' /tmp/devasync-pr2/*/integration-*.log || echo "nessuno shard troncato"
grep -h -E '^(Passed|Failed)!  -' /tmp/devasync-pr2/*/integration-*.log
```

Expected:
- **nessuno** `Aborting test run` — è il criterio di accettazione principale;
- `Total:` per shard pari o superiore alla selezione dichiarata nella matrice (910 / 1123 / 929), non ai 926 / 743 / 825 troncati della baseline;
- `Failed:` per shard non superiore a **KnowledgeBase 4 · Games 3 · Core 9**. Un aumento va attribuito a un test nominato — confrontando i `.trx` di PR1 e PR2 — non accettato in blocco: sono i flaky da riallocazione previsti dalla spec.

- [ ] **Step 11: PR verso `main-dev`**

```bash
gh pr create --base main-dev \
  --title "test(ci): collection ortogonali ai filtri di shard (#XXXX)" \
  --body "$(cat <<'EOF'
Le collection `Integration-Group{A..D}` erano assegnate per bounded context, cioè sullo stesso asse
dei `filter_extra` di `dev-async.yml`. xUnit parallelizza FRA collection e serializza DENTRO: lo
shard Games (SharedGameCatalog + GameManagement + Administration) finiva tutto in GroupC + GroupD,
con GroupA e GroupB vuote — 2 thread su 4 e una catena seriale di 113 classi.

Ora il gruppo si deriva da SHA-256(FQN) mod 4, ortogonale al dominio.

Il diff è grosso ma meccanico: una stringa per classe. La sostanza sta in
`IntegrationCollectionBalanceArchitectureTests`, che impone la regola e, soprattutto, l'invariante
che si era rotta — ogni shard deve contenere tutte e 4 le collection. Il commento della fixture
dichiarava «~39-42 classi per gruppo» mentre GroupC era arrivata a 157: la deriva non era visibile
perché niente la misurava.

**Verifica**: nessuno shard troncato, `Total:` per shard pari alla selezione dichiarata,
`Failed:` entro la baseline (KnowledgeBase 4 · Games 3 · Core 9).
EOF
)"
```

---

# PR3 — template database

Branch: `feature/issue-XXXX-integration-template-db`, creato da `main-dev` **dopo** il merge di PR2.

## Task 5: il meccanismo del modello, senza cambiare il comportamento

**Files:**
- Modify: `apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs:710` (firma e corpo di `CreateIsolatedDatabaseAsync`, più i membri nuovi)
- Create: `apps/api/tests/Api.Tests/Infrastructure/IsolatedDatabaseTemplateTests.cs`

**Interfaces:**
- Consumes: `SharedTestcontainersFixture.PostgresConnectionString`, `TestHelpers.CreateDbContextAndMigrateAsync(string)`.
- Produces: `Task<string> CreateIsolatedDatabaseAsync(string databaseName, bool useTemplate = false)`. **In questo task il default resta `false`**: nessun comportamento cambia. Task 6 lo ribalta.

- [ ] **Step 1: Scrivere il test, che deve fallire**

La classe va in `Integration-GroupA`: è il gruppo che l'hash impone per `Api.Tests.Infrastructure.IsolatedDatabaseTemplateTests`, e il guard di Task 3 lo verifica.

```csharp
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Issue #3633 — pinna il clone da database-modello.
///
/// <para>
/// Ognuna delle 362 classi di integrazione creava il proprio database e ci applicava tutte le
/// migration: 250 CreateTable e 727 CreateIndex, misurati in 5,1-7,4 s di solo SQL. Un
/// <c>CREATE DATABASE ... TEMPLATE</c> costa 135-159 ms perché è una copia a livello di file.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection("Integration-GroupA")]
public sealed class IsolatedDatabaseTemplateTests
{
    private readonly SharedTestcontainersFixture _fixture;

    public IsolatedDatabaseTemplateTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task WithTemplate_TheDatabaseArrivesWithTheSchemaAlreadyApplied()
    {
        var databaseName = $"test_tplon_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName, useTemplate: true);

        try
        {
            (await TableExistsAsync(connectionString, "__EFMigrationsHistory"))
                .Should().BeTrue(
                    "clonare il modello copia anche la history di EF. È ciò che rende il " +
                    "MigrateAsync() già presente nelle 362 classi un no-op invece di una " +
                    "riesecuzione: senza, il guadagno sparirebbe e i file di test andrebbero toccati");
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task WithoutTemplate_TheDatabaseArrivesEmpty()
    {
        var databaseName = $"test_tploff_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName, useTemplate: false);

        try
        {
            (await TableExistsAsync(connectionString, "__EFMigrationsHistory"))
                .Should().BeFalse(
                    "l'opt-out serve ai canary delle migration, che devono continuare a esercitare " +
                    "il percorso vero: se il modello arrivasse anche a loro, una migration rotta " +
                    "smetterebbe di essere intercettata");
        }
        finally
        {
            await _fixture.DropIsolatedDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task TheTemplate_RefusesConnections()
    {
        // Costruisce il modello come effetto collaterale.
        var databaseName = $"test_tplseal_{Guid.NewGuid():N}";
        await _fixture.CreateIsolatedDatabaseAsync(databaseName, useTemplate: true);
        await _fixture.DropIsolatedDatabaseAsync(databaseName);

        var builder = new NpgsqlConnectionStringBuilder(_fixture.PostgresConnectionString)
        {
            Database = "meepleai_test_template",
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        var connect = async () => await connection.OpenAsync();

        await connect.Should().ThrowAsync<PostgresException>(
            "una sola connessione aperta sul modello fa fallire con 55006 ogni CREATE DATABASE ... " +
            "TEMPLATE concorrente. Negare le connessioni trasforma la convenzione 'non " +
            "connettersi' in un invariante imposto dal server");
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_name = @t);";
        command.Parameters.AddWithValue("t", table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
```

- [ ] **Step 2: Eseguire il test e verificare che fallisca**

Serve Docker attivo.

Run:

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~IsolatedDatabaseTemplateTests" \
  --logger "console;verbosity=normal"
```

Expected: errore di **compilazione** — `CreateIsolatedDatabaseAsync` non accetta un secondo argomento. È il fallimento giusto per questo passo.

- [ ] **Step 3: Aggiungere il modello alla fixture**

In `SharedTestcontainersFixture.cs`, accanto agli altri campi della classe:

```csharp
    /// <summary>
    /// Database-modello da cui si clonano i database isolati. Issue #3633.
    /// </summary>
    private const string TemplateDatabaseName = "meepleai_test_template";

    // Lo stato è DI PROCESSO, non d'istanza: ICollectionFixture istanzia una fixture per collection
    // (quattro, oggi), quindi un campo d'istanza farebbe costruire il modello quattro volte e le
    // costruzioni concorrenti si ostacolerebbero a vicenda sul CREATE DATABASE.
    private static readonly SemaphoreSlim TemplateGate = new(1, 1);
    private static bool _templateReady;

    private async Task EnsureTemplateDatabaseAsync()
    {
        if (_templateReady)
        {
            return;
        }

        await TemplateGate.WaitAsync();
        try
        {
            if (_templateReady)
            {
                return;
            }

            var adminConnectionString = new NpgsqlConnectionStringBuilder(PostgresConnectionString)
            {
                Database = "postgres",
            }.ConnectionString;

            await using (var admin = new NpgsqlConnection(adminConnectionString))
            {
                await admin.OpenAsync();
                await using var create = admin.CreateCommand();
                create.CommandText =
                    $"DROP DATABASE IF EXISTS \"{TemplateDatabaseName}\" WITH (FORCE); " +
                    $"CREATE DATABASE \"{TemplateDatabaseName}\";";
                await create.ExecuteNonQueryAsync();
            }

            var templateConnectionString = new NpgsqlConnectionStringBuilder(PostgresConnectionString)
            {
                Database = TemplateDatabaseName,
            }.ConnectionString;

            // L'unico scopo del contesto è applicare le migration una volta.
            await using (await TestHelpers.CreateDbContextAndMigrateAsync(templateConnectionString))
            {
            }

            // Da qui il modello non deve più avere connessioni: `CREATE DATABASE ... TEMPLATE`
            // fallisce con 55006 se il sorgente è in uso. Si svuota il pool del SOLO modello —
            // ClearAllPools() colpirebbe anche i database delle classi che stanno già girando in
            // parallelo — poi si chiudono i backend residui e si nega la connessione.
            await using (var templateConnection = new NpgsqlConnection(templateConnectionString))
            {
                NpgsqlConnection.ClearPool(templateConnection);
            }

            await using (var admin = new NpgsqlConnection(adminConnectionString))
            {
                await admin.OpenAsync();

                await using (var terminate = admin.CreateCommand())
                {
                    terminate.CommandText =
                        "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                        $"WHERE datname = '{TemplateDatabaseName}' AND pid <> pg_backend_pid();";
                    await terminate.ExecuteNonQueryAsync();
                }

                await using (var seal = admin.CreateCommand())
                {
                    // È la stessa configurazione di template0: un database con datallowconn=false
                    // resta clonabile ma non connettibile.
                    seal.CommandText =
                        $"ALTER DATABASE \"{TemplateDatabaseName}\" WITH ALLOW_CONNECTIONS false;";
                    await seal.ExecuteNonQueryAsync();
                }
            }

            _templateReady = true;
        }
        finally
        {
            TemplateGate.Release();
        }
    }
```

- [ ] **Step 4: Aggiungere il parametro a `CreateIsolatedDatabaseAsync`**

Cambiare la firma (riga 710) da:

```csharp
    public async Task<string> CreateIsolatedDatabaseAsync(string databaseName)
```

a:

```csharp
    /// <param name="useTemplate">
    /// Se true, clona il database-modello già migrato invece di creare un database vuoto
    /// (#3633: 5,1-7,4 s contro 135-159 ms). Passare false solo dove il test deve esercitare le
    /// migration reali o assume uno schema vuoto.
    /// </param>
    public async Task<string> CreateIsolatedDatabaseAsync(string databaseName, bool useTemplate = false)
```

Poi, dentro il `try` del ciclo di retry, prima di aprire la connessione:

```csharp
                if (useTemplate)
                {
                    await EnsureTemplateDatabaseAsync();
                }
```

e sostituire l'assegnazione di `cmd.CommandText`:

```csharp
#pragma warning disable CA2100 // SQL injection safe: databaseName validated with regex ^[a-zA-Z0-9_]+$
                cmd.CommandText = useTemplate
                    ? $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE); CREATE DATABASE \"{databaseName}\" TEMPLATE \"{TemplateDatabaseName}\";"
                    : $"DROP DATABASE IF EXISTS \"{databaseName}\"; CREATE DATABASE \"{databaseName}\";";
#pragma warning restore CA2100
```

- [ ] **Step 5: Eseguire il test e verificare che passi**

Run:

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~IsolatedDatabaseTemplateTests" \
  --logger "console;verbosity=normal"
```

Expected: **3 passati, 0 falliti**.

- [ ] **Step 6: Verificare che nessun altro comportamento sia cambiato**

Il default è ancora `false`, quindi le altre classi devono comportarsi esattamente come prima.

Run:

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "Category=Integration&FullyQualifiedName~Administration" \
  --logger "console;verbosity=minimal"
```

Expected: stesso `Failed:` di prima della modifica.

- [ ] **Step 7: Commit**

```bash
git add apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs \
        apps/api/tests/Api.Tests/Infrastructure/IsolatedDatabaseTemplateTests.cs
git commit -m "test(ci): clona il db isolato da un modello migrato (#3633)"
git log --oneline -1
```

---

## Task 6: ribaltare il default e pinnare gli opt-out

**Files:**
- Modify: `apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs` (default del parametro)
- Modify: `apps/api/tests/Api.Tests/Infrastructure/MigrationSeedInventoryIntegrationTests.cs:55`
- Modify: gli eventuali altri call site individuati allo Step 1

**Interfaces:**
- Consumes: `CreateIsolatedDatabaseAsync(string, bool)` (Task 5).
- Produces: niente per task successivi.

- [ ] **Step 1: Enumerare i call site che devono restare fuori**

Solo due categorie devono passare `useTemplate: false`:
1. i **canary delle migration**, che devono continuare a esercitare il percorso vero;
2. i test che **asseriscono uno schema vuoto**.

Un test che chiama `MigrateAsync()` dopo aver creato il database **non** è un opt-out: col modello quella chiamata diventa un no-op e il test continua a funzionare. `DatabaseMetricsQueryTests` rientra qui — il suo commento «schema is empty» spiega perché migra, non pretende che lo schema resti vuoto.

Run:

```bash
cd apps/api/tests/Api.Tests
grep -rl "CreateIsolatedDatabaseAsync" --include=*.cs . > /tmp/iso.txt
while read -r f; do
  grep -q "MigrateAsync\|CreateDbContextAndMigrateAsync\|MigrateWithRetryAsync\|EnsureCreated" "$f" \
    || echo "DA ISPEZIONARE: $f"
done < /tmp/iso.txt
rm -f /tmp/iso.txt
```

Expected: circa 11 file. Aprirli uno per uno e passare `useTemplate: false` **solo** dove il test asserisce che una tabella non esiste, che il conteggio delle tabelle è zero, o che il database è appena stato creato. Negli altri (che non toccano lo schema affatto) uno schema preesistente è innocuo: lasciarli sul default.

`MigrationSeedInventoryIntegrationTests` è un opt-out **certo** — verifica i seed prodotti dalle migration — e non compare in quell'elenco perché migra: va corretto comunque.

- [ ] **Step 2: Pinnare il canary dei seed**

In `apps/api/tests/Api.Tests/Infrastructure/MigrationSeedInventoryIntegrationTests.cs`, riga 55:

```csharp
        // #3633: NON clonare il modello. Questo test misura ciò che le migration producono, quindi
        // deve eseguirle davvero: sul modello troverebbe i seed già applicati e passerebbe anche
        // con una migration di seed rotta.
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName, useTemplate: false);
```

- [ ] **Step 3: Verificare che l'altro canary non passi dalla fixture**

Run:

```bash
grep -n "CreateIsolatedDatabaseAsync" apps/api/tests/Api.Tests/Infrastructure/MeepleAiDbContextNpgsqlCanaryTests.cs || \
  echo "OK: non usa la fixture, fa EnsureDeletedAsync + MigrateAsync sul proprio database"
```

Expected: la riga `OK: ...`. Quel canary ricrea il database da zero per conto proprio, quindi il modello non lo tocca. Se invece la usasse, aggiungere `useTemplate: false` anche lì.

- [ ] **Step 4: Ribaltare il default**

In `SharedTestcontainersFixture.cs`, cambiare la firma:

```csharp
    public async Task<string> CreateIsolatedDatabaseAsync(string databaseName, bool useTemplate = true)
```

- [ ] **Step 5: Il test di Task 5 deve continuare a passare**

Run:

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~IsolatedDatabaseTemplateTests" \
  --logger "console;verbosity=normal"
```

Expected: **3 passati**. Passano `useTemplate` esplicitamente in entrambe le direzioni, quindi il cambio di default non li tocca — ed è il motivo per cui sono scritti così.

- [ ] **Step 6: Verificare un bounded context intero in locale**

Run:

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "Category=Integration&FullyQualifiedName~Administration" \
  --logger "console;verbosity=minimal"
```

Expected: stesso `Failed:` dello Step 6 di Task 5, e una `Duration:` sensibilmente più bassa. Non lanciare l'intera `Category=Integration` in locale: satura Docker e produce falsi fallimenti — la misura completa è quella della CI.

- [ ] **Step 7: Commit**

```bash
git add apps/api/tests/Api.Tests
git commit -m "test(ci): il clone da modello diventa il default (#3633)"
git log --oneline -1
```

- [ ] **Step 8: Verifica sul campo**

```bash
git push -u origin feature/issue-XXXX-integration-template-db
gh workflow run dev-async.yml --ref feature/issue-XXXX-integration-template-db
```

A run finita — l'id si ottiene con `gh run list --workflow=dev-async.yml --branch feature/issue-XXXX-integration-template-db --limit 1 --json databaseId --jq '.[0].databaseId'`:

```bash
gh run download <id> --dir /tmp/devasync-pr3
grep -h 'Aborting test run' /tmp/devasync-pr3/*/integration-*.log || echo "nessuno shard troncato"
grep -h -E '^(Passed|Failed)!  -' /tmp/devasync-pr3/*/integration-*.log
```

Expected:
- nessuno shard troncato;
- `Failed:` per shard invariato rispetto a PR2;
- `Duration:` per shard inferiore a PR2. Il confronto per-test si fa sui `.trx` dei due artifact: il calo deve concentrarsi nel setup delle classi, non essere distribuito uniformemente. Se è uniforme, il guadagno viene da altro e va capito prima di attribuirlo a questo task.
- I due canary (`MigrationSeedInventoryIntegrationTests`, `MeepleAiDbContextNpgsqlCanaryTests`) devono avere una durata **invariata** nel `.trx`: se crolla anche la loro, sono finiti sul modello e la copertura sulle migration è stata persa.

- [ ] **Step 9: PR verso `main-dev`**

```bash
gh pr create --base main-dev \
  --title "test(ci): clone da database-modello per i test isolati (#XXXX)" \
  --body "$(cat <<'EOF'
Ognuna delle 362 classi di integrazione creava il proprio database e ci applicava tutte le
migration: 250 `CreateTable` e 727 `CreateIndex`. Misurato su `pgvector/pgvector:pg16`, SQL puro:
**5,1-7,4 s** contro i **135-159 ms** di un `CREATE DATABASE ... TEMPLATE`, che è una copia a
livello di file.

Il modello si costruisce una volta per processo — `ICollectionFixture` istanzia quattro fixture,
quindi lo stato è statico — e poi viene **sigillato** con `ALLOW_CONNECTIONS false`: una sola
connessione aperta sul sorgente fa fallire con 55006 ogni clone concorrente, e negare le
connessioni trasforma quella convenzione in un invariante imposto dal server.

Nessun file di test toccato per il clone: la history di EF viene copiata insieme allo schema,
quindi il `MigrateAsync()` già presente nelle 362 classi diventa un no-op.

**Opt-out deliberati**: i canary delle migration restano sul percorso vero, altrimenti una
migration rotta smetterebbe di essere intercettata. La loro durata nel `.trx` deve restare
invariata — se crolla anche la loro, la copertura è stata persa.
EOF
)"
```

---

## Dopo PR3

Se uno shard resta oltre il budget dei 75 minuti, **allora** si apre la discussione su `TestSessionTimeout`, numero di shard o `maxParallelThreads` — con le durate per-test dei `.trx` in mano. Non prima: il budget di #3634 fu ereditato da una misura presa su una suite che non eseguiva nulla, ed è l'errore che questo piano evita ripetendolo al contrario.

Resta aperta l'osservazione non dimostrata della spec: `integration.runsettings` dichiara il data collector `Code Coverage` ma `dev-async` non usa `dotnet-coverage`. Il `ModulePath` dichiarato (`.*\\Api\\.dll$`) usa separatori Windows e su Linux potrebbe non corrispondere a nulla. Verificabile sui log raccolti da PR1.
