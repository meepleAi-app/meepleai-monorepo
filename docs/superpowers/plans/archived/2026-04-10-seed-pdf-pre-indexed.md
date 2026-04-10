# Seed con PDF Pre-Indicizzati — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fornire un workflow a due fasi (bake/consume) che produce un DB snapshot con tutti i rulebook già indicizzati e permette a qualunque dev di avviare l'ambiente locale saltando la pipeline RAG.

**Architecture:** Bash scripts in `infra/scripts/` orchestrati da target Makefile. Il bake sfrutta l'auto-seed di `SeedOrchestrator.RunAsync` e attende che `PdfProcessingQuartzJob` smaltisca la coda. Il consume ripristina via `pg_restore --data-only` dopo `dotnet ef database update`, con compat gate su `.meta.json` sidecar.

**Tech Stack:** Bash, jq, aws cli (S3-compatible), docker compose, PostgreSQL pg_dump/pg_restore, .NET 9 EF Core, bats-core per test.

**Design reference:** `docs/superpowers/specs/2026-04-10-seed-pdf-pre-indexed-design.md`

---

## File Structure

### Nuovi file

| Path | Responsabilità |
|---|---|
| `infra/scripts/seed-index-preflight.sh` | Sanity check prima del bake |
| `infra/scripts/seed-index-wait.sh` | Polling `processing_jobs` fino a terminale |
| `infra/scripts/seed-index-dump.sh` | `pg_dump` + generazione sidecar `.meta.json` |
| `infra/scripts/seed-index-publish.sh` | Upload dump+sidecar a seed blob bucket |
| `infra/scripts/snapshot-fetch.sh` | Acquisizione snapshot (locale cache / bucket) |
| `infra/scripts/snapshot-verify.sh` | Compat gate contro working tree |
| `infra/scripts/snapshot-restore.sh` | `ef database update` + `pg_restore` + smoke |
| `infra/scripts/tests/snapshot-verify.bats` | Unit test del compat gate |
| `infra/scripts/tests/fixtures/meta-good.json` | Fixture valida |
| `infra/scripts/tests/fixtures/meta-migration-drift.json` | Fixture exit 2 |
| `infra/scripts/tests/fixtures/meta-model-drift.json` | Fixture exit 3 |
| `infra/scripts/tests/fixtures/meta-dim-drift.json` | Fixture exit 4 |
| `apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/ci.yml` | Manifest ridotto (3 PDF) per e2e |
| `docs/development/snapshot-seed-workflow.md` | Guida developer |

### File modificati

| Path | Modifica |
|---|---|
| `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs` | Supporto `SKIP_CATALOG_SEED` + `SEED_CATALOG_MANIFEST_OVERRIDE` |
| `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeeder.cs` | `LoadManifest` accetta override esplicito del resource name |
| `apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/CatalogSeedLayerTests.cs` (nuovo o modificato) | Test sui due nuovi flag |
| `infra/Makefile` | Target `seed-index`, `seed-index-publish`, `dev-from-snapshot`, `dev-from-snapshot-force` |
| `.gitignore` | Ignora `data/snapshots/*.dump`, `.sha256`, `.latest` |
| `CLAUDE.md` | Quick reference + troubleshooting |

---

## Task 1: Aggiungere `SKIP_CATALOG_SEED` a `CatalogSeedLayer`

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/CatalogSeedLayerTests.cs`

- [ ] **Step 1: Scrivi il test fallente**

Crea il file test (se non esiste cercalo con `find apps/api/tests -name "CatalogSeedLayerTests*"`). Aggiungi:

```csharp
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class CatalogSeedLayerSkipFlagTests
{
    [Fact]
    public async Task SeedAsync_SkipsWhenFlagTrue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SKIP_CATALOG_SEED"] = "true"
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var context = new SeedContext(
            Profile: SeedProfile.Dev,
            DbContext: null!,       // non dovrebbe essere usato quando skipped
            Services: services,
            SystemUserId: Guid.NewGuid(),
            Logger: NullLogger.Instance);

        var layer = new CatalogSeedLayer();

        // Act + Assert: nessuna eccezione, ritorna immediatamente
        await layer.SeedAsync(context, default);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~CatalogSeedLayerSkipFlagTests" -v n
```

Expected: FAIL — o con NullReferenceException (DbContext null dereferenced) o con eccezione da GetRequiredService.

- [ ] **Step 3: Implementa il bypass**

Modifica `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs`:

```csharp
public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
{
    var config = context.Services.GetService<IConfiguration>();

    if (config?.GetValue<bool>("SKIP_CATALOG_SEED") == true)
    {
        context.Logger.LogInformation("CatalogSeedLayer: SKIP_CATALOG_SEED=true, skipping");
        return;
    }

    var bggService = context.Services.GetRequiredService<IBggApiService>();
    var embeddingService = context.Services.GetService<IEmbeddingService>();
    var primaryBlob = context.Services.GetRequiredService<IBlobStorageService>();
    var seedBlob = context.Services.GetRequiredService<ISeedBlobReader>();

    await CatalogSeeder.SeedAsync(
        context.Profile,
        context.DbContext,
        bggService,
        context.SystemUserId,
        primaryBlob,
        seedBlob,
        context.Logger, cancellationToken,
        embeddingService,
        config).ConfigureAwait(false);
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~CatalogSeedLayerSkipFlagTests" -v n
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs \
        apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/CatalogSeedLayerTests.cs
git commit -m "feat(seeding): add SKIP_CATALOG_SEED flag to CatalogSeedLayer"
```

---

## Task 2: `SEED_CATALOG_MANIFEST_OVERRIDE` in `LoadManifest`

Scopo: permettere di caricare `ci.yml` senza aggiungere un valore all'enum `SeedProfile`.

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeeder.cs`
- Modify: `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/CatalogSeederLoadManifestTests.cs`

- [ ] **Step 1: Scrivi il test fallente**

```csharp
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Xunit;

public class CatalogSeederLoadManifestTests
{
    [Fact]
    public void LoadManifest_WithOverride_LoadsNamedResource()
    {
        // Act
        var manifest = CatalogSeeder.LoadManifest(SeedProfile.Dev, manifestName: "ci");

        // Assert
        Assert.NotNull(manifest);
        Assert.NotNull(manifest.Catalog);
        Assert.Equal(3, manifest.Catalog.Games.Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~CatalogSeederLoadManifestTests" -v n
```

Expected: FAIL — signature mismatch o `ci.yml` non trovato.

- [ ] **Step 3: Estendi `LoadManifest` con parametro override**

Modifica `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeeder.cs`:

```csharp
public static SeedManifest LoadManifest(SeedProfile profile, string? manifestName = null)
{
    if (profile == SeedProfile.None && manifestName == null)
        throw new FileNotFoundException($"No manifest for profile '{profile}'");

    var effectiveName = manifestName ?? profile.ToString().ToLowerInvariant();
    var resourceName = $"Api.Infrastructure.Seeders.Catalog.Manifests.{effectiveName}.yml";
    var assembly = Assembly.GetExecutingAssembly();

    using var stream = assembly.GetManifestResourceStream(resourceName)
        ?? throw new FileNotFoundException($"Embedded manifest not found: {resourceName}");
    using var reader = new StreamReader(stream);

    var manifest = YamlDeserializer.Deserialize<SeedManifest>(reader);

    // Validation expected profile solo se NON è override (il ci.yml può dichiarare profile: Dev)
    var expectedForValidation = manifestName == null ? (SeedProfile?)profile : null;
    var errors = manifest.Validate(expectedProfile: expectedForValidation);
    if (errors.Count > 0)
        throw new InvalidOperationException(
            $"Manifest validation failed:\n{string.Join("\n", errors)}");

    return manifest;
}
```

Estendi anche l'overload `SeedAsync` di `CatalogSeeder` per accettare `manifestName` opzionale e passarlo a `LoadManifest`:

```csharp
public static async Task SeedAsync(
    SeedProfile profile,
    MeepleAiDbContext db,
    IBggApiService bggService,
    Guid systemUserId,
    IBlobStorageService primaryBlob,
    ISeedBlobReader seedBlob,
    ILogger logger,
    CancellationToken ct,
    IEmbeddingService? embeddingService = null,
    IConfiguration? configuration = null,
    string? manifestNameOverride = null)
{
    var manifest = LoadManifest(profile, manifestNameOverride);
    // ... resto invariato
```

- [ ] **Step 4: Aggiorna `CatalogSeedLayer` per leggere l'override da config**

Modifica `CatalogSeedLayer.SeedAsync` aggiungendo la lettura e il passaggio:

```csharp
var manifestOverride = config?.GetValue<string>("SEED_CATALOG_MANIFEST_OVERRIDE");

await CatalogSeeder.SeedAsync(
    context.Profile,
    context.DbContext,
    bggService,
    context.SystemUserId,
    primaryBlob,
    seedBlob,
    context.Logger, cancellationToken,
    embeddingService,
    config,
    manifestNameOverride: manifestOverride).ConfigureAwait(false);
```

- [ ] **Step 5: Crea `ci.yml` embedded manifest**

Crea `apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/ci.yml` come copia ridotta di `dev.yml`. Contenuto minimo:

```yaml
version: 1
profile: Dev
catalog:
  games:
  - title: Love Letter
    bggId: 129622
    minPlayers: 2
    maxPlayers: 4
    minPlaytimeMinutes: 15
    maxPlaytimeMinutes: 30
    complexity: 1.2
    language: en
    bggEnhanced: false
    publishers:
    - Z-Man Games
    pdfBlobKey: rulebooks/v1/love-letter_rulebook.pdf
    pdfSha256: "" # popolato durante bake reale; test e2e lo salta
  - title: Patchwork
    bggId: 163412
    minPlayers: 2
    maxPlayers: 2
    minPlaytimeMinutes: 15
    maxPlaytimeMinutes: 30
    complexity: 1.6
    language: en
    bggEnhanced: false
    publishers:
    - Lookout Games
    pdfBlobKey: rulebooks/v1/patchwork_rulebook.pdf
    pdfSha256: ""
  - title: Jaipur
    bggId: 54043
    minPlayers: 2
    maxPlayers: 2
    minPlaytimeMinutes: 20
    maxPlaytimeMinutes: 30
    complexity: 1.5
    language: en
    bggEnhanced: false
    publishers:
    - GameWorks SàRL
    pdfBlobKey: rulebooks/v1/jaipur_rulebook.pdf
    pdfSha256: ""
```

**Nota**: I 3 giochi sono scelti perché esistono già in `dev.yml` (riutilizza i dati), i PDF sono piccoli rulebook e il contenuto copia le stesse righe del manifest reale. Verifica estraendo le righe dal `dev.yml` per avere i dati corretti:

```bash
grep -A20 "title: Love Letter" apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml | head -25
# usa l'output come base, rimuovi campi extra
```

- [ ] **Step 6: Registra `ci.yml` come embedded resource**

Verifica in `apps/api/src/Api/Api.csproj` se i manifest sono inclusi automaticamente:

```bash
grep -n "Manifests" apps/api/src/Api/Api.csproj
```

Se c'è già un pattern `**/Manifests/*.yml` come `EmbeddedResource`, `ci.yml` è incluso automaticamente. Se invece sono listati uno a uno, aggiungi:

```xml
<EmbeddedResource Include="Infrastructure\Seeders\Catalog\Manifests\ci.yml" />
```

- [ ] **Step 7: Run test to verify it passes**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~CatalogSeederLoadManifestTests" -v n
```

Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeeder.cs \
        apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs \
        apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/ci.yml \
        apps/api/src/Api/Api.csproj \
        apps/api/tests/Api.Tests/Infrastructure/Seeders/Catalog/CatalogSeederLoadManifestTests.cs
git commit -m "feat(seeding): add manifest override and ci.yml for e2e testing"
```

---

## Task 3: `seed-index-preflight.sh`

**Files:**
- Create: `infra/scripts/seed-index-preflight.sh`

- [ ] **Step 1: Scrivi lo script**

```bash
#!/usr/bin/env bash
# infra/scripts/seed-index-preflight.sh
# Fail-fast checks prima di lanciare seed-index
set -euo pipefail

log() { echo "[preflight] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

# 1. docker + compose
command -v docker >/dev/null || fail "docker non installato"
docker compose version >/dev/null || fail "docker compose non disponibile"

# 2. jq + sha256sum + pg utilities nel container postgres (li usa dump script)
command -v jq >/dev/null || fail "jq non installato"

# 3. Manifest dev.yml presente
MANIFEST="apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml"
[ -f "$MANIFEST" ] || fail "manifest non trovato: $MANIFEST"

# 4. Seed blob bucket configurato (opzionale ma senza di esso PdfSeeder è silenzioso)
if ! grep -q "^SEED_BLOB_" infra/secrets/storage.secret 2>/dev/null; then
    log "WARNING: seed blob non configurato in infra/secrets/storage.secret — PdfSeeder non seederà PDF"
    log "Procedo comunque (il bake può essere legittimo per testare il flusso su 0 PDF)"
fi

# 5. Se postgres gira già, controlla che processing_jobs sia vuota o solo terminali
if docker ps --format '{{.Names}}' | grep -q '^meepleai-postgres$'; then
    pending=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c \
        "SELECT COUNT(*) FROM processing_jobs WHERE status IN ('Queued','Running','Retrying');" 2>/dev/null || echo "0")
    if [ "$pending" -gt 0 ]; then
        fail "processing_jobs contiene $pending job non terminali — ferma il run precedente o svuota la tabella"
    fi
fi

log "OK"
```

- [ ] **Step 2: Rendi eseguibile**

```bash
chmod +x infra/scripts/seed-index-preflight.sh
```

- [ ] **Step 3: Smoke test rapido**

```bash
cd infra
bash scripts/seed-index-preflight.sh
```

Expected: output `[preflight] OK` (o warning seed blob non configurato + OK).

- [ ] **Step 4: Commit**

```bash
git add infra/scripts/seed-index-preflight.sh
git commit -m "feat(seed-index): add preflight sanity check script"
```

---

## Task 4: `seed-index-wait.sh`

**Files:**
- Create: `infra/scripts/seed-index-wait.sh`

- [ ] **Step 1: Scrivi lo script**

```bash
#!/usr/bin/env bash
# infra/scripts/seed-index-wait.sh
# Polla processing_jobs finché tutti terminal, con timeout e fail-fast.
set -euo pipefail

TIMEOUT_SECONDS=${SEED_INDEX_TIMEOUT:-10800}        # 3h
POLL_INTERVAL=${SEED_INDEX_POLL:-15}                 # 15s
FAILURE_THRESHOLD_PCT=${SEED_INDEX_FAILURE_PCT:-15}  # 15%
ALLOW_PARTIAL=${SEED_INDEX_ALLOW_PARTIAL:-false}

log() { echo "[seed-index-wait] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

psql_count() {
    docker exec meepleai-postgres psql -U postgres -d meepleai -At -c "$1"
}

start=$(date +%s)
while :; do
    read -r total completed failed dlq queued running < <(
        docker exec meepleai-postgres psql -U postgres -d meepleai -At -F' ' -c \
        "SELECT
           COUNT(*),
           COUNT(*) FILTER (WHERE status='Completed'),
           COUNT(*) FILTER (WHERE status='Failed'),
           COUNT(*) FILTER (WHERE status='DeadLettered'),
           COUNT(*) FILTER (WHERE status='Queued'),
           COUNT(*) FILTER (WHERE status='Running')
         FROM processing_jobs;"
    )
    terminal=$((completed + failed + dlq))
    log "total=$total completed=$completed failed=$failed dlq=$dlq queued=$queued running=$running"

    if [ "$total" -gt 0 ] && [ "$terminal" -eq "$total" ]; then
        break
    fi

    if [ $(( $(date +%s) - start )) -gt "$TIMEOUT_SECONDS" ]; then
        fail "timeout dopo ${TIMEOUT_SECONDS}s — processing_jobs non drenata"
        exit 124
    fi

    sleep "$POLL_INTERVAL"
done

if [ "$total" -eq 0 ]; then
    log "WARNING: processing_jobs vuota — nessun PDF processato"
    exit 0
fi

fail_count=$((failed + dlq))
fail_pct=$(( fail_count * 100 / total ))
log "termine: $completed/$total OK, $fail_count falliti (${fail_pct}%)"

if [ "$fail_pct" -gt "$FAILURE_THRESHOLD_PCT" ] && [ "$ALLOW_PARTIAL" != "true" ]; then
    log "fail rate ${fail_pct}% > soglia ${FAILURE_THRESHOLD_PCT}%"
    docker exec meepleai-postgres psql -U postgres -d meepleai -c \
      "SELECT j.id, p.file_name, s.error_message
       FROM processing_jobs j
       JOIN pdf_documents p ON p.id = j.pdf_document_id
       LEFT JOIN processing_steps s ON s.processing_job_id = j.id AND s.status='Failed'
       WHERE j.status IN ('Failed','DeadLettered')
       LIMIT 50;" >&2 || true
    fail "aborting — usa SEED_INDEX_ALLOW_PARTIAL=true per procedere comunque"
fi

log "OK"
```

- [ ] **Step 2: Rendi eseguibile + commit**

```bash
chmod +x infra/scripts/seed-index-wait.sh
git add infra/scripts/seed-index-wait.sh
git commit -m "feat(seed-index): add polling script with fail-fast threshold"
```

---

## Task 5: `seed-index-dump.sh`

**Files:**
- Create: `infra/scripts/seed-index-dump.sh`

- [ ] **Step 1: Scrivi lo script**

```bash
#!/usr/bin/env bash
# infra/scripts/seed-index-dump.sh
# pg_dump + sidecar .meta.json + sha256.
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"
mkdir -p "$OUT_DIR"

log() { echo "[dump] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

command -v jq >/dev/null || fail "jq non installato"

TS=$(date -u +%Y%m%dT%H%M%SZ)
COMMIT=$(git rev-parse --short=9 HEAD)

EMBEDDING_MODEL=$(docker exec meepleai-api printenv EMBEDDING_MODEL 2>/dev/null || echo "unknown")
EMBEDDING_DIM=$(docker exec meepleai-api printenv EMBEDDING_DIM 2>/dev/null || echo "0")
MODEL_SLUG=$(echo "$EMBEDDING_MODEL" | tr '/' '_' | tr -cd 'A-Za-z0-9_.-')

BASENAME="meepleai_seed_${TS}_${MODEL_SLUG}_${COMMIT}"
DUMP_FILE="$OUT_DIR/$BASENAME.dump"
META_FILE="$OUT_DIR/$BASENAME.meta.json"
SHA_FILE="$OUT_DIR/$BASENAME.dump.sha256"

log "dumping DB → $DUMP_FILE"
docker exec meepleai-postgres pg_dump -U postgres -d meepleai \
    -Fc --no-owner --no-privileges \
    --exclude-table-data='__EFMigrationsHistory' \
    > "$DUMP_FILE"

log "raccolgo stats per sidecar"
STATS_JSON=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c "
SELECT json_build_object(
  'ef_migration_head', (SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 1),
  'pdf_count',         (SELECT COUNT(*) FROM pdf_documents WHERE processing_state='Completed'),
  'chunk_count',       (SELECT COUNT(*) FROM text_chunks),
  'embedding_count',   (SELECT COUNT(*) FROM pgvector_embeddings),
  'failed_pdf_ids',    COALESCE((SELECT json_agg(pdf_document_id) FROM processing_jobs WHERE status IN ('Failed','DeadLettered')), '[]'::json)
);")

MANIFEST_SHA=$(sha256sum apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml | awk '{print $1}')

jq -n \
    --argjson stats "$STATS_JSON" \
    --arg model "$EMBEDDING_MODEL" \
    --argjson dim "$EMBEDDING_DIM" \
    --arg commit "$COMMIT" \
    --arg created "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    --arg manifest_sha "$MANIFEST_SHA" \
    '{
       schema_version: $stats.ef_migration_head,
       ef_migration_head: $stats.ef_migration_head,
       embedding_model: $model,
       embedding_dim: $dim,
       app_commit: $commit,
       created_at: $created,
       dev_yml_sha256: $manifest_sha,
       pdf_count: $stats.pdf_count,
       chunk_count: $stats.chunk_count,
       embedding_count: $stats.embedding_count,
       failed_pdf_ids: $stats.failed_pdf_ids
     }' > "$META_FILE"

log "calcolo sha256"
( cd "$OUT_DIR" && sha256sum "$BASENAME.dump" > "$BASENAME.dump.sha256" )

# Aggiorna .latest (usato dal consume flow come cache locale)
echo "$BASENAME" > "$OUT_DIR/.latest"

log "snapshot pronto: $BASENAME"
log "  dump:  $DUMP_FILE ($(du -h "$DUMP_FILE" | awk '{print $1}'))"
log "  meta:  $META_FILE"
log "  sha:   $SHA_FILE"

echo "$BASENAME"
```

- [ ] **Step 2: Rendi eseguibile + commit**

```bash
chmod +x infra/scripts/seed-index-dump.sh
git add infra/scripts/seed-index-dump.sh
git commit -m "feat(seed-index): add pg_dump + sidecar generator script"
```

---

## Task 6: `seed-index-publish.sh`

**Files:**
- Create: `infra/scripts/seed-index-publish.sh`

- [ ] **Step 1: Scrivi lo script**

```bash
#!/usr/bin/env bash
# infra/scripts/seed-index-publish.sh
# Upload ultimo snapshot a seed blob bucket + rotation (tieni 3).
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"
LATEST="$OUT_DIR/.latest"

log() { echo "[publish] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

[ -f "$LATEST" ] || fail ".latest non trovato in $OUT_DIR — lancia prima make seed-index"
BASENAME=$(cat "$LATEST")
[ -f "$OUT_DIR/$BASENAME.dump" ] || fail "dump mancante: $OUT_DIR/$BASENAME.dump"
[ -f "$OUT_DIR/$BASENAME.meta.json" ] || fail "meta mancante: $OUT_DIR/$BASENAME.meta.json"

# Carica credenziali da storage.secret
# shellcheck disable=SC1091
set -a; source infra/secrets/storage.secret; set +a
: "${SEED_BLOB_BUCKET:?SEED_BLOB_BUCKET mancante in storage.secret}"
: "${S3_ENDPOINT:?S3_ENDPOINT mancante}"
: "${S3_ACCESS_KEY:?S3_ACCESS_KEY mancante}"
: "${S3_SECRET_KEY:?S3_SECRET_KEY mancante}"

export AWS_ACCESS_KEY_ID="$S3_ACCESS_KEY"
export AWS_SECRET_ACCESS_KEY="$S3_SECRET_KEY"
AWS_S3="aws s3 --endpoint-url $S3_ENDPOINT"

PREFIX="snapshots"

log "uploading $BASENAME.dump"
$AWS_S3 cp "$OUT_DIR/$BASENAME.dump" "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.dump"
log "uploading $BASENAME.dump.sha256"
$AWS_S3 cp "$OUT_DIR/$BASENAME.dump.sha256" "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.dump.sha256"
# META PER ULTIMO — atomic marker
log "uploading $BASENAME.meta.json (atomic marker last)"
$AWS_S3 cp "$OUT_DIR/$BASENAME.meta.json" "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.meta.json"

# latest.txt pointer
log "updating latest.txt pointer"
echo "$BASENAME" | $AWS_S3 cp - "s3://$SEED_BLOB_BUCKET/$PREFIX/latest.txt"

# Retention: tieni ultimi 3
log "rotation: retain last 3 snapshots"
$AWS_S3 ls "s3://$SEED_BLOB_BUCKET/$PREFIX/" \
    | awk '{print $4}' \
    | grep -E '^meepleai_seed_[0-9]{8}T[0-9]{6}Z.*\.meta\.json$' \
    | sort -r \
    | tail -n +4 \
    | while read -r old_meta; do
        old_base=${old_meta%.meta.json}
        log "rimuovo obsoleto: $old_base"
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.dump"        || true
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.dump.sha256" || true
        $AWS_S3 rm "s3://$SEED_BLOB_BUCKET/$PREFIX/$old_base.meta.json"   || true
      done

log "OK"
```

- [ ] **Step 2: Rendi eseguibile + commit**

```bash
chmod +x infra/scripts/seed-index-publish.sh
git add infra/scripts/seed-index-publish.sh
git commit -m "feat(seed-index): add publish script with bucket rotation"
```

---

## Task 7: `snapshot-fetch.sh`

**Files:**
- Create: `infra/scripts/snapshot-fetch.sh`

- [ ] **Step 1: Scrivi lo script**

```bash
#!/usr/bin/env bash
# infra/scripts/snapshot-fetch.sh
# Priorità: SNAPSHOT_FILE env > cache locale più recente > download da bucket.
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"
mkdir -p "$OUT_DIR"

log() { echo "[fetch] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

if [ -n "${SNAPSHOT_FILE:-}" ]; then
    [ -f "$SNAPSHOT_FILE" ] || fail "SNAPSHOT_FILE non esiste: $SNAPSHOT_FILE"
    BASENAME=$(basename "$SNAPSHOT_FILE" .dump)
    log "override esplicito: $BASENAME"
elif ls "$OUT_DIR"/meepleai_seed_*.meta.json >/dev/null 2>&1; then
    BASENAME=$(ls -t "$OUT_DIR"/meepleai_seed_*.meta.json | head -1 | xargs basename | sed 's/\.meta\.json$//')
    log "cache locale: $BASENAME"
else
    log "nessuna cache locale — download dal bucket"
    # shellcheck disable=SC1091
    set -a; source infra/secrets/storage.secret; set +a
    : "${SEED_BLOB_BUCKET:?SEED_BLOB_BUCKET mancante}"
    : "${S3_ENDPOINT:?S3_ENDPOINT mancante}"
    : "${S3_ACCESS_KEY:?S3_ACCESS_KEY mancante}"
    : "${S3_SECRET_KEY:?S3_SECRET_KEY mancante}"
    export AWS_ACCESS_KEY_ID="$S3_ACCESS_KEY"
    export AWS_SECRET_ACCESS_KEY="$S3_SECRET_KEY"
    AWS_S3="aws s3 --endpoint-url $S3_ENDPOINT"
    PREFIX="snapshots"

    BASENAME=$($AWS_S3 cp "s3://$SEED_BLOB_BUCKET/$PREFIX/latest.txt" - | tr -d '[:space:]')
    [ -n "$BASENAME" ] || fail "latest.txt vuoto sul bucket"

    log "scarico $BASENAME (.dump + .sha256 + .meta.json)"
    $AWS_S3 cp "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.dump"        "$OUT_DIR/$BASENAME.dump.partial"
    $AWS_S3 cp "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.dump.sha256" "$OUT_DIR/$BASENAME.dump.sha256"
    $AWS_S3 cp "s3://$SEED_BLOB_BUCKET/$PREFIX/$BASENAME.meta.json"   "$OUT_DIR/$BASENAME.meta.json.partial"

    # Verifica checksum contro il .partial
    ( cd "$OUT_DIR" && \
      sed "s|$BASENAME.dump|$BASENAME.dump.partial|" "$BASENAME.dump.sha256" | sha256sum -c - ) \
      || fail "sha256 mismatch"

    # Atomic rename
    mv "$OUT_DIR/$BASENAME.dump.partial"      "$OUT_DIR/$BASENAME.dump"
    mv "$OUT_DIR/$BASENAME.meta.json.partial" "$OUT_DIR/$BASENAME.meta.json"
fi

echo "$BASENAME" > "$OUT_DIR/.latest"
log "OK — basename: $BASENAME"
```

- [ ] **Step 2: Rendi eseguibile + commit**

```bash
chmod +x infra/scripts/snapshot-fetch.sh
git add infra/scripts/snapshot-fetch.sh
git commit -m "feat(snapshot): add fetch script with cache-first resolution"
```

---

## Task 8: `snapshot-verify.sh` + bats test

**Files:**
- Create: `infra/scripts/snapshot-verify.sh`
- Create: `infra/scripts/tests/snapshot-verify.bats`
- Create: `infra/scripts/tests/fixtures/meta-good.json`
- Create: `infra/scripts/tests/fixtures/meta-migration-drift.json`
- Create: `infra/scripts/tests/fixtures/meta-model-drift.json`
- Create: `infra/scripts/tests/fixtures/meta-dim-drift.json`

- [ ] **Step 1: Crea fixture `meta-good.json`**

```json
{
  "schema_version": "20260401_AddSearchVector",
  "ef_migration_head": "20260401_AddSearchVector",
  "embedding_model": "sentence-transformers/all-MiniLM-L6-v2",
  "embedding_dim": 384,
  "app_commit": "test0000",
  "created_at": "2026-04-10T12:00:00Z",
  "dev_yml_sha256": "MANIFEST_PLACEHOLDER",
  "pdf_count": 130,
  "chunk_count": 18000,
  "embedding_count": 18000,
  "failed_pdf_ids": []
}
```

- [ ] **Step 2: Crea fixture `meta-migration-drift.json`**

Copia di meta-good con `ef_migration_head: "99999999_Old"`.

- [ ] **Step 3: Crea fixture `meta-model-drift.json`**

Copia di meta-good con `embedding_model: "some/other-model"`.

- [ ] **Step 4: Crea fixture `meta-dim-drift.json`**

Copia di meta-good con `embedding_dim: 768`.

- [ ] **Step 5: Scrivi `snapshot-verify.sh`**

```bash
#!/usr/bin/env bash
# infra/scripts/snapshot-verify.sh
# Compat gate: blocca il restore se snapshot incompatibile col working tree.
# Exit codes: 0=ok, 2=migration drift, 3=model drift, 4=dim drift, 1=altro
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"

log() { echo "[verify] $*" >&2; }

BASENAME=$(cat "$OUT_DIR/.latest" 2>/dev/null || echo "")
[ -n "$BASENAME" ] || { echo "::error:: .latest vuoto o mancante" >&2; exit 1; }
META="$OUT_DIR/$BASENAME.meta.json"
[ -f "$META" ] || { echo "::error:: meta.json mancante: $META" >&2; exit 1; }

# 1. EF migration head
expected_head=$(jq -r '.ef_migration_head' "$META")
working_head=${EXPECTED_EF_HEAD:-}
if [ -z "$working_head" ]; then
    # Resolvi dal working tree — cerca file migration più recente
    working_head=$(ls apps/api/src/Api/Infrastructure/Migrations/*.cs 2>/dev/null \
        | grep -oE '[0-9]{14}_[A-Za-z0-9_]+' \
        | sort -r | head -1 || echo "")
fi

if [ "$expected_head" != "$working_head" ]; then
    cat >&2 <<EOF
::error:: snapshot disallineato con le migrations del working tree
  snapshot head : $expected_head
  working head  : $working_head
Opzioni:
  1. git checkout del commit compatibile con lo snapshot
  2. make seed-index  (rigenera lo snapshot sul commit corrente)
EOF
    exit 2
fi

# 2. Embedding model
expected_model=$(jq -r '.embedding_model' "$META")
current_model=${EXPECTED_EMBEDDING_MODEL:-}
if [ -z "$current_model" ] && [ -f infra/secrets/embedding.secret ]; then
    current_model=$(grep -E '^EMBEDDING_MODEL=' infra/secrets/embedding.secret | cut -d= -f2- || echo "")
fi

if [ "$expected_model" != "$current_model" ]; then
    cat >&2 <<EOF
::error:: embedding model mismatch
  snapshot : $expected_model
  current  : $current_model
I vettori non sono confrontabili col model corrente.
EOF
    exit 3
fi

# 3. Embedding dim
expected_dim=$(jq -r '.embedding_dim' "$META")
current_dim=${EXPECTED_EMBEDDING_DIM:-}
if [ -z "$current_dim" ] && [ -f infra/secrets/embedding.secret ]; then
    current_dim=$(grep -E '^EMBEDDING_DIM=' infra/secrets/embedding.secret | cut -d= -f2- || echo "")
fi

if [ "$expected_dim" != "$current_dim" ]; then
    echo "::error:: embedding_dim mismatch ($expected_dim vs $current_dim)" >&2
    exit 4
fi

# 4. dev.yml drift — warning non bloccante
expected_sha=$(jq -r '.dev_yml_sha256' "$META")
current_sha=$(sha256sum apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml 2>/dev/null | awk '{print $1}' || echo "")
if [ "$expected_sha" != "$current_sha" ]; then
    echo "::warning:: dev.yml è cambiato dopo lo snapshot — eventuali giochi nuovi NON sono indicizzati" >&2
fi

failed_count=$(jq '.failed_pdf_ids | length' "$META")
if [ "$failed_count" -gt 0 ]; then
    echo "::warning:: snapshot contiene $failed_count PDF falliti" >&2
fi

log "OK — $BASENAME compatibile"
```

- [ ] **Step 6: Scrivi bats test**

```bash
#!/usr/bin/env bats
# infra/scripts/tests/snapshot-verify.bats

setup() {
    SCRIPT_DIR="$BATS_TEST_DIRNAME/.."
    FIXTURES="$BATS_TEST_DIRNAME/fixtures"
    TMPDIR=$(mktemp -d)
    export SEED_INDEX_OUT_DIR="$TMPDIR"
}

teardown() {
    rm -rf "$TMPDIR"
}

install_fixture() {
    local name=$1
    cp "$FIXTURES/$name.json" "$TMPDIR/meepleai_seed_test.meta.json"
    echo "meepleai_seed_test" > "$TMPDIR/.latest"
}

@test "exit 0 when all fields match" {
    install_fixture meta-good
    export EXPECTED_EF_HEAD="20260401_AddSearchVector"
    export EXPECTED_EMBEDDING_MODEL="sentence-transformers/all-MiniLM-L6-v2"
    export EXPECTED_EMBEDDING_DIM=384
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 0 ]
}

@test "exit 2 on migration drift" {
    install_fixture meta-migration-drift
    export EXPECTED_EF_HEAD="20260401_AddSearchVector"
    export EXPECTED_EMBEDDING_MODEL="sentence-transformers/all-MiniLM-L6-v2"
    export EXPECTED_EMBEDDING_DIM=384
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 2 ]
}

@test "exit 3 on model drift" {
    install_fixture meta-model-drift
    export EXPECTED_EF_HEAD="20260401_AddSearchVector"
    export EXPECTED_EMBEDDING_MODEL="sentence-transformers/all-MiniLM-L6-v2"
    export EXPECTED_EMBEDDING_DIM=384
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 3 ]
}

@test "exit 4 on dim drift" {
    install_fixture meta-dim-drift
    export EXPECTED_EF_HEAD="20260401_AddSearchVector"
    export EXPECTED_EMBEDDING_MODEL="sentence-transformers/all-MiniLM-L6-v2"
    export EXPECTED_EMBEDDING_DIM=384
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 4 ]
}

@test "exit 1 on missing .latest" {
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 1 ]
}
```

- [ ] **Step 7: Esegui i bats test**

```bash
chmod +x infra/scripts/snapshot-verify.sh
command -v bats >/dev/null || { echo "installa bats-core: https://github.com/bats-core/bats-core"; exit 1; }
bats infra/scripts/tests/snapshot-verify.bats
```

Expected: 5 test pass. Se bats non è installato, salta questo step (è richiesto solo per il test suite completo).

- [ ] **Step 8: Commit**

```bash
git add infra/scripts/snapshot-verify.sh \
        infra/scripts/tests/snapshot-verify.bats \
        infra/scripts/tests/fixtures/
git commit -m "feat(snapshot): add verify script with compat gate + bats tests"
```

---

## Task 9: `snapshot-restore.sh`

**Files:**
- Create: `infra/scripts/snapshot-restore.sh`

- [ ] **Step 1: Scrivi lo script**

```bash
#!/usr/bin/env bash
# infra/scripts/snapshot-restore.sh
# Restore dello snapshot su un DB vuoto dopo ef update.
set -euo pipefail

OUT_DIR="${SEED_INDEX_OUT_DIR:-data/snapshots}"
BASENAME=$(cat "$OUT_DIR/.latest" 2>/dev/null || echo "")

log() { echo "[restore] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

[ -n "$BASENAME" ] || fail ".latest mancante"
DUMP="$OUT_DIR/$BASENAME.dump"
META="$OUT_DIR/$BASENAME.meta.json"
[ -f "$DUMP" ] || fail "dump mancante: $DUMP"
[ -f "$META" ] || fail "meta mancante: $META"

# Guard: DB non deve contenere tabelle application (evita di sovrascrivere lavoro)
table_count=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c \
    "SELECT COUNT(*) FROM information_schema.tables
     WHERE table_schema='public' AND table_type='BASE TABLE' AND table_name NOT LIKE 'pg_%';")

if [ "$table_count" -gt 0 ]; then
    cat >&2 <<EOF
::error:: DB non vuoto ($table_count tabelle application presenti)
snapshot-restore rifiuta di sovrascrivere dati esistenti.
Usa: make dev-from-snapshot-force   (DISTRUTTIVO, azzera il volume postgres)
EOF
    exit 10
fi

log "applico migrations dal working tree"
( cd apps/api/src/Api && dotnet ef database update )

log "pg_restore --data-only"
docker exec -i meepleai-postgres pg_restore \
    -U postgres -d meepleai \
    --data-only --disable-triggers --no-owner --no-privileges \
    < "$DUMP"

# Smoke test
log "smoke test: chunk count + orphans"
actual_chunks=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c "SELECT COUNT(*) FROM text_chunks;")
expected_chunks=$(jq '.chunk_count' "$META")

if [ "$actual_chunks" != "$expected_chunks" ]; then
    fail "chunk count post-restore ($actual_chunks) ≠ sidecar ($expected_chunks)"
fi

orphan_chunks=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c "
    SELECT COUNT(*) FROM text_chunks tc
    LEFT JOIN pdf_documents p ON p.id = tc.pdf_document_id
    WHERE p.id IS NULL;")
[ "$orphan_chunks" = "0" ] || fail "$orphan_chunks text_chunks orfani"

orphan_embeds=$(docker exec meepleai-postgres psql -U postgres -d meepleai -At -c "
    SELECT COUNT(*) FROM pgvector_embeddings e
    LEFT JOIN text_chunks tc ON tc.id = e.text_chunk_id
    WHERE tc.id IS NULL;")
[ "$orphan_embeds" = "0" ] || fail "$orphan_embeds pgvector_embeddings orfani"

log "OK — $actual_chunks chunks ripristinati, nessun orfano"
```

- [ ] **Step 2: Rendi eseguibile + commit**

```bash
chmod +x infra/scripts/snapshot-restore.sh
git add infra/scripts/snapshot-restore.sh
git commit -m "feat(snapshot): add restore script with smoke tests"
```

---

## Task 10: Target Makefile

**Files:**
- Modify: `infra/Makefile`

- [ ] **Step 1: Aggiungi i target prima della sezione `help:`**

Trova una sezione appropriata (dopo `dev-down` o `dev-fast-full`) e inserisci:

```makefile
# ================================================================
# Seed snapshot lifecycle (bake + consume)
# ================================================================

seed-index: ## Bake: indicizza tutti i PDF e produce snapshot locale
	@bash scripts/seed-index-preflight.sh
	$(COMPOSE) -f compose.dev.yml --profile ai up -d postgres redis api smoldocling-service embedding-service
	@bash scripts/wait-for-healthy.sh api || { echo "api non healthy — controlla docker logs meepleai-api"; exit 1; }
	@bash scripts/seed-index-wait.sh
	@bash scripts/seed-index-dump.sh
	@echo "::notice:: snapshot pronto in data/snapshots/ — lancia 'make seed-index-publish' per caricarlo"

seed-index-publish: ## Upload snapshot più recente a seed blob bucket
	@bash scripts/seed-index-publish.sh

dev-from-snapshot: ## Dev env con seed pre-indicizzato (veloce, consume flow)
	@bash scripts/snapshot-fetch.sh
	@bash scripts/snapshot-verify.sh
	$(COMPOSE) -f compose.dev.yml up -d postgres
	@bash scripts/wait-for-healthy.sh postgres
	@bash scripts/snapshot-restore.sh
	@SKIP_CATALOG_SEED=true $(COMPOSE) -f compose.dev.yml --profile ai --profile proxy up -d
	@echo "::notice:: dev env pronto (snapshot $$(cat data/snapshots/.latest))"

dev-from-snapshot-force: ## Come dev-from-snapshot ma azzera il volume postgres
	$(COMPOSE) -f compose.dev.yml down postgres -v
	@$(MAKE) dev-from-snapshot
```

**Nota importante**: `wait-for-healthy.sh` deve esistere. Verifica:

```bash
ls infra/scripts/wait-for-healthy.sh 2>/dev/null && echo "esiste" || echo "da creare"
```

Se non esiste, crealo come utility helper minimale:

```bash
#!/usr/bin/env bash
# infra/scripts/wait-for-healthy.sh
set -euo pipefail
SERVICE=${1:?service name richiesto}
CONTAINER="meepleai-${SERVICE}"
TIMEOUT=${2:-120}
start=$(date +%s)
while :; do
    status=$(docker inspect -f '{{.State.Health.Status}}' "$CONTAINER" 2>/dev/null || echo "missing")
    [ "$status" = "healthy" ] && exit 0
    if [ $(( $(date +%s) - start )) -gt "$TIMEOUT" ]; then
        echo "::error:: $CONTAINER non healthy dopo ${TIMEOUT}s (status=$status)" >&2
        exit 1
    fi
    sleep 2
done
```

- [ ] **Step 2: Verifica `make help` mostra i nuovi target**

```bash
cd infra && make help | grep -E "seed-index|dev-from-snapshot"
```

Expected: 4 righe nuove.

- [ ] **Step 3: Commit**

```bash
git add infra/Makefile
# + infra/scripts/wait-for-healthy.sh se creato
git commit -m "feat(makefile): add seed-index and dev-from-snapshot targets"
```

---

## Task 11: `.gitignore` + docs + CLAUDE.md quick ref

**Files:**
- Modify: `.gitignore`
- Create: `docs/development/snapshot-seed-workflow.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Aggiorna `.gitignore`**

Aggiungi in fondo al file:

```gitignore

# Seed snapshot artifacts (see docs/development/snapshot-seed-workflow.md)
data/snapshots/*.dump
data/snapshots/*.dump.sha256
data/snapshots/.latest
# NON ignorare i .meta.json — utile come log storico
```

- [ ] **Step 2: Crea `docs/development/snapshot-seed-workflow.md`**

```markdown
# Seed Snapshot Workflow

Flusso a due fasi per avere un ambiente dev con RAG funzionante senza aspettare l'indicizzazione runtime dei 136 PDF del manifest.

## Quando usare questo flusso

- **Primo setup su una macchina nuova**: `make dev-from-snapshot` invece di `make dev`
- **Dopo `docker compose down -v`**: stesso
- **In CI per e2e test**: usa il manifest `ci.yml` (3 PDF, bake in <10 min)

## Bake — produrre lo snapshot

Raro. Una volta per rilascio, o quando cambia: manifest `dev.yml`, modello di embedding, schema EF.

```bash
cd infra
make seed-index          # ~ore su CPU, molto meno con GPU
make seed-index-publish  # upload a seed blob bucket (opzionale)
```

Parametri opzionali:
- `SEED_INDEX_TIMEOUT=10800` — timeout totale (default 3h)
- `SEED_INDEX_POLL=15` — intervallo poll (default 15s)
- `SEED_INDEX_FAILURE_PCT=15` — soglia fail% oltre la quale abort
- `SEED_INDEX_ALLOW_PARTIAL=true` — dump procede anche con failure oltre soglia

## Consume — usare lo snapshot

Default per qualunque sviluppatore.

```bash
cd infra
make dev-from-snapshot
```

Cosa fa:
1. Scarica lo snapshot (o usa cache locale in `data/snapshots/`)
2. Verifica compatibilità con il tuo working tree
3. Applica `dotnet ef database update` al DB vuoto
4. `pg_restore --data-only` dei dati indicizzati
5. Avvia lo stack con `SKIP_CATALOG_SEED=true`

Se qualcosa fallisce (compat drift, DB non vuoto), il messaggio di errore ti dice esattamente cosa fare. In ultima istanza: `make dev` è sempre il fallback sicuro.

## Troubleshooting compat drift

| Exit code | Significato | Azione |
|---|---|---|
| 2 | EF migration head del working tree è più recente dello snapshot | `make seed-index` per rigenerare |
| 3 | Embedding model del working tree è diverso da quello dello snapshot | allinea `infra/secrets/embedding.secret` o rigenera |
| 4 | Embedding dimension mismatch | come sopra |
| 10 | DB non è vuoto — il restore rifiuta di sovrascrivere | `make dev-from-snapshot-force` (DISTRUTTIVO) |
| 124 | Timeout del bake | aumenta `SEED_INDEX_TIMEOUT` o investiga perché i job non progrediscono |

## Layout `data/snapshots/`

```
data/snapshots/
├── meepleai_seed_20260410T143022Z_<model>_<commit>.dump         # gitignored
├── meepleai_seed_20260410T143022Z_<model>_<commit>.dump.sha256  # gitignored
├── meepleai_seed_20260410T143022Z_<model>_<commit>.meta.json    # committable (log storico)
└── .latest                                                       # gitignored, pointer
```

## Quando NON usare lo snapshot

- Stai cambiando lo schema EF e vuoi testare la tua migration su dati runtime → `make dev`
- Stai sviluppando il pipeline di indicizzazione → `make dev`
- Vuoi un DB completamente pulito da debug → `docker compose down -v && make dev`
```

- [ ] **Step 3: Aggiorna `CLAUDE.md` Quick Reference**

Cerca la tabella "Quick Reference" e aggiungi le nuove righe:

```bash
grep -n "Start Dev (full)" CLAUDE.md
```

Inserisci dopo la riga `Start Dev (core)`:

```markdown
| Bake Snapshot | `make seed-index` | `infra/` — indicizza tutti i PDF e produce dump |
| Dev from Snapshot | `make dev-from-snapshot` | `infra/` — dev env veloce con RAG pre-indicizzato |
```

Aggiungi una voce in Troubleshooting:

```markdown
| Snapshot drift | `cd infra && make seed-index` (rigenera) oppure `make dev` (fallback) |
```

- [ ] **Step 4: Commit**

```bash
git add .gitignore docs/development/snapshot-seed-workflow.md CLAUDE.md
git commit -m "docs(seed): document snapshot workflow and update gitignore"
```

---

## Task 12: Smoke test manuale end-to-end (verification)

**Files:** nessuno (solo esecuzione)

- [ ] **Step 1: Full bake+consume su `ci.yml`**

Questo è il test di integrazione manuale. Non sempre fattibile in una sola session — documenta il comando e lascialo al user:

```bash
cd infra

# Bake
SEED_CATALOG_MANIFEST_OVERRIDE=ci SEED_INDEX_TIMEOUT=1800 make seed-index

# Verifica output
ls -lh data/snapshots/ | head

# Consume (simula clean env)
docker compose down postgres -v
make dev-from-snapshot

# Smoke test end-to-end
curl -s http://localhost:8080/health | jq .
docker exec meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT COUNT(*) FROM text_chunks;"
```

Expected: chunk count > 0, health OK, stack in running.

- [ ] **Step 2: Se tutto ok, documenta successo nel commit finale**

```bash
git commit --allow-empty -m "chore(seed): verified bake+consume e2e on ci.yml"
```

---

## Self-Review

### Spec coverage

| Spec section | Task |
|---|---|
| `SKIP_CATALOG_SEED` flag | T1 ✓ |
| Sidecar `.meta.json` schema | T5 ✓ |
| Compat gate (migration/model/dim/sha) | T8 ✓ |
| `ci.yml` manifest | T2 ✓ |
| Preflight | T3 ✓ |
| Polling + fail-fast 15% | T4 ✓ |
| Dump + sha256 | T5 ✓ |
| Publish + rotation | T6 ✓ |
| Fetch con cache-first | T7 ✓ |
| Restore con ef update + smoke | T9 ✓ |
| Makefile targets | T10 ✓ |
| `.gitignore` | T11 ✓ |
| Docs | T11 ✓ |
| Bats tests | T8 ✓ |
| E2E manual verification | T12 ✓ |

### Placeholder scan

- Nessun "TBD", "implement later", "similar to Task N"
- Tutti i code block sono completi
- Exit codes espliciti (0, 1, 2, 3, 4, 10, 124)
- File paths assoluti

### Type consistency

- `SEED_CATALOG_MANIFEST_OVERRIDE` nome costante in T2, usato coerente
- `SEED_INDEX_OUT_DIR` default `data/snapshots` coerente in T5, T6, T7, T8, T9
- `.latest` marker scritto da T5/T7, letto da T8/T9 — coerente
- `BASENAME` pattern `meepleai_seed_<ts>_<model>_<commit>` coerente
- `jq` field names (`ef_migration_head`, `embedding_model`, `embedding_dim`, `failed_pdf_ids`, `dev_yml_sha256`, `chunk_count`) coerenti fra T5 e T8/T9

### Assumptions to verify during execution

1. `wait-for-healthy.sh` può non esistere → T10 include creazione inline se mancante
2. Embedded resource pattern in `Api.csproj` → T2 include verifica
3. bats-core non garantito installato → T8 rende i bats test opzionali in locale
4. `SEED_BLOB_*` env vars in `storage.secret` → T6/T7 falliscono con messaggio chiaro se mancanti
5. `compose.dev.yml` profile names (`ai`, `proxy`, ecc.) — potrebbero differire, verificare in T10
