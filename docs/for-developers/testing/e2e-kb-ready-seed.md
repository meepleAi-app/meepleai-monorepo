# E2E KB-Ready seed (`e2e.yml`)

**Issue:** #2502 — provide a reproducible game with a `Ready` knowledge base for the
live-session E2E user story ([#2506](https://github.com/meepleAi-app/meepleai-monorepo/issues/2506)).

## Why

The live-session user story (Mage Knight) needs at least one game whose rulebook is
**indexed** (PDF → chunks → embeddings in pgvector, i.e. `PdfProcessingState.Ready`)
so the RAG flows — agent chat with citations (#2500) and the setup guide (#2504) —
have real content to retrieve. The legacy `data/rulebook/manifest.json` is a historical
reference, **not** the runtime seed; the real seed loads from the embedded YAML manifests
under `apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/`.

`e2e.yml` is a dedicated, intentionally minimal manifest: a **single** game (Love Letter,
reused from `ci.yml` so its blob + sha256 are already verified) with `seedAgent: true`.
When baked it reaches `Ready`, giving the E2E suite a guaranteed KB-Ready game without the
cost of the slow full bake.

## Bake + consume

```bash
# 1. Bake the e2e snapshot (one-off / when the manifest or pipeline changes).
#    Brings up the bake stack, seeds e2e.yml, polls processing_jobs to Ready, dumps.
cd infra
SEED_CATALOG_MANIFEST_OVERRIDE=e2e make seed-index

# 2. (optional) publish the dump to the seed blob bucket so CI/others can fetch it
SEED_CATALOG_MANIFEST_OVERRIDE=e2e make seed-index-publish

# 3. Consume the snapshot for E2E (restores the pre-indexed DB, skips runtime seeding)
make dev-from-snapshot   # sets SKIP_CATALOG_SEED=true

# 4. Run the E2E suite — Love Letter is queryable with embeddings ready
cd ../apps/web && pnpm test:e2e
```

The profile-name override mechanism is documented in
[`snapshot-seed-workflow.md`](../workflows/snapshot-seed-workflow.md);
`SEED_CATALOG_MANIFEST_OVERRIDE` accepts any manifest name (no code change needed —
the manifests are embedded via the `Manifests\*.yml` glob).

## Validation

`e2e.yml` is validated for **loadability + shape** by the unit test
`CatalogSeederManifestOverrideTests.LoadManifest_WithE2eOverride_LoadsSingleKbReadyGame`
(runs in `Backend Fast` — confirms the manifest is embedded, parses, has exactly one game
with a `pdfBlobKey`, `seedAgent: true`, and a `defaultAgent`).

> **The actual bake is NOT validated in CI.** The `seed-snapshot-bake-ci.yml` gate
> (`bake-ci-smoke`) requires `infra/secrets/*.secret` (redis, seed blob bucket) that are
> not provisioned in the PR runner, so it has been failing on every run — including
> `main-dev` — for weeks (`redis.secret not found → exit 2`). That is a pre-existing,
> known-red gate, unrelated to this manifest. Validate the e2e bake in an environment
> that has the secrets:
>
> ```bash
> cd infra && make secrets-sync          # pull .secret files from staging
> SEED_CATALOG_MANIFEST_OVERRIDE=e2e make seed-index
> ```
>
> Fixing the CI bake gate (provisioning secrets in the runner) is tracked separately and
> out of scope for #2502.

## Constraints

- **BGG freeze (#2123):** do not add image properties to the manifest — `SeedManifestGame`
  has no image fields by design (`SeedManifestGameSchemaTests` guards this).
- **`seedAgent: true` requires a `defaultAgent` section** (manifest validation), already
  present in `e2e.yml`.
- Keep `e2e.yml` minimal (ideally one game) so the bake stays fast.
