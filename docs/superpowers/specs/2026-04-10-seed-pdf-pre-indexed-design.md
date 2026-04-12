# Seed con PDF pre-indicizzati — Design

**Data**: 2026-04-10
**Stato**: Draft (awaiting user review)
**Autore**: brainstorming session

## Problema

Allo stato attuale, `PdfSeeder` crea `PdfDocumentEntity` in stato `Pending` ed enqueuea un `ProcessingJob` Queued. L'indicizzazione vettoriale vera (extract → chunk → embed → index) viene eseguita in background da `PdfProcessingQuartzJob` (ciclo 10s). Per il manifest `dev.yml` (136 PDF) questo significa ore di attesa e dipendenze pesanti (SmolDocling + embedding service) al primo avvio di un ambiente dev.

Conseguenze:
- Un developer che parte da DB vuoto non ha RAG funzionante prima di ore
- Ogni reset del volume postgres re-innesca la pipeline
- L'esperienza di onboarding è scoraggiante
- CI integration test non può ragionevolmente aspettare il completamento

## Obiettivo

Produrre una volta un DB snapshot con **tutti i rulebook del manifest già indicizzati** (chunks + embeddings), e consentire a qualunque ambiente dev di partire da quello snapshot invece di rieseguire la pipeline.

## Non-obiettivi

- Sostituire il flusso `make dev` esistente (rimane alternativa disponibile)
- Snapshot per `staging.yml` / `prod.yml` (questo spec copre solo `dev.yml` e `ci.yml`)
- GitHub Actions automation (rimane trigger manuale on-demand)
- Delta/incremental snapshots
- Multi-embedding-model nello stesso snapshot
- Encryption client-side del dump (il bucket S3 è già SSE-AES256 side-server)
- Promozione di `make dev-from-snapshot` a default (verrà valutato dopo ≥1 settimana di uso)

## Architettura — due fasi separate

### Fase "bake" — produce lo snapshot

On-demand, rara, idealmente una volta per rilascio o quando cambiano manifest / modello di embedding / migration schema. Gira su macchina con GPU (consigliata) o CPU con tempo a disposizione.

```
make seed-index
  │
  ├─ 1. preflight sanity check (api, smoldocling, embedding healthy; seed blob configurato; processing_jobs vuota)
  ├─ 2. docker compose up -d postgres redis api smoldocling-service embedding-service
  ├─ 3. wait-for-healthy api
  │       (SeedOrchestrator.RunAsync parte automaticamente durante lo startup dell'API,
  │        vedi apps/api/src/Api/Program.cs:535 — crea games, PDF Pending, jobs Queued)
  ├─ 4. poll processing_jobs finché tutti terminal (Completed | Failed | DeadLettered) oppure timeout 3h
  ├─ 5. fail-fast se fail_rate > 15% (soglia configurabile)
  ├─ 6. pg_dump -Fc --exclude-table-data='__EFMigrationsHistory'
  ├─ 7. scrivi sidecar .meta.json
  ├─ 8. sha256sum del dump
  └─ 9. (opzionale, target separato) upload a seed blob bucket
```

### Fase "consume" — usa lo snapshot

```
make dev-from-snapshot
  │
  ├─ 1. snapshot-fetch.sh    (cache locale first, fallback a latest.txt sul bucket)
  ├─ 2. snapshot-verify.sh   (compat gate contro working tree)
  ├─ 3. docker compose up -d postgres + wait healthy
  ├─ 4. snapshot-restore.sh  (ef database update → pg_restore --data-only → smoke test)
  └─ 5. SKIP_CATALOG_SEED=true docker compose up -d   (avvia resto dello stack saltando il seed)
```

`make dev` rimane invariato. `make dev-from-snapshot` è un target **parallelo e opt-in**. Il fallback esplicito è sempre `make dev`.

## Contract dello snapshot

### Naming convention

```
meepleai_seed_<timestamp>_<embedding_model_slug>_<commit>.dump
meepleai_seed_<timestamp>_<embedding_model_slug>_<commit>.meta.json
meepleai_seed_<timestamp>_<embedding_model_slug>_<commit>.dump.sha256
```

Esempio: `meepleai_seed_20260410T143022Z_sentence-transformers_all-MiniLM-L6-v2_3a75a9a10.dump`

Il nome include tutto ciò che serve per capire a colpo d'occhio se uno snapshot è compatibile, ma il sidecar `.meta.json` rimane l'unica fonte autoritativa letta dagli script.

### Sidecar `.meta.json`

```json
{
  "schema_version": "20260401_AddSearchVector",
  "ef_migration_head": "20260401_AddSearchVector",
  "embedding_model": "sentence-transformers/all-MiniLM-L6-v2",
  "embedding_dim": 384,
  "smoldocling_version": "1.3.0",
  "app_commit": "3a75a9a10",
  "created_at": "2026-04-10T14:22:00Z",
  "pdf_count": 136,
  "chunk_count": 18432,
  "embedding_count": 18432,
  "failed_pdf_ids": [],
  "dev_yml_sha256": "…"
}
```

Note sui campi:
- `smoldocling_version` è popolato interrogando `smoldocling-service/version` all'istante del dump; se il service non espone un endpoint version, il campo viene omesso (informational, non partecipa al compat gate).
- `_comment_*` non è previsto, il sidecar è JSON stretto.
- `failed_pdf_ids` è l'array dei `pdf_document_id` con job in stato `Failed` o `DeadLettered` al momento del dump. Vuoto nel caso strict, potenzialmente non vuoto nel caso `SEED_INDEX_ALLOW_PARTIAL=true`.

### Compat-check al consume (gate)

Tutti bloccanti tranne `dev_yml_sha256` (warning):

| Check | Fonte "expected" | Exit code al mismatch |
|---|---|---|
| `ef_migration_head` vs `dotnet ef migrations list` del working tree | tree EF | 2 |
| `embedding_model` vs `EMBEDDING_MODEL` env | `infra/secrets/embedding.secret` | 3 |
| `embedding_dim` vs `EMBEDDING_DIM` env | `infra/secrets/embedding.secret` | 4 |
| `dev_yml_sha256` vs `sha256sum dev.yml` | working tree | warning, non bloccante |

Codici di uscita distinti per facilitare scripting/CI: exit 2 = "rigenera", exit 3/4 = "config drift".

### DB-only — contratto esplicito

Lo snapshot contiene **solo stato DB**. I PDF blob files vivono già idempotentemente nel seed blob bucket (`rulebooks/v1/*.pdf`). `make dev-from-snapshot` richiede che `STORAGE_PROVIDER` punti a un blob storage dove quei file sono raggiungibili, altrimenti i `FilePath` in `PdfDocumentEntity` puntano nel vuoto.

### Esclusione `__EFMigrationsHistory`

`pg_dump --exclude-table-data='__EFMigrationsHistory'` è intenzionale. Al restore si applica prima `dotnet ef database update` sul DB vuoto (scrive schema + history del working tree), poi `pg_restore --data-only` carica i dati. Questo garantisce che snapshot e working tree condividano sempre lo stesso schema, ed elimina il classico scenario "snapshot crede di avere migrations X, Y, Z applicate ma nel tree corrente ce ne sono solo X, Y".

## Fase bake — dettaglio

### Preflight (`infra/scripts/seed-index-preflight.sh`)

Fail-fast prima di investire ore:

- `api/health` OK
- `smoldocling-service/health` OK
- `embedding-service/health` OK
- seed blob bucket raggiungibile e contiene almeno un `rulebooks/v1/*.pdf`
- `processing_jobs` vuota o contiene solo job terminali (evita di mescolare run precedenti non chiusi)

### Polling (`infra/scripts/seed-index-wait.sh`)

Parametri (env var con default):

| Var | Default | Scopo |
|---|---|---|
| `SEED_INDEX_TIMEOUT` | `10800` (3h) | timeout hard dell'intero loop |
| `SEED_INDEX_POLL` | `15` (s) | intervallo fra poll |
| `SEED_INDEX_FAILURE_PCT` | `15` | soglia fail% oltre la quale abort |
| `SEED_INDEX_ALLOW_PARTIAL` | `false` | permette dump anche con failed > threshold |

Logica:
1. Ogni `SEED_INDEX_POLL` secondi query aggregata su `processing_jobs`:
   - totale, completed, failed, dead-lettered, queued, running
2. Terminal quando `completed + failed + dead-lettered == totale && totale > 0`
3. Se supera `SEED_INDEX_TIMEOUT` → exit 124
4. Al termine calcola `fail_pct`; se `> SEED_INDEX_FAILURE_PCT` (default 15%) e `SEED_INDEX_ALLOW_PARTIAL=false` → dump SQL dei falliti + exit 1
5. Altrimenti prosegue

### Politica sui fallimenti parziali

- **Strict (default)**: abort se `fail% > 15%`. Soglia scelta in accordo con la realtà del set: ~20 rulebook su 136 possono fallire legittimamente (OCR scadente, layout esotico, lingua non supportata) senza invalidare lo snapshot complessivo. Se vuoi stringere, abbassa a 5% e investiga caso per caso.
- **Permissive opt-in**: `SEED_INDEX_ALLOW_PARTIAL=true` → dump procede, `failed_pdf_ids` nel sidecar elenca i PDF assenti, il consumer può decidere.
- **No auto-retry**: deliberatamente escluso. Retrymashing maschera bug veri (OOM, PDF corrotto, service giù).

### Dump (`infra/scripts/seed-index-dump.sh`)

```bash
docker exec -e PGPASSWORD="$POSTGRES_PASSWORD" meepleai-postgres \
  pg_dump -U postgres -d meepleai \
    -Fc --no-owner --no-privileges \
    --exclude-table-data='__EFMigrationsHistory' \
  > "$DUMP"
```

Sidecar costruito interrogando il DB post-seed per `pdf_count`, `chunk_count`, `embedding_count`, `failed_pdf_ids`, e leggendo le env var per `embedding_model`/`embedding_dim`.

Al termine: `sha256sum "$DUMP" > "$DUMP.sha256"`.

### Upload (`infra/scripts/seed-index-publish.sh`, target separato `make seed-index-publish`)

Ordine atomico:
1. `.dump` → bucket
2. `.dump.sha256` → bucket
3. `.meta.json` → bucket (per ultimo; se il processo muore qui, il consumer vede dump senza sidecar e skippa)
4. `latest.txt` aggiornato col basename

Retention: ultimi 3 snapshot, più vecchi rimossi.

### Target Makefile (bake)

```makefile
seed-index:                      ## Bake: indicizza tutti i PDF e produce snapshot locale
	@bash scripts/seed-index-preflight.sh
	@docker compose up -d postgres redis api smoldocling-service embedding-service
	@bash scripts/wait-for-healthy.sh api
	@bash scripts/seed-index-wait.sh         # polling processing_jobs
	@bash scripts/seed-index-dump.sh         # pg_dump + sidecar
	@echo "::notice:: snapshot in data/snapshots/"

seed-index-publish: seed-index   ## Bake + upload a seed blob bucket
	@bash scripts/seed-index-publish.sh
```

`seed-index` non chiama nessun endpoint HTTP — sfrutta l'auto-seed di `SeedOrchestrator.RunAsync` eseguito allo startup dell'API (`Program.cs:535`). `seed-index-publish` dipende da `seed-index` e aggiunge l'upload, così si possono tenere separati i due passi se vuoi validare localmente prima di pubblicare.

## Fase consume — dettaglio

### Target Makefile

```makefile
SNAPSHOT_DIR := data/snapshots
LATEST_MARKER := $(SNAPSHOT_DIR)/.latest

dev-from-snapshot:                        ## Dev env con seed pre-indicizzato (veloce)
	@bash scripts/snapshot-fetch.sh
	@bash scripts/snapshot-verify.sh
	@docker compose up -d postgres
	@bash scripts/wait-for-healthy.sh postgres
	@bash scripts/snapshot-restore.sh
	@SKIP_CATALOG_SEED=true docker compose up -d
	@echo "::notice:: dev env pronto (snapshot $$(cat $(LATEST_MARKER)))"

dev-from-snapshot-force:                  ## Come sopra ma azzera il volume postgres prima
	@docker compose down postgres -v
	@$(MAKE) dev-from-snapshot
```

Due entry point distinti: uno strict (rifiuta di sovrascrivere un DB non vuoto), uno esplicitamente distruttivo.

### `snapshot-fetch.sh`

Precedenze:
1. `SNAPSHOT_FILE` env var (override esplicito per testing)
2. Snapshot locale più recente in `data/snapshots/`
3. Download da seed blob bucket usando `snapshots/latest.txt`

Il download è atomic via `.partial` + `mv`, con verifica `sha256sum -c` prima del rename.

### `snapshot-verify.sh`

Legge `.meta.json` dello snapshot corrente e applica i 4 check del contract.

### `snapshot-restore.sh`

1. Guard: rifiuta se `information_schema.tables` public conta > 0 tabelle application (exit 10)
2. `dotnet ef database update` (schema + history dal working tree)
3. `pg_restore --data-only --disable-triggers` (carica i dati)
4. Smoke test:
   - `chunk_count` post-restore == sidecar `chunk_count` (exit 11 al mismatch)
   - nessun `text_chunks` orfano (no parent `pdf_documents`)
   - nessun `pgvector_embeddings` orfano (no parent `text_chunks`)
   - `array_length(embedding::real[], 1)` == sidecar `embedding_dim`

`--disable-triggers` è necessario per il multi-tabella con FK denso.

### Interazione con `SKIP_CATALOG_SEED`

Aggiungere short-circuit in `CatalogSeedLayer`:

```csharp
public async Task SeedAsync(SeedContext context, CancellationToken ct)
{
    if (context.Configuration.GetValue<bool>("SKIP_CATALOG_SEED"))
    {
        context.Logger.LogInformation("CatalogSeedLayer: SKIP_CATALOG_SEED=true, skipping");
        return;
    }
    // resto invariato
}
```

Default `false`, zero impatto sui flussi esistenti.

### Fallback esplicito

Qualunque errore in `make dev-from-snapshot` deve terminare con un messaggio che indica `make dev` come fallback. Una condizione "no snapshot disponibile" è normale nei primi commit di un branch nuovo.

## Testing strategy

### Livello 1 — Script contract tests (bats)

```
infra/scripts/tests/
├── snapshot-verify.bats
├── snapshot-fetch.bats
└── fixtures/
    ├── meta-good.json
    ├── meta-migration-drift.json
    ├── meta-model-drift.json
    └── meta-malformed.json
```

Mock di `aws`, `docker`, `jq`, `dotnet ef` via PATH override. Target: ogni exit code di `snapshot-verify.sh` ha almeno un test case.

### Livello 2 — e2e locale (`infra/scripts/tests/e2e-bake-and-consume.sh`)

Usa un manifest ridotto `Manifests/ci.yml` con 3 PDF piccoli per completare bake+consume in pochi minuti. Il profile switch usa il meccanismo esistente (`dev.yml` / `staging.yml` / `prod.yml`) estendendolo con `ci.yml`.

Flusso:
1. Reset `data/snapshots/` + volume postgres
2. `make seed-index CATALOG_PROFILE=ci SEED_INDEX_TIMEOUT=3600`
3. Verify `.dump` + `.meta.json` + `.sha256` presenti e coerenti
4. `docker compose down -v`
5. `make dev-from-snapshot SNAPSHOT_FILE=<output>`
6. `curl /api/v1/chat` con query nota → assert che il chunk atteso compare nel retrieval
7. `docker compose down -v`

### Livello 3 — smoke test post-restore

Già descritto in `snapshot-restore.sh`. Gira sempre, anche in uso normale del consume flow.

## Rollout

### Step 1 — bake path (opt-in)
- `make seed-index`, script bake, manifest `ci.yml`
- Nessuna modifica a `make dev`
- `SKIP_CATALOG_SEED` default false
- Zero impatto per chi non lancia esplicitamente `make seed-index`

### Step 2 — consume path (opt-in)
- `make dev-from-snapshot`
- Documentato come alternativa, non default
- Almeno 2 dev lo provano in locale prima di procedere

### Step 3 — default switch (FUORI SCOPE)
Esplicitamente non parte di questo spec. Valutazione separata dopo ≥1 settimana di uso reale.

## Documentazione da aggiornare

| File | Modifiche |
|---|---|
| `CLAUDE.md` Quick Reference | +righe `make seed-index` + `make dev-from-snapshot` |
| `CLAUDE.md` Troubleshooting | voce "snapshot drift" con exit codes |
| `docs/development/snapshot-seed-workflow.md` | nuovo: quando bake, quando consume, compat errors, rigenerazione |
| `docs/operations/operations-manual.md` | sezione "seed snapshot lifecycle" — chi rigenera, quando, dove pubblica |
| `infra/Makefile` | `make help` include nuovi target |
| `.gitignore` | `data/snapshots/*.dump`, `data/snapshots/*.sha256`, `data/snapshots/.latest`. I `.meta.json` NON vengono ignorati (log storico utile) |
| `infra/secrets/storage.secret.example` | `SEED_BLOB_BUCKET`, `SEED_BLOB_SNAPSHOTS_PREFIX` (se mancanti) |

## Rischi e mitigazioni

| Rischio | Prob. | Mitigazione |
|---|---|---|
| Snapshot diventa "ombra di verità" vs manifest aggiornato | alta | `dev_yml_sha256` warning + retention 3 snapshot |
| Dev dimentica di rigenerare dopo cambio embedding | media | `snapshot-verify.sh` blocca con exit 3 |
| Bake appeso in CI | media | `SEED_INDEX_TIMEOUT` 3h + exit 124 |
| Dump 150MB bloat nel repo | bassa | `.gitignore` + pre-commit hook rifiuta `*.dump > 10MB` |
| Snapshot pubblicato con chunk corrotti | bassa | smoke test post-restore (orfani + dim check) |
| Seed blob non accessibile → consume bloccato | media | fallback esplicito a `make dev` |

## Decisioni di design principali

| Aspetto | Decisione | Alternativa scartata | Perché |
|---|---|---|---|
| Tipo di artefatto | DB `.dump` | chunks+embeddings committati come JSONL per-rulebook | complessità loader custom, duplicazione stato |
| `pg_dump` mode | `-Fc` (custom) | `-Fp` (plain SQL) | compressione, restore selettivo, `--data-only` |
| `__EFMigrationsHistory` | escluso dal dump, ricreato via `ef update` | incluso | evita divergenze schema snapshot ↔ tree |
| Trigger bake | manuale (`make seed-index`) | GitHub Action automatica | richiede GPU runner, rimandato |
| Scope manifest | `dev.yml` + nuovo `ci.yml` | include `staging.yml`/`prod.yml` | prod non usa snapshot-seed, scope creep |
| Failure policy bake | strict + permissive opt-in | auto-retry | retry maschera bug reali |
| Policy consume DB non-vuoto | rifiuta con messaggio | sovrascrive sempre | evita di distruggere lavoro del dev |
| Fallback a `make dev` | esplicito in tutti i messaggi errore | fallback automatico | trasparenza, dev capisce cosa sta succedendo |

## Open questions (da chiudere prima del plan)

Nessuna bloccante. Punti da validare in implementazione:

1. ~~**Esistenza endpoint `/api/v1/admin/seeds/catalog`**~~: **RISOLTO** — verificato in `apps/api/src/Api/Routing/AdminSeedingEndpoints.cs`. L'endpoint reale è `POST /admin/seeding/orchestrate` protetto da `RequireAdminSessionFilter`. **Più importante**: il `SeedOrchestrator.RunAsync` viene invocato automaticamente allo startup in `Program.cs:535`, quindi il bake flow NON richiede una chiamata HTTP esplicita. Basta avviare lo stack e il seed parte da solo. L'endpoint rimane utile come re-trigger manuale (senza restart) ma non è nel path critico di `make seed-index`.
2. **`EMBEDDING_DIM` env var**: assunta presente in `embedding.secret`. Da verificare; se manca, va aggiunta.
3. **`SEED_BLOB_BUCKET` + credenziali**: il design assume che seed blob sia già configurato in storage.secret. Da verificare lo schema attuale di `storage.secret`.
4. **Profile switch `ci.yml`**: il meccanismo `CatalogProfile` deve supportare "ci" come valore. Da verificare enum/parsing esistente.

Queste non invalidano il design, ma vanno verificate nel plan implementativo per decidere se richiedono task aggiuntivi.
