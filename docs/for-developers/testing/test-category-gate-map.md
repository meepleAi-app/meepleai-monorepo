# Mappa categorie di test → gate CI

**Quale workflow esegue quali test, e come non finire fuori da tutti**

Riferimenti: [#3622](https://github.com/meepleAi-app/meepleai-monorepo/issues/3622) (inversione del default del gate veloce) · [#3625](https://github.com/meepleAi-app/meepleai-monorepo/issues/3625) (58 test fuori da ogni gate) · [ADR-054](../../for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md) (ripartizione dei gate per branch)

---

## La mappa

Il trait letto dai filtri CI è **solo** `Category`. Ogni altro trait (`BoundedContext`, `Dependency`, `Issue`, `Epic`) è documentazione: nessun workflow lo interroga.

| Categoria | Eseguita da | Bloccante |
|---|---|---|
| `Unit` · `Security` · `Contract` · `PDF` · `CrossContext` | `dev-fast.yml` + `ci.yml` (gate a deny-list) | sì |
| `Integration` | `dev-async.yml` + `ci.yml` (shard `Category=Integration`) | solo in `ci.yml` |
| `E2E` | `backend-e2e-tests.yml` | sì |
| `Performance` · `Slow` · `Manual` | **nessuno** | — |

Il gate veloce seleziona per **esclusione** (`Category!=Integration&Category!=E2E&…`): un test che dimentica il trait viene eseguito comunque. Il default è quindi sicuro — ma solo per chi il trait lo dimentica. Chi lo mette sbagliato esce da tutto.

## La regola

> Una classe di test deve dichiarare almeno una categoria che qualche gate seleziona, oppure avere un `Skip` esplicito su ogni fact.

`Performance`, `Slow` e `Manual` **non bastano da sole**. Se una classe ha bisogno di container, la categoria giusta è `Integration` (che `Performance` può accompagnare come etichetta descrittiva); se gira in-process, è `Unit`.

La regola è verificata da `TestCategoryGateArchitectureTests.EveryTestClass_IsSelectedByAGate_OrIsExplicitlySkipped`. Quando fallisce, il messaggio elenca le classi invisibili: scegli tra aggiungere una categoria eseguita, mettere uno `Skip` motivato su ogni fact, o cancellarle.

## Perché la regola esiste

`Performance` era classificata correttamente come esclusa dal gate veloce (#3622) e selezionata da nessun altro workflow. Nessuna delle due condizioni è un errore presa da sola; insieme erano 58 test che non venivano eseguiti da anni.

Quando li si è eseguiti per la prima volta, **quattro erano rossi**: tre `ArbitroBenchmarkTests` rotti all'istanziazione (`ITestOutputHelper` senza fixture data — segnalato come «blocking backend test suite» già nel febbraio 2026 e mai risolto) e un callback Moq in `WizardConcurrencyAndPerformanceTests` rimasto alla firma di `StoreAsync` precedente all'aggiunta di `BlobCategory`. Un test che nessuno esegue non è copertura dormiente: è codice che marcisce mentre il resto avanza.

## Soglie temporali e runner condivisi

Le assert di latenza assoluta (`p95.Should().BeLessThan(1.0)`) non sono decidibili su un runner CI condiviso: una pausa GC o un vicino rumoroso durante la finestra di campionamento tinge di rosso una build senza che nulla sia regredito.

Convenzione adottata in #3625 per i test in `MultiTierCachePerformanceTests`:

- l'**invariante strutturale** è l'assert vera (`result.SourceTier.Should().Be(CacheTier.L1Memory)`) — deterministica, e cattura la regressione che conta: un tier che smette di servire e scivola su quello sotto;
- la **soglia temporale** resta come guard-rail con un ordine di grandezza di margine sul target di prodotto, e il valore misurato va su `Console.WriteLine` per l'ispezione;
- il **target di prodotto** (L1 < 1ms, L2 < 10ms) si misura in produzione con le dashboard Prometheus, non in CI.

## Aggiungere una categoria nuova

1. Aggiungila a `TestCategories` con la sua caratterizzazione (durata attesa, dipendenze).
2. Decidi il gate e aggiornala **sia** nel `--filter` di `dev-fast.yml` e `ci.yml`, **sia** in `ExcludedFromFastGate`/`AllowedInFastGate` di `TestCategoryGateArchitectureTests`.
3. Se non deve girare nel gate veloce, registrala in `SelectedByAGate` con il workflow che la esegue — oppure accetta che le classi che la usano debbano essere skippate esplicitamente.
