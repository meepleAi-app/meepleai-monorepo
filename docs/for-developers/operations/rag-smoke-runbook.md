# RAG Smoke Test Runbook (#2480)

Tier 3 D8 quality gate of [#2126](https://github.com/meepleAi-app/meepleai-monorepo/issues/2126). Catches **silent retrieval regressions** — embedding-model swaps, chunker changes, index drift — that the bake smoke (`seed-snapshot-bake-ci.yml`) does not cover.

## What it does

`infra/scripts/rag-smoke-assert.sh` runs the canonical queries in `infra/fixtures/rag-canonical-queries.json` against `POST /api/v1/knowledge-base/ask/global` (SSE) and asserts the **top-3 retrieved chunks** per query match `infra/fixtures/rag-golden-baseline.json`.

The suite covers **EN + IT** (10 queries: 5 EN + 5 IT, added for [#3269](https://github.com/meepleAi-app/meepleai-monorepo/issues/3269)). `multilingual-e5-base` does **cross-lingual retrieval**, so each `-it` query pins the IT→EN retrieval behavior. This is the concrete implementation of the epic [#3266](https://github.com/meepleAi-app/meepleai-monorepo/issues/3266) LOCKED safety-net: *"EN+IT non-regression suite on staging before prod"*. Motivating case: `catan-setup-it` ("Setup per N giocatori" style IT query) must still retrieve the right EN chunks.

### 🔴 Il corpus NON è tutto inglese (#3740)

Questo runbook diceva «the corpus is English rulebooks», ed è falso — misurato su staging: **51.505 chunk `en`, 4.332 `it`, 530 `de`**. Tredici PDF del manifest `dev.yml` sono nativamente in un'altra lingua (root, scacchi-fide, descent, barrage, agricola, pandemic, 7-wonders, azul, terraforming-mars, ticket-to-ride, carcassone, splendor · great-western-trail in tedesco), pur essendo dichiarati `language: en` nel manifest.

Conta perché cambia come si legge un `-it` rosso. Nello spazio di e5 la lingua del testo è una componente dominante, quindi una query italiana ha per vicini i chunk **italiani di qualunque gioco**: un `-it` che recupera il manuale sbagliato può essere clustering linguistico e non una regressione del ranking. La distinzione è misurabile — restringere l'ordinamento cosine a `lang='en'` — e i numeri stanno in [2026-08-17-e5-prefix-and-cross-lingual-retrieval-audit.md](../audits/2026-08-17-e5-prefix-and-cross-lingual-retrieval-audit.md).

Regola pratica: prima di trattare un `-it` rosso come drift, controlla se il gioco atteso ha contenuto nella lingua della query. **Catan, Dominion e Ark Nova sono solo in inglese**, ed è la ragione per cui le loro query IT sono le più fragili del set. ⚠️ Il caso speculare esiste e sfugge più facilmente: il manuale di **7 Wonders è solo in italiano** (`7-wonders_rulebook.pdf`), quindi è la query **inglese** a mancarlo — ripiegando su `7-wonders-duel`, che è un altro gioco. Vedi § *Due criteri, non uno*.

It reads the **Citations SSE event (`type: 1`)**, which the vector search emits *before* the LLM streams tokens — so the assertion is independent of OpenRouter/LLM availability. Each chunk is keyed by the **document name** (`{document, page}`, baseline v2 — see § *La chiave della baseline*); `page` and `score` are advisory (not asserted, to tolerate chunking shifts and minor embedding/search float drift).

### 🔴 Cosa misura questo gate — e cosa NON misura (#3768)

Il corpus del gate **non è quello che la produzione serve**, e la differenza non è una scelta di configurazione: è il fallback di emergenza della catena di estrazione.

`EnhancedPdfProcessingOrchestrator` prova Unstructured (soglia 0.80, heading-aware) → SmolDocling (0.70) → **Docnet** come ultimo stadio. Il target `seed-index` avvia `postgres redis api embedding-service smoldocling-service`: **`unstructured-service` non c'è**, quindi il bake finisce sullo stage 3. A parità di 127 PDF, il gate ha ~10.900 chunk grossi e staging 56.367 heading-aware (mediana 90 caratteri). Dettagli e opzioni in [#3794](https://github.com/meepleAi-app/meepleai-monorepo/issues/3794).

**Conseguenza pratica**: la stessa modifica può migliorare un corpus e peggiorare l'altro, senza che nessuno dei due risultati sia sbagliato. Su chunk grossi il nome del gioco è un proxy accettabile per «pagina di contenuto»; su chunk fini è un proxy per «pagina dei crediti». È successo tre volte:

| modifica | staging | gate |
|---|---|---|
| [#3769](https://github.com/meepleAi-app/meepleai-monorepo/issues/3769) filtro lessicale sul nome del gioco | positivo | neutro (10/11 → 10/11) |
| [#3787](https://github.com/meepleAi-app/meepleai-monorepo/pull/3787) stesso filtro sul boost di heading | positivo (8/11 → **9/11** end-to-end) | neutro (8/11 → 8/11) |
| [#3773](https://github.com/meepleAi-app/meepleai-monorepo/issues/3773) il filtro lessicale diventa effettivo | positivo | **−2** (10/11 → 8/11) |

**Quindi**: questo gate è un test di **non-regressione della pipeline** su un corpus stabile e riproducibile — compito per cui funziona, e che ha dimostrato isolando #3773 in due run a parità di snapshot. Non è un giudice della **qualità del ranking**.

Per decidere una modifica al ranking usa il banco offline, § *Tarare la fusione senza spendere una run*: è validato a tre livelli — punteggi per-candidato (41.616 ricostruiti, 0 divergenze), selezione per-gioco (11 query su 11 coincidenti col dump globale) ed **esito finale** (ha predetto 9/11 per #3787, e la misura end-to-end sulle citazioni SSE reali ha dato 9/11, con le stesse due query fuori bersaglio).

⚠️ Il banco richiede un **campione sano**: conta la copertura del braccio vettoriale prima di leggerne i numeri, o misurerai una degradazione invece della modifica ([#3786](https://github.com/meepleAi-app/meepleai-monorepo/issues/3786)). È già successo: la prima misura di #3787 disse «non serve» su un campione in cui il gioco atteso non aveva candidati vettoriali da promuovere.

### Per-query `language`

The fixture top-level `language` (`"en"`) is the **default**. Each query MAY set a per-query `"language"` override — the IT queries carry `"language": "it"`, the 5 EN queries omit it and inherit the default. The harness resolves `(.language) // <top-level default>` per query and sends it as the request-body `language`, so retrieval ranking is deterministic per query.

### SKIP for un-baselined queries

A query with **no golden-baseline entry** reports `SKIP` (via a `::notice::`), not `FAIL`, and does **not** fail the gate. This lets new queries (e.g. the IT set) land *before* the ops `--update-baseline` capture without redding the weekly cron. Real drift and no-citations still `FAIL`. The summary reports `N passed, N failed, N skipped (pending baseline)`; exit is non-zero only on a real `FAIL`.

### Due criteri, non uno: deriva e correttezza (#3740)

Il confronto con la golden baseline misura la **deriva**: fallisce a ogni cambiamento dei top-*K*, anche quando il cambiamento è un miglioramento. Non dice nulla su *quale* manuale sia stato recuperato.

La distinzione non è teorica. Fino a #3740 la baseline pinnava, per `catan-setup-it`:

```
star-wars-rebellion_rulebook.pdf · imperial-settlers_rulebook.pdf · cthulhu-death-may-die_rulebook.pdf
```

Nessun Catan — e il gate era **verde**, perché il retrieval non era cambiato rispetto a quando la baseline fu catturata. Un gate che certifica come corretto un risultato sbagliato è la stessa classe di difetto dei gate che non eseguono nulla ([#3622](https://github.com/meepleAi-app/meepleai-monorepo/issues/3622) e seguenti): il colore verde e l'assenza di problemi si confondono.

Accanto alla baseline l'harness conta quindi quante query hanno, nei primi *K* chunk, il manuale **proprio** del gioco nominato — `expectedDocument` in `rag-canonical-queries.json` — e confronta il totale con `semanticFloor`.

**Perché un conteggio con pavimento e non un pass/fail per query.** Due query oggi mancano il bersaglio per un difetto noto e non ancora corretto: farle fallire una per una renderebbe il gate rosso in permanenza, e un gate sempre rosso si ignora. Il pavimento distingue «sappiamo che due sono fuori» da «ne è appena uscita una terza».

**Perché stretto e non largo.** Il criterio largo — qualunque documento il cui nome cominci col gioco — è stato misurato e **nasconde un difetto**: `seven-wonders-military` recupera tre volte su tre `7-wonders-duel_rulebook.pdf`, che è **un altro gioco**, e passerebbe per via del prefisso comune. Con il criterio stretto il conteggio è 9/11 invece di 10/11, e i due fuori bersaglio sono:

| query | manuale atteso | lingua del manuale | perché manca |
|---|---|---|---|
| `catan-setup-it` | `catan_en_rulebook.pdf` | **en** | query IT, manuale EN |
| `seven-wonders-military` | `7-wonders_rulebook.pdf` | **it** | query EN, manuale IT — ripiega su Duel, che è in inglese |

Sono **lo stesso difetto in direzioni opposte**, ed è la prova più diretta che il meccanismo è la lingua e non una particolarità di Catan: quando il manuale e la domanda non coincidono di lingua, il braccio vettoriale non porta il documento giusto abbastanza in alto. La cura sta in [#3737](https://github.com/meepleAi-app/meepleai-monorepo/issues/3737) (il prefisso `query:` di e5), non nella fusione — che è già al suo ottimo: nessuna combinazione di pesi supera 9/11, misurato replicando `FuseGlobally` offline sull'artifact `rag-fusion-tuning-<run_id>`.

🔴 **Quando il pavimento sale, alzalo.** Se l'harness stampa `Criterio semantico salito a N/11`, fissa il guadagno aggiornando `semanticFloor` nello stesso commit. Se scende, il messaggio d'errore chiede esplicitamente di **non** abbassarlo per far passare la build: un pavimento che insegue il risultato non è un pavimento.

### L'interruttore del prefisso e5 `query:` (#3737)

La cura descritta sopra è **presente nel codice ma spenta**. Il prefisso corretto secondo il model card di e5 è anche, misurato su questo corpus, un peggioramento: 10/11 → 8/11 nella run `32053791375`, perché il conteggio precedente dipendeva in parte dalla codifica sbagliata, che il braccio lessicale compensava. Tornare indietro è costato un revert (#3747) più un redeploy; l'interruttore esiste perché il prossimo tentativo costi un flip.

| dove | valore |
|---|---|
| chiave | `Embedding:E5QueryPrefixEnabled` |
| tipo | `bool` |
| riga assente | **spento** — il deploy non cambia nulla finché non si accende deliberatamente |
| propagazione | ≤ 5 min (cache di `IConfigurationService`), nessun restart |

**Per una run del gate** — non serve toccare staging, e non servirebbe a nulla: lo stack del gate è **effimero** e nasce dallo snapshot pubblicato, quindi non vede le righe di configurazione di staging. Si accende con l'input del dispatch:

```bash
gh workflow run rag-smoke-dispatch.yml -f e5_query_prefix=true
```

Lo step `Turn on the e5 query prefix` semina la riga nel Postgres effimero prima dello smoke. Una run **senza** quell'input misura il comportamento attuale: è il termine di paragone dell'A/B, ed è per questo che l'interruttore va lasciato spento come default anche qui.

**Su staging o in produzione**:

```bash
curl -sS -X POST "$API/api/v1/admin/configurations" \
  -H 'Content-Type: application/json' -b "$ADMIN_COOKIE" \
  -d '{"key":"Embedding:E5QueryPrefixEnabled","value":"true","valueType":"bool",
       "description":"e5 query: prefix on search queries (#3737)",
       "category":"general","environment":"All","requiresRestart":false}'
```

`environment: "All"` per [ADR-062](../../for-claude/architecture/adr/adr-062-config-environment-field-semantics.md): è una chiave globale, non un valore che diverge per ambiente. Per spegnere, `PUT /admin/configurations/{id}` con `value: "false"` — non serve rimuovere la riga.

⚠️ **L'ingestione non passa dall'interruttore.** Solo le query sono commutabili: i chunk restano `passage:` per costruzione, perché un chunk codificato `query:` richiederebbe un re-bake completo. Nessun re-index è necessario né quando si accende né quando si spegne.

⚠️ **Cache semantica.** `SemanticResponseCache` (Redis, TTL 24 h, soglia 0.95) confronta il vettore della domanda: cambiare il prefisso cambia quel vettore e produce cache-miss finché il TTL non scade. Misurato, `cos(passage: X, query: X)` sta fra 0.935 e 0.960 — **a cavallo** della soglia, quindi il degrado è parziale. Un hit resta corretto, perché è la stessa domanda: nessuna invalidazione manuale.

### Tarare la fusione senza spendere una run

`infra/scripts/rag-fusion-bench.py` rimette il dump `[RAG-TUNE]` nella stessa formula che gira in produzione, così una modifica alla fusione si valuta in secondi invece che in ~45 minuti di CI.

```bash
gh run download <run_id> -n rag-fusion-tuning-<run_id> -D /tmp/tuning-off
gh run download <run_id2> -n rag-fusion-tuning-<run_id2> -D /tmp/tuning-on
python infra/scripts/rag-fusion-bench.py --reference /tmp/tuning-off --compare /tmp/tuning-on
```

🔴 **La directory `--reference` deve venire da una run in cui il gate è PASSATO.** È ciò che rende la golden baseline un ground truth: lo script ricostruisce i top-3 e li allinea ai nomi dei documenti, e **si ferma se trova conflitti** — un GUID allineato a nomi diversi in query diverse significa che la replica non riproduce l'ordinamento reale, e ogni numero successivo sarebbe infondato. Una replica che diverge dal codice produce risultati sbagliati con l'aria di essere autorevole, ed è il modo in cui questo lavoro ha già sbagliato due volte.

Storia: quattro configurazioni provate contro il gate fra il 17 e il 22 agosto (10/11 → 8/11 → 5/11 → 7/11), tutte a scommessa. Lo script ha ripagato il costo di scriverlo alla prima domanda a cui ha risposto — se la correzione per lingua funzionasse ora che `lang` arriva davvero alla fusione.

⚠️ **Se `FuseGlobally` cambia, questo script va cambiato con essa.** La validazione lo scopre, ma solo quando qualcuno lo esegue — ed è già successo: dopo il merge della correzione per lingua (#3740) la replica ha riportato **8 conflitti**, perché validava ancora col comportamento precedente. Il segnale è arrivato al primo utilizzo utile e il fix è stato una riga (il default di `language_correction`). Un banco senza quella validazione avrebbe continuato a stampare numeri, semplicemente sbagliati.

### La chiave della baseline: il documento, non il suo id (v2, #3666)

La baseline **v2** pinna, per ogni query, la sequenza ordinata dei **documenti** da cui provengono i primi *K* chunk. Le pagine restano nel file ma sono **advisory**: una pagina diversa dentro il manuale giusto produce un `::notice::`, non un fallimento.

**Perché.** Fino alla v1 la chiave era `{source, page}`, dove `source` è `pdf_documents.Id` — un `Guid.NewGuid()` generato a ogni ingest (`PdfDocument.cs:1012`, e `StreamQaQueryHandler:396-399` spiega perché la citation porta proprio quell'id: serve al viewer PDF del frontend). Ne seguiva che **ogni re-bake invalidava la baseline per costruzione**, a retrieval identico: fra il 2026-07-20 e il 2026-08-10 il gate è stato rosso in 7 run su 8, e l'unico verde è stato il dispatch che ha catturato la baseline. Un gate spento sette volte su otto non protegge nulla.

Le *query* canoniche non hanno mai avuto questo problema perché identificano i giochi per nome (`"game": "Catan"`). La v2 allinea la baseline allo stesso livello semantico.

**Il compromesso, esplicito.** Pinnare il documento e non la pagina **perde** il drift di ranking fine *all'interno dello stesso manuale*: se il chunk giusto scivola da pagina 3 a pagina 12 restando nel manuale corretto, il gate lo segnala ma non fallisce. È una perdita di sensibilità reale, accettata perché l'alternativa non era «un gate più severo» ma «un gate spento»: la sensibilità della v1 era teorica, dato che la baseline era scaduta quasi sempre. Ciò che il gate continua a rilevare — un chunk che arriva dal manuale sbagliato — è la regressione per cui esiste.

**Come viene risolto il nome.** Dopo il login l'harness fa **una** chiamata a `GET /api/v1/admin/pdfs?pageSize=500` e costruisce la mappa `id → fileName`, poi traduce ogni citation. Serve quindi un account **admin** in `SMOKE_EMAIL`/`SMOKE_PASSWORD`. Un id citato che non compare nella mappa non viene confuso con un drift: fallisce con il suo messaggio («documento rimosso o mappa incompleta»).

### Baseline v1: rigenerazione una tantum (exit 3)

Una baseline con `schemaVersion < 2` **esce 3 senza eseguire alcuna query**:

```
::error:: baseline in formato v1 (id fisici) — va rigenerata una volta sola
```

Non è la toil ricorrente di prima: la v2 sopravvive ai re-bake, quindi questa rigenerazione si fa **una volta**. Procedura: § *Capturing the EN + IT baseline via CI dispatch*.

| Exit | Significato | Azione |
|---|---|---|
| `0` | tutte le query combaciano (o baseline aggiornata) | — |
| `1` | drift reale (documenti diversi), o nessuna citation, o id non risolvibile | indagare |
| `3` | baseline ancora in formato v1 | rigenerare **una volta** |

**Lo snapshot diverso non blocca più** (era exit 3 in #3645, quando la chiave era fisica). Con la chiave v2 il confronto resta significativo attraverso un re-bake, quindi la divergenza viene solo segnalata:

```
::notice:: baseline catturata su un altro snapshot (…20260729T070620Z → …20260809T060634Z).
  Con la chiave v2 (nome documento) il confronto resta valido: un fallimento qui
  è drift del retrieval, non una baseline scaduta.
```

Il campo `snapshot` resta nella fixture: non serve più a invalidare, ma a dire su quale corpus la baseline è stata catturata quando si legge un fallimento.

⚠️ L'auto-opener deduplica sulla label `rag-smoke-failure`: **finché una issue resta aperta non ne viene emessa un'altra**. Una issue di baseline scaduta lasciata aperta silenzia gli alert successivi, inclusi quelli di un drift vero.

## Capturing / updating the golden baseline

The baseline must be captured against a **fresh, compatible snapshot** (`snapshot-verify.sh` exit 0). Do this:

1. Ensure a fresh snapshot is the most recent in `data/snapshots/` (e.g. from `make seed-index`). `snapshot-fetch.sh` picks the newest `*.meta.json`.
2. Boot it:
   ```bash
   cd infra && make dev-from-snapshot
   bash scripts/wait-for-healthy.sh api 300
   ```
3. Capture:
   ```bash
   API_BASE_URL=http://localhost:8080 \
   SMOKE_EMAIL=<admin-email> SMOKE_PASSWORD=<password> \
   bash scripts/rag-smoke-assert.sh --update-baseline
   ```
   This writes `infra/fixtures/rag-golden-baseline.json` (`baseline`, `snapshot`, `embeddingModel`, `capturedAt`).
4. Review the diff and commit it.

**Con la baseline v2 un semplice re-bake NON richiede più una rigenerazione** (#3666): la chiave è il nome del documento, che il re-bake conserva. Rigenera solo quando cambia davvero ciò che il gate misura — modello di embedding, chunker, o un bump di `seed-schema.version` che sposta il retrieval — e solo dopo aver stabilito che lo scostamento è **voluto**. Un drift non intenzionale è esattamente ciò che questo gate esiste per segnalare: indagalo prima di rigenerare, altrimenti la nuova baseline lo certifica come normale.

### Capturing the EN + IT baseline via CI dispatch (preferred)

The IT queries ship with **no baseline entries** — they `SKIP` until captured. To capture the full EN + IT baseline against the current published snapshot without a local boot:

1. Dispatch `.github/workflows/rag-smoke-dispatch.yml` with `update_baseline=true` (runs against the published snapshot the workflow fetches).
2. Download the `rag-golden-baseline-<run_id>` artifact from that run.
3. Commit the artifact's `infra/fixtures/rag-golden-baseline.json` (now containing all 10 EN + IT entries).

After the commit, the weekly assert run covers EN + IT with zero `SKIP`s.

### 🔴 SP3 #3269 critical note — capture the EN + IT baseline BEFORE the big-bang re-index

The SP3 big-bang re-index (**Slice 3**) changes retrieval ranking, so a baseline captured *after* it would silently absorb any pre-existing IT regression. Sequence:

1. **Pre-SP3**: capture the EN + IT baseline on the *current* published snapshot (via the CI dispatch above) and commit it. This freezes the known-good IT→EN behavior.
2. Run the Slice 3 big-bang re-index + publish the new snapshot.
3. **Post-SP3**: re-capture the EN + IT baseline on the new snapshot and commit it — then diff against the pre-SP3 baseline to review the intentional ranking change.

Skipping step 1 forfeits the non-regression signal the epic #3266 safety-net exists to provide.

## Asserting (CI / local)

```bash
cd infra
make rag-smoke          # or: bash scripts/rag-smoke-assert.sh
```
Exit 0 = all queries match. Exit 1 = a query drifted (prints expected vs got) or returned no citations.

## CI status — WEEKLY CRON (assert) + manual re-baseline

`.github/workflows/rag-smoke-dispatch.yml` runs **weekly on a schedule** (Monday 05:37 UTC) in **assert** mode against the committed golden baseline, and can still be dispatched manually to re-capture the baseline (`update_baseline=true`). R2 snapshot distribution works ([#2516](https://github.com/meepleAi-app/meepleai-monorepo/issues/2516)): the dedicated `meepleai-seed-snapshots` bucket + `SEED_BLOB_*` repo secrets are configured, and `dev-from-snapshot` fetches the published snapshot via the read creds synthesized in the workflow's "Configure snapshot bucket read credentials" step. On a `schedule` trigger `github.event.inputs.update_baseline` is empty → assert mode; a drift opens a tracking issue automatically.

The golden baseline was first committed for snapshot `meepleai_seed_20260628T211806Z_intfloat_multilingual-e5-base_7cee37d47` (#2480), after the RAG retrieval fixes in #2556 (cross-game DbContext concurrency) + #2559 (restored `text_chunks`/`pdf_documents.search_vector` columns).

**Re-baseline after an intentional re-bake** (EF head / embedding-model / chunker change):
1. re-bake + publish: `seed-snapshot-bake-full.yml -f publish=true`
2. dispatch this workflow with `update_baseline=true`
3. download the `rag-golden-baseline-<run_id>` artifact, commit `infra/fixtures/rag-golden-baseline.json`

## Canonical queries

| queryId | game | language | targets |
|---|---|---|---|
| `catan-setup` | Catan | en (default) | board setup, initial settlements/roads |
| `wingspan-round-goals` | Wingspan | en (default) | end-of-round goal scoring |
| `dominion-buy-phase` | Dominion | en (default) | buy phase, coins |
| `ark-nova-conservation` | Ark Nova | en (default) | conservation projects, reputation |
| `seven-wonders-military` | 7 Wonders | en (default) | military conflict per age |
| `catan-setup-it` | Catan | it | board setup, initial settlements/roads (IT→EN cross-lingual) |
| `wingspan-round-goals-it` | Wingspan | it | end-of-round goal scoring (IT→EN cross-lingual) |
| `dominion-buy-phase-it` | Dominion | it | buy phase, coins (IT→EN cross-lingual) |
| `ark-nova-conservation-it` | Ark Nova | it | conservation projects, reputation (IT→EN cross-lingual) |
| `seven-wonders-military-it` | 7 Wonders | it | military conflict per age (IT→EN cross-lingual) |

The 5 EN queries omit `language` and inherit the top-level default (`en`); the 5 IT queries (#3269) set `"language": "it"`. All 5 games are indexed in the `dev.yml` seed manifest. The IT queries report `SKIP` until their baseline is captured (see *Capturing the EN + IT baseline via CI dispatch* above).
