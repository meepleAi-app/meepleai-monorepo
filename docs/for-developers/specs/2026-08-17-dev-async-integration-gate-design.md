# Dev Async — il gate di integrazione non misura la selezione intera

**Data**: 2026-08-17
**Stato**: design approvato, implementazione non iniziata
**Contesto**: residuo di [#3633](https://github.com/meepleAi-app/meepleai-monorepo/issues/3633) (problema 2) + un difetto nuovo sulla concurrency
**File toccati**: `.github/workflows/dev-async.yml`, `apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs`, ~370 file di test (solo l'attributo `[Collection]`)

---

## 1. Sintomi

Due sintomi distinti, che si presentano insieme e vanno tenuti separati.

**S1 — due shard su tre non completano la selezione.** `Aborting test run: test run timeout of
4500000 milliseconds exceeded` sui 75 minuti di `integration.runsettings`. I conteggi di quegli
shard misurano una **selezione parziale**: restano confrontabili con la baseline solo perché la
baseline è troncata allo stesso modo.

**S2 — lo shard Games non arriva quasi mai in fondo**, e non per un guasto: è l'ultimo a chiudere e
`concurrency: cancel-in-progress: true` lo falcia a ogni push su `main-dev`. Il 2026-08-17, su 15
run consecutive, **9 sono `cancelled`**.

### Misura di riferimento

Run [`32026945696`](https://github.com/meepleAi-app/meepleai-monorepo/actions/runs/32026945696),
sha `a3d9df5c`, l'ultima ad aver prodotto un esito completo:

| shard | esito | Failed | Passed | Total | sessione | throughput |
|---|---|---|---|---|---|---|
| KnowledgeBase | completo | 4 | 900 | 926 | 54m26s | **17,0 test/min** |
| Games | `Test Run Aborted.` | 3 | 731 | 743 | 74m | **10,0 test/min** |
| Core | `Test Run Aborted.` | 9 | 771 | 825 | 74m | **11,1 test/min** |

Setup + `Build (Release)` occupano ~7,5 min per shard *prima* che la sessione parta.

Estrapolando sulle selezioni dichiarate nella matrice (910 / 1123 / 929): Games richiederebbe
**~112 min**, Core **~84**, KnowledgeBase **55**. Totale **~250 min di sessione contro 225 di
budget** (3 × 75). Il budget fissato in [#3634](https://github.com/meepleAi-app/meepleai-monorepo/issues/3634)
su «~61 min per l'intera selezione» è stato superato dalla crescita della suite.

---

## 2. Diagnosi

### C1 — l'asse dello sharding e l'asse del parallelismo sono la stessa partizione

xUnit parallelizza **fra** collection e serializza **dentro** una collection. Le quattro collection
`Integration-Group{A..D}` sono state assegnate per bounded context — il commento in
`SharedTestcontainersFixture.cs:1212` lo dice: «Group A: KnowledgeBase + DocumentProcessing …
Group C: SharedGameCatalog + GameManagement …». I filtri di shard di `dev-async.yml` tagliano sugli
stessi nomi.

Distribuzione reale, 370 classi:

| shard | GroupA | GroupB | GroupC | GroupD | tot | collection non vuote |
|---|---|---|---|---|---|---|
| KnowledgeBase | 68 | 31 | 3 | 10 | 112 | 4 (61% in una) |
| **Games** | **0** | **1** | **113** | 46 | 160 | 3 su 4, ma GroupB con una sola classe |
| Core | 6 | 11 | 41 | 42 | 100 | 4 |

> La somma dei `tot` per shard (112 + 160 + 100 = 372) supera il totale di 370 classi perché i
> filtri di shard sono **non esclusivi**: due classi contengono token sia di `KnowledgeBaseTokens`
> sia di `GamesTokens` e vengono quindi conteggiate — ed eseguite — da entrambi gli shard.

Lo shard Games ha GroupA **vuota** e GroupB che si esaurisce dopo una sola classe: per costruzione
lascia il runner privo di lavoro su quei due thread quasi per l'intera durata, e resta appeso alla
catena seriale di 113 classi in GroupC per il resto della run. È lo shard che sfora per primo, e non
è un caso.

Il commento dichiara «~39-42 classi per gruppo». La realtà è 74 / 42 / 157 / 97: la ripartizione è
derivata da sola, senza che niente la misurasse.

> ⚠️ **Limite dell'evidenza.** Lo skew spiega *perché* Games è il peggiore, non l'intero
> ordinamento: KnowledgeBase ha il 61% concentrato in un gruppo ed è comunque il più veloce, quindi
> contano anche le durate per-classe. Quantificare il guadagno richiede le durate del `.trx` — che
> `dev-async` oggi **non pubblica**. Questa è la ragione dell'ordine delle PR.

### C2 — ogni classe ricostruisce lo schema da zero

`CreateIsolatedDatabaseAsync` crea un database per classe di test; la classe poi chiama
`MigrateAsync()`. Le migration sono 18, di cui `InitialCreate` da 11.557 righe con **250
`CreateTable` e 727 `CreateIndex`**. Le classi che lo fanno sono **362**.

Benchmark su `pgvector/pgvector:pg16`, SQL puro generato con `dotnet ef migrations script
--idempotent` (nessun overhead EF, nessuna rete):

| operazione | tempo |
|---|---|
| `CREATE DATABASE` + 18 migration da zero | **5.147 / 5.394 / 7.437 ms** |
| `CREATE DATABASE … TEMPLATE` | **135 / 135 / 142 / 145 / 159 ms** |
| `DROP DATABASE` (teardown, invariato) | 76 / 79 ms |

Template risultante: 21 MB, 221 tabelle. **Rapporto ~38x.**

> Attenzione al peso relativo: 362 × ~6 s sono ~36 minuti di **thread-time**, non di wall-clock. È
> una leva reale ma **secondaria** rispetto a C1 — diventa significativa proprio perché C1 collassa
> il parallelismo e trasforma quel thread-time in tempo seriale.

### C3 — la concurrency uccide la run (indipendente da C1 e C2)

`cancel-in-progress: true` su un gate che dura 83 minuti, su un branch che riceve merge ogni 40-70
minuti. La finestra non esiste. Effetto collaterale: `dev-auto-revert` classifica `cancelled` come
`green-pending` → no-op, quindi su `main-dev` non esiste alcun esito, né verde né rosso — la stessa
classe di difetto di [#3629](https://github.com/meepleAi-app/meepleai-monorepo/issues/3629).

---

## 3. Design

Tre interventi, **tre PR sequenziali**. L'ordine non è estetico: PR1 costruisce lo strumento con cui
PR2 e PR3 si misurano. Il costo di sbagliare attribuzione qui è alto — il budget di #3634 fu
ereditato da una misura presa su una suite che non eseguiva nulla
([#3632](https://github.com/meepleAi-app/meepleai-monorepo/issues/3632)).

### PR1 — riavere un segnale

Solo `.github/workflows/dev-async.yml`.

**a) `concurrency.cancel-in-progress: false`.**
GitHub non accoda una fila: a gruppo occupato mette la nuova run in *pending* e **cancella la
pending precedente**. La run in volo arriva quindi sempre in fondo, e quando lo slot si libera parte
l'ultimo sha. È un debounce, non un backlog. Rende inutile il workaround del ref effimero e
restituisce a `dev-auto-revert` l'attribuzione per-sha.

**b) `.trx` come artifact**, `if: always()`, uno per shard.
`integration.runsettings` lo produce già (`integration-test-results.trx`); nessuno lo raccoglie.
Senza le durate per-test, PR2 e PR3 non sono misurabili.

**c) Guard sul troncamento + riepilogo per shard.**
Modellato su quello di `ci.yml` (#3632) ed esteso:

- fallisce se il `.trx` non esiste o se `executed < 100`;
- fallisce con `::error::` esplicito se l'output contiene `Aborting test run: test run timeout` —
  oggi quel fatto è sepolto nel log e indistinguibile da un fallimento ordinario;
- scrive `Failed / Passed / Total` per shard nello step summary.

Il riepilogo per shard è il confronto corretto contro la baseline (Core 9 · KnowledgeBase 4 ·
Games 3): **il colore del job non è un metro**, perché il rosso è atteso finché #3633 è aperta.

Nota sull'implementazione: il `dotnet test` va incanalato in un file preservandone l'exit code
(`set -o pipefail`), altrimenti il `tee` maschera il fallimento.

**Criteri di accettazione**
- Una run su `main-dev` durante un push concorrente arriva a `completed`, non `cancelled`.
- I tre `.trx` sono scaricabili dalla pagina della run.
- Su uno shard troncato compare un `::error::` che nomina il troncamento.

**Non alziamo il budget in questa PR.** PR1 non nasconde il troncamento: gli dà un nome.

Il guard **non introduce un rosso nuovo**: un `Test Run Aborted.` esce già 134 e fa fallire lo step,
quindi il colore del job non cambia. Cambia solo che la causa è nominata invece che sepolta nel log
insieme ai fallimenti ordinari.

---

### PR2 — collection ortogonali all'asse dello sharding

~370 file di test (solo l'attributo), `SharedTestcontainersFixture.cs` (commento), un test di
guardia nuovo.

**Regola.** L'appartenenza a `Integration-Group{A..D}` si determina con un **hash stabile del nome
pienamente qualificato della classe**, mod 4. Ortogonale al bounded context per costruzione. L'hash
si calcola una volta con uno script e si scrive **l'attributo letterale** nel file: nessuna magia a
runtime, discovery di xUnit invariata, attributo greppabile.

L'hash è **SHA-256 dell'FQN, primi 4 byte in big-endian, mod 4**. Il vincolo è la riproducibilità
dello script fra esecuzioni e macchine: `string.GetHashCode()` in .NET è randomizzato per processo e
darebbe una ripartizione diversa a ogni run: non è utilizzabile qui neanche a valle, perché a un
rerun dello script il diff sarebbe illeggibile.

Il numero di gruppi resta **4** e `maxParallelThreads` resta **4**: si ripara il collasso senza
alzare la concorrenza di picco oltre quella già prevista dal design attuale.

**Test di guardia** (unit, senza DB), che riflette sull'assembly e asserisce:

1. ogni gruppo tiene fra il 20% e il 30% delle classi di integrazione;
2. **per ciascuno dei tre filtri di shard di `dev-async.yml`, tutti e 4 i gruppi sono non vuoti.**

I tre insiemi di token dello shard (`KnowledgeBase|DocumentProcessing|Authentication`,
`SharedGameCatalog|GameManagement|Administration`, e il complemento) sono **duplicati come costanti
nel test**, non letti dal YAML: parsare il workflow legherebbe un test unitario al formato di un
file di CI, e il guard diventerebbe fragile per la ragione sbagliata. La duplicazione è accettata e
va annotata da entrambi i lati — commento nel test verso il workflow, commento nel workflow verso il
test — perché una divergenza fra i due elenchi rende il guard cieco proprio sullo shard che cambia.

L'invariante 2 è quella che si è rotta. Il commento diceva «~39-42 classi per gruppo» mentre GroupC
era arrivata a 157: la deriva è passata inosservata perché nessuno la misurava. Codificarla come
test è ciò che impedisce che riaccada — un commento non è un guard.

**Rischio dichiarato.** Le classi che si affidavano implicitamente alla serializzazione coi propri
vicini ora girano accanto a vicini diversi. Sono attesi flaky nuovi. Il `.trx` di PR1 è come li si
attribuisce: confronto per-test, non per-colore.

**Criteri di accettazione**
- Lo shard Games completa la selezione entro il budget di 75 min, senza `Test Run Aborted.`.
- Il test di guardia fallisce se si sposta a mano una classe fra gruppi fino a svuotare un gruppo
  dentro uno shard.
- Il conteggio `Failed` per shard non cresce oltre la baseline (Core 9 · KB 4 · Games 3); ogni
  scostamento va attribuito a un test nominato, non accettato in blocco.

---

### PR3 — template database

`SharedTestcontainersFixture.cs` più i call site che devono restare fuori.

`CREATE DATABASE x TEMPLATE tpl` al posto di `CREATE DATABASE` + 18 migration.

- Il template si costruisce **una volta per processo**. `ICollectionFixture` istanzia **quattro**
  fixture, quindi serve un `Lazy<Task<…>>` statico, non un campo d'istanza.
- Dopo la costruzione **non ci si connette mai più al template**: una connessione aperta fa fallire
  `CREATE DATABASE … TEMPLATE`. Servono `NpgsqlConnection.ClearAllPools()` e
  `pg_terminate_backend` sul template al termine della costruzione.
- Il `MigrateAsync()` già presente nei test diventa un no-op: la history table viene copiata insieme
  allo schema. **Zero modifiche ai 362 file di test.**
- **Opt-out** per le classi che assumono uno schema vuoto: `CreateIsolatedDatabaseAsync(name,
  useTemplate: true)` con `false` dove serve. `DatabaseMetricsQueryTests` lo dichiara
  esplicitamente in un commento. Vanno **enumerate una per una**, non stimate: il grep iniziale ne
  segnala ~10 candidate, ma alcune migrano indirettamente via `WebApplicationFactory`.
- 🔴 **I canary restano sul percorso vero.** `MeepleAiDbContextNpgsqlCanaryTests` e
  `MigrationSeedInventoryIntegrationTests` devono continuare a migrare da zero, altrimenti una
  migration rotta smette di essere intercettata. È un requisito di correttezza.
- `CREATE DATABASE … TEMPLATE` prende un lock sul database sorgente, quindi le copie si serializzano
  fra loro: 362 × 0,14 s ≈ 51 s per suite. Accettabile.

**Criteri di accettazione**
- Le durate per-test del `.trx` mostrano un calo del setup per-classe coerente col benchmark.
- I due canary eseguono ancora le migration reali (verificabile dal loro tempo di esecuzione, che
  non deve crollare).
- Nessuna variazione del conteggio `Failed` per shard.

---

## 4. Fuori scope

Dichiarati esplicitamente, per non confondere le misure:

- **`TestSessionTimeout`, `maxParallelThreads` e la matrice degli shard non si toccano.** Se dopo
  PR2 e PR3 uno shard resta oltre budget, quella diventa una decisione separata **con numeri veri**.
- **I 16 fallimenti di baseline restano** (#3633, triage dei fallimenti).
- **Build-once via artifact** come in `ci.yml` (`Build (Release)` costa ~6 min × 3 shard): risparmio
  in minuti CI, quasi nullo in wall-clock. Non serve a questi sintomi.

### Osservazione non dimostrata

`integration.runsettings` dichiara il data collector `Code Coverage`, ma `dev-async` non usa
`dotnet-coverage` (`ci.yml` sì). Se il collector si attiva comunque, è costo di strumentazione per
zero beneficio. **Da verificare con l'artifact di PR1**, non da assumere: il `ModulePath` dichiarato
(`.*\\Api\\.dll$`) usa separatori Windows e potrebbe non corrispondere a nulla su Linux.

---

## 5. Verifica

Per ogni PR, dispatch di `dev-async.yml` e confronto contro la misura di riferimento della §1 —
**per shard**, mai per colore del job:

| shard | Failed | Passed | Total | sessione |
|---|---|---|---|---|
| KnowledgeBase | 4 | 900 | 926 | 54m26s (completo) |
| Games | 3 | 731 | 743 | 74m (troncato) |
| Core | 9 | 771 | 825 | 74m (troncato) |

Dopo PR1 il dispatch su un ref effimero non serve più: con `cancel-in-progress: false` la run in
volo non viene falciata. Il workaround resta valido come ripiego.

---

## 6. Tracciamento

Issue nuova per questo lavoro. **#3633 resta al triage dei fallimenti**: quella issue separa già i
due problemi («Due problemi distinti, da non confondere») e sovrapporli riporterebbe la confusione
che si è presa la briga di evitare.
