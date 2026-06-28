# Seed Snapshot Workflow

Flusso a due fasi per avere un ambiente dev con RAG funzionante senza aspettare l'indicizzazione runtime dei 136 PDF del manifest `dev.yml`.

**Spec di design**: `docs/for-developers/specs/2026-04-10-seed-pdf-pre-indexed-design.md`
**Plan implementativo**: `docs/for-developers/plans/2026-04-10-seed-pdf-pre-indexed.md`

## Quando usare questo flusso

- **Primo setup su una macchina nuova**: `make dev-from-snapshot` invece di `make dev`
- **Dopo `docker compose down -v`**: stesso
- **In CI per e2e test**: usa il manifest `ci.yml` (3 PDF, bake in pochi minuti)

## Architettura a due fasi

### Fase bake — produrre lo snapshot

Rara. Una volta per rilascio, o quando cambia: manifest `dev.yml`, modello di embedding, schema EF.

```bash
cd infra
make seed-index          # ore su CPU, molto meno con GPU
make seed-index-publish  # upload a seed blob bucket (opzionale)
```

Cosa fa `seed-index`:

1. `seed-index-preflight.sh` — sanity check (docker, jq, manifest, seed blob, stato processing_jobs)
2. `docker compose up -d postgres redis api embedding-service smoldocling-service`
3. `wait-for-healthy api` — attesa del boot
4. L'API al boot chiama automaticamente `SeedOrchestrator.RunAsync` (`Program.cs:535`) che enqueuea tutti i PDF del manifest
5. `seed-index-wait.sh` — poll su `processing_jobs` finché tutti terminal (timeout 3h, fail-fast a 15%)
6. `seed-index-dump.sh` — `pg_dump -Fc` (escluso `__EFMigrationsHistory`), sidecar `.meta.json`, sha256sum
7. Il file si trova in `data/snapshots/` con naming `meepleai_seed_<ts>_<model>_<commit>`

Parametri opzionali:

| Var | Default | Scopo |
|---|---|---|
| `SEED_INDEX_TIMEOUT` | `10800` (3h) | timeout hard del polling |
| `SEED_INDEX_POLL` | `15` (s) | intervallo di poll |
| `SEED_INDEX_FAILURE_PCT` | `15` | soglia fail% oltre la quale abort |
| `SEED_INDEX_ALLOW_PARTIAL` | `false` | permette dump con failures oltre soglia |

### Fase consume — usare lo snapshot

Default per qualunque developer.

```bash
cd infra
make dev-from-snapshot
```

Cosa fa:

1. `snapshot-fetch.sh` — cache-first (`SNAPSHOT_FILE` env > cache locale > download da bucket via `latest.txt`)
2. `snapshot-verify.sh` — compat gate (vedi sotto)
3. `docker compose up -d postgres` + wait healthy
4. `snapshot-restore.sh` — `dotnet ef database update` (schema) poi `pg_restore --data-only --disable-triggers` (dati), smoke test finale
5. `SKIP_CATALOG_SEED=true docker compose ... up -d` — avvia resto dello stack saltando il seed runtime

Se qualcosa fallisce, il messaggio di errore ti dice esattamente cosa fare. **Fallback sempre disponibile**: `make dev`.

#### Credenziali consumer (`storage.secret`)

Su una macchina **senza cache locale**, `snapshot-fetch.sh` scarica lo snapshot dal
bucket dedicato **`meepleai-seed-snapshots`**. Lo fa leggendo da `infra/secrets/storage.secret`:

| Key | Valore |
|---|---|
| `SEED_BLOB_BUCKET` | `meepleai-seed-snapshots` |
| `S3_ENDPOINT` | il tuo endpoint R2 EU (`https://<account>.eu.r2.cloudflarestorage.com`) |
| `S3_ACCESS_KEY` / `S3_SECRET_KEY` | un token R2 **"Object Read only"** scoped a `meepleai-seed-snapshots` |

⚠️ `snapshot-fetch.sh` usa le `S3_*` (non un pair `SEED_BLOB_S3_*`), quindi quelle
credenziali devono avere **read** sul bucket snapshot. Per un consumer puro
(nessun upload S3 in dev) la config raccomandata è:

```
STORAGE_PROVIDER=local          # l'app salva i PDF su disco; le S3_* servono SOLO a snapshot-fetch.sh
S3_ENDPOINT=https://<account>.eu.r2.cloudflarestorage.com
S3_ACCESS_KEY=<readonly key del token scoped a meepleai-seed-snapshots>
S3_SECRET_KEY=<readonly secret>
SEED_BLOB_BUCKET=meepleai-seed-snapshots
```

Template completo + commenti: `infra/secrets/storage.secret.example`. Se hai già
una snapshot in `data/snapshots/`, il fetch usa quella e **non** tocca il bucket
(le credenziali servono solo al primo download su macchina nuova).

> **Nota cron / publisher**: la pubblicazione (full-bake settimanale) usa i GH
> Actions secrets `SEED_BLOB_S3_*` del repo, non questo file. Vedi § *Secret richiesti*.

### Force reset

Se hai già un DB non vuoto e vuoi ripartire dallo snapshot, serve il force:

```bash
make dev-from-snapshot-force
```

⚠️ Distruttivo: cancella il volume postgres prima del restore.

## Compat gate — exit codes

Lo script `snapshot-verify.sh` blocca il restore con un exit code distinto per ogni tipo di drift:

| Exit | Significato | Azione |
|---|---|---|
| `0` | Tutto compatibile | procedi con restore |
| `1` | `.latest` o `.meta.json` mancante | `make seed-index` per rigenerare |
| `2` | EF migration head del working tree ≠ snapshot | `git checkout` del commit compatibile, oppure `make seed-index` per rigenerare |
| `3` | Embedding model del working tree ≠ snapshot | allinea `infra/secrets/embedding.secret` oppure rigenera |
| `4` | Embedding dimension mismatch | idem come exit 3 |
| `5` | `seed_table_schema_version` del sidecar ≠ `infra/seed-schema.version` (#2126 D9) | `make seed-index` — una tabella seedata è stata rinominata/ristrutturata dopo il bake |
| `6` | Sidecar invariant: `chunk_count ≠ embedding_count` (#2126 D7) | `make seed-index` — bake parziale, investiga embedding-service logs |
| `10` | DB non vuoto (guard di `snapshot-restore.sh`) | usa `make dev-from-snapshot-force` |
| `124` | Timeout del bake (`seed-index-wait.sh`) | aumenta `SEED_INDEX_TIMEOUT` o investiga perché i job non progrediscono |

Warning non bloccanti:
- `dev.yml` sha256 diverso dallo snapshot → eventuali giochi aggiunti dopo il bake NON sono indicizzati
- `failed_pdf_ids` non vuoto → lo snapshot contiene N PDF che sono falliti durante il bake (contabilizzati nel sidecar)

## Contract dello snapshot

### Naming

```
meepleai_seed_<timestamp>_<embedding_model_slug>_<commit>.dump
meepleai_seed_<timestamp>_<embedding_model_slug>_<commit>.dump.sha256
meepleai_seed_<timestamp>_<embedding_model_slug>_<commit>.meta.json
```

Esempio: `meepleai_seed_20260410T143022Z_sentence-transformers_all-MiniLM-L6-v2_3a75a9a10.dump`

### Sidecar `.meta.json`

```json
{
  "schema_version": "20260401_AddSearchVector",
  "ef_migration_head": "20260401_AddSearchVector",
  "seed_table_schema_version": 1,
  "embedding_model": "sentence-transformers/all-MiniLM-L6-v2",
  "embedding_dim": 384,
  "app_commit": "3a75a9a10",
  "created_at": "2026-04-10T14:22:00Z",
  "dev_yml_sha256": "…",
  "pdf_count": 136,
  "chunk_count": 18432,
  "embedding_count": 18432,
  "failed_pdf_ids": []
}
```

### `seed_table_schema_version` — quando bumparlo (#2126 D9)

Conta i rename/drop strutturali di tabelle che fanno parte del dump
(`pdf_documents`, `text_chunks`, `pgvector_embeddings`, `vector_documents`,
`shared_games`, e qualsiasi altra colpita da `EXCLUDE_TABLES` o
`GENERATED_TABLES`). Una sola fonte di verità: il file
`infra/seed-schema.version`, che `seed-index-dump.sh` legge al bake e
`snapshot-verify.sh` confronta al consume.

Bumpa il counter nella **stessa PR** che introduce uno di questi cambi:

- Rename di una tabella seedata (es. `embeddings` → `vector_documents`,
  storicamente 2026-05)
- Drop di una colonna seedata
- Cambio di tipo di una colonna seedata che invalida un dump esistente
- Cambio della shape della query usata da `seed-index-dump.sh` (lista
  tabelle dumpate, escluded, generated)

Il bump rende immediatamente "stale" qualunque snapshot precedente:
`snapshot-verify.sh` esce con `5` e suggerisce `make seed-index`, e il
prossimo `make dev-from-snapshot` di un developer ricarica
automaticamente il nuovo dump dal bucket. Nessuna confusione su «perché
RAG ritorna 0 risultati».

### DB-only — vincolo esplicito

Lo snapshot contiene **solo stato DB**. I PDF blob files vivono già idempotentemente nel seed blob bucket (`rulebooks/v1/*.pdf`). `make dev-from-snapshot` richiede che `STORAGE_PROVIDER` punti a un blob storage dove quei file sono raggiungibili, altrimenti i `FilePath` in `PdfDocumentEntity` puntano nel vuoto.

### Esclusione `__EFMigrationsHistory`

`pg_dump --exclude-table-data='__EFMigrationsHistory'` è intenzionale. Al restore si applica prima `dotnet ef database update` sul DB vuoto (scrive schema + history del working tree), poi `pg_restore --data-only` carica i dati. Questo garantisce che snapshot e working tree condividano sempre lo stesso schema.

## Layout `data/snapshots/`

```
data/snapshots/
├── meepleai_seed_20260410T143022Z_<model>_<commit>.dump         # gitignored
├── meepleai_seed_20260410T143022Z_<model>_<commit>.dump.sha256  # gitignored
├── meepleai_seed_20260410T143022Z_<model>_<commit>.meta.json    # committable (log storico)
└── .latest                                                       # gitignored, pointer al basename
```

## Retention sul seed blob bucket

Il target `make seed-index-publish` mantiene gli ultimi **3 snapshot** sul bucket (per `snapshots/` prefix). Quelli più vecchi vengono rimossi automaticamente (dump, sha, meta tutti insieme).

`snapshots/latest.txt` è un piccolo file testuale con il basename dello snapshot corrente, aggiornato ad ogni publish. `snapshot-fetch.sh` lo legge per scoprire cosa scaricare senza dover listare il bucket.

## Automated bake — GitHub Actions (#2126 D4)

Due workflow gestiscono il bake automatico:

| Workflow | Manifest | Trigger | Budget | Publish |
|---|---|---|---|---|
| `.github/workflows/seed-snapshot-bake-ci.yml` | `ci.yml` (3 PDF) | push/PR su seeder, migrations, scripts, manifests, `seed-schema.version` + `workflow_dispatch` | 30 min | opt-in (dispatch input `publish=true`) |
| `.github/workflows/seed-snapshot-bake-full.yml` | `dev.yml` (113+ PDF) | weekly cron (Sun 03:00 UTC) + `workflow_dispatch` | 360 min | default on (off-switch su dispatch) |

Razionale dei trigger (R-SNAP-FRESH-02 amendment):

- **CI smoke (push-trigger)** — cattura *regressions del pipeline di bake* a basso costo. Il `ci.yml` ha 3 PDF, lo bake è < 15 min sul runner Ubuntu standard. Se questo workflow rompe, una PR rompe il bake reale: blocca prima della merge.
- **Full bake (weekly cron)** — *anti-drift heartbeat*. Anche se nessuno tocca `dev.yml` per un mese, ogni domenica notte lo snapshot pubblicato è ≤ 7 giorni vecchio. La SLO di freschezza nasce da qui.
- **Workflow_dispatch** — escape hatch: «ho appena mergiato una migration che bumpa `seed-schema.version`, non aspetto domenica».

### Secret richiesti

I 2 workflow leggono `secrets.SEED_BLOB_*` solo quando *pubblicano*. Da configurare in **Settings → Secrets and variables → Actions** del repo:

| Secret | Esempio | Note |
|---|---|---|
| `SEED_BLOB_S3_ENDPOINT` | `https://<accountid>.r2.cloudflarestorage.com` | per R2; AWS S3 = vuoto / regione |
| `SEED_BLOB_S3_ACCESS_KEY` | `…` | access key con WRITE sul bucket |
| `SEED_BLOB_S3_SECRET_KEY` | `…` | secret key gemella |
| `SEED_BLOB_BUCKET` | `meepleai-seed-snapshots` | name del bucket |

Senza i secret, **entrambi** i bake girano lo stesso ma **non pubblicano**: lo step `Publish to seed blob bucket` fa **soft-skip** (warning + step summary, exit 0) e lo snapshot verificato resta scaricabile come **workflow artifact** (90gg). Quando i secret SONO presenti il publish è strict: un errore reale (creds errate, R2 down) fallisce il job. Questo evita sia un cron permanentemente rosso sia il rischio di sovrascrivere `latest.txt` con uno snapshot vuoto (#2516).

### Cosa succede se il bake fallisce

`snapshot-verify.sh` exit codes (vedi sopra) si applicano *prima* del publish. Se il sidecar fallisce i gate (`5` = schema-version drift, `6` = invariant violation), il publish step è skippato dal workflow. `latest.txt` continua a puntare allo snapshot precedente — degradato, non broken. Il maintainer riceve un fail su GHA Actions, e l'artifact metadata è retained 90 giorni per debug.

> Tip: per riprodurre localmente uno step del workflow, lancialo come al solito sul tuo host:
> ```bash
> cd infra
> SEED_CATALOG_MANIFEST_OVERRIDE=ci SEED_INDEX_TIMEOUT=1500 make seed-index
> bash scripts/snapshot-verify.sh
> ```

## Audit trail (#2126 D6)

Ogni publish riuscito appende una riga a `data/snapshots/AUDIT.md` (committable markdown). La riga viene scritta **dopo** l'upload di dump+sha+sidecar+`latest.txt` e dopo la rotation — un fail su qualsiasi step precedente esce con `set -e` senza scrivere, garantendo che il trail combaci con il bucket.

Schema della tabella:

```markdown
| Published at | Basename | App commit | EF migration | seed_schema | PDFs | Chunks | Embeddings | Model | Published by |
```

`Published by` è auto-risolto: `GITHUB_ACTOR` se siamo in GHA, `git config user.email` localmente, `whoami` come fallback. Niente token.

Query rapide:
- *«Chi ha pubblicato lo snapshot del 15 aprile?»* → `git blame data/snapshots/AUDIT.md` sulla riga del 2026-04-15.
- *«Cronologia ultimi 30 giorni»* → `git log --since='30 days ago' -- data/snapshots/AUDIT.md`.
- *«Quale snapshot serviva il dev X durante l'incident del 2026-06-11 alle 14:00?»* → cerca la riga il cui `Published at` precede 14:00 (era il `latest.txt` puntato).

## Freshness & ownership requirements

Formal requirements governing snapshot freshness and bake ownership. These
requirements are enforced or surfaced by the tooling described in this document.

### R-SNAP-FRESH-01 — Migration alignment

> A published snapshot's `ef_migration_head` MUST equal `main`'s HEAD EF
> migration at the time the first developer runs `make dev-from-snapshot` after
> a migration merge.

**Enforcement**: the D4 bake workflow (`seed-snapshot-bake-ci.yml`) triggers on
push to paths that include migration files and `seed-schema.version`. When a
migration lands, the smoke bake runs automatically and, if `publish=true`, moves
`latest.txt` to a snapshot whose `ef_migration_head` matches the new HEAD.
Developers who run `make dev-from-snapshot` before the bake completes will be
blocked by `snapshot-verify.sh` exit code `2` (migration drift) and prompted to
wait for the workflow or run a local bake.

### R-SNAP-FRESH-02 — Age SLO (formalized)

> The delta `created_at → today` of the published snapshot MUST be ≤ 7 days.
> A snapshot aged 7–30 days is **warning** (orange); ≥ 30 days or missing is
> **stale** (red).

**Enforcement**:

- `seed-status.sh` surfaces the age in every mode (`default`, `--brief`,
  `--badge`) using the thresholds `WARNING_AGE_DAYS=7` and `STALE_AGE_DAYS=30`.
- `seed-status.sh --strict` exits `1` when the snapshot is stale or missing,
  blocking CI pipelines that depend on a fresh snapshot.
- `snapshot-verify.sh` exit codes (downstream of the bake) also gate the
  publish step — a half-baked snapshot never replaces `latest.txt`.
- The weekly cron on `seed-snapshot-bake-full.yml` (Sundays 03:00 UTC) is the
  heartbeat that keeps the published snapshot within the 7-day SLO even when no
  migration or manifest changes are made.
- The README snapshot-freshness badge (`make seed-status-badge`) provides a
  visible signal to developers browsing the repository. Refresh it after any
  successful bake.

### R-SNAP-OWNER-01 — Bake ownership

> Bake ownership is **shared** between:
>
> 1. **Merging developer** — responsible for triggering a fresh bake (or
>    verifying the push-triggered CI smoke completed successfully) when merging
>    a PR that changes `dev.yml`, EF migrations, or `seed-schema.version`.
> 2. **Rotating release captain** — responsible for ensuring the weekly cron
>    (`seed-snapshot-bake-full.yml`) is healthy and that `latest.txt` points to
>    a snapshot within the 7-day SLO before a sprint release.

**Practical checklist for the merging developer**:
- After merging a migration PR, verify `seed-snapshot-bake-ci.yml` completed
  green (or manually dispatch `seed-snapshot-bake-full.yml`).
- If the bake fails, open a follow-up issue tagged `seed/snapshot` and announce
  in the team channel that `make dev-from-snapshot` may surface a schema-drift
  warning until resolved.

**Practical checklist for the release captain**:
- On release day: `make seed-status` to check age.
- If ≥ 7 days: `gh workflow run seed-snapshot-bake-full.yml --field publish=true`.
- After a successful bake: `make seed-status-badge` (from `infra/`) and commit
  the updated badge in a `chore(infra): refresh snapshot freshness badge` PR.

See also: `CONTRIBUTING.md` §"Seed snapshot bake ownership".

## Testing

### Manual e2e con `ci.yml`

Il manifest `ci.yml` contiene solo 3 PDF (Love Letter, Patchwork, Jaipur) e permette un bake+consume completo in pochi minuti. Per testare il flusso:

```bash
cd infra
SEED_CATALOG_MANIFEST_OVERRIDE=ci SEED_INDEX_TIMEOUT=1800 make seed-index

# verifica
ls -lh data/snapshots/
cat data/snapshots/*.meta.json | jq .

# consume su clean env
docker compose down postgres -v
make dev-from-snapshot

# smoke
curl -s http://localhost:8080/health | jq .
docker exec meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT COUNT(*) FROM text_chunks;"
```

### Bats unit test (compat gate)

`infra/scripts/tests/snapshot-verify.bats` copre tutti gli exit code con fixture JSON isolate. Richiede `bats-core`:

```bash
# Installa bats-core (Windows: choco install bats, macOS: brew install bats-core)
bats infra/scripts/tests/snapshot-verify.bats
```

## Quando NON usare lo snapshot

- Stai cambiando lo schema EF e vuoi testare la tua migration su dati runtime → usa `make dev`
- Stai sviluppando il pipeline di indicizzazione → usa `make dev`
- Vuoi un DB completamente pulito da debug → `docker compose down -v && make dev`
