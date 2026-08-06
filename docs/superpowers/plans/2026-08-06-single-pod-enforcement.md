# Enforcement single-pod enrichment (#3383) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendere realmente effettivo il vincolo single-pod dell'enrichment Wikidata: accendere il tripwire che non è mai stato caricato, rendere il gauge dead-letter corretto a più pod, e aggiungere la hard-prevention sul batch.

**Architecture:** Tre livelli indipendenti. (1) *Rilevamento*: il tripwire Prometheus va montato e referenziato — oggi il file esiste ma nessun ambiente lo carica. (2) *Correttezza della metrica*: il gauge passa da ibrido (incremento in memoria + ri-ancoraggio giornaliero) a puramente DB-derivato via background service periodico, così a più pod ogni processo riporta lo stesso ground truth. (3) *Prevenzione*: lease Redis fail-closed sul batch, riusando `IBackgroundTaskOrchestrator.ExecuteWithDistributedLockAsync` invece di reimplementare un lock.

**Tech Stack:** .NET 9, Quartz, StackExchange.Redis (via `IBackgroundTaskOrchestrator`), xUnit + FluentAssertions + Moq, Prometheus + promtool.

## Global Constraints

- **Branch impilata**: parte da `feature/issue-3583-egress-observability-reasons` (PR #3596), non da `main-dev`. La PR va aperta con `--base feature/issue-3583-egress-observability-reasons`. Se PR #3596 mergia prima, ribasare su `main-dev` e cambiare la base della PR.
- Il vincolo single-pod tocca la **correttezza legale** (rate cap Wikimedia 5 RPS, DEC-3e): un secondo pod raddoppia il rate verso Wikidata/Commons. Nessuna modifica deve rendere il vincolo più implicito di com'è ora.
- Fail-closed significa che un'indisponibilità di Redis **ferma l'enrichment**. È voluto, e va documentato in ADR e runbook.
- Working dir: `D:/Repositories/meepleai-monorepo-main/.claude/worktrees/i3583`. Branch: `feature/issue-3383-single-pod-enforcement`.
- Comando test: da `apps/api`, `dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "<filtro>"`.
- Comando promtool (l'immagine ha `prometheus` come entrypoint; su Git Bash serve `MSYS_NO_PATHCONV=1`):
  `MSYS_NO_PATHCONV=1 docker run --rm --entrypoint promtool -v "<abs>/infra/prometheus/alerts:/work" prom/prometheus:v3.7.0 test rules /work/<file>.test.yml`

## File Structure

| File | Responsabilità | Azione |
|---|---|---|
| `infra/docker-compose.yml` · `infra/compose.staging.yml` · `infra/compose.prod.yml` | Mount delle regole nel container prometheus | Modifica: montano `api-single-instance.yml` |
| `infra/prometheus.yml` · `.staging.yml` · `.prod.yml` | `rule_files:` per ambiente | Modifica: caricano `api-single-instance.yml` |
| `.../SharedGameCatalog/Infrastructure/BackgroundJobs/WikidataDeadLetterMetricsRefreshService.cs` | Push periodico del COUNT dead-letter nel gauge | **Crea** |
| `apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs:130` | Registrazione hosted services | Modifica: registra il refresh service |
| `.../SharedGameCatalog/Application/Services/WikidataCoverEnrichmentRunner.cs:121` | Runner enrichment | Modifica: rimuove l'incremento ottimistico |
| `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.WikidataEnrichment.cs:134-179` | Gauge dead-letter | Modifica: rimuove `IncrementWikidataDeadLetterCount`, aggiorna il commento |
| `.../SharedGameCatalog/Application/Jobs/WikidataCoverEnrichmentJob.cs:64-76` | Batch Quartz | Modifica: lease Redis attorno al batch |
| `docs/for-claude/architecture/adr/adr-087-cover-procedure-design-decisions.md:38-44` | ADR D4 | Modifica: corregge l'affermazione sul tripwire, documenta il fail-closed |
| `docs/for-developers/operations/operations-manual.md` | Runbook | Modifica: sezione su enrichment fermo per Redis |
| `apps/api/tests/.../WikidataDeadLetterMetricsRefreshServiceTests.cs` | Test refresh service | **Crea** |
| `apps/api/tests/.../WikidataCoverEnrichmentJobTests.cs` | Test del job | Modifica: casi lease |

---

### Task 1: accendere il tripwire single-instance

**Files:**
- Modify: `infra/docker-compose.yml:357` (blocco volumes di prometheus)
- Modify: `infra/compose.staging.yml:359`
- Modify: `infra/compose.prod.yml:244`
- Modify: `infra/prometheus.yml` · `infra/prometheus.staging.yml` · `infra/prometheus.prod.yml` (blocco `rule_files:`)

**Interfaces:**
- Consumes: la regola già scritta in `infra/prometheus/alerts/api-single-instance.yml` e il suo test `api-single-instance.test.yml` (che già dichiara `exp_annotations`).
- Produces: alert `MultipleApiInstances` effettivamente caricato nei tre ambienti.

**Perché prima di tutto:** ADR-087 D4 rinvia la hard-prevention perché «il tripwire rende un scale-out rumoroso». Il file esiste, ha il suo test promtool, ma **non è montato in nessun compose e non compare in nessun `rule_files:`, dev incluso**: l'alert non è mai stato attivo. È il task più piccolo e quello che ripristina il presupposto dichiarato dell'ADR.

- [ ] **Step 1: verificare lo stato di partenza**

```bash
grep -rn "api-single-instance" infra/ | grep -v "alerts/api-single-instance"
```

Atteso: **nessun risultato** (il file non è referenziato da nulla). Se invece compare già da qualche parte, fermati e rileggi: il presupposto di questo task è cambiato.

- [ ] **Step 2: montare il file nei tre compose**

In ciascuno dei tre file, nel blocco `volumes:` del servizio `prometheus`, subito dopo la riga di `egress-guard.yml`, aggiungi:

```yaml
      - ./prometheus/alerts/api-single-instance.yml:/etc/prometheus/api-single-instance.yml:ro
```

Righe di riferimento: `infra/docker-compose.yml:358`, `infra/compose.staging.yml:360`, `infra/compose.prod.yml:245`.

- [ ] **Step 3: referenziare il file nei tre `rule_files:`**

In `infra/prometheus.yml`, `infra/prometheus.staging.yml`, `infra/prometheus.prod.yml`, dopo la riga di `egress-guard.yml`:

```yaml
  # #3373 D4 / #3383 — tripwire single-pod: count(up{job="meepleai-api"}) > 1.
  # Il file esisteva dal #3373 ma non era montato ne' referenziato in ALCUN ambiente,
  # quindi l'alert non e' mai stato attivo: era il presupposto dichiarato di ADR-087 D4.
  - '/etc/prometheus/api-single-instance.yml'
```

- [ ] **Step 4: validare con promtool**

Dalla root del worktree:

```bash
MSYS_NO_PATHCONV=1 docker run --rm --entrypoint promtool \
  -v "$(pwd)/infra/prometheus/alerts:/work" prom/prometheus:v3.7.0 \
  test rules /work/api-single-instance.test.yml
```

Atteso: `SUCCESS`. **Bloccante**: nessun workflow CI invoca promtool. Se Docker non è disponibile, riportalo invece di dichiarare il passo fatto.

- [ ] **Step 5: verificare che i tre config restino YAML validi e contengano la regola**

```bash
python -c "
import yaml
for f in ['infra/prometheus.yml','infra/prometheus.staging.yml','infra/prometheus.prod.yml']:
    rf = yaml.safe_load(open(f,encoding='utf-8')).get('rule_files',[])
    assert any('api-single-instance' in r for r in rf), f
    print(f, 'OK', len(rf), 'rule files')
"
```

Atteso: tre righe `OK`.

- [ ] **Step 6: commit**

```bash
git add infra/docker-compose.yml infra/compose.staging.yml infra/compose.prod.yml \
        infra/prometheus.yml infra/prometheus.staging.yml infra/prometheus.prod.yml
git commit -m "fix(observability): carica davvero il tripwire single-pod (#3383)"
```

---

### Task 2: gauge dead-letter da `COUNT` DB

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/BackgroundJobs/WikidataDeadLetterMetricsRefreshService.cs`
- Modify: `apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs:130`
- Modify: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.WikidataEnrichment.cs:134-179`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/WikidataCoverEnrichmentRunner.cs:121`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/BackgroundJobs/WikidataDeadLetterMetricsRefreshServiceTests.cs`

**Interfaces:**
- Consumes: `IWikidataCoverEnrichmentAttemptRepository.CountDeadLettersAsync(CancellationToken) -> Task<int>` (`IWikidataCoverEnrichmentAttemptRepository.cs:53`); `MeepleAiMetrics.SetWikidataDeadLetterCount(int)`.
- Produces: `WikidataDeadLetterMetricsRefreshService` con `internal Task RefreshOnceAsync(CancellationToken)` pubblicamente testabile.
- **Rimuove**: `MeepleAiMetrics.IncrementWikidataDeadLetterCount()` — nessun altro chiamante oltre a `WikidataCoverEnrichmentRunner.cs:121` (verificare con grep prima di cancellare).

**Perché DB-COUNT:** oggi il gauge è ibrido — il runner incrementa in memoria a ogni nuovo dead-letter, e `WikidataCoverDeadLetterRetentionJob.cs:92-93` ri-ancora al `COUNT` reale, ma solo **una volta al giorno alle 03:00 UTC**. A più pod ogni processo ha il suo contatore: `sum()` raddoppia, `max()` deriva. Con un refresh periodico da DB ogni pod riporta lo stesso ground truth, quindi `max()` diventa corretto. L'`ObservableGauge` continua a leggere il campo in memoria — **non** interroga il DB a ogni scrape, che è l'anti-pattern segnalato dall'issue.

- [ ] **Step 1: verificare che l'incremento abbia un solo chiamante**

```bash
grep -rn "IncrementWikidataDeadLetterCount" --include=*.cs apps/api
```

Atteso: la definizione in `MeepleAiMetrics.WikidataEnrichment.cs`, l'unico uso in `WikidataCoverEnrichmentRunner.cs:121`, più eventuali test. Se compaiono altri chiamanti di produzione, fermati: la rimozione va ripensata.

- [ ] **Step 2: scrivere il test che fallisce**

Crea `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/BackgroundJobs/WikidataDeadLetterMetricsRefreshServiceTests.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.BackgroundJobs;
using Api.Observability;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.BackgroundJobs;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WikidataDeadLetterMetricsRefreshServiceTests
{
    private static (WikidataDeadLetterMetricsRefreshService Sut, Mock<IWikidataCoverEnrichmentAttemptRepository> Repo) Build()
    {
        var repo = new Mock<IWikidataCoverEnrichmentAttemptRepository>();
        var services = new ServiceCollection();
        services.AddScoped(_ => repo.Object);
        var provider = services.BuildServiceProvider();

        var sut = new WikidataDeadLetterMetricsRefreshService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WikidataDeadLetterMetricsRefreshService>.Instance);

        return (sut, repo);
    }

    [Fact]
    public async Task RefreshOnceAsync_PushesTheCountedValueIntoTheGauge()
    {
        var (sut, repo) = Build();
        repo.Setup(r => r.CountDeadLettersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(42);

        await sut.RefreshOnceAsync(CancellationToken.None);

        repo.Verify(r => r.CountDeadLettersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshOnceAsync_RepositoryThrows_PropagatesSoTheLoopCanLogAndRetry()
    {
        // Il servizio NON deve inghiottire qui: e' il loop di ExecuteAsync a catturare, loggare e
        // ritentare al tick successivo (stesso contratto di ImpersonationMetricsRefreshService).
        var (sut, repo) = Build();
        repo.Setup(r => r.CountDeadLettersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = async () => await sut.RefreshOnceAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
```

- [ ] **Step 3: eseguire il test per verificare che fallisca**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~WikidataDeadLetterMetricsRefreshServiceTests"
```

Atteso: FALLISCE in compilazione — il tipo `WikidataDeadLetterMetricsRefreshService` non esiste.

- [ ] **Step 4: creare il background service**

Crea `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/BackgroundJobs/WikidataDeadLetterMetricsRefreshService.cs`, modellato su `ImpersonationMetricsRefreshService.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Observability;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.BackgroundJobs;

/// <summary>
/// #3383 (ADR-087 D4, task deferito) — ri-ancora periodicamente il gauge
/// <c>meepleai.wikidata.dead_letter_count</c> al <c>COUNT</c> reale sulla tabella degli attempt.
/// <para>
/// Prima di questo servizio il gauge era ibrido: il runner incrementava un contatore in memoria e
/// il <c>WikidataCoverDeadLetterRetentionJob</c> lo ri-ancorava al ground truth una sola volta al
/// giorno (03:00 UTC). A più di un'istanza ogni processo aveva il proprio contatore, quindi
/// <c>sum()</c> raddoppiava e <c>max()</c> derivava. Con un valore puramente DB-derivato ogni
/// istanza riporta lo stesso ground truth e <c>max()</c> torna corretto.
/// </para>
/// <para>
/// Il gauge continua a leggere il campo in memoria: è QUESTO servizio ad aggiornarlo. Interrogare
/// il DB dentro la callback dell'<c>ObservableGauge</c> — cioè a ogni scrape Prometheus — sarebbe
/// l'anti-pattern che l'issue segnala esplicitamente.
/// </para>
/// </summary>
internal sealed class WikidataDeadLetterMetricsRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WikidataDeadLetterMetricsRefreshService> _logger;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(60);

    public WikidataDeadLetterMetricsRefreshService(
        IServiceScopeFactory scopeFactory,
        ILogger<WikidataDeadLetterMetricsRefreshService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshOnceAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Resilience: un refresh fallito non deve abbattere l'host
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex,
                    "WikidataDeadLetterMetricsRefreshService: refresh fallito; riprovo al prossimo tick");
            }

            try
            {
                await Task.Delay(_refreshInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // shutdown regolare
            }
        }
    }

    /// <summary>Conta i dead-letter e pusha il valore nel gauge. Esposto per i test.</summary>
    public async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var attempts = scope.ServiceProvider
            .GetRequiredService<IWikidataCoverEnrichmentAttemptRepository>();
        var count = await attempts.CountDeadLettersAsync(cancellationToken).ConfigureAwait(false);
        MeepleAiMetrics.SetWikidataDeadLetterCount(count);
    }
}
```

- [ ] **Step 5: eseguire il test per verificare che passi**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~WikidataDeadLetterMetricsRefreshServiceTests"
```

Atteso: PASS, 2 test.

- [ ] **Step 6: registrare il servizio**

In `apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs`, accanto alla riga 130 che registra `ImpersonationMetricsRefreshService`:

```csharp
        // #3383 — il gauge dead-letter deve essere DB-derivato per restare corretto a >1 istanza.
        services.AddHostedService<Api.BoundedContexts.SharedGameCatalog.Infrastructure.BackgroundJobs.WikidataDeadLetterMetricsRefreshService>();
```

- [ ] **Step 7: rimuovere l'incremento ottimistico dal runner**

In `WikidataCoverEnrichmentRunner.cs:121` elimina la chiamata `MeepleAiMetrics.IncrementWikidataDeadLetterCount();` e il commento che la accompagna. Con il refresh a 60s l'incremento in memoria è solo una fonte di drift, ed è ciò che rendeva il gauge scorretto a più pod.

- [ ] **Step 8: rimuovere il metodo ora inutilizzato e aggiornare la documentazione del gauge**

In `MeepleAiMetrics.WikidataEnrichment.cs` elimina `IncrementWikidataDeadLetterCount()` (righe ~175-179) e riscrivi il commento XML del gauge (righe 134-152), che oggi descrive la strategia ibrida ormai superata:

```csharp
    /// <summary>
    /// Numero di <c>WikidataCoverEnrichmentAttempt</c> in stato <c>DeadLetter</c> attualmente
    /// presenti in tabella. Il valore è **puramente DB-derivato** (#3383): il
    /// <c>WikidataDeadLetterMetricsRefreshService</c> esegue un <c>COUNT</c> ogni 60s e chiama
    /// <see cref="SetWikidataDeadLetterCount(int)"/>; il <c>WikidataCoverDeadLetterRetentionJob</c>
    /// ri-ancora anche dopo ogni sweep. Nessun incremento ottimistico in memoria — era la ragione
    /// per cui il gauge non era corretto a più di un'istanza.
    ///
    /// Alerting: vedi infra/prometheus/alerts/wikidata-enrichment.yml
    /// (WikidataDeadLetterHigh &gt; 100 per 1h, WikidataDeadLetterSpike delta &gt; 50 in 5m).
    /// </summary>
```

Mantieni `SetWikidataDeadLetterCount` con l'`Interlocked.Exchange`: è ancora il punto di scrittura, chiamato sia dal refresh service sia dal retention job.

- [ ] **Step 8b: aggiornare i due test che asserivano l'incremento ottimistico**

Verificato: sono esattamente due, e **falliranno di proposito** dopo gli step 7-8. Non sono regressioni da mascherare — il comportamento è cambiato per progetto.

1. `apps/api/tests/Api.Tests/Observability/WikidataEnrichmentMetricsTests.cs:113` —
   `IncrementWikidataDeadLetterCount_FromAnchor_IncreasesByOnePerCall`: **eliminare il test**, testa un metodo che non esiste più (altrimenti non compila).

2. `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/WikidataCoverEnrichmentRunnerTests.cs:~256` — ancora il gauge a 5 e asserisce `Be(6)` con motivazione *«F1 hybrid update: runner increments by 1 per persisted DeadLetter attempt»*. Il runner non tocca più il gauge: l'asserzione diventa

```csharp
        ReadGauge(MeepleAiMetrics.WikidataDeadLetterCount).Should().Be(5,
            "#3383: il gauge è DB-derivato — il runner NON lo incrementa più; " +
            "è WikidataDeadLetterMetricsRefreshService a ri-ancorarlo al COUNT reale");
```

Il test vicino (`~307`, che asserisce `Be(7)` per un Failed non terminale) **resta verde**: quel percorso non incrementava comunque. Aggiorna solo il commento che cita «F1» se menziona l'incremento del runner.

- [ ] **Step 9: eseguire i test delle aree toccate**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~WikidataDeadLetter|FullyQualifiedName~WikidataCoverEnrichmentRunner|FullyQualifiedName~WikidataEnrichmentMetrics|FullyQualifiedName~WikidataCoverDeadLetterRetentionJob"
```

Atteso: PASS.

- [ ] **Step 10: build e commit**

```bash
cd apps/api/src/Api && dotnet build --nologo -v q
```

Atteso: `Avvisi: 0`, `Errori: 0`.

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/BackgroundJobs/WikidataDeadLetterMetricsRefreshService.cs \
        apps/api/src/Api/Extensions/ApplicationServiceExtensions.cs \
        apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.WikidataEnrichment.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/WikidataCoverEnrichmentRunner.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/BackgroundJobs/WikidataDeadLetterMetricsRefreshServiceTests.cs
git commit -m "feat(enrichment): gauge dead-letter da COUNT DB invece che in memoria (#3383)"
```

---

### Task 2b: aggregare le regole del gauge dead-letter

**Files:**
- Modify: `infra/prometheus/alerts/wikidata-enrichment.yml:21-39`

**Interfaces:**
- Consumes: il gauge ora DB-derivato del Task 2.
- Produces: `WikidataDeadLetterHigh` / `WikidataDeadLetterSpike` corrette a più istanze.

**Perché il Task 2 da solo è incompleto:** la giustificazione del gauge DB-derivato è «così `max()` diventa corretto a più pod» — ma **le regole non usano `max()`**. Oggi sono `meepleai_wikidata_dead_letter_count > 100` e `delta(meepleai_wikidata_dead_letter_count[5m]) > 50`, senza aggregazione: a N istanze Prometheus vede N serie distinte (una per `instance`) e ciascuna fa scattare il proprio alert → **N notifiche duplicate** per lo stesso backlog. Con il gauge DB-derivato i valori sono identici, quindi `max()` è esatto e collassa i duplicati in uno.

Nota sul comportamento di `delta()`: prima il runner incrementava in tempo reale, ora il valore si aggiorna ogni 60s dal `COUNT`. Su una finestra di 5 minuti restano ~5 punti di refresh, quindi uno spike di +50 viene ancora catturato, con al più 60s di ritardo. Accettabile per un alert con `for: 0m` su finestra di 5m — vale la pena saperlo, non richiede di cambiare la soglia.

- [ ] **Step 1: aggiungere l'aggregazione alle due regole**

In `wikidata-enrichment.yml`, sostituisci le due `expr:`:

```yaml
        expr: max(meepleai_wikidata_dead_letter_count) > 100
```

```yaml
        expr: delta(max(meepleai_wikidata_dead_letter_count)[5m:]) > 50
```

e aggiungi sopra il gruppo un commento:

```yaml
# #3383 — le due regole dead-letter aggregano con max(): dal gauge DB-derivato ogni istanza riporta
# lo stesso COUNT, quindi max() è esatto e collassa le N serie (una per instance) in un solo alert.
# Senza aggregazione, a più di un'istanza lo stesso backlog notificherebbe N volte.
```

Attenzione alla sintassi del secondo: `delta()` vuole un range vector, e `max(...)` è un instant vector — serve la **subquery** `[5m:]`, non `[5m]`. Scriverlo come `delta(max(...)[5m])` è un errore di parsing che promtool intercetta.

- [ ] **Step 2: validare con promtool**

```bash
MSYS_NO_PATHCONV=1 docker run --rm --entrypoint promtool \
  -v "$(pwd)/infra/prometheus/alerts:/work" prom/prometheus:v3.7.0 \
  test rules /work/wikidata-enrichment.test.yml
```

Atteso: `SUCCESS`. Le serie di test sono senza label (`meepleai_wikidata_dead_letter_count` nuda) e le `exp_labels` attese provengono dal blocco `labels:` della regola, quindi `max()` non le cambia: i casi esistenti devono restare verdi senza modifiche. **Se falliscono, non toccare i test per farli passare** — significa che l'espressione è sbagliata.

- [ ] **Step 3: commit**

```bash
git add infra/prometheus/alerts/wikidata-enrichment.yml
git commit -m "fix(observability): aggrega con max() le regole dead-letter (#3383)"
```

---

### Task 3: lease Redis fail-closed sul batch

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/WikidataCoverEnrichmentJob.cs:64-76`
- Modify: `apps/api/tests/Api.Tests/.../WikidataCoverEnrichmentJobTests.cs` (trovare il path esatto con `find apps/api/tests -name "WikidataCoverEnrichmentJobTests.cs"`)

**Interfaces:**
- Consumes: `IBackgroundTaskOrchestrator.ExecuteWithDistributedLockAsync(string lockKey, Func<CancellationToken, Task> taskFactory, TimeSpan lockTimeout, CancellationToken ct) -> Task<bool>` (`IBackgroundTaskOrchestrator.cs:83`, implementata in `RedisBackgroundTaskOrchestrator.cs:211`, registrata singleton in `InfrastructureServiceExtensions.cs:617`).
- Produces: `WikidataCoverEnrichmentJob` che salta il tick quando il lease non è ottenibile.

**Perché riusare l'orchestrator invece di scrivere un lease nuovo:** `ExecuteWithDistributedLockAsync` fa già esattamente `SET NX PX` + rilascio verificato per valore (così un'istanza non rilascia il lease di un'altra), è già in uso da `AnalyzeRulebookCommandHandler.cs:218`, ed è già **fail-closed** nella sostanza: se il lock non si acquisisce ritorna `false` senza eseguire il task; se Redis è irraggiungibile logga e **rilancia** (`RedisBackgroundTaskOrchestrator.cs:258-263`), quindi il batch non gira comunque. Reimplementare un lease sarebbe duplicazione.

**Verificato in review**: il rilascio è **atomico**, implementato con uno script Lua che confronta il valore prima di cancellare. Non c'è la race del GET+DEL, in cui un'istanza cancella il lease scaduto e riacquisito da un'altra.

Poiché l'orchestrator **rilancia** su Redis irraggiungibile, l'eccezione risale a Quartz. È il comportamento voluto (il tick fallisce rumorosamente anziché girare senza protezione), ma va saputo: comparirà nei log come job fallito, non come tick saltato.

**TTL:** il batch è fino a `BatchSize`=30 giochi con throttle di 1s ciascuno più il lavoro per gioco, quindi può durare minuti. TTL a **10 minuti**, molto sopra la durata attesa: se un'istanza muore a metà batch il lease blocca l'enrichment per al più quel tempo — accettabile, e comunque preferibile a due istanze che raddoppiano il rate verso Wikimedia.

- [ ] **Step 1: leggere il fixture del file di test**

Il file è `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Jobs/WikidataCoverEnrichmentJobTests.cs` (verificato). **Verificato anche l'assunto chiave**: i test esistenti chiamano `RunBatchAsync` 8 volte e `Execute` **zero** volte — quindi mettere il lease in `Execute` non ne rompe nessuno, e il seam di test resta intatto.

Leggi come costruiscono il job e quali mock preparano, per riusare lo stesso stile nei due test nuovi (che invece devono passare da `Execute`, l'unico punto dove vive il lease).

- [ ] **Step 2: scrivere i test che falliscono**

Aggiungi al file di test del job:

```csharp
    [Fact]
    public async Task Execute_LeaseNotAcquired_SkipsTheBatch()
    {
        // Un'altra istanza tiene il lease: il tick non deve processare nulla (DEC-3e: due istanze
        // raddoppierebbero il rate verso Wikimedia, violazione ToS).
        var orchestrator = new Mock<IBackgroundTaskOrchestrator>();
        orchestrator
            .Setup(o => o.ExecuteWithDistributedLockAsync(
                It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var runner = new Mock<IWikidataCoverEnrichmentRunner>();
        var job = BuildJobWith(orchestrator.Object, runner.Object, out var context);

        await job.Execute(context);

        runner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Execute_LeaseAcquired_RunsTheBatchUnderTheLock()
    {
        var orchestrator = new Mock<IBackgroundTaskOrchestrator>();
        Func<CancellationToken, Task>? captured = null;
        orchestrator
            .Setup(o => o.ExecuteWithDistributedLockAsync(
                It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, Func<CancellationToken, Task>, TimeSpan, CancellationToken>(
                (_, factory, _, _) => captured = factory)
            .ReturnsAsync(true);

        var runner = new Mock<IWikidataCoverEnrichmentRunner>();
        var job = BuildJobWith(orchestrator.Object, runner.Object, out var context);

        await job.Execute(context);

        captured.Should().NotBeNull("il batch deve essere passato all'orchestrator, non eseguito fuori dal lock");
    }
```

`BuildJobWith` è un helper da scrivere adattandolo al fixture già presente nel file: costruisce un `ServiceCollection` con i mock di `IWikidataCoverEnrichmentAttemptRepository`, `IWikidataCoverEnrichmentRunner` e `IBackgroundTaskOrchestrator`, ne fa il provider, e istanzia il job più un `IJobExecutionContext` mockato. Se il file di test ha già un helper equivalente, **riusalo** invece di aggiungerne uno secondo.

- [ ] **Step 3: eseguire i test per verificare che falliscano**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~WikidataCoverEnrichmentJobTests"
```

Atteso: FALLISCE — il job non risolve ancora `IBackgroundTaskOrchestrator`.

- [ ] **Step 4: implementare il lease**

In `WikidataCoverEnrichmentJob.cs`, sostituisci il corpo di `Execute` (righe 64-76):

```csharp
    /// <summary>
    /// TTL del lease. Molto sopra la durata attesa di un tick (fino a <see cref="BatchSize"/> giochi
    /// con throttle di <see cref="DelayBetweenItemsMs"/>ms ciascuno): se un'istanza muore a metà
    /// batch, l'enrichment resta fermo al più per questo tempo — preferibile a due istanze che
    /// raddoppiano il rate verso Wikimedia (DEC-3e, violazione ToS).
    /// </summary>
    private static readonly TimeSpan LeaseTtl = TimeSpan.FromMinutes(10);

    private const string LeaseKey = "wikidata-cover-enrichment-batch";

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        _logger.LogDebug("WikidataCoverEnrichmentJob started: FireTime={FireTime}", context.FireTimeUtc);

        using var scope = _serviceProvider.CreateScope();

        var attempts = scope.ServiceProvider.GetRequiredService<IWikidataCoverEnrichmentAttemptRepository>();
        var runner = scope.ServiceProvider.GetRequiredService<IWikidataCoverEnrichmentRunner>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IBackgroundTaskOrchestrator>();

        // #3383 — hard-prevention del vincolo single-pod (ADR-087 D4). [DisallowConcurrentExecution]
        // garantisce l'unicità solo DENTRO un processo: non impedisce a due istanze di eseguire lo
        // stesso batch e raddoppiare il rate verso Wikidata/Commons.
        //
        // FAIL-CLOSED, e non è gratis: se il lease non si acquisisce il tick viene saltato, e se
        // Redis è irraggiungibile l'orchestrator rilancia, quindi l'enrichment SI FERMA. È voluto —
        // si preferisce non arricchire piuttosto che rischiare di violare il rate cap Wikimedia — ma
        // significa che un'indisponibilità di Redis ferma anche questa pipeline. Vedi il runbook.
        var acquired = await orchestrator
            .ExecuteWithDistributedLockAsync(
                LeaseKey,
                innerCt => RunBatchAsync(attempts, runner, innerCt),
                LeaseTtl,
                ct)
            .ConfigureAwait(false);

        if (!acquired)
        {
            _logger.LogInformation(
                "WikidataCoverEnrichmentJob: lease '{LeaseKey}' già detenuto da un'altra istanza — tick saltato. " +
                "Se accade in modo persistente con una sola istanza attiva, il lease è orfano: scadrà entro {TtlMinutes} minuti.",
                LeaseKey, LeaseTtl.TotalMinutes);
        }
    }
```

Aggiungi il `using` per `IBackgroundTaskOrchestrator` (namespace `Api.Infrastructure.BackgroundTasks`).

- [ ] **Step 5: eseguire i test per verificare che passino**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~WikidataCoverEnrichmentJobTests"
```

Atteso: PASS. I test preesistenti che chiamano `RunBatchAsync` direttamente devono restare verdi senza modifiche.

- [ ] **Step 6: build e commit**

```bash
cd apps/api/src/Api && dotnet build --nologo -v q
```

Atteso: `Avvisi: 0`, `Errori: 0`.

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/WikidataCoverEnrichmentJob.cs \
        apps/api/tests/Api.Tests/.../WikidataCoverEnrichmentJobTests.cs
git commit -m "feat(enrichment): lease Redis fail-closed sul batch enrichment (#3383)"
```

---

### Task 4: correggere ADR-087 D4 e il runbook

**Files:**
- Modify: `docs/for-claude/architecture/adr/adr-087-cover-procedure-design-decisions.md:38-44`
- Modify: `docs/for-developers/operations/operations-manual.md` (sezione alert Wikidata enrichment)

**Interfaces:** nessuna — documentazione.

**Perché:** ADR-087 D4 afferma oggi che il tripwire «makes a scale-out **loud**» e che la hard-prevention è deferita di conseguenza. Entrambe le affermazioni vanno aggiornate: il tripwire non era caricato (Task 1) e la hard-prevention ora esiste (Task 3). Lasciare l'ADR com'è significa lasciare a verbale una protezione che non c'era e una decisione che non è più quella in vigore.

- [ ] **Step 1: emendare D4**

In `adr-087-cover-procedure-design-decisions.md`, sotto la sezione `### D4 — Deployment contract: single-pod, presidiato *(ratified)*`, aggiungi in coda:

```markdown
> **Emendamento 2026-08-06 (#3383).** Due fatti hanno modificato questa decisione.
>
> 1. Il tripwire descritto sopra come «Now» **non era mai stato attivo**: il file
>    `infra/prometheus/alerts/api-single-instance.yml` esisteva, con il suo test promtool, ma non era
>    montato in alcun compose né referenziato in alcun `rule_files:`, dev incluso. Fra la ratifica di
>    D4 e oggi il vincolo single-pod è stato presidiato **solo** da `container_name` — esattamente la
>    situazione che D4 dichiarava inaccettabile. Wiring corretto in #3383.
> 2. Di conseguenza il rinvio della hard-prevention, motivato dalla presenza del tripwire, non aveva
>    più fondamento: il **lease Redis fail-closed** sul batch è stato implementato in #3383 e non è
>    più «deferred». Il gauge dead-letter è ora DB-derivato, quindi corretto sotto `max()` a più
>    istanze.
>
> **Costo del fail-closed, da conoscere prima di un incidente**: il lease non è opzionale, quindi
> un'indisponibilità di Redis **ferma l'enrichment Wikidata**. È la semantica voluta — non arricchire
> è preferibile a violare il rate cap Wikimedia — ma va cercata attivamente quando l'enrichment si
> ferma senza altra spiegazione.
>
> Il rate-limiter distribuito (Redis token bucket) resta in riserva per un'eventuale roadmap HPA:
> lease e tripwire coprono la correttezza a una istanza, non il throughput a N.
```

- [ ] **Step 2: aggiornare il runbook**

In `docs/for-developers/operations/operations-manual.md`, nella sezione degli alert Wikidata enrichment (quella puntata dal `runbook_url` di `api-single-instance.yml`, ancora `#wikidata-enrichment-alerts`), aggiungi una voce:

```markdown
#### Enrichment Wikidata fermo senza errori applicativi

Sintomo: `meepleai_wikidata_queue_depth` non cala, nessun nuovo attempt, nessuna eccezione nei log
dell'handler.

Causa da escludere per prima: **Redis irraggiungibile**. Dal #3383 il batch di enrichment gira sotto
un lease Redis **fail-closed** (`wikidata-cover-enrichment-batch`, TTL 10 min): se Redis non risponde
il tick viene saltato di proposito, perché due istanze che processano lo stesso batch
raddoppierebbero il rate verso Wikidata/Commons (violazione ToS, DEC-3e). L'enrichment fermo è quindi
un **sintomo atteso** di un'indisponibilità Redis, non un guasto della pipeline cover.

Verifica: stato del container redis, poi cerca nei log dell'API
`WikidataCoverEnrichmentJob: lease ... già detenuto`. Se il messaggio ricorre con una sola istanza
attiva, il lease è orfano (istanza morta a metà batch) e scade da solo entro 10 minuti.
```

- [ ] **Step 3: verificare che i link non si rompano**

```bash
grep -n "wikidata-enrichment-alerts" docs/for-developers/operations/operations-manual.md infra/prometheus/alerts/api-single-instance.yml
```

Atteso: l'ancora citata nel `runbook_url` esiste ancora nel manuale. Il gate Docs Link Check fallisce anche su un `<a href=` letterale dentro inline code: non introdurre HTML grezzo nel markdown.

- [ ] **Step 4: commit**

```bash
git add docs/for-claude/architecture/adr/adr-087-cover-procedure-design-decisions.md \
        docs/for-developers/operations/operations-manual.md
git commit -m "docs(adr): emenda D4 — il tripwire non era attivo, il lease non e' piu' deferito (#3383)"
```

---

### Task 5: verifica finale e PR

**Files:** nessuna modifica di codice.

- [ ] **Step 1: build pulita**

Da `apps/api/src/Api`: `dotnet build --nologo -v q` → `Avvisi: 0`, `Errori: 0`.

- [ ] **Step 2: regressione sulla categoria Unit**

Da `apps/api`:

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "Category=Unit"
```

Riporta il conteggio esatto passati/falliti. Il baseline su questa branch (dopo PR #3596) è **21361 passati, 0 falliti, 23 skipped**: non deve peggiorare.

- [ ] **Step 3: promtool su entrambi i file alert toccati o adiacenti**

```bash
MSYS_NO_PATHCONV=1 docker run --rm --entrypoint promtool \
  -v "$(pwd)/infra/prometheus/alerts:/work" prom/prometheus:v3.7.0 \
  test rules /work/api-single-instance.test.yml /work/egress-guard.test.yml
```

Atteso: `SUCCESS` per entrambi.

- [ ] **Step 4: formattazione backend**

Dalla root del worktree (non da `apps/api`: i path del git diff sono relativi alla root):

```bash
dotnet format apps/api/MeepleAI.Api.sln --include $(git diff --name-only feature/issue-3583-egress-observability-reasons...HEAD -- '*.cs' | tr '\n' ' ')
```

`--include` è obbligatorio: senza, `dotnet format` applica i fix degli analyzer a tutto il progetto e ha già cancellato costruttori DI usati solo via reflection (S1144).

- [ ] **Step 5: push e PR**

```bash
git push -u origin feature/issue-3383-single-pod-enforcement
gh pr create --base feature/issue-3583-egress-observability-reasons \
  --title "feat(enrichment): enforcement single-pod — tripwire, gauge DB-derivato, lease (#3383)" \
  --body "<vedi contenuti obbligatori sotto>"
```

**La base è il branch di PR #3596**, non `main-dev`: questa branch è impilata. Se #3596 mergia prima, ribasare su `main-dev` e cambiare la base con `gh pr edit --base main-dev`.

Il corpo della PR deve contenere:
1. Che il tripwire **non era mai stato attivo** in alcun ambiente, e che questo invalidava il presupposto dichiarato di ADR-087 D4.
2. Che il gauge passa da ibrido a DB-derivato, e perché questo è ciò che lo rende corretto sotto `max()` a più istanze.
3. Che il lease è **fail-closed**: un'indisponibilità di Redis ferma l'enrichment. È voluto, ed è documentato in ADR e runbook.
4. Esito di build, categoria `Unit` e promtool.
5. Che serve un **force-recreate di prometheus su staging** dopo il merge perché le nuove regole diventino attive.

---

## Self-Review

**Copertura dell'issue #3383:**
- Checkbox riaperta «tripwire» → Task 1. ✔
- Deferito «gauge dead-letter da COUNT DB» → Task 2. ✔
- Deferito «lease Redis fail-closed» → Task 3. ✔
- «Va corretto ADR-087 D4» (aggiornamento in issue) → Task 4. ✔
- Rate-limiter distribuito: esplicitamente **fuori scope**, resta in riserva per una roadmap HPA — dichiarato in Task 4 Step 1.

**Scan dei placeholder:** due punti restano deliberatamente adattivi, ed è segnalato sul posto. Task 3 Step 1 chiede di individuare il file di test del job e riusarne il fixture invece di trascrivere un helper che non ho letto; Task 3 Step 2 dice esplicitamente di riusare un helper equivalente se già esiste. Trascrivere a memoria un fixture non verificato produrrebbe codice plausibile ma falso — peggio di un'istruzione esplicita a leggerlo.

**Coerenza dei tipi:** `ExecuteWithDistributedLockAsync(string, Func<CancellationToken, Task>, TimeSpan, CancellationToken) -> Task<bool>` è verificata in `IBackgroundTaskOrchestrator.cs:83`. `CountDeadLettersAsync(CancellationToken) -> Task<int>` è verificata in `IWikidataCoverEnrichmentAttemptRepository.cs:53`. `SetWikidataDeadLetterCount(int)` resta, `IncrementWikidataDeadLetterCount()` viene rimossa dopo aver verificato al Task 2 Step 1 che abbia un solo chiamante di produzione.

**Ordine:** i task sono indipendenti tranne il 4, che documenta ciò che fanno 1 e 3. Il Task 1 va comunque per primo: è il più piccolo e ripristina il presupposto su cui poggia il resto.
