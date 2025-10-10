# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MeepleAI is a monorepo containing an AI-powered board game rules assistant. The system processes PDF rulebooks, performs semantic search using vector embeddings, and provides intelligent answers to gameplay questions.

**Tech Stack:**
- **Backend API**: ASP.NET Core 8.0 (C#)
- **Frontend**: Next.js 14 with React 18 (TypeScript)
- **Databases**: PostgreSQL (relational), Qdrant (vector search), Redis (caching)
- **AI/ML**: OpenRouter API for embeddings and LLM
- **Workflow Automation**: n8n
- **Infrastructure**: Docker Compose

## Repository Structure

```
apps/
  api/                    # ASP.NET Core backend
    src/Api/
      Services/           # Core business logic services
      Infrastructure/     # Database context and entities
      Models/            # DTOs and domain models
      Migrations/        # EF Core database migrations
      Program.cs         # Application entry point and DI configuration
    tests/Api.Tests/     # xUnit tests with Testcontainers
  web/                   # Next.js frontend
    src/
      pages/            # Next.js pages (index, admin, chat, editor, upload, etc.)
      lib/              # Shared utilities and API client
infra/
  docker-compose.yml    # Multi-service orchestration
  env/                  # Environment file templates (.env.*.example)
  init/                 # Database initialization scripts
schemas/                # JSON schemas for data validation
docs/                   # Technical documentation
tools/                  # PowerShell automation scripts
```

## Development Commands

### Backend (API)

**Working Directory**: `apps/api`

```bash
# Build the solution
dotnet build

# Run tests (includes unit and integration tests)
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsDirectory=coverage

# Quick coverage measurement (see docs/code-coverage.md for details)
pwsh tools/measure-coverage.ps1 -Project api

# Apply database migrations
dotnet ef database update --project src/Api

# Create a new migration
dotnet ef migrations add <MigrationName> --project src/Api

# Run the API locally (not via Docker)
cd src/Api
dotnet run
```

**Test Framework**: xUnit with Moq for mocking and Testcontainers for integration tests

### Frontend (Web)

**Working Directory**: `apps/web`

**Package Manager**: pnpm (version 9)

```bash
# Install dependencies
pnpm install

# Run development server (port 3000)
pnpm dev

# Build for production
pnpm build

# Start production server
pnpm start

# Run linter
pnpm lint

# Fix linting issues
pnpm lint:fix

# Type checking
pnpm typecheck

# Run unit tests (Jest)
pnpm test

# Run tests in watch mode
pnpm test:watch

# Run tests with coverage
pnpm test:coverage

# Run E2E tests (Playwright)
pnpm test:e2e

# Run E2E tests with UI
pnpm test:e2e:ui

# Show E2E test report
pnpm test:e2e:report
```

**Test Frameworks**:
- Jest with Testing Library for unit/integration tests
- Playwright for E2E tests
- Coverage threshold: 90% (branches, functions, lines, statements)

### Docker Environment

**Working Directory**: `infra`

```bash
# Start all services
docker compose up -d

# Start and rebuild all services
docker compose up -d --build

# View logs
docker compose logs -f

# View logs for specific service
docker compose logs -f api
docker compose logs -f web

# Stop all services
docker compose down

# Stop and remove volumes (CAUTION: deletes data)
docker compose down -v

# Restart a specific service
docker compose restart api
docker compose restart web

# Check service health
docker compose ps
```

**Services and Ports**:
- `postgres`: 5432 (PostgreSQL 16.4)
- `qdrant`: 6333 (HTTP REST), 6334 (gRPC) - Vector database
- `redis`: 6379 - Caching layer
- `n8n`: 5678 - Workflow automation
- `api`: 8080 - ASP.NET Core backend
- `web`: 3000 - Next.js frontend

## Architecture

### Backend Service Layer

The API follows a service-oriented architecture with dependency injection configured in `Program.cs:100-139`:

**AI/Vector Search Services** (AI-01):
- `EmbeddingService`: Generates text embeddings via OpenRouter API
- `QdrantService`: Manages Qdrant vector database collections
- `TextChunkingService`: Splits documents into overlapping chunks (512 chars, 50 char overlap)
- `RagService`: Semantic search and retrieval-augmented generation
- `LlmService`: LLM interactions via OpenRouter

**PDF Processing Services** (PDF-02):
- `PdfStorageService`: Manages PDF uploads and storage
- `PdfTextExtractionService`: Extracts text from PDFs using Docnet.Core
- `PdfTableExtractionService`: Extracts tables from PDFs using iText7

**Domain Services**:
- `GameService`: Game metadata management
- `RuleSpecService`: Rule specification CRUD and versioning
- `RuleSpecDiffService`: Compares rule specification versions
- `SetupGuideService`: Game setup instructions

**Infrastructure Services**:
- `AuthService`: Session-based authentication with cookie management
- `AuditService`: Audit logging for user actions
- `AiRequestLogService`: Tracks AI API requests
- `AiResponseCacheService`: Redis-backed AI response caching (AI-05)
- `RateLimitService`: Request rate limiting
- `N8nConfigService`: n8n workflow configuration
- `BackgroundTaskService`: Background task execution

### Database Layer

**ORM**: Entity Framework Core 8.0 with PostgreSQL provider

**DbContext**: `MeepleAiDbContext` in `Infrastructure/MeepleAiDbContext.cs`

**Key Entities** (in `Infrastructure/Entities/`):
- `UserEntity`, `UserSessionEntity`, `UserRole` - Authentication
- `GameEntity`, `RuleSpecEntity` - Game data
- `PdfDocumentEntity`, `VectorDocumentEntity` - Document management
- `ChatEntity`, `ChatLogEntity` - Conversation history
- `AiRequestLogEntity`, `AuditLogEntity` - Logging
- `AgentEntity`, `AgentFeedbackEntity` - Agent management
- `N8nConfigEntity` - Workflow configuration

**Migrations**: Auto-applied on startup via `Program.cs:184` unless in test mode

**Demo Seed Data** (DB-02): The database includes demo seed data that is automatically populated on startup:
- **Demo Users** (password: `Demo123!`):
  - `admin@meepleai.dev` - Admin role
  - `editor@meepleai.dev` - Editor role
  - `user@meepleai.dev` - User role
- **Demo Games**:
  - Tic-Tac-Toe (ID: `tic-tac-toe`)
  - Chess (ID: `chess`)
- **Demo Rule Specs**: v1.0 for each game
- **Demo Agents**: Explain and Q&A agents for each game

The seed data migration is idempotent and uses `WHERE NOT EXISTS` clauses to prevent duplicate insertion. Seed data is applied through:
- **Production/Docker**: EF Core migration `20251009140700_SeedDemoData`
- **Test Environment**: Programmatic seeding in `WebApplicationFactoryFixture.SeedDemoData()`

**Tests**: See `apps/api/tests/Api.Tests/SeedDataTests.cs` for comprehensive seed data validation tests.

### Frontend Architecture

**Framework**: Next.js 14 with Pages Router

**Key Pages**:
- `index.tsx`: Landing/dashboard page (9.7KB)
- `chat.tsx`: AI chat interface (14.3KB)
- `upload.tsx`: PDF upload and processing (44KB - complex multi-step workflow)
- `editor.tsx`: Rule specification editor (15.7KB)
- `versions.tsx`: Rule version comparison (20.1KB)
- `admin.tsx`: Admin dashboard (14.2KB)
- `n8n.tsx`: Workflow management (16KB)
- `logs.tsx`: Activity logs (6.9KB)

**API Client**: `src/lib/api.ts` - Centralized API client with:
- Automatic base URL configuration from `NEXT_PUBLIC_API_BASE`
- Cookie-based authentication (`credentials: "include"`)
- Methods: `get()`, `post()`, `put()`, `delete()`
- 401 handling for auth failures

**Testing**:
- Unit tests in `__tests__/` directories alongside source
- E2E tests separate from unit tests (excluded via `testPathIgnorePatterns`)
- Coverage requirements enforced at 90% for all metrics

### Authentication Flow

Cookie-based session authentication implemented in `Program.cs:226-248`:

1. Client sends session cookie (name configurable via `SessionCookieConfiguration`)
2. Middleware validates session via `AuthService.ValidateSessionAsync()`
3. Valid sessions populate `ClaimsPrincipal` with user claims
4. Active session stored in `HttpContext.Items["ActiveSession"]`
5. Claims include: UserId, Email, DisplayName, Role

### Vector Search Pipeline (AI-01)

Document indexing flow:

```
PDF Upload → PdfTextExtractionService
           ↓
      TextChunkingService (512 chars, 50 overlap)
           ↓
      EmbeddingService (OpenRouter API)
           ↓
      QdrantService (vector storage)
           ↓
      Searchable via RagService
```

**Qdrant Collection**: Initialized on startup via `Program.cs:186-188`

### CORS Configuration

Configured in `Program.cs:141-170`:
- Policy name: "web"
- Origins from: `Cors:AllowedOrigins` or `AllowedOrigins` config sections
- Fallback: `http://localhost:3000`
- Credentials enabled for cookie auth

### Logging

**Framework**: Serilog with structured logging (`Program.cs:23-32`)

**Configuration**:
- Console output with custom template
- Enriched with: MachineName, EnvironmentName, CorrelationId
- Request logging with user context (if authenticated)
- Correlation ID in `X-Correlation-Id` response header

**Log Levels**:
- Default: Information
- AspNetCore: Warning
- EntityFrameworkCore: Warning

## Testing

### API Tests

**Framework**: xUnit with Moq and Testcontainers

**Infrastructure**:
- `Testcontainers.PostgreSql`: Spin up real Postgres for integration tests
- `Testcontainers.Qdrant`: Spin up real Qdrant for vector search tests
- `Microsoft.AspNetCore.Mvc.Testing`: WebApplicationFactory for API testing
- `coverlet`: Code coverage collection

**Running Tests**:
- Tests use SQLite in-memory for unit tests (configured via environment)
- Integration tests use Testcontainers for real database
- CI environment detected via `CI=true` env var
- Docnet runtime set to Linux in CI (`DocnetRuntime=linux`)

### Web Tests

**Unit Tests (Jest)**:
- Test files: `**/__tests__/**/*.[jt]s?(x)` or `**/*.(spec|test).[jt]s?(x)`
- Environment: jsdom
- Setup: `jest.setup.js`
- Module alias: `@/*` → `<rootDir>/src/*`
- Excluded: `_app.tsx`, `_document.tsx`, type definitions

**E2E Tests (Playwright)**:
- Configuration: `playwright.config.ts`
- Excluded from Jest via `testPathIgnorePatterns: ['/e2e/']`

### Code Coverage

**Documentation**: See `docs/code-coverage.md` for detailed coverage measurement guide

**Quick Start**:
```bash
# Measure coverage for all projects
pwsh tools/measure-coverage.ps1

# API only
pwsh tools/measure-coverage.ps1 -Project api

# Web only
pwsh tools/measure-coverage.ps1 -Project web

# Generate HTML reports
pwsh tools/measure-coverage.ps1 -GenerateHtml
```

**Coverage Tools**:
- **Backend**: Coverlet (cobertura/lcov/json formats)
- **Frontend**: Jest built-in coverage (90% threshold enforced)

**Note**: Full API coverage with Testcontainers can take 10+ minutes. Use test filters for faster feedback during development.

## CI/CD

### Main CI Pipeline

**Workflow**: `.github/workflows/ci.yml`

**Jobs**:

1. **ci-web**: Lint → Typecheck → Test
   - Node 20, pnpm 9
   - Runs on: PRs and pushes to `main`

2. **ci-api**: Build → Test
   - .NET 8.0
   - Services: postgres, qdrant
   - Installs libgdiplus for PDF extraction
   - Environment variables: `CI=true`, test API keys, connection strings

### Security Scanning (SEC-03)

**Workflow**: `.github/workflows/security-scan.yml`
**Documentation**: See `docs/security-scanning.md` for complete details

**Security Layers**:

1. **CodeQL SAST** - Static application security testing
   - Languages: C# (.NET 8), JavaScript/TypeScript
   - Queries: `security-extended`, `security-and-quality`
   - Results in GitHub Security tab

2. **Dependency Scanning** - Vulnerability detection
   - Backend: `dotnet list package --vulnerable`
   - Frontend: `pnpm audit`
   - **Fails pipeline on HIGH/CRITICAL severity**

3. **Security Analyzers** - .NET-specific code analysis
   - SecurityCodeScan.VS2019 v5.6.7
   - Microsoft.CodeAnalysis.NetAnalyzers v9.0.0
   - Detects: SQL injection, XSS, weak crypto, etc.

4. **Dependabot** - Automated dependency updates
   - Configuration: `.github/dependabot.yml`
   - Weekly scans (Mondays)
   - Ecosystems: NuGet, npm, GitHub Actions, Docker

**Quick Security Scan**:
```bash
# Check .NET vulnerabilities
cd apps/api && dotnet list package --vulnerable --include-transitive

# Check frontend vulnerabilities
cd apps/web && pnpm audit --audit-level=high
```

**Reports**: All security scan results are archived as CI artifacts (30-day retention)

## Environment Configuration

**Templates**: `infra/env/*.env.*.example`

**Key Variables**:

API (`api.env.dev`):
- `OPENROUTER_API_KEY`: OpenRouter API key for embeddings/LLM
- `QDRANT_URL`: Qdrant endpoint (default: `http://qdrant:6333`)
- `REDIS_URL`: Redis endpoint (default: `redis:6379`)
- `ConnectionStrings__Postgres`: PostgreSQL connection string

Web (`web.env.dev`):
- `NEXT_PUBLIC_API_BASE`: API base URL (default: `http://localhost:8080`)

n8n (`n8n.env.dev`):
- n8n workflow engine configuration

**Security**: Never commit `.env.dev`, `.env.local`, or `.env.prod` files (blocked in `.gitignore`)

## Common Workflows

### Adding a New API Endpoint

1. Create service method in appropriate service (e.g., `Services/GameService.cs`)
2. Add endpoint in `Program.cs` (endpoints defined after line 250+)
3. Update models in `Models/` if needed
4. Write tests in `tests/Api.Tests/`
5. Ensure authentication/authorization logic if required

### Adding a New Database Entity

1. Create entity class in `Infrastructure/Entities/`
2. Add `DbSet<T>` to `MeepleAiDbContext`
3. Create migration: `dotnet ef migrations add <Name> --project src/Api`
4. Review migration in `Migrations/` directory
5. Apply: `dotnet ef database update --project src/Api`

### Working with Vector Search

The vector search system uses a shared data context (no tenant partitioning):

1. Upload PDF via `PdfStorageService`
2. Extract text via `PdfTextExtractionService`
3. Chunk text via `TextChunkingService`
4. Generate embeddings via `EmbeddingService`
5. Store vectors via `QdrantService.IndexTextChunksAsync()`
6. Query via `RagService.SearchAsync()`

**Note**: Legacy tenant/partition fields should be removed before deployment.

### Creating a New Frontend Page

1. Create page file in `apps/web/src/pages/<name>.tsx`
2. Use API client from `@/lib/api` for backend calls
3. Add tests in `apps/web/src/pages/__tests__/<name>.test.tsx`
4. Ensure coverage meets 90% threshold

### Running Full Stack Locally

```bash
# Terminal 1: Start infrastructure
cd infra
docker compose up postgres qdrant redis n8n

# Terminal 2: Run API
cd apps/api/src/Api
dotnet run

# Terminal 3: Run web
cd apps/web
pnpm dev

# Access:
# - Web: http://localhost:3000
# - API: http://localhost:8080
# - n8n: http://localhost:5678
```

### Working with RuleSpec Schema

The **RuleSpec v0** schema (`schemas/rulespec.v0.schema.json`) defines a formal specification for normalized board game rules. It enables machine-readable rule representation for AI/LLM processing.

**Schema Structure**:
- **Metadata**: Game info (name, players, time, age)
- **Setup**: Initial game setup instructions and components
- **Phases**: Game flow structure (turns, rounds, steps)
- **Actions**: Player actions with prerequisites and effects
- **Scoring**: Scoring methods, sources, and tiebreakers
- **End Conditions**: Conditions that trigger game end
- **Edge Cases**: Exceptions, clarifications, variants, FAQs
- **Glossary**: Game-specific terminology

**Examples**:
- `schemas/examples/tic-tac-toe.rulespec.json` - Simple game example
- `schemas/examples/chess.rulespec.json` - Complex game with 1146 lines

**Validation**:
```bash
# Validate all RuleSpec examples (runs in CI)
node schemas/validate-all-examples.js

# Validate single example
node schemas/validate-example.js
```

**C# Models**: `apps/api/src/Api/Models/RuleSpecV0.cs`
**Backend Entity**: `apps/api/src/Api/Infrastructure/Entities/RuleSpecEntity.cs`
**Services**: `RuleSpecService`, `RuleSpecDiffService`
**Tests**: `apps/api/tests/Api.Tests/RuleSpecV0ModelTests.cs` (786 lines)

**Documentation**: See `schemas/README.md` for complete reference.

## Code Quality Standards

### C# (.NET)

- **Style**: Follow standard C# conventions
- **Null Safety**: Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- **Async/Await**: Use async patterns for I/O operations
- **DI**: Register all services in `Program.cs` builder configuration
- **Logging**: Use `ILogger<T>` injection, Serilog for structured logging
- **Error Handling**: Return appropriate HTTP status codes

### TypeScript/React

- **Strict Mode**: TypeScript strict mode enabled
- **Formatting**: ESLint with Next.js config
- **Typing**: Avoid `any`, use explicit types
- **API Calls**: Use centralized API client (`@/lib/api`)
- **Testing**: Arrange-Act-Assert pattern, descriptive test names

## Troubleshooting

### "Migration already applied" errors
- Check `__EFMigrationsHistory` table in Postgres
- Use `dotnet ef database update <PreviousMigration>` to rollback
- Delete migration file and recreate if needed

### Qdrant connection failures
- Verify Qdrant is running: `curl http://localhost:6333/healthz`
- Check collection initialization in API logs on startup

### Frontend API calls fail with CORS
- Verify `NEXT_PUBLIC_API_BASE` matches API URL
- Check CORS origins in `Program.cs` CORS configuration
- Ensure `credentials: "include"` in fetch calls

### Tests failing in CI but passing locally
- Check Docker/Linux-specific issues (e.g., Docnet runtime)
- Verify environment variables set in CI workflow
- Ensure test services (postgres, qdrant) are healthy before tests run

### Session/Auth not working
- Check cookie name configuration in `SessionCookieConfiguration`
- Verify session exists in database (`user_sessions` table)
- Check browser allows cookies from localhost
