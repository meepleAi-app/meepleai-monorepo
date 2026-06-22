# Plan — Issue #2123 BGG ToS Compliance (atomic single PR)

**Date**: 2026-06-10
**Issue**: [#2123](https://github.com/meepleAi-app/meepleai-monorepo/issues/2123)
**Spec**: `docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md`
**Branch**: `feature/issue-2123-bgg-tos-compliance`
**Parent**: `main-dev`
**Effort estimate**: 5-7 working days (atomic PR, big surface but well-bounded)
**TDD discipline**: every task that touches production code starts with a failing test.

---

## Phases overview

| Phase | Scope | Effort | Output |
|---|---|---|---|
| **Phase A** — Foundations | Codemod scripts (Python), DB migration, model surgery, metrics | ~1.5gg | `scripts/scrub-bgg-manifest.py`, `scripts/bootstrap-wikidata-qid.py`, DB migration, `SeedManifestGame` cleanup, metric counters |
| **Phase B** — Backend pipeline | M8 batch endpoint, IT tests, CoverUrlResolver metric integration | ~1gg | `EnrichCatalogCoverBatchCommand`, admin endpoint, IT tests |
| **Phase C** — Manifest cleanup + QID bootstrap | Run codemod, run QID bootstrap, run M8 batch, commit YAML diff | ~0.5gg | Cleaned `dev/staging/prod.yml`, populated `wikidata_qid` for 159 entries |
| **Phase D** — Frontend network | Next.js allowlist, custom Image loader, `<Cover>` wrapper mandatory, ESLint rules | ~1.5gg | `next.config.js`, `<Cover>` rollout, 2 ESLint rules, `lint:bgg` script |
| **Phase E** — Tests | xUnit IT, Vitest unit, Playwright E2E, ESLint rule tests | ~1gg | Full test suite green |
| **Phase F** — Docs & rollout | ADR-059 amendment, operations runbook, CLAUDE.md pointer, draft PR ready | ~0.5gg | Triplet docs + PR description |

---

## Phase A — Foundations (≈1.5gg)

### A.1 Failing test for SeedManifestGame model surgery (xUnit)

**File**: `tests/Api.Tests/Unit/Infrastructure/Seeders/SeedManifestGameTests.cs`

```csharp
[Fact]
public void SeedManifestGame_DoesNotExposeBggImageProperties()
{
    var type = typeof(SeedManifestGame);
    type.GetProperty("BggEnhanced").Should().BeNull();
    type.GetProperty("ImageUrl").Should().BeNull();
    type.GetProperty("ThumbnailUrl").Should().BeNull();
    type.GetProperty("FallbackImageUrl").Should().BeNull();
    type.GetProperty("FallbackThumbnailUrl").Should().BeNull();
}
```

**Expected state**: RED — properties currently exist.

### A.2 Remove properties from `SeedManifestGame` and `GameManifestEntry`

**Files**:
- `apps/api/src/Api/Infrastructure/Seeders/SeedManifest.cs` (lines 70-104): remove 5 properties
- `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedManifestModels.cs` (lines 20-21): remove 2 properties

**Expected state**: A.1 test passes; downstream consumers break (compile error).

### A.3 Failing test for `GameSeeder` write path

**File**: `tests/Api.Tests/Unit/Infrastructure/Seeders/GameSeederTests.cs`

```csharp
[Fact]
public async Task CreateFromEnhancedData_AssignsNullImageColumns()
{
    var entry = new SeedManifestGame { Title = "X", BggId = 1, Description = "..." };
    var entity = GameSeeder.CreateFromEnhancedData(entry, Guid.Empty);
    entity.ImageUrl.Should().BeNull();
    entity.ThumbnailUrl.Should().BeNull();
}
```

**Expected state**: RED — current implementation writes BGG URL strings.

### A.4 Update `GameSeeder.cs` write paths

**File**: `apps/api/src/Api/Infrastructure/Seeders/Catalog/GameSeeder.cs`

Changes:
- Remove `PlaceholderImageBase` const (no longer needed at seed time)
- `CreateFromBggData`: `ImageUrl = null, ThumbnailUrl = null`
- `CreateFromEnhancedData`: `ImageUrl = null, ThumbnailUrl = null`; remove `entry.ImageUrl ?? entry.FallbackImageUrl` fallback chain
- `CreateMinimalGame`: `ImageUrl = null, ThumbnailUrl = null`
- Remove `existing.ImageUrl.Contains("placehold.co")` placeholder-update branch (lines 61-71) — no longer needed since seeded values are null
- Remove `entry.BggEnhanced` branching: detect "enhanced" by `!string.IsNullOrWhiteSpace(entry.Description)` instead

**Expected state**: A.3 passes; existing GameSeeder integration tests need fixture update.

### A.5 DB migration: nullify image columns

**File**: `apps/api/src/Api/Infrastructure/Migrations/20260610xxxxxx_NullifyBggImageColumns.cs`

```csharp
public partial class NullifyBggImageColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(name: "image_url", table: "shared_games",
            type: "character varying", nullable: true, oldType: "character varying", oldNullable: false);
        migrationBuilder.AlterColumn<string>(name: "thumbnail_url", table: "shared_games",
            type: "character varying", nullable: true, oldType: "character varying", oldNullable: false);
        migrationBuilder.Sql(@"
            UPDATE shared_games
            SET image_url = NULL
            WHERE image_url ILIKE '%geekdo%' OR image_url ILIKE '%boardgamegeek%';
            UPDATE shared_games
            SET thumbnail_url = NULL
            WHERE thumbnail_url ILIKE '%geekdo%' OR thumbnail_url ILIKE '%boardgamegeek%';
        ");
    }
}
```

Update `SharedGameEntity.cs`: `public string? ImageUrl` and `public string? ThumbnailUrl` (lines 21-22).

Update EF mapping if any explicit `IsRequired()` exists in `SharedGameConfiguration.cs`.

### A.6 Codemod script: `scripts/scrub-bgg-manifest.py`

**File**: `scripts/scrub-bgg-manifest.py`

```python
"""
Removes BGG-hosted URL fields from catalog seed manifests for ToS compliance (issue #2123).

Usage: python scripts/scrub-bgg-manifest.py apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/{dev,staging,prod}.yml

Strips per game entry:
- imageUrl
- thumbnailUrl
- fallbackImageUrl
- fallbackThumbnailUrl
- bggEnhanced
"""
import sys
from pathlib import Path
from ruamel.yaml import YAML

STRIPPED = {'imageUrl', 'thumbnailUrl', 'fallbackImageUrl', 'fallbackThumbnailUrl', 'bggEnhanced'}

def _parse_safely(parser: YAML, text: str):
    """
    SECURITY WRAPPER: ruamel.yaml ``YAML(typ='rt', pure=True)`` is the
    documented-safe variant. It does NOT execute Python via ``!!python/object``
    tags: if the input contains such a tag, ConstructorError is raised
    instead. Round-trip is preferred over PyYAML safe_load+safe_dump because
    it preserves comments/anchors/indent so the 12k-line prod.yml diff stays
    human-reviewable for code review. PyYAML's vulnerable ``yaml.load`` API
    is intentionally NOT imported anywhere in this script.
    """
    return parser.load(text)  # safe: typ='rt' + pure=True

def _make_safe_parser() -> YAML:
    parser = YAML(typ='rt', pure=True)
    parser.preserve_quotes = True
    parser.indent(mapping=2, sequence=4, offset=2)
    return parser

def scrub(path: Path) -> tuple[int, int]:
    parser = _make_safe_parser()
    text = path.read_text(encoding='utf-8')
    data = _parse_safely(parser, text)
    stripped_count, game_count = 0, 0
    for game in data.get('catalog', {}).get('games', []):
        game_count += 1
        for key in list(game.keys()):
            if key in STRIPPED:
                del game[key]
                stripped_count += 1
    with path.open('w', encoding='utf-8') as f:
        parser.dump(data, f)
    return game_count, stripped_count

if __name__ == '__main__':
    for arg in sys.argv[1:]:
        games, stripped = scrub(Path(arg))
        print(f'{arg}: {games} games, {stripped} fields removed')
```

**Verification**: run on a copy of `ci.yml` first → expect 0 stripped (already clean).

### A.7 Failing test for codemod (Python pytest or shell smoke)

**File**: `scripts/__tests__/test_scrub_bgg_manifest.py`

```python
def test_scrub_removes_target_fields(tmp_path):
    src = tmp_path / 'test.yml'
    src.write_text("""catalog:
  games:
    - title: Catan
      bggId: 13
      imageUrl: https://cf.geekdo-images.com/foo.jpg
      thumbnailUrl: https://cf.geekdo-images.com/bar.jpg
      fallbackImageUrl: https://cf.geekdo-images.com/baz.jpg
      fallbackThumbnailUrl: https://cf.geekdo-images.com/qux.jpg
      bggEnhanced: true
      description: a game
""")
    from scripts import scrub_bgg_manifest
    games, stripped = scrub_bgg_manifest.scrub(src)
    assert games == 1
    assert stripped == 5
    result = src.read_text()
    assert 'imageUrl' not in result
    assert 'bggEnhanced' not in result
    assert 'title: Catan' in result
    assert 'description: a game' in result
```

**Expected state**: RED → GREEN after A.6.

### A.8 Metrics counter setup

**File**: `apps/api/src/Api/SharedKernel/Observability/MeepleAiMetrics.cs`

Add:
```csharp
public static readonly Counter<long> CoverResolution = Meter.CreateCounter<long>(
    name: "meepleai_cover_resolution_total",
    description: "Cover resolution outcomes by source layer (#2123)");

public static readonly Counter<long> BggUrlAttemptedRender = Meter.CreateCounter<long>(
    name: "meepleai_bgg_url_attempted_render_total",
    description: "SLO=0: any nonzero increment indicates ToS violation attempt (#2123)");
```

### A.9 Wire metric in `CoverUrlResolver`

**File**: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs`

Emit `MeepleAiMetrics.CoverResolution.Add(1, new("source", "<layer>"))` for each `if` block (L3/L4/L2.5/L2) + a final `Add(1, new("source", "null"))` when nothing resolves.

Add unit test asserting metric is emitted for each layer.

---

## Phase B — Backend pipeline (≈1gg)

### B.1 Failing test: `EnrichCatalogCoverBatchCommand`

**File**: `tests/Api.Tests/Unit/SharedGameCatalog/EnrichCatalogCoverBatchCommandTests.cs`

```csharp
[Fact]
public async Task Handle_DispatchesSingleEntryCommandPerGameId()
{
    var mediator = new Mock<IMediator>();
    mediator.Setup(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(EnrichCatalogCoverResult.Skipped("NoQid"));
    var handler = new EnrichCatalogCoverBatchCommandHandler(mediator.Object, NullLogger<EnrichCatalogCoverBatchCommandHandler>.Instance);

    var result = await handler.Handle(new EnrichCatalogCoverBatchCommand(new[] { Guid.NewGuid(), Guid.NewGuid() }), CancellationToken.None);

    result.TotalRequested.Should().Be(2);
    result.Skipped.Should().Be(2);
    mediator.Verify(m => m.Send(It.IsAny<EnrichCatalogCoverCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
}
```

**Expected state**: RED — types don't exist.

### B.2 Implement batch command

**Files**:
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCoverBatch/EnrichCatalogCoverBatchCommand.cs`
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCoverBatch/EnrichCatalogCoverBatchCommandHandler.cs`
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCoverBatch/EnrichCatalogCoverBatchResult.cs`
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Validators/EnrichCatalogCoverBatchCommandValidator.cs`

Handler iterates input GameIds, dispatches `EnrichCatalogCoverCommand` per id via `IMediator`, aggregates results into `{TotalRequested, Success, Skipped, Failed}` counters. NOT `[AtomicAudit]` (Redis side effects from M8). Each child command keeps its own audit if applicable.

Validator: NotEmpty list, list size ≤ 200 (protect Wikimedia rate limit at admin invocation time), distinct GameIds.

### B.3 Admin endpoint

**File**: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs`

Extend with `POST /api/v1/admin/catalog/covers/enrich-batch`:

```csharp
adminGroup.MapPost("/covers/enrich-batch", async (
    EnrichCatalogCoverBatchCommand cmd, IMediator mediator) =>
{
    var result = await mediator.Send(cmd);
    return Results.Ok(result);
})
.RequireAuthorization(policy => policy.RequireRole("admin"))
.WithName("EnrichCatalogCoverBatch");
```

### B.4 Failing test: `BggToSComplianceIntegrationTests`

**File**: `tests/Api.Tests/Integration/SharedGameCatalog/BggToSComplianceIntegrationTests.cs`

```csharp
public class BggToSComplianceIntegrationTests : SharedDatabaseFixture
{
    [Fact]
    public async Task NoBggHostsInProdSeed()
    {
        await SeedFromManifestAsync("prod.yml");
        var count = await Db.SharedGames.CountAsync(g =>
            EF.Functions.ILike(g.ImageUrl ?? "", "%geekdo%") ||
            EF.Functions.ILike(g.ImageUrl ?? "", "%boardgamegeek%") ||
            EF.Functions.ILike(g.ThumbnailUrl ?? "", "%geekdo%") ||
            EF.Functions.ILike(g.ThumbnailUrl ?? "", "%boardgamegeek%"));
        count.Should().Be(0);
    }
}
```

**Expected state**: RED → GREEN after Phase C runs codemod on prod.yml.

---

## Phase C — Manifest cleanup + QID bootstrap (≈0.5gg)

### C.1 Run codemod (one-shot, committed diff)

```bash
python scripts/scrub-bgg-manifest.py \
  apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml \
  apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/staging.yml \
  apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/prod.yml
```

Expected diff: ~568 URL removals + ~426 `bggEnhanced` removals across 3 files.

Commit message: `chore(catalog): #2123 scrub BGG URLs from seed manifests (568 URLs × 3 files)`.

### C.2 QID bootstrap script

**File**: `scripts/bootstrap-wikidata-qid.py`

```python
"""
Populates shared_games.wikidata_qid for entries with bgg_id via SPARQL wdt:P2339 lookup.

Usage: python scripts/bootstrap-wikidata-qid.py --connection-string "Host=...;Database=..." [--dry-run]

Rate limit: 1 SPARQL request per second (Wikimedia policy). Batches in groups of 50 BggIds.
"""
import argparse, time, psycopg2, requests

SPARQL_ENDPOINT = 'https://query.wikidata.org/sparql'
SPARQL_QUERY = """
SELECT ?item ?bggId WHERE {{
  VALUES ?bggId {{ {bgg_ids} }}
  ?item wdt:P2339 ?bggId .
}}
"""

def query_batch(bgg_ids: list[int]) -> dict[int, str]:
    """Returns {bgg_id: qid} mapping."""
    formatted_ids = ' '.join(f'"{bid}"' for bid in bgg_ids)
    response = requests.get(SPARQL_ENDPOINT,
        params={'query': SPARQL_QUERY.format(bgg_ids=formatted_ids), 'format': 'json'},
        headers={'User-Agent': 'MeepleAI/1.0 (issue#2123; abuse@meepleai.app)'},
        timeout=30)
    response.raise_for_status()
    result = {}
    for binding in response.json()['results']['bindings']:
        bgg_id = int(binding['bggId']['value'])
        qid = binding['item']['value'].rsplit('/', 1)[-1]
        result[bgg_id] = qid
    return result

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--connection-string', required=True)
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()
    conn = psycopg2.connect(args.connection_string)
    with conn.cursor() as cur:
        cur.execute("SELECT id, bgg_id FROM shared_games WHERE bgg_id IS NOT NULL AND wikidata_qid IS NULL")
        rows = cur.fetchall()
    print(f'Found {len(rows)} candidates')
    qid_map = {}
    for i in range(0, len(rows), 50):
        batch = rows[i:i+50]
        bgg_ids = [r[1] for r in batch]
        try:
            qid_map.update(query_batch(bgg_ids))
        except Exception as e:
            print(f'Batch {i}-{i+50} failed: {e}')
        time.sleep(1.0)
    print(f'Resolved {len(qid_map)}/{len(rows)} ({100*len(qid_map)//len(rows)}%)')
    if args.dry_run:
        print('Dry-run: skipping UPDATE')
        return
    with conn.cursor() as cur:
        for row_id, bgg_id in rows:
            qid = qid_map.get(bgg_id)
            if qid:
                cur.execute("UPDATE shared_games SET wikidata_qid = %s, wikidata_qid_last_verified_at = NOW() WHERE id = %s", (qid, row_id))
        conn.commit()
    print('Done')

if __name__ == '__main__':
    main()
```

### C.3 Failing test for bootstrap script (Python pytest with mock SPARQL)

**File**: `scripts/__tests__/test_bootstrap_wikidata_qid.py`

Tests parse of SPARQL JSON response, batching, rate-limit sleep, dry-run mode.

### C.4 Run QID bootstrap (one-shot, dev DB)

In dev environment:
```bash
python scripts/bootstrap-wikidata-qid.py --connection-string "$DEV_DB_URL"
```

Expected: ≥120/159 resolved (AC-5 target). Document actual % in PR description.

### C.5 Run M8 batch (one-shot, dev DB via admin endpoint)

```bash
# Get list of game IDs with non-null QID
GAME_IDS=$(psql "$DEV_DB_URL" -t -c "SELECT id FROM shared_games WHERE wikidata_qid IS NOT NULL")

# POST batch
curl -X POST https://dev.meepleai.app/api/v1/admin/catalog/covers/enrich-batch \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"gameIds\":[$(echo $GAME_IDS | tr ' ' ',')]}"
```

Expected: success rate ≥80% of QID-populated (some games will have no `wdt:P18` claim, or no Commons file, or incompatible license).

**Note**: this step runs once per environment (dev → staging → prod) during deployment. Document procedure in operations manual (Phase F).

---

## Phase D — Frontend network (≈1.5gg)

### D.1 Failing test: Next.js config audit

**File**: `apps/web/src/__tests__/next-config-bgg.test.ts` (or shell test integrated into `lint:bgg`)

```typescript
import nextConfig from '../../next.config.js';

describe('next.config.js BGG ToS compliance', () => {
  it('does not whitelist BGG hosts', () => {
    const patterns = nextConfig.images.remotePatterns;
    expect(patterns).not.toContainEqual(expect.objectContaining({ hostname: expect.stringMatching(/geekdo|boardgamegeek/i) }));
  });
  it('does not contain catch-all wildcard', () => {
    const patterns = nextConfig.images.remotePatterns;
    expect(patterns).not.toContainEqual(expect.objectContaining({ hostname: '**' }));
  });
});
```

**Expected state**: RED.

### D.2 Update `next.config.js`

Replace the `remotePatterns` block (lines 122-146):

```javascript
images: {
  remotePatterns: [
    { protocol: 'https', hostname: 'picsum.photos', pathname: '/**' },
    { protocol: 'https', hostname: '**.r2.cloudflarestorage.com', pathname: '/**' },
    { protocol: 'https', hostname: '*.r2.dev', pathname: '/**' },
    { protocol: 'https', hostname: 'commons.wikimedia.org', pathname: '/**' },
    { protocol: 'https', hostname: 'upload.wikimedia.org', pathname: '/**' },
    { protocol: 'https', hostname: 'placehold.co', pathname: '/**' },
    { protocol: 'https', hostname: 'meepleai.app', pathname: '/**' },
  ],
},
```

**Expected state**: D.1 passes.

### D.3 `<Cover>` wrapper rollout

**File**: `apps/web/src/components/ui/data-display/cover/Cover.tsx`

Audit current implementation (already exists at `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx`). Extract / promote to a generic `<Cover>` primitive consumable by non-MeepleCard consumers.

API:
```tsx
interface CoverProps {
  coverUrl: string | null | undefined;
  title: string;
  gameId: string;
  variant?: 'hero' | 'grid' | 'list' | 'thumbnail';
  alt?: string;
}
```

Internally: calls `shouldUsePlaceholder(coverUrl)` → either `<Image>` or deterministic placeholder.

### D.4 Replace raw `<Image>` consumers (semantic audit)

Run grep to identify all `<Image>` consumers with SharedGame-typed props:

```bash
grep -rn 'next/image' apps/web/src --include='*.tsx' | xargs grep -l 'imageUrl\|coverUrl' | sort -u
```

Map each file to `<Cover>` wrapper. Likely targets (from audit):
- `HubGameCard.tsx`
- `HeroCard.tsx` (already uses shouldUsePlaceholder)
- `GameDetailDesktop.tsx`
- `GameHero.tsx`
- `RecentLibraryCard.tsx`
- `GameDiscoverHero.tsx`
- `MeepleCard` variants (already use Cover internally)
- `library/CatalogSearchStep.tsx`
- `private-game/PrivateGameDetailClient.tsx`
- `library/private-game-detail/PrivateGameHub.tsx`
- `dashboard/AddToLibraryModal.tsx`
- `dashboard/recent-games-section.tsx`
- `game-night/steps/SearchGameStep.tsx`
- `session/LiveSessionContextBar.tsx`
- `session/InviteSession.tsx`
- `library/GameActionsModal.tsx`
- `library/GameHeader.tsx`
- ... (~25 files)

Per file:
1. Replace `<Image src={imageUrl} … />` with `<Cover coverUrl={coverUrl} title={title} gameId={id} … />`
2. Update component props if needed (add `gameId`, drop `imageUrl` if redundant)
3. Add Vitest snapshot for the new `<Cover>` consumer

### D.5 ESLint rule `local/no-bgg-host`

**File**: `apps/web/eslint-rules/no-bgg-host.js`

```javascript
module.exports = {
  meta: { type: 'problem', schema: [], messages: {
    bggHost: 'BGG host literal forbidden by ToS compliance (issue #2123). Use R2 / Wikimedia / placeholder instead.',
  }},
  create(context) {
    const BGG_PATTERN = /(cf\.geekdo-images\.com|geekdo-images\.com|boardgamegeek\.com|images\.geekdo\.com)/i;
    return {
      Literal(node) {
        if (typeof node.value === 'string' && BGG_PATTERN.test(node.value)) {
          context.report({ node, messageId: 'bggHost' });
        }
      },
      TemplateElement(node) {
        if (BGG_PATTERN.test(node.value?.raw || '')) {
          context.report({ node, messageId: 'bggHost' });
        }
      },
    };
  },
};
```

Register in `apps/web/.eslintrc` at `error` level. Allow opt-out via `// eslint-disable-next-line local/no-bgg-host` comment in admin-only files where BGG URL fetching is legitimate (server-side only).

### D.6 ESLint rule `local/no-raw-next-image-for-shared-game`

**File**: `apps/web/eslint-rules/no-raw-next-image-for-shared-game.js`

Detects `<Image src={x} />` where `x` is `<...>.imageUrl` for SharedGame-shaped consumer. Heuristic: if JSX prop spread or named binding has identifier ending in `imageUrl` AND parent component imports `SharedGameDto` or its FE types, error.

Conservative implementation: lint-time string match on `\.imageUrl` next to `<Image`; opt-out via comment.

Register at `warn` level for 1 week (R4 mitigation), promote to `error` after baseline sweep.

### D.7 `lint:bgg` script

**File**: `apps/web/scripts/lint-bgg.sh` (or `.mjs` for Windows compatibility)

```bash
#!/usr/bin/env bash
set -euo pipefail
FAILURES=0

check() {
  local description="$1"; shift
  local match
  match=$("$@" 2>/dev/null || true)
  if [ -n "$match" ]; then
    echo "❌ $description:"; echo "$match"; FAILURES=$((FAILURES+1))
  else
    echo "✅ $description"
  fi
}

check "Manifest YAML files" grep -rnE 'cf\.geekdo|boardgamegeek' apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests
check "next.config.js" grep -nE 'geekdo|boardgamegeek' apps/web/next.config.js
check "FE source" grep -rnE 'cf\.geekdo|boardgamegeek' apps/web/src --include='*.{ts,tsx,js,jsx}' --exclude-dir=__tests__ --exclude='*.stories.tsx'
check "BE seeder source" grep -rnE 'cf\.geekdo|boardgamegeek' apps/api/src/Api/Infrastructure/Seeders --include='*.cs'

if [ $FAILURES -gt 0 ]; then
  echo "BGG ToS lint failed: $FAILURES violation(s)"; exit 1
fi
```

Add to `apps/web/package.json`:
```json
{ "scripts": { "lint:bgg": "node scripts/lint-bgg.mjs" } }
```

(Use `.mjs` ESM script to be Windows-compatible — same logic via `execSync('grep …')` with proper error handling.)

### D.8 Custom Image loader / runtime guard

**File**: `apps/web/src/lib/images/safe-loader.ts`

```typescript
import { shouldUsePlaceholder } from '@/lib/games/cover-utils';

export default function safeImageLoader({ src, width, quality }: { src: string; width: number; quality?: number }) {
  if (shouldUsePlaceholder(src)) {
    // Emit metric via Web Vitals or a simple beacon to /api/metrics/bgg-attempt
    if (typeof window !== 'undefined') {
      navigator.sendBeacon('/api/metrics/bgg-attempt', JSON.stringify({ src, path: window.location.pathname }));
    }
    return '/_placeholder.svg';
  }
  return `${src}?w=${width}&q=${quality ?? 75}`;
}
```

Register in `next.config.js`:
```javascript
images: { loader: 'custom', loaderFile: './src/lib/images/safe-loader.ts', remotePatterns: [...] }
```

Add API route `apps/web/src/app/api/metrics/bgg-attempt/route.ts` that proxies to backend metrics endpoint (POST `meepleai_bgg_url_attempted_render_total` increment).

---

## Phase E — Tests (≈1gg)

### E.1 Vitest extension

- Extend `cover-utils.test.ts` with 7 BGG hostname variants.
- New `<Cover>` snapshot tests for grid/list/hero/thumbnail variants.
- `safeImageLoader.test.ts` covers placeholder fallback path.
- `no-bgg-host.test.js` ESLint rule test.
- `no-raw-next-image-for-shared-game.test.js` ESLint rule test.

### E.2 xUnit IT

- `BggToSComplianceIntegrationTests.NoBggHostsInProdSeed` (already drafted Phase B).
- `WikidataQidBootstrapIntegrationTests.PopulatesQidForKnownBggIds` — uses recorded SPARQL JSON cassettes (no live network).
- `EnrichCatalogCoverBatchIntegrationTests` — mocks Wikidata + Commons + R2, verifies M8 dispatch chain end-to-end.

### E.3 Playwright E2E

**File**: `apps/web/e2e/bgg-tos-compliance.spec.ts`

```typescript
import { test, expect } from '@playwright/test';

const BGG_HOSTS = /cf\.geekdo-images\.com|geekdo-images\.com|boardgamegeek\.com/i;

const ROUTES = [
  { path: '/shared-games', auth: false },
  { path: '/hub/games', auth: true },
  { path: '/discover', auth: false },
  { path: '/library', auth: true },
];

for (const route of ROUTES) {
  test(`no BGG network requests on ${route.path}`, async ({ page }) => {
    const violations: string[] = [];
    page.on('request', (req) => {
      if (BGG_HOSTS.test(req.url())) violations.push(req.url());
    });
    if (route.auth) await loginAs(page, 'aaron@meepleai.test');
    await page.goto(route.path);
    await page.waitForLoadState('networkidle', { timeout: 10000 });
    expect(violations, `Found BGG requests: ${violations.join(', ')}`).toEqual([]);
  });
}
```

### E.4 Lint script smoke

```bash
pnpm lint:bgg
# Expect: 4× "✅ <description>", exit 0
```

---

## Phase F — Docs & rollout (≈0.5gg)

### F.1 ADR-059 amendment

**File**: `docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md`

Append §5:

```markdown
## 5. Amendment 2026-06-10 — User-side BGG asset ban enforcement (issue #2123)

While ADR-059 §2 narrowly addressed the admin pipeline whitelist filter, it left user-side BGG asset traffic (cover images served from `cf.geekdo-images.com`) uncontrolled. Issue #2123 closes this gap with a 3-layer ban:

1. **Data plane** — `dev/staging/prod.yml` seed manifests scrubbed of BGG URLs; `SeedManifestGame.{BggEnhanced, ImageUrl, ThumbnailUrl, FallbackImageUrl, FallbackThumbnailUrl}` properties removed; `shared_games.image_url`+`thumbnail_url` columns nullable + nullified for BGG-pattern rows.
2. **Resolution plane** — `SharedGameDto.CoverUrl` (R2 presigned) is the single source of truth for FE rendering. `<Cover>` wrapper mandatory; ESLint `local/no-raw-next-image-for-shared-game` enforces.
3. **Network plane** — `next.config.js` `remotePatterns` allowlist-only (no catch-all `**`, no BGG hosts); custom Image loader applies runtime guard via `shouldUsePlaceholder`; Prometheus metric `meepleai_bgg_url_attempted_render_total` alert SLO = 0.

Bootstrap: SPARQL `wdt:P2339` batch lookup populates `WikidataQid` for catalog entries with BGG ID; M8 orchestrator batch run populates R2 cover where Wikidata + Wikimedia license permit. Residual coverage gap (~25%) renders deterministic placeholder via `cover-utils.ts`.

CI gating: blocking jobs `Frontend - BGG Lint` (grep + ESLint) + `Backend - BGG ToS IT` (xUnit IT seeding `prod.yml` + asserting 0 BGG hosts in DB).

See: [spec](../../../superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md), [plan](../../../superpowers/plans/2026-06-10-issue-2123-bgg-tos-compliance.md).
```

### F.2 Operations runbook

**File**: `docs/for-developers/operations/operations-manual.md`

Add new section:

```markdown
## Catalog covers — BGG ToS compliance (issue #2123)

### Alert: `meepleai_bgg_url_attempted_render_total > 0`

**Severity**: P1 (legal exposure).

**Trigger**: any browser attempted to render an image whose hostname is in the BGG block list. The custom Next.js Image loader caught it and redirected to placeholder, but the attempt occurred.

**Investigation**:
1. Query Prometheus: `sum by (path) (rate(meepleai_bgg_url_attempted_render_total[5m]))` — identifies offending page route.
2. Inspect FE source on that route for raw `<Image src={…}>` with BGG URL data source.
3. Inspect DB: `SELECT id, title, image_url, thumbnail_url FROM shared_games WHERE image_url ILIKE '%geekdo%' OR thumbnail_url ILIKE '%geekdo%';` — should return zero rows; if not, manifest scrub regressed.
4. Inspect manifests: `pnpm lint:bgg` should pass; if not, scrub regressed.

**Resolution**:
- If DB pollution: run `UPDATE shared_games SET image_url = NULL, thumbnail_url = NULL WHERE image_url ILIKE '%geekdo%' OR thumbnail_url ILIKE '%geekdo%';`
- If FE regression: revert raw `<Image>` consumer to `<Cover>` wrapper.
- If manifest regression: run `python scripts/scrub-bgg-manifest.py` + re-deploy.

### Periodic Wikidata QID + M8 re-enrichment

Wave 3 M9 BackgroundService scheduler (tracked separately) will automate quarterly QID re-verification. Until then, manual procedure:

```bash
# 1. Bootstrap QIDs for newly added games
python scripts/bootstrap-wikidata-qid.py --connection-string "$STAGING_DB_URL"

# 2. Trigger M8 batch for entries with QID but no R2 cover
GAME_IDS=$(psql "$STAGING_DB_URL" -t -c \
  "SELECT id FROM shared_games WHERE wikidata_qid IS NOT NULL AND wikidata_cover_r2_key IS NULL")
curl -X POST https://staging.meepleai.app/api/v1/admin/catalog/covers/enrich-batch \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d "{\"gameIds\":[$(echo $GAME_IDS | tr ' ' ',')]}"
```
```

### F.3 CLAUDE.md pointer

**File**: `CLAUDE.md` § Active Freezes

Add:
```markdown
**BGG user-side asset ban — 2026-06-10** (issue #2123) — Hard ban on browser requests to `cf.geekdo-images.com` / `**.boardgamegeek.com`. Three-layer enforcement: manifest YAML stripped, `next.config.js` allowlist explicit, custom Image loader + ESLint rule `local/no-bgg-host`. Metric `meepleai_bgg_url_attempted_render_total` SLO = 0. See [ADR-059 §5](./docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md) and operations runbook.
```

### F.4 PR description

Open draft PR with title:
> `chore(catalog): #2123 ban user-side BGG asset traffic — manifest scrub + Next.js allowlist + runtime guard`

Body sections: Summary, Scope (Phase A-F), Migration safety (DB), Rollout procedure (one-shot scripts), Test evidence, AC checklist linked to spec.

---

## Phase G — Branch + PR + acceptance loop

### G.1 Commit cadence

Suggested commits:
1. `chore(scripts): #2123 add scrub-bgg-manifest.py codemod`
2. `chore(scripts): #2123 add bootstrap-wikidata-qid.py SPARQL bootstrap`
3. `feat(catalog): #2123 SeedManifestGame model surgery — remove BGG image properties`
4. `feat(api): #2123 EnrichCatalogCoverBatchCommand + admin endpoint`
5. `feat(catalog): #2123 SharedGameEntity image columns nullable migration`
6. `feat(api): #2123 metrics: cover_resolution_total + bgg_url_attempted_render_total`
7. `chore(catalog): #2123 scrub BGG URLs from seed manifests (568 URLs × 3 files)` — codemod execution
8. `feat(web): #2123 Cover wrapper + Next.js allowlist + custom Image loader`
9. `chore(web): #2123 ESLint local/no-bgg-host + local/no-raw-next-image-for-shared-game`
10. `chore(ci): #2123 BGG ToS lint + IT blocking jobs`
11. `test(api): #2123 BggToSComplianceIntegrationTests + Phase B/C/E test suite`
12. `test(web): #2123 Playwright bgg-tos-compliance.spec.ts`
13. `docs(adr): #2123 ADR-059 §5 amendment + operations runbook + CLAUDE.md pointer`

### G.2 Open draft PR

```bash
gh pr create --base main-dev --draft \
  --title "chore(catalog): #2123 ban user-side BGG asset traffic — manifest scrub + Next.js allowlist + runtime guard" \
  --body-file docs/superpowers/plans/2026-06-10-issue-2123-bgg-tos-compliance-pr-body.md
```

### G.3 Code review subagent before un-draft

Run `feature-dev:code-reviewer` on the diff. Expect at minimum:
- Verify metric label cardinality (5 sources OK, no PII)
- Verify migration safety (single ALTER + UPDATE; no orphan FK)
- Verify ESLint rule false-positive rate
- Verify Next.js custom loader doesn't break Storybook / dev (`pnpm dev` smoke)

### G.4 Acceptance verification — AC ↔ test matrix

| AC | Verified by |
|---|---|
| AC-1 | A.7 codemod test + lint:bgg manifest check |
| AC-2 | Same |
| AC-3 | A.1 SeedManifestGameTests |
| AC-4 | A.5 migration applied + B.4 IT |
| AC-5 | C.4 manual run + WikidataQidBootstrapIntegrationTests |
| AC-6 | C.5 manual run + EnrichCatalogCoverBatchIntegrationTests |
| AC-7 | A.3 GameSeederTests + diff review |
| AC-8 | D.6 ESLint rule + 25-file replacement |
| AC-9 | D.4 audit + diff review |
| AC-10 | D.1 next-config-bgg.test.ts |
| AC-11 | D.8 safe-loader + safeImageLoader.test |
| AC-12 | B.4 BggToSComplianceIntegrationTests |
| AC-13 | E.3 Playwright |
| AC-14 | E.1 Vitest |
| AC-15 | E.1 ESLint rule tests |
| AC-16 | G.3 CI workflow `Frontend - BGG Lint` |
| AC-17 | G.3 CI workflow `Backend - BGG ToS IT` |
| AC-18-19 | A.8-A.9 metric counters + Grafana panels (manual) |
| AC-20 | F.1 ADR-059 amendment |
| AC-21 | F.2 operations runbook |
| AC-22 | F.3 CLAUDE.md pointer |

---

## Risk mitigations summary

| Risk | Mitigation in plan |
|---|---|
| R1 QID coverage <75% | C.4 + spec §9 documents gap; placeholder fallback covers residual |
| R2 Wikimedia rate-limit | C.2 sleeps 1.0s between batches (already implemented in M8) |
| R3 `**` removal breaks | D.4 audit + R4 mitigation |
| R4 ESLint false-positives | D.6 warn level for 1 week before promoting |
| R5 Migration slow | A.5 single UPDATE on 159 rows; trivially fast |
| R6 Hybrid mapper fallback | D.4 explicit replacement |
| R7 YAML formatting | A.6 ruamel.yaml round-trip preserves anchors |
| R8 Deprecation breaks consumers | F1 deferred to follow-up |

---

## Done definition

All 22 acceptance criteria green + CI blocking jobs green + code review approved + atomic PR squash-merged to `main-dev` + ADR-059 §5 published + operations runbook live + `CLAUDE.md` § Active Freezes updated + follow-up issues F2/F3/F6 created and linked.
