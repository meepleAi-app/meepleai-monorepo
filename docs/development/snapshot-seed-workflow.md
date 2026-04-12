# Seed Snapshot Workflow

Flusso a due fasi per avere un ambiente dev con RAG funzionante senza aspettare l'indicizzazione runtime dei 136 PDF del manifest `dev.yml`.

**Spec di design**: `docs/superpowers/specs/2026-04-10-seed-pdf-pre-indexed-design.md`
**Plan implementativo**: `docs/superpowers/plans/2026-04-10-seed-pdf-pre-indexed.md`

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
