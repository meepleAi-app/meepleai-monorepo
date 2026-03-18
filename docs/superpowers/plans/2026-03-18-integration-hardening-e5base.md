# Integration Hardening + e5-base Switch — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable Traefik basic auth on staging, align staging DB schema (5 migrations), and switch embedding model from e5-large (1024d) to e5-base (768d) globally.

**Architecture:** Three independent operational tasks. Task 3A (Traefik) and 3B (migrations) are staging-server operations via SSH. Task 4 (e5-base) is code changes to C# + compose files. Deploy to staging after all code changes committed.

**Tech Stack:** Docker Compose, Traefik v3, EF Core migrations, PostgreSQL 16, pgvector 0.8.2, Python embedding service

**Spec:** `docs/superpowers/specs/2026-03-18-integration-hardening-e5base-design.md`

---

## Task 1: Traefik Basic Auth on Staging

**Files:**
- Remote: `/opt/meepleai/secrets/traefik.secret` (staging server)

- [ ] **Step 1: Generate htpasswd hash on staging server**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "htpasswd -nbB admin 'MeepleAI-Services-2026'" 2>/dev/null || \
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "openssl passwd -apr1 'MeepleAI-Services-2026'"
```

Save the output hash (e.g. `admin:$2y$05$...` or `admin:$apr1$...`).

- [ ] **Step 2: Add INTEGRATION_BASIC_AUTH to traefik.secret on staging**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "echo 'INTEGRATION_BASIC_AUTH=admin:<ESCAPED_HASH>' >> /opt/meepleai/secrets/traefik.secret"
```

**Critical**: Escape every `$` as `$$` in the hash for Docker Compose interpolation.
Example: `$2y$05$abc` → `$$2y$$05$$abc`

- [ ] **Step 3: Redeploy Traefik to pick up new env var**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "cd /opt/meepleai && set -a && for f in secrets/*.secret; do source \"\$f\" 2>/dev/null; done && set +a && docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml up -d traefik"
```

- [ ] **Step 4: Verify basic auth works**

```bash
# Should return 401 (no auth)
curl -s -o /dev/null -w "%{http_code}" https://meepleai.app/services/embedding/health

# Should return 200 (with auth)
curl -s -o /dev/null -w "%{http_code}" -u admin:MeepleAI-Services-2026 https://meepleai.app/services/embedding/health
```

Expected: `401` then `200`.

---

## Task 2: Apply Pending Migrations to Staging DB

**Files:**
- Read: `apps/api/src/Api/Infrastructure/Migrations/` (5 pending migrations)

- [ ] **Step 1: Start SSH tunnel to staging PostgreSQL**

In a separate terminal:
```bash
ssh -i ~/.ssh/meepleai-staging -L 15432:localhost:5432 -N deploy@204.168.135.69
```

- [ ] **Step 2: Verify current migration state**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "docker exec -i meepleai-postgres psql -U meepleai -d meepleai -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";'"
```

Expected: 1 row (`20260316055120_Beta0`).

- [ ] **Step 3: Apply migrations via dotnet ef**

```powershell
cd apps/api/src/Api
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=15432;Database=meepleai;Username=meepleai;Password=MeepleAI-Integration-2026"
dotnet ef database update
```

Expected: 5 migrations applied sequentially.

- [ ] **Step 4: Verify migrations applied**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "docker exec -i meepleai-postgres psql -U meepleai -d meepleai -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";'"
```

Expected: 6 rows (BetaV0 + 5 new).

- [ ] **Step 5: Close SSH tunnel**

Kill the SSH tunnel process from Step 1.

---

## Task 3: Switch Embedding Model to e5-base (Code Changes)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/EmbeddingProviderType.cs:64,78`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs:331`
- Modify: `infra/compose.dev.yml:60-62`
- Modify: `infra/compose.integration.yml:41-43`
- Modify: `infra/compose.staging.yml:75` (add after line 75)
- Modify: `infra/compose.prod.yml:97` (add after line 97)
- Modify: `infra/docker-compose.yml:207-208`

### 3a: C# Code Changes

- [ ] **Step 1: Update External model name in EmbeddingProviderType.cs**

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/EmbeddingProviderType.cs`

Line 64: Change:
```csharp
EmbeddingProviderType.External => "intfloat/multilingual-e5-large",
```
To:
```csharp
EmbeddingProviderType.External => "intfloat/multilingual-e5-base",
```

- [ ] **Step 2: Update External dimensions in EmbeddingProviderType.cs**

Same file, line 78: Change:
```csharp
EmbeddingProviderType.External => 1024,
```
To:
```csharp
EmbeddingProviderType.External => 768,
```

- [ ] **Step 3: Update PgVectorStoreAdapter default dimension**

File: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs`

Line 331: Change:
```csharp
int vectorDimension = 1024,
```
To:
```csharp
int vectorDimension = 768,
```

- [ ] **Step 4: Build to verify no compile errors**

```bash
cd apps/api/src/Api && dotnet build
```

Expected: Build succeeded. 0 errors.

### 3b: Compose File Changes

- [ ] **Step 5: Update compose.dev.yml**

File: `infra/compose.dev.yml`

Lines 60-62: Change:
```yaml
      EMBEDDING_MODEL: intfloat/multilingual-e5-large
      EMBEDDING_DIMENSIONS: 1024
      Embedding__Dimensions: 1024
```
To:
```yaml
      EMBEDDING_MODEL: intfloat/multilingual-e5-base
      EMBEDDING_DIMENSIONS: 768
      Embedding__Dimensions: 768
```

- [ ] **Step 6: Update compose.integration.yml**

File: `infra/compose.integration.yml`

Lines 41-43: Change:
```yaml
      EMBEDDING_MODEL: intfloat/multilingual-e5-large
      EMBEDDING_DIMENSIONS: 1024
      Embedding__Dimensions: 1024
```
To:
```yaml
      EMBEDDING_MODEL: intfloat/multilingual-e5-base
      EMBEDDING_DIMENSIONS: 768
      Embedding__Dimensions: 768
```

- [ ] **Step 7: Add embedding config to compose.staging.yml**

File: `infra/compose.staging.yml`

After line 75 (`RERANKER_URL: http://reranker-service:8003`), add:
```yaml
      EMBEDDING_MODEL: intfloat/multilingual-e5-base
      Embedding__Dimensions: 768
```

- [ ] **Step 8: Add embedding config to compose.prod.yml**

File: `infra/compose.prod.yml`

After line 97 (`RERANKER_URL: http://reranker-service:8003`), add:
```yaml
      EMBEDDING_MODEL: intfloat/multilingual-e5-base
      Embedding__Dimensions: 768
```

- [ ] **Step 9: Update docker-compose.yml base default**

File: `infra/docker-compose.yml`

Lines 207-208: Change:
```yaml
      # Override: EMBEDDING_MODEL=intfloat/multilingual-e5-base
      - EMBEDDING_MODEL=${EMBEDDING_MODEL:-intfloat/multilingual-e5-large}
```
To:
```yaml
      # Model: e5-base (768d, 3.7x faster than e5-large on ARM64)
      - EMBEDDING_MODEL=${EMBEDDING_MODEL:-intfloat/multilingual-e5-base}
```

- [ ] **Step 10: Commit all changes**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/EmbeddingProviderType.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs \
       infra/compose.dev.yml \
       infra/compose.integration.yml \
       infra/compose.staging.yml \
       infra/compose.prod.yml \
       infra/docker-compose.yml
git commit -m "feat(embedding): switch from e5-large to e5-base (768d, 3.7x faster)"
```

---

## Task 4: Deploy to Staging & Verify

**Depends on:** Task 1, 2, 3 completed.

- [ ] **Step 1: Push changes to remote**

```bash
git push origin frontend-dev
```

- [ ] **Step 2: Deploy to staging**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "cd /opt/meepleai/repo && git pull && cd /opt/meepleai && set -a && for f in secrets/*.secret; do source \"\$f\" 2>/dev/null; done && set +a && docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml --profile ai --profile monitoring --profile proxy build --parallel && docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml --profile ai --profile monitoring --profile proxy up -d --remove-orphans"
```

- [ ] **Step 3: Verify embedding service loaded e5-base**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "docker logs meepleai-embedding-service --tail=20 2>&1 | grep -i model"
```

Expected: Log shows `multilingual-e5-base` loaded.

- [ ] **Step 4: Verify basic auth still works after full redeploy**

```bash
# 401 without auth
curl -s -o /dev/null -w "%{http_code}" https://meepleai.app/services/embedding/health

# 200 with auth
curl -s -o /dev/null -w "%{http_code}" -u admin:MeepleAI-Services-2026 https://meepleai.app/services/embedding/health
```

- [ ] **Step 5: Verify migrations persisted**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "docker exec -i meepleai-postgres psql -U meepleai -d meepleai -c 'SELECT COUNT(*) FROM \"__EFMigrationsHistory\";'"
```

Expected: `6`.

---

## Summary

| Task | Type | Duration | Risk |
|------|------|----------|------|
| 1. Traefik auth | Server config | ~5 min | Low |
| 2. Migrations | DB schema | ~5 min | Zero (empty DB) |
| 3. e5-base code | Code changes | ~10 min | Low |
| 4. Deploy + verify | Deployment | ~10 min | Low |
