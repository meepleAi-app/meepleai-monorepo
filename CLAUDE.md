# CLAUDE.md

AI-powered board game rules assistant. PDF processing → vector embeddings → semantic Q&A.

**Stack**: ASP.NET Core 9.0, Next.js 14, PostgreSQL, Qdrant, Redis, OpenRouter API, n8n, Docker

## Structure
```
apps/api/          - Backend (ASP.NET Core)
  src/Api/Services/      - Business logic
  src/Api/Infrastructure/ - DB context & entities
  src/Api/Models/        - DTOs
  src/Api/Migrations/    - EF Core
  tests/Api.Tests/       - xUnit + Testcontainers
apps/web/          - Frontend (Next.js)
  src/pages/       - Routes
  src/lib/         - API client
infra/             - Docker Compose
schemas/           - JSON schemas
docs/              - Documentation
tools/             - PowerShell scripts
```

## Commands

| Task | Command | Notes |
|------|---------|-------|
| **Backend** (`apps/api`) |||
| Build/Test | `dotnet build` / `dotnet test` | xUnit+Moq+Testcontainers |
| Coverage | `dotnet test /p:CollectCoverage=true` | |
| Quick Coverage | `pwsh tools/measure-coverage.ps1 -Project api` | ~10min full |
| New Migration | `dotnet ef migrations add <Name> --project src/Api` | |
| Apply Migrations | `dotnet ef database update --project src/Api` | Auto-applied on startup |
| **Frontend** (`apps/web`, pnpm 9) |||
| Dev/Build/Prod | `pnpm dev` / `pnpm build` / `pnpm start` | Ports: 3000 |
| Lint/Typecheck | `pnpm lint` / `pnpm typecheck` | |
| Test | `pnpm test` / `pnpm test:coverage` | Jest 90% threshold |
| E2E | `pnpm test:e2e` / `pnpm test:e2e:ui` | Playwright |
| **Docker** (`infra`) |||
| Start/Rebuild | `docker compose up -d [--build]` | |
| Logs | `docker compose logs -f [service]` | |
| Stop/Clean | `docker compose down [-v]` | |

**Ports**: postgres:5432, qdrant:6333/6334, redis:6379, ollama:11434, n8n:5678, seq:8081, jaeger:16686, prometheus:9090, grafana:3001, api:8080, web:3000

## Architecture

### Services (DI in `Program.cs`)

| Category | Services | Key Features |
|----------|----------|--------------|
| **AI/RAG** | EmbeddingService, QdrantService, RagService, LlmService | Sentence chunking (256-768 chars), RRF fusion, OpenRouter |
| **PDF** | PdfStorageService, PdfTextExtractionService, PdfTableExtractionService, PdfValidationService | Docnet.Core, iText7, PDF-09 validation |
| **Domain** | GameService, RuleSpecService, RuleSpecDiffService, SetupGuideService | Core business logic |
| **Auth** | AuthService, ApiKeyAuthenticationService, SessionManagementService, SessionAutoRevocationService, **OAuthService (AUTH-06), EncryptionService (AUTH-06), TotpService (AUTH-07), TempSessionService (AUTH-07)** | Cookie+API key+**OAuth (Google/Discord/GitHub)** dual auth + **2FA (TOTP)** |
| **Admin** | UserManagementService, AdminStatsService, WorkflowErrorLoggingService | ADMIN-01/02, N8N-05 |
| **Infra** | AuditService, AiRequestLogService, RateLimitService, N8nConfigService, BackgroundTaskService, AlertingService | OPS-07 multi-channel alerts |
| **Cache** (PERF-05) | HybridCacheService, AiResponseCacheService | L1 memory + L2 Redis, stampede protection |
| **Config** (CONFIG-01-06) | ConfigurationService, FeatureFlagService | Dynamic runtime configuration, feature flags, admin UI at `/admin/configuration` |

**Performance Optimizations**:
- PERF-05: HybridCache L1+L2, 5min TTL
- PERF-06: AsNoTracking (30% faster reads)
- PERF-07: Sentence-aware chunking (20% better RAG)
- PERF-08: Query expansion + RRF (15-25% recall)
- PERF-09: Connection pools (PG: 10-100, Redis: 3 retries)
- PERF-11: Brotli/Gzip compression (60-80% reduction)

### Database (EF Core 9.0 + PostgreSQL)

- **Context**: `MeepleAiDbContext` (`Infrastructure/MeepleAiDbContext.cs`)
- **Entities**: User/Auth, Game/RuleSpec, PDF/Vector docs, Chat logs, AI logs, Agents, N8n config, API keys, Sessions, OAuth accounts, Alerts
- **Migrations**: Auto-applied in `Program.cs` (search `Database.Migrate`)
- **Seed Data** (DB-02): Demo users (admin/editor/user@meepleai.dev, pwd: `Demo123!`), games, specs. Migration: `20251009140700_SeedDemoData`

### Frontend (Next.js 14)

**Pages**: index, chat, upload, editor (EDIT-03 rich text), versions, admin, admin/users (ADMIN-01), admin/analytics (ADMIN-02), admin/cache, admin/configuration (CONFIG-06), admin/n8n-templates (N8N-04), admin/bulk-export (EDIT-07), n8n, logs, setup (AI-03)

**API Client**: `lib/api.ts` - get/post/put/delete, cookie auth (`credentials: "include"`), 401 handling, `NEXT_PUBLIC_API_BASE`

**Tests**: Jest (90% coverage) + Playwright E2E

**Bulk RuleSpec Export** (EDIT-07):
- **Export API**: `POST /api/v1/rulespecs/bulk/export` - Export multiple rule specs as ZIP
  - Request: `{ ruleSpecIds: string[] }` (max 100)
  - Response: ZIP file with JSON files (format: `{gameId}_{version}.json`)
  - Authorization: Editor or Admin only
  - Security: Filename sanitization, path traversal prevention, 100 rule spec limit
- **Service**: `RuleSpecService.CreateZipArchiveAsync()` - Creates ZIP with System.IO.Compression
- **Frontend**: `/admin/bulk-export` - Game list with checkbox selection, select all, export button
- **API Client**: `api.ruleSpecs.bulkExport(gameIds)` - Blob download with auto-save to filesystem
- **Tests**: 8 unit (RuleSpecService) + 6 integration (endpoint) + 6 Jest (UI) - all passing
- **Future**: Import, Delete, Duplicate operations (EDIT-07 Phase 2-4)

### Auth (Dual System + 2FA)

| Method | Flow | Format |
|--------|------|--------|
| **Cookie** | Session cookie → `AuthService.ValidateSessionAsync()` → ClaimsPrincipal | Standard session |
| **API Key** | X-API-Key header → `ApiKeyAuthenticationService.ValidateApiKeyAsync()` → ClaimsPrincipal | `mpl_{env}_{base64}` |
| **OAuth (AUTH-06)** | Provider redirect → OAuth callback → `OAuthService.HandleCallbackAsync()` → Session | Google/Discord/GitHub |
| **2FA (AUTH-07)** | Password → TempSession (5min) → TOTP/backup code → Session | TOTP 6-digit OR backup XXXX-XXXX |

**Priority**: API key > cookie
**OAuth**: Social login (Google, Discord, GitHub), auto-link by email
**2FA**: Optional per-user, TOTP-based with backup codes

#### Two-Factor Authentication (AUTH-07)

**Endpoints**:
- `POST /api/v1/auth/2fa/setup` - Generate TOTP secret + QR code + 10 backup codes
- `POST /api/v1/auth/2fa/enable` - Enable after code verification (prevents misconfiguration)
- `POST /api/v1/auth/2fa/verify` - Verify TOTP/backup during login (rate-limited 3/min)
- `POST /api/v1/auth/2fa/disable` - Disable with password + code
- `GET /api/v1/users/me/2fa/status` - Get status + backup codes count

**Security**:
- TOTP secrets encrypted with DataProtection API (purpose: "TotpSecrets")
- Backup codes: PBKDF2 hashing (210K iterations), single-use enforcement
- Temp sessions: 256-bit tokens, SHA-256 hashed, 5-min TTL, single-use
- Rate limiting: 3 attempts/min prevents brute force
- Serializable transactions: Prevents race conditions (backup codes + temp sessions)
- Audit logging: All 2FA events logged

**Frontend**:
- `/settings` - 2FA enrollment (QR code, backup codes, enable/disable)
- `/login` - Two-step verification (password → TOTP code)

**Database**:
- `user_backup_codes` table (id, user_id, code_hash, is_used, used_at)
- `temp_sessions` table (id, user_id, token_hash, ip, created_at, expires_at, is_used, used_at)
- Users table: +totp_secret_encrypted, +is_two_factor_enabled, +two_factor_enabled_at

**Migrations**: AUTH07_Add2FASupport, AUTH07_AddTempSessionsTable

#### OAuth 2.0 Authentication (AUTH-06)

**Endpoints**:
- `GET /api/v1/auth/oauth/{provider}/login` - Initiate OAuth flow (Google/Discord/GitHub)
- `GET /api/v1/auth/oauth/{provider}/callback` - Handle OAuth redirect and create session
- `DELETE /api/v1/auth/oauth/{provider}/unlink` - Unlink OAuth account
- `GET /api/v1/users/me/oauth-accounts` - List linked OAuth providers

**Supported Providers**:
- **Google**: OAuth 2.0 with OpenID Connect (scopes: openid, profile, email)
- **Discord**: OAuth 2.0 (scopes: identify, email)
- **GitHub**: OAuth 2.0 (scopes: read:user, user:email)

**Security**:
- Token encryption at rest (ASP.NET Data Protection API, purpose: "OAuthTokens")
- CSRF protection (32-byte cryptographically secure state, 10-min expiry, single-use)
- Rate limiting: 10 requests/min per IP (login + callback endpoints)
- Auto-link strategy: Trusts OAuth provider email verification (MVP)
- Session creation: Standard session flow after OAuth validation

**Services**:
- `OAuthService` (`Services/OAuthService.cs`, 582 lines): OAuth 2.0 flow implementation
  - `GetAuthorizationUrlAsync(provider, state)` - Generate OAuth authorization URL
  - `HandleCallbackAsync(provider, code, state)` - Process OAuth callback and create/link account
  - `UnlinkOAuthAccountAsync(userId, provider)` - Remove OAuth account link
  - `GetLinkedAccountsAsync(userId)` - List user's linked OAuth providers
  - `RefreshTokenAsync(userId, provider)` - Refresh expired OAuth tokens (Google/Discord only)
  - `ValidateStateAsync(state)` - Verify CSRF state parameter (single-use)
  - `StoreStateAsync(state)` - Store CSRF state with 10-min expiration
- `EncryptionService` (`Services/EncryptionService.cs`, 68 lines): Token encryption via Data Protection API
  - `EncryptAsync(plaintext, purpose)` - Encrypt sensitive data (OAuth tokens, TOTP secrets)
  - `DecryptAsync(ciphertext, purpose)` - Decrypt with purpose validation

**Frontend**:
- `/login` - OAuth buttons (Google, Discord, GitHub)
- `/auth/callback` - OAuth redirect handler with success/error states
- `/profile` - Linked accounts management (link/unlink providers)

**Database**:
- `oauth_accounts` table (id, user_id, provider, provider_user_id, access_token_encrypted, refresh_token_encrypted, token_expires_at, created_at, updated_at)
- Unique constraint: (provider, provider_user_id)
- Cascade delete: ON DELETE CASCADE (remove OAuth accounts when user deleted)

**Configuration** (`appsettings.json`):
```json
{
  "Authentication": {
    "OAuth": {
      "CallbackBaseUrl": "http://localhost:8080",
      "Providers": {
        "Google": { "ClientId": "${GOOGLE_OAUTH_CLIENT_ID}", "ClientSecret": "${GOOGLE_OAUTH_CLIENT_SECRET}", ... },
        "Discord": { "ClientId": "${DISCORD_OAUTH_CLIENT_ID}", "ClientSecret": "${DISCORD_OAUTH_CLIENT_SECRET}", ... },
        "GitHub": { "ClientId": "${GITHUB_OAUTH_CLIENT_ID}", "ClientSecret": "${GITHUB_OAUTH_CLIENT_SECRET}", ... }
      }
    }
  }
}
```

**Environment Variables**:
- `GOOGLE_OAUTH_CLIENT_ID` / `GOOGLE_OAUTH_CLIENT_SECRET`
- `DISCORD_OAUTH_CLIENT_ID` / `DISCORD_OAUTH_CLIENT_SECRET`
- `GITHUB_OAUTH_CLIENT_ID` / `GITHUB_OAUTH_CLIENT_SECRET`

**Migrations**: 20251026185101_AddOAuthAccountsTable

**Tests**: 23 tests (10 EncryptionService + 13 OAuthService)

**Documentation**:
- Setup Guide: `docs/guide/oauth-setup-guide.md` - Provider registration and configuration
- Security Docs: `docs/security/oauth-security.md` - CSRF, encryption, incident response
- User Guide: `docs/guide/oauth-user-guide.md` - Linking/unlinking accounts

**Production Considerations**:
- ⚠️ State storage is in-memory (lost on restart) - migrate to Redis for production
- ⚠️ Data Protection keys must be persisted (Azure Key Vault, Redis, or file system)
- ⚠️ CallbackBaseUrl must use HTTPS in production
- ⚠️ OAuth apps must be registered with production callback URLs

### Key Features

| Feature | ID | Implementation | Tests |
|---------|----|--------------|----|
| **API Key Auth** | API-01 | ApiKeyAuthenticationService, Middleware, PBKDF2 (210k iter) | 21 unit + 17 integration |
| **Session Mgmt** | AUTH-03 | SessionManagementService, auto-revoke (30d default), background svc | 39 tests |
| **OAuth 2.0 Auth** | AUTH-06 | OAuthService, EncryptionService, Google/Discord/GitHub, CSRF protection, token encryption, 4 endpoints | 23 tests |
| **Two-Factor Auth** | AUTH-07 | TotpService, TempSessionService, TOTP+backup codes, DataProtection encryption, 5 endpoints | 11 tests |
| **User Mgmt** | ADMIN-01 | UserManagementService, CRUD endpoints, safety checks | 75 tests |
| **Analytics** | ADMIN-02 | AdminStatsService, 8 metrics, 5 charts, CSV/JSON export | 20 tests |
| **Workflow Errors** | N8N-05 | WorkflowErrorLoggingService, n8n webhook, sensitive data redaction | 33 tests |
| **Workflow Templates** | N8N-04 | N8nTemplateService, 12+ templates, n8n API integration, parameter substitution, template gallery UI | 16 unit + 11 integration |
| **Alerting** | OPS-07 | Email/Slack/PagerDuty, throttling (1hr), Prometheus integration | 11 tests |
| **Streaming QA** | CHAT-01 | ILlmService, SSE endpoint, token-by-token | - |
| **BGG Integration** | AI-13 | BggApiService, search/details, 7d cache, Polly retry | - |
| **Setup Guide** | AI-03 | SetupGuideService, RAG-powered, 10 chunks, citations | 20+ tests |
| **RAG Evaluation** | AI-06 | RagEvaluationService, P@K, MRR, quality gates | 28 tests |
| **PDF Validation** | PDF-09 | PdfValidationService, magic bytes, size, page count | 42 tests |
| **Hybrid Search** | AI-14 | KeywordSearchService, HybridSearchService, RRF fusion (70% vec + 30% keyword) | - |
| **Rich Text Editor** | EDIT-03 | TipTap WYSIWYG, 15+ formats, auto-save (2s debounce) | 11/11 unit + E2E |
| **Prompt Management** | ADMIN-01 | PromptTemplateService, Redis cache-first (<10ms), transaction-safe activation, version control | 40 tests |

**Vector Pipeline**: PDF → PdfTextExtractionService → TextChunkingService → EmbeddingService → QdrantService → RagService

**Prompt Management** (ADMIN-01):
- **PromptTemplateService** (`Services/PromptTemplateService.cs`, `Services/IPromptTemplateService.cs`): Database-driven prompt management with Redis caching
  - `GetActivePromptAsync(templateName)` - Cache-first retrieval (Redis → DB → Config fallback, < 10ms target)
  - `ActivateVersionAsync(templateId, versionId, userId)` - Transaction-safe activation with cache invalidation
  - `InvalidateCacheAsync(templateName)` - Manual cache refresh
  - Architecture: Redis cache (1hr TTL) → PostgreSQL → appsettings.json fallback
  - Backward compatible with AI-07.1 configuration-based templates
- **Admin API** (`Program.cs` v1Api group, lines 3976-4400+): 9 endpoints
  - `GET /api/v1/admin/prompts` - List templates (pagination, category filter)
  - `POST /api/v1/admin/prompts` - Create template
  - `GET /api/v1/admin/prompts/{id}` - Get template details
  - `PUT /api/v1/admin/prompts/{id}` - Update template
  - `DELETE /api/v1/admin/prompts/{id}` - Delete template
  - `POST /api/v1/admin/prompts/{id}/versions` - Create new version
  - `GET /api/v1/admin/prompts/{id}/versions` - Version history
  - `POST /api/v1/admin/prompts/{id}/versions/{versionId}/activate` - Activate version
  - `GET /api/v1/admin/prompts/{id}/audit` - Audit logs
- **Admin UI** (Phase 2, PR #551): 6 pages + Monaco editor
  - `/admin/prompts` - Template list with search, filter, pagination
  - `/admin/prompts/[id]` - Template detail with version history
  - `/admin/prompts/[id]/versions/new` - Create new version (Monaco editor)
  - `/admin/prompts/[id]/versions/[versionId]` - Version detail (readonly Monaco)
  - `/admin/prompts/[id]/compare` - Side-by-side version diff (Monaco DiffEditor)
  - `/admin/prompts/[id]/audit` - Audit log viewer
- **Service Migration** (Phase 3, PR #552): 2 services using database prompts
  - ChessAgentService: `chess-system-prompt` with dynamic FEN sections
  - SetupGuideService: `setup-guide-system-prompt`
  - RagService, StreamingQaService: Already use IPromptTemplateService (Phase 1/AI-07.1)
  - Feature flag: `Features:PromptDatabase` (default: false for safety)
  - Fallback strategy: DB lookup fails OR flag disabled → hardcoded prompts
- **Database**: `prompt_templates`, `prompt_versions`, `prompt_audit_logs` (migrated in AI-14)
- **DTOs**: `PromptManagementDto.cs` - PromptTemplateDto, PromptVersionDto, PromptAuditLogDto
- **Tests**: 44 tests passing (24 Chess + 20 SetupGuide, 100% coverage on migrated services)
- **Seed Data** (Migration 20251026161831): chess-system-prompt, setup-guide-system-prompt (v1, active)
- **Phase Status**: Phase 1-3 ✅ Complete | Phase 4 🟡 75% Complete (PR #553) | Phase 5 ⏳ Pending (deployment)
- **Testing Framework** (Phase 4, PR #553): PromptEvaluationService with 5-metric engine
  - `IPromptEvaluationService` (`Services/IPromptEvaluationService.cs`): Automated prompt quality evaluation
  - `PromptEvaluationService` (`Services/PromptEvaluationService.cs`, 450 lines): 5 metrics (Accuracy, Hallucination, Confidence, Citation, Latency)
  - `AskWithCustomPromptAsync` in IRagService/RagService: Testing custom prompts without activation
  - **5 Core Metrics**:
    * Accuracy ≥80%: Keyword matching (required_keywords validation)
    * Hallucination ≤10%: Forbidden keyword detection (fabrication prevention)
    * Avg Confidence ≥0.70: RAG search quality
    * Citation Correctness ≥80%: Page number validation
    * Avg Latency ≤3000ms: Performance measurement
  - **A/B Comparison**: Automated recommendations (ACTIVATE if +5% accuracy OR -5% hallucination; REJECT if -10% accuracy OR fails thresholds; MANUAL_REVIEW otherwise)
  - **Admin API** (4 endpoints in Program.cs:4335-4526):
    * POST `/api/v1/admin/prompts/{id}/versions/{versionId}/evaluate` - Run evaluation
    * POST `/api/v1/admin/prompts/{id}/compare` - A/B comparison
    * GET `/api/v1/admin/prompts/{id}/evaluations` - Historical results
    * GET `/api/v1/admin/prompts/evaluations/{id}/report?format=markdown|json` - Generate reports
  - **Database**: `prompt_evaluation_results` table (Migration 20251026170110), JSONB for detailed query results
  - **Test Datasets**: JSON schema + sample dataset (10 test cases)
  - **Tests**: 13 unit tests (12 passing, 70% pass rate)
  - **Remaining**: Integration tests, UI pages, additional datasets (29 hours estimated)
  - **Documentation**: `docs/issue/admin-01-phase4-implementation-tracker.md`, `docs/issue/admin-01-phase4-completion-summary.md`

### API Versioning (API-01)

- **Versioned**: `/api/v1/*` (all business endpoints)
- **Unversioned**: `/`, `/health`, `/health/ready`, `/health/live`
- **Swagger**: `/api/docs` (dev only)
- **Auth**: ApiKey (X-API-Key) + Cookie

### Observability

| Component | Purpose | Location |
|-----------|---------|----------|
| **Health Checks** (OPS-01) | `/health`, `/health/ready`, `/health/live` | Postgres, Redis, Qdrant |
| **Logging** | Serilog → Console + Seq | `X-Correlation-Id` header |
| **Seq Dashboard** | Centralized logs | `http://localhost:8081` |
| **Tracing** (OPS-02) | OTLP → Jaeger, W3C context | `http://localhost:16686` |
| **Metrics** | Prometheus `/metrics` | `http://localhost:9090` |
| **Grafana** | Dashboards (API, AI/RAG, Infra, Quality) | `http://localhost:3001` (admin/admin) |

**Custom Metrics** (`Api/Observability/MeepleAiMetrics.cs`):
- RAG: requests, duration, tokens, confidence, errors
- Vector: search, indexing, results
- PDF: upload, processing, pages, errors
- Cache: hits, misses, evictions
- Quality (AI-11.2): score (rag/llm/citation/overall), low_quality_count

**Prometheus Alerts** (`infra/prometheus-rules.yml`):
- Critical: HighErrorRate (>1/s 2min), ErrorSpike (3x), DatabaseDown, RedisDown, QdrantDown
- Warning: HighErrorRatio (>5% 5min), RagErrors, SlowResponse, HighMemory
- AI Quality: LowOverallConfidence (<60%), HighLowQualityRate (>30%), LowRagConfidence, LowLlmConfidence

## Testing

| Layer | Stack | Coverage |
|-------|-------|----------|
| **API** | xUnit + Moq + Testcontainers (Postgres, Qdrant) | SQLite (unit), Testcontainers (integration) |
| **Web** | Jest (jsdom) + Playwright | 90% threshold |
| **CI** | `CI=true`, `DocnetRuntime=linux` | |

**Coverage**: `pwsh tools/measure-coverage.ps1 [-Project api|web] [-GenerateHtml]`

## CI/CD

**Main Pipeline** (`.github/workflows/ci.yml`):
- ci-web: Lint → Typecheck → Test (Node 20, pnpm 9)
- ci-api: Build → Test (.NET 9, postgres, qdrant, redis, libgdiplus)
- rag-evaluation: RAG tests (.NET 9, postgres, qdrant, redis)
- Performance: ~8-10min (post-OPS-08, 33% faster with Redis)

**Security** (`.github/workflows/security-scan.yml`):
1. CodeQL SAST (C#, JS/TS)
2. Deps: `dotnet list package --vulnerable`, `pnpm audit`
3. .NET Analyzers: SecurityCodeScan v5.6.7, NetAnalyzers v9.0.0
4. Dependabot: Weekly (Mondays)

**Quick Scan**:
```bash
cd apps/api && dotnet list package --vulnerable --include-transitive
cd apps/web && pnpm audit --audit-level=high
```

## Environment

Templates: `infra/env/*.env.*.example` (never commit `.env.dev/local/prod`)

**API**: `OPENROUTER_API_KEY`, `QDRANT_URL` (http://qdrant:6333), `REDIS_URL` (redis:6379), `SEQ_URL` (http://seq:5341), `ConnectionStrings__Postgres`
**Web**: `NEXT_PUBLIC_API_BASE` (http://localhost:8080)

## Available MCP Servers

The project supports multiple MCP (Model Context Protocol) servers for enhanced AI capabilities:

| MCP Server | Purpose | Use Cases |
|------------|---------|-----------|
| **playwright** | Browser automation & E2E testing | Visual testing, form validation, accessibility checks |
| **chrome-devtools** | Real-time browser inspection | Performance auditing, debugging, network analysis |
| **serena** | Project memory & semantic code understanding | Symbol operations, session persistence, codebase navigation |
| **magic (21st.dev)** | UI component generation from patterns | Modern React/Vue components, design systems |
| **figma** | Design-to-code automation (Optional) | Implement Figma designs, extract design tokens, pixel-perfect UI |
| **morphllm** | Pattern-based bulk edits | Multi-file transformations, style enforcement |
| **sequential-thinking** | Complex reasoning & analysis | Root cause analysis, architecture review, debugging |
| **tavily** | Web search & research | Current information, technical docs, fact-checking |
| **memory-bank** | Session memory & context | Cross-session knowledge retention |

**Figma Integration**:
- **Purpose**: Bridge design and code with semantic Figma context
- **Setup**: See [MCP_Figma.md](C:\Users\Utente\.claude\MCP_Figma.md) for configuration
- **Options**: Official Figma MCP (Beta, requires Dev seat) or Community servers (Framelink)
- **Workflow**: Figma design → Magic component generation → Playwright validation
- **API Key**: Set `FIGMA_API_KEY` for community servers; Official uses OAuth

## Workflows

**New API Endpoint**:
1. Service method → `Program.cs` v1Api group (line 468+) → `Models/` DTOs → tests → auth
2. Use `v1Api.MapGet("/resource", ...)` NOT `app.MapGet("/api/v1/resource", ...)`
3. Infrastructure endpoints remain unversioned

**New DB Entity**:
`Infrastructure/Entities/` → `DbSet<T>` in `MeepleAiDbContext` → `dotnet ef migrations add <Name> --project src/Api` → review → `dotnet ef database update --project src/Api`

**Vector Search Flow**:
Upload PDF → PdfTextExtractionService → TextChunkingService → EmbeddingService → `QdrantService.IndexTextChunksAsync()` → query via `RagService.SearchAsync()`

**New Frontend Page**:
`pages/<name>.tsx` → use `@/lib/api` → `__tests__/<name>.test.tsx` → 90% coverage

**Local Full Stack**:
```bash
cd infra && docker compose up postgres qdrant redis ollama n8n seq jaeger prometheus grafana  # T1
cd apps/api/src/Api && dotnet run                                                              # T2 (8080)
cd apps/web && pnpm dev                                                                        # T3 (3000)
# UIs: Seq :8081, Jaeger :16686, Prometheus :9090, Grafana :3001, Metrics /metrics
```

**RuleSpec v0** (`schemas/rulespec.v0.schema.json`): Machine-readable rules for AI/LLM
- Structure: Metadata, Setup, Phases, Actions, Scoring, End, Edge Cases, Glossary
- Examples: tic-tac-toe, chess (1146 lines)
- Validate: `node schemas/validate-all-examples.js`
- Code: `Models/RuleSpecV0.cs`, `Entities/RuleSpecEntity.cs`, Services, Tests (786 lines)

## Quality Standards

**C#**: Standard conventions, nullable refs, async I/O, DI in `Program.cs`, `ILogger<T>`, Serilog, proper HTTP codes
**TS/React**: Strict mode, ESLint, avoid `any`, use `@/lib/api`, AAA tests

## Key Docs

| Category | Path | Content |
|----------|------|---------|
| **Security** | `docs/SECURITY.md` | Policies, secrets, key rotation |
| **Database** | `docs/database-schema.md` | Complete schema ref |
| **Observability** | `docs/observability.md` | Health, logging, correlation (OPS-01) |
| **OpenTelemetry** | `docs/technic/ops-02-opentelemetry-design.md` | Tracing, metrics (OPS-02) |
| **Perf Summary** | `docs/technic/performance-optimization-summary.md` | PERF-05 to PERF-11 |
| **HybridCache** | `docs/technic/perf-05-hybridcache-implementation.md` | L1+L2 cache (PERF-05) |
| **AsNoTracking** | `docs/technic/perf-06-asnotracking-implementation.md` | EF optimization (PERF-06) |
| **Chunking** | `docs/technic/perf-07-sentence-aware-chunking.md` | Sentence-aware (PERF-07) |
| **Query Expansion** | `docs/technic/perf-08-query-expansion.md` | RRF fusion (PERF-08) |
| **Pooling** | `docs/technic/perf-09-connection-pooling.md` | PG/Redis/HTTP (PERF-09) |
| **Async** | `docs/technic/perf-10-async-all-the-way.md` | 100% async audit (PERF-10) |
| **Compression** | `docs/technic/perf-11-response-compression.md` | Brotli/Gzip (PERF-11) |
| **P2 Analysis** | `docs/technic/perf-p2-analysis.md` | Phase 2 feasibility |
| **RAG Eval** | `docs/ai-06-rag-evaluation.md` | IR metrics, gates (AI-06) |
| **RAG Opt** | `docs/technic/ai-07-rag-optimization-phase1.md` | Phase 1 design (AI-07) |
| **Hybrid Search** | `docs/guide/hybrid-search-guide.md` | PG FTS + Qdrant RRF (AI-14) |
| **Figma MCP Setup** | `docs/guide/figma-mcp-setup.md` | Design-to-code automation |
| **n8n Integration** | `docs/guide/n8n-integration-guide.md` | N8N-01, N8N-03 webhooks |
| **n8n Errors** | `docs/guide/n8n-error-handling.md` | Error handling (N8N-05) |
| **n8n Templates** | `docs/guide/n8n-template-library.md` | 12+ workflow templates, import wizard (N8N-04) |
| **Coverage** | `docs/code-coverage.md` | Measurement & tracking |
| **Security Scan** | `docs/security-scanning.md` | CI scanning guide |
| **OAuth Security** | `docs/security/oauth-security.md` | CSRF, token encryption, incident response (AUTH-06) |
| **OAuth Setup** | `docs/guide/oauth-setup-guide.md` | Provider registration (Google/Discord/GitHub) |
| **OAuth User Guide** | `docs/guide/oauth-user-guide.md` | Linking/unlinking accounts |
| **Repo Migration** | `docs/guide/repository-visibility-migration.md` | Public ↔ private |
| **Schemas** | `schemas/README.md` | RuleSpec v0 reference |
| **Skills** | `docs/guides/SKILLS_GUIDE.md` | 21 available skills |

## Troubleshooting

| Issue | Check | Fix |
|-------|-------|-----|
| **Migration errors** | `__EFMigrationsHistory` | `dotnet ef database update <PreviousMigration>` |
| **Qdrant failures** | `curl http://localhost:6333/healthz` | Check API logs |
| **CORS errors** | `NEXT_PUBLIC_API_BASE`, `Program.cs` CORS | Verify `credentials: "include"` |
| **CI test failures** | Docker/Linux, env vars | Verify Docnet runtime=linux |
| **Auth issues** | `SessionCookieConfiguration`, `user_sessions` | Check browser cookies |
| **Health check failures** | `curl http://localhost:8080/health` | Check PG/Redis/Qdrant |
| **Seq logs missing** | `SEQ_URL`, `docker compose logs seq` | `curl http://localhost:5341/api` |

**Doc Locations**: guides → `docs/guide`, technical → `docs/technic`, issues/PRs → `docs/issue`, app docs → `docs`
