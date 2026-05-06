# P0 Critical Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 3 critical issues discovered by the comprehensive codebase analysis: missing HNSW vector index (RAG sequential scan), admin secrets endpoint exposed in production without middleware auth, and N8N webhook HMAC bypass when secret is unconfigured.

**Architecture:** Each fix is independent — HNSW index via EF Core migration, secrets endpoint via route group hardening + env guard, N8N webhook via fail-closed guard. No shared state between tasks.

**Tech Stack:** .NET 9, EF Core + Npgsql + pgvector, ASP.NET Minimal APIs, Quartz background jobs.

---

## Task 1: Add HNSW Index on pgvector Embedding Columns

**Context:** Every RAG similarity search in `HybridSearchService` triggers a full sequential scan because no vector similarity index exists on `pgvector_embeddings.vector`. The column is `vector(768)`.

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddHnswIndexPgvectorEmbeddings.cs`
- Modify: `apps/api/src/Api/Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/PgVectorEmbeddingEntityConfiguration.cs`

---

- [ ] **Step 1: Verify the current state — no HNSW index exists**

```bash
cd apps/api/src/Api
dotnet ef migrations list
```

Expected: last migration is `20260328162157_AddServiceCallLogs`. No HNSW index migration.

- [ ] **Step 2: Add HNSW index via HasIndex in entity configuration**

Open `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/PgVectorEmbeddingEntityConfiguration.cs`.

After the last `builder.HasIndex(...)` call (currently `builder.HasIndex(e => new { e.GameId, e.ChunkIndex })`), add:

```csharp
// HNSW index for vector similarity search (cosine distance).
// m=16: connections per node (accuracy vs build time). ef_construction=64: search width during build.
// These are conservative defaults — tune to m=32/ef_construction=128 once >100K rows.
builder.HasIndex(e => e.Vector)
    .HasMethod("hnsw")
    .HasOperators("vector_cosine_ops")
    .HasStorageParameter("m", 16)
    .HasStorageParameter("ef_construction", 64);
```

The full Configure method after the change:

```csharp
public void Configure(EntityTypeBuilder<PgVectorEmbeddingEntity> builder)
{
    builder.ToTable("pgvector_embeddings");

    builder.HasKey(e => e.Id);

    builder.Property(e => e.Id)
        .ValueGeneratedNever();

    builder.Property(e => e.VectorDocumentId)
        .IsRequired();

    builder.Property(e => e.GameId)
        .IsRequired();

    builder.Property(e => e.TextContent)
        .IsRequired();

    builder.Property(e => e.Vector)
        .IsRequired()
        .HasColumnType("vector(768)");

    builder.Property(e => e.Model)
        .IsRequired();

    builder.Property(e => e.ChunkIndex)
        .IsRequired();

    builder.Property(e => e.PageNumber)
        .IsRequired();

    builder.Property(e => e.CreatedAt)
        .IsRequired();

    builder.Property(e => e.Lang)
        .IsRequired()
        .HasMaxLength(5)
        .HasDefaultValue("en");

    builder.Property(e => e.SourceChunkId)
        .IsRequired(false);

    builder.Property(e => e.IsTranslation)
        .IsRequired()
        .HasDefaultValue(false);

    builder.HasIndex(e => e.VectorDocumentId);
    builder.HasIndex(e => e.GameId);
    builder.HasIndex(e => new { e.GameId, e.ChunkIndex });

    // HNSW index for vector similarity search (cosine distance).
    builder.HasIndex(e => e.Vector)
        .HasMethod("hnsw")
        .HasOperators("vector_cosine_ops")
        .HasStorageParameter("m", 16)
        .HasStorageParameter("ef_construction", 64);
}
```

- [ ] **Step 3: Generate the migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddHnswIndexPgvectorEmbeddings
```

Expected: new file `Infrastructure/Migrations/<timestamp>_AddHnswIndexPgvectorEmbeddings.cs` created.

- [ ] **Step 4: Verify the migration SQL is correct**

```bash
dotnet ef migrations script --idempotent --from 20260328162157_AddServiceCallLogs
```

Expected output should contain something like:
```sql
CREATE INDEX "ix_pgvector_embeddings_vector" ON pgvector_embeddings USING hnsw (vector vector_cosine_ops) WITH (m = '16', ef_construction = '64');
```

If the generated SQL doesn't include `USING hnsw`, the Npgsql.EntityFrameworkCore.PostgreSQL version may not support HNSW via Fluent API. In that case, follow Step 4b (manual migration) instead and skip Step 5.

- [ ] **Step 4b (fallback only): Manually edit migration if Fluent API doesn't generate HNSW syntax**

Open the generated migration file. Replace its `Up` and `Down` methods with:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("""
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_pgvector_embeddings_vector_hnsw
        ON pgvector_embeddings USING hnsw (vector vector_cosine_ops)
        WITH (m = 16, ef_construction = 64);
        """);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DROP INDEX IF EXISTS ix_pgvector_embeddings_vector_hnsw;");
}
```

Note: `CONCURRENTLY` prevents table lock during index build, safe for production use.

- [ ] **Step 5: Apply migration to dev database**

Ensure dev Docker stack is running (`cd infra && make dev-core`), then:

```bash
cd apps/api/src/Api
dotnet ef database update
```

Expected: `Applying migration '..._AddHnswIndexPgvectorEmbeddings'` in output, no errors.

- [ ] **Step 6: Verify index exists in database**

```bash
pwsh -c "docker exec meepleai-postgres psql -U postgres -d meepleai -c \"\di+ ix_pgvector*\""
```

Expected: row showing `ix_pgvector_embeddings_vector_hnsw` with `hnsw` access method.

- [ ] **Step 7: Commit**

```bash
cd apps/api/src/Api
git add Infrastructure/EntityConfigurations/KnowledgeBase/PgVectorEmbeddingEntityConfiguration.cs \
        Infrastructure/Migrations/ \
        Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs
git commit -m "perf(kb): add HNSW index on pgvector_embeddings.vector for RAG similarity search"
```

---

## Task 2: Harden Admin Secrets Endpoint (Env Guard + Middleware Auth)

**Context:** `/api/v1/admin/secrets/` endpoints can read/write `.secret` files and restart the API. They are registered in all environments (comment says "staging/dev" but `Program.cs:701` has no env guard) and the route group lacks `.AddEndpointFilter<RequireAdminSessionFilter>()` (defense-in-depth is missing; relies solely on manual handler checks).

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminSecretsEndpoints.cs` (add filter to group)
- Modify: `apps/api/src/Api/Program.cs` (add env guard)

---

- [ ] **Step 1: Add `RequireAdminSessionFilter` to the route group in `AdminSecretsEndpoints.cs`**

Open `apps/api/src/Api/Routing/AdminSecretsEndpoints.cs`. Find the `MapAdminSecretsEndpoints` method.

The current code:
```csharp
public static IEndpointRouteBuilder MapAdminSecretsEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/v1/admin/secrets")
        .WithTags("Admin", "Secrets");

    group.MapGet("/", HandleGetSecrets).WithName("GetSecrets");
    group.MapPut("/", HandleUpdateSecrets).WithName("UpdateSecrets");
    group.MapPost("/restart", HandleRestart).WithName("RestartApi");

    return app;
}
```

Replace with:
```csharp
public static IEndpointRouteBuilder MapAdminSecretsEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/v1/admin/secrets")
        .WithTags("Admin", "Secrets")
        .AddEndpointFilter<RequireAdminSessionFilter>();

    group.MapGet("/", HandleGetSecrets).WithName("GetSecrets");
    group.MapPut("/", HandleUpdateSecrets).WithName("UpdateSecrets");
    group.MapPost("/restart", HandleRestart).WithName("RestartApi");

    return app;
}
```

- [ ] **Step 2: Add environment guard in `Program.cs`**

Open `apps/api/src/Api/Program.cs`. Find line 701:
```csharp
    app.MapAdminSecretsEndpoints();          // Admin secrets management (staging/dev)
```

Replace with:
```csharp
    if (!app.Environment.IsProduction())
        app.MapAdminSecretsEndpoints();      // Admin secrets management (non-prod only)
```

- [ ] **Step 3: Verify the project builds**

```bash
cd apps/api/src/Api
dotnet build --no-restore
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Run existing tests to check for regressions**

```bash
cd tests/Api.Tests
dotnet test --filter "Category=Unit" --no-build 2>&1 | tail -10
```

Expected: All tests pass (or same failures as before this change).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/AdminSecretsEndpoints.cs \
        apps/api/src/Api/Program.cs
git commit -m "fix(security): add middleware auth and env guard to admin secrets endpoints"
```

---

## Task 3: Fail-Closed on N8N Webhook When Secret Is Not Configured

**Context:** `N8nWebhookEndpoints.cs` skips HMAC validation entirely when `WebhookSecret` is empty. Any caller can then inject `send_notification` payloads targeting arbitrary users. Fix: return HTTP 503 if the secret is not configured, rather than allowing unauthenticated access.

**Files:**
- Modify: `apps/api/src/Api/Routing/N8nWebhookEndpoints.cs`

---

- [ ] **Step 1: Change the secret validation to fail-closed**

Open `apps/api/src/Api/Routing/N8nWebhookEndpoints.cs`. Find the HMAC validation block (approx lines 48-66):

```csharp
// Validate HMAC signature
var secret = options.Value.WebhookSecret;
if (!string.IsNullOrEmpty(secret))
{
    var signature = context.Request.Headers["X-Webhook-Signature"].FirstOrDefault();
    if (string.IsNullOrEmpty(signature))
    {
        logger.LogWarning("n8n webhook missing X-Webhook-Signature header");
        return Results.Unauthorized();
    }

    var expectedSignature = ComputeHmacSha256(body, secret);
    if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSignature)))
    {
        logger.LogWarning("n8n webhook signature mismatch");
        return Results.Unauthorized();
    }
}
```

Replace with:
```csharp
// Validate HMAC signature — fail-closed: reject all requests if secret is not configured.
var secret = options.Value.WebhookSecret;
if (string.IsNullOrEmpty(secret))
{
    logger.LogError("n8n webhook received but WebhookSecret is not configured — rejecting request. Set N8N_WEBHOOK_SECRET in n8n.secret");
    return Results.Problem(
        detail: "Webhook endpoint is not configured",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}

var signature = context.Request.Headers["X-Webhook-Signature"].FirstOrDefault();
if (string.IsNullOrEmpty(signature))
{
    logger.LogWarning("n8n webhook missing X-Webhook-Signature header");
    return Results.Unauthorized();
}

var expectedSignature = ComputeHmacSha256(body, secret);
if (!CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(signature),
        Encoding.UTF8.GetBytes(expectedSignature)))
{
    logger.LogWarning("n8n webhook HMAC signature mismatch");
    return Results.Unauthorized();
}
```

- [ ] **Step 2: Build to verify no compile errors**

```bash
cd apps/api/src/Api
dotnet build --no-restore
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3: Verify `StatusCodes` is available (no new using needed)**

`StatusCodes` is in `Microsoft.AspNetCore.Http` which is already imported via global usings in .NET 9 web projects. If the build fails with "StatusCodes not found", add at top of file:
```csharp
using Microsoft.AspNetCore.Http;
```

- [ ] **Step 4: Run existing tests**

```bash
cd tests/Api.Tests
dotnet test --filter "Category=Unit" --no-build 2>&1 | tail -10
```

Expected: all passing (or same baseline failures).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/N8nWebhookEndpoints.cs
git commit -m "fix(security): fail-closed on n8n webhook when HMAC secret is not configured"
```

---

## Task 4: Validate SQL Identifiers in DataDiffEngine (Defense-in-Depth)

**Context:** `DataDiffEngine.FetchRowHashesAsync` interpolates `schema`, `table`, `pkColumns`, and `safeColumns` directly into SQL strings using identifier quoting. Column names are safe (they come from the DB via parameterized `information_schema` queries), but `schema` and `table` are passed by the caller without validation. An admin with malicious intent could pass `table = "games\" WHERE 1=1 --"` style values. Add an allowlist validator for identifier names.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/DataDiffEngine.cs`

---

- [ ] **Step 1: Add a static identifier validation method to `DataDiffEngine`**

Open `apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/DataDiffEngine.cs`.

Add this private static method before `FilterSafeColumns`:

```csharp
/// <summary>
/// Validates a PostgreSQL identifier (schema name, table name, column name).
/// Only allows letters, digits, underscores, and dollar signs — the safe subset
/// for PostgreSQL unquoted identifiers. Prevents identifier injection even when
/// identifiers are double-quoted in SQL.
/// </summary>
private static bool IsValidIdentifier(string identifier)
{
    if (string.IsNullOrEmpty(identifier) || identifier.Length > 63)
        return false;

    foreach (var ch in identifier)
    {
        if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '$')
            return false;
    }

    return true;
}

private static void ValidateIdentifier(string identifier, string paramName)
{
    if (!IsValidIdentifier(identifier))
        throw new ArgumentException(
            $"Invalid PostgreSQL identifier '{identifier}'. Only letters, digits, underscores, and dollar signs are allowed.",
            paramName);
}
```

- [ ] **Step 2: Add validation calls to `GetColumnsAsync`**

Find `GetColumnsAsync` in `DataDiffEngine.cs`. Add validation at the start of the method body:

```csharp
public static async Task<List<(string name, string type)>> GetColumnsAsync(
    NpgsqlConnection conn, string schema, string table, CancellationToken ct = default)
{
    ValidateIdentifier(schema, nameof(schema));
    ValidateIdentifier(table, nameof(table));

    var columns = new List<(string, string)>();
    // ... rest unchanged
```

- [ ] **Step 3: Add validation calls to `GetPrimaryKeyColumnsAsync`**

Same pattern:

```csharp
public static async Task<List<string>> GetPrimaryKeyColumnsAsync(
    NpgsqlConnection conn, string schema, string table, CancellationToken ct = default)
{
    ValidateIdentifier(schema, nameof(schema));
    ValidateIdentifier(table, nameof(table));

    var pkCols = new List<string>();
    // ... rest unchanged
```

- [ ] **Step 4: Add validation calls to `FetchRowHashesAsync`**

```csharp
public static async Task<Dictionary<string, string>> FetchRowHashesAsync(
    NpgsqlConnection conn, string schema, string table,
    IReadOnlyList<string> pkColumns, IReadOnlyList<string> safeColumns,
    CancellationToken ct = default)
{
    ValidateIdentifier(schema, nameof(schema));
    ValidateIdentifier(table, nameof(table));
    foreach (var col in pkColumns) ValidateIdentifier(col, "pkColumns");
    foreach (var col in safeColumns) ValidateIdentifier(col, "safeColumns");

    var pkExpr = string.Join(" || '::' || ", pkColumns.Select(c => $"\"{c}\"::text"));
    // ... rest unchanged
```

- [ ] **Step 5: Add validation to `GetEstimatedRowCountAsync`**

```csharp
public static async Task<long> GetEstimatedRowCountAsync(
    NpgsqlConnection conn, string schema, string table, CancellationToken ct = default)
{
    ValidateIdentifier(schema, nameof(schema));
    ValidateIdentifier(table, nameof(table));

    var cmd = new NpgsqlCommand(
    // ... rest unchanged
```

- [ ] **Step 6: Build**

```bash
cd apps/api/src/Api
dotnet build --no-restore
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 7: Run tests**

```bash
cd tests/Api.Tests
dotnet test --filter "Category=Unit" --no-build 2>&1 | tail -10
```

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DatabaseSync/Infrastructure/DataDiffEngine.cs
git commit -m "fix(security): validate SQL identifiers in DataDiffEngine to prevent identifier injection"
```

---

## Task 5: Fix Python Embedding Service Blocking Event Loop

**Context:** `apps/embedding-service/main.py` declares `generate_embeddings` as `async def` but calls `model.encode()` which is synchronous CPU-bound PyTorch inference. This blocks the FastAPI event loop for the full inference duration, preventing any concurrent request handling.

Same issue exists in `apps/reranker-service/main.py` with `model.predict()`.

**Files:**
- Modify: `apps/embedding-service/main.py`
- Modify: `apps/reranker-service/main.py`

---

- [ ] **Step 1: Fix embedding service — wrap model.encode in run_in_executor**

Open `apps/embedding-service/main.py`. Find the `generate_embeddings` endpoint (line ~133).

Find this block (approximate lines 178-192):
```python
        embeddings = model.encode(
            prefixed_texts,
            convert_to_numpy=True,
            show_progress_bar=False,
            normalize_embeddings=True
        )
```

The entire encoding call should be offloaded to a thread. Add at the top of the file (after existing imports):
```python
import asyncio
from functools import partial
```

Then replace the `model.encode(...)` call with:
```python
        loop = asyncio.get_event_loop()
        encode_fn = partial(
            model.encode,
            prefixed_texts,
            convert_to_numpy=True,
            show_progress_bar=False,
            normalize_embeddings=True
        )
        embeddings = await loop.run_in_executor(None, encode_fn)
```

- [ ] **Step 2: Fix reranker service — wrap model.predict in run_in_executor**

Open `apps/reranker-service/main.py`. Find the `rerank` endpoint and the `model.predict(pairs, ...)` call (line ~207).

Add at the top of the file (after existing imports):
```python
import asyncio
from functools import partial
```

Find the `model.predict(pairs, ...)` call. Replace with:
```python
        loop = asyncio.get_event_loop()
        predict_fn = partial(
            model.predict,
            pairs,
            batch_size=BATCH_SIZE,
            show_progress_bar=False
        )
        scores = await loop.run_in_executor(None, predict_fn)
```

(Adjust parameter names to match the actual call in the file.)

- [ ] **Step 3: Verify services start correctly**

```bash
cd infra
make dev-core
# Wait 30s for services to be healthy, then:
pwsh -c "docker logs meepleai-embedding-service --tail=20"
pwsh -c "docker logs meepleai-reranker-service --tail=20"
```

Expected: no import errors, services report `Application startup complete`.

- [ ] **Step 4: Test embedding endpoint responds**

```bash
pwsh -c "Invoke-WebRequest -Uri 'http://localhost:8001/health' -UseBasicParsing | Select-Object StatusCode, Content"
```

Expected: `StatusCode: 200`.

- [ ] **Step 5: Commit**

```bash
git add apps/embedding-service/main.py \
        apps/reranker-service/main.py
git commit -m "fix(ai-services): offload CPU-bound model inference to thread pool to unblock asyncio event loop"
```

---

## Final Validation

After all 5 tasks are complete:

- [ ] **Full backend build**
```bash
cd apps/api/src/Api
dotnet build
```

- [ ] **All unit tests green**
```bash
cd tests/Api.Tests
dotnet test --filter "Category=Unit" 2>&1 | tail -5
```

- [ ] **Create PR to `main-dev`**
```bash
git checkout main-dev && git pull
git checkout -b fix/p0-critical-security-perf
# cherry-pick or merge all 5 fix commits
git push -u origin fix/p0-critical-security-perf
gh pr create \
  --base main-dev \
  --title "fix: P0 critical security and performance fixes" \
  --body "$(cat <<'EOF'
## Summary
- **perf**: HNSW index on pgvector_embeddings.vector — eliminates sequential scan on every RAG query
- **security**: Admin secrets endpoints now require middleware-level auth and are hidden in production
- **security**: N8N webhook fails closed when HMAC secret is not configured
- **security**: DataDiffEngine validates SQL identifiers before string interpolation
- **fix**: Python embedding/reranker services no longer block asyncio event loop during inference

## Test Plan
- [ ] Run `dotnet test --filter Category=Unit`
- [ ] Verify HNSW index exists: `psql -c "\di+ ix_pgvector*"`
- [ ] Verify `/api/v1/admin/secrets/` returns 401 without valid admin session
- [ ] Verify n8n webhook returns 503 when `N8N_WEBHOOK_SECRET` is unset
- [ ] Verify embedding service health endpoint responds after restart

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
