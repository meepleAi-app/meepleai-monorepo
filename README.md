# MeepleAI

[![CI](https://github.com/DegrassiAaron/meepleai-monorepo/actions/workflows/ci.yml/badge.svg)](https://github.com/DegrassiAaron/meepleai-monorepo/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/DegrassiAaron/meepleai-monorepo/branch/main/graph/badge.svg)](https://codecov.io/gh/DegrassiAaron/meepleai-monorepo)
[![Frontend Coverage](https://img.shields.io/badge/coverage%20(frontend)-90%25-brightgreen)](./docs/code-coverage.md)
[![Backend Coverage](https://img.shields.io/badge/coverage%20(backend)-90%25-brightgreen)](./docs/code-coverage.md)

> AI-powered board game rules assistant with semantic search and intelligent Q&A

MeepleAI helps players quickly find answers to gameplay questions by processing PDF rulebooks, performing semantic search using vector embeddings, and providing intelligent answers through LLM integration.

## 🎯 Features

- **PDF Processing**: Upload and extract text/tables from board game rulebooks
- **Semantic Search**: Vector-based search using Qdrant for finding relevant rule sections
- **AI Q&A**: Natural language question answering powered by OpenRouter LLM
- **Rule Editing**: Web-based editor for creating and versioning rule specifications
- **Game Setup Guides**: Step-by-step setup instructions for board games
- **Chat Interface**: Interactive chat for asking gameplay questions
- **Admin Dashboard**: Statistics, logs, and system monitoring

## 🎮 Per gli Utenti (Giocatori)

**Sei un giocatore e vuoi usare MeepleAI?** Consulta la nostra guida utente:

- **[Guida Utente (IT)](./docs/00-getting-started/user-guide.md)** - Guida completa in italiano per iniziare
  - Come caricare i regolamenti dei tuoi giochi
  - Come fare domande durante le partite
  - Interfaccia chat e funzionalita avanzate
  - FAQ e supporto

**Quick Start per Giocatori**:
1. Accedi su [meepleai.dev](https://meepleai.dev)
2. Carica il PDF del regolamento del tuo gioco
3. Inizia a fare domande in linguaggio naturale
4. Ricevi risposte con citazioni dal regolamento

> **Nota**: MeepleAI e ottimizzato per il mercato italiano con interfaccia e risposte in italiano.

## 🏗️ Architecture

**Domain-Driven Design (DDD) - 99% Complete**:
- **7 Bounded Contexts** with CQRS/MediatR pattern
- **72+ CQRS handlers** (Commands/Queries)
- **60+ endpoints** migrated to MediatR
- **2,070 lines** of legacy code eliminated
- **Pattern**: Domain (pure logic) → Application (CQRS) → Infrastructure (adapters) → HTTP (MediatR)

**Bounded Contexts**:
```
apps/api/src/Api/BoundedContexts/
├── Authentication/         Auth, sessions, API keys, OAuth, 2FA
├── GameManagement/         Games catalog, play sessions
├── KnowledgeBase/          RAG, vectors, chat (Hybrid: vector+keyword RRF)
├── DocumentProcessing/     PDF upload, extraction, validation
├── WorkflowIntegration/    n8n workflows, error logging
├── SystemConfiguration/    Runtime config, feature flags
└── Administration/         Users, alerts, audit, analytics
```

**Monorepo Structure**:
- `apps/api` - ASP.NET Core 9.0 backend (DDD/CQRS)
- `apps/web` - Next.js 16 frontend (React 19)
- `apps/unstructured-service` - PDF extraction (Unstructured)
- `apps/smoldocling-service` - PDF extraction (SmolDocling VLM)
- `infra` - Docker Compose infrastructure
- `docs` - Technical documentation (organized in numbered folders)
- `tools` - Automation scripts

**Tech Stack**:
- **Backend**: C# / .NET 9, EF Core, MediatR (CQRS), DDD
- **Frontend**: TypeScript, React 19, Next.js 16, Shadcn/UI, Tailwind CSS 4
- **Databases**: PostgreSQL 16, Qdrant (vector DB), Redis (cache)
- **AI/ML**: OpenRouter API (embeddings & LLM), Hybrid RAG (vector+keyword RRF)
- **PDF Processing**: 3-stage pipeline (Unstructured → SmolDocling → Docnet)
- **Automation**: n8n workflows
- **Observability**: Serilog, Seq, OpenTelemetry, Jaeger, Prometheus, Grafana
- **Infrastructure**: Docker Compose

## 🚀 Quick Start

### Prerequisites

- Docker & Docker Compose
- .NET 9 SDK
- Node.js 20+ with pnpm 9
- Git

### Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
   cd meepleai-monorepo
   ```

2. **Initialize Docker Secrets** (SEC-708)
   ```bash
   cd tools/secrets
   ./init-secrets.sh
   # Follow prompts to configure secrets or use defaults
   # Secrets are stored in infra/secrets/*.txt (gitignored)
   ```

3. **Start infrastructure services**
   ```bash
   cd infra
   docker compose up -d postgres qdrant redis n8n
   ```

4. **Run the API**
   ```bash
   cd apps/api/src/Api
   dotnet run
   ```

5. **Run the Web app**
   ```bash
   cd apps/web
   pnpm install
   pnpm dev
   ```

6. **Access the applications**
   - Web: http://localhost:3000
   - API: http://localhost:8080
   - n8n: http://localhost:5678

**Security Note**: All secrets are managed via Docker Secrets (Issue #708). See [`docs/guide/secrets-management.md`](./docs/guide/secrets-management.md) for rotation and advanced management.

## 📚 Documentation

- **[CLAUDE.md](./CLAUDE.md)** - Comprehensive development guide (DDD 99% complete)
- **[Documentation Index](./docs/README.md)** - Complete documentation organized by role/topic
- **[Architecture Overview](./docs/01-architecture/overview/system-architecture.md)** - Complete system architecture (60+ pages)
- **[ADRs](./docs/01-architecture/adr/)** - Architecture Decision Records (Hybrid RAG, PDF Pipeline, etc.)
- **[DDD Quick Reference](./docs/01-architecture/ddd/quick-reference.md)** - DDD patterns and bounded contexts
- **[Testing Guidelines](./docs/02-development/testing/test-writing-guide.md)** - BDD-style test naming conventions
- **[API Specification](./docs/03-api/board-game-ai-api-specification.md)** - Complete REST API specification
- **[Multi-Environment Strategy](./docs/05-operations/deployment/multi-environment-strategy.md)** - Development, Staging, Production configuration guide
- **[Environment Variables](./docs/06-security/environment-variables-production.md)** - Complete production variables reference

## 🧪 Testing

**Coverage**: 90%+ enforced (frontend 90.03%, backend 90%+)

**Test Counts**:
- **Backend**: 162 tests (xUnit + Testcontainers)
- **Frontend**: 4,033 tests (Jest + React Testing Library)
- **E2E**: 30+ tests (Playwright + Page Object Model)
- **Total**: 4,225+ tests

### Backend (API)

```bash
cd apps/api

# Run all tests
dotnet test

# Run with coverage
dotnet test -p:CollectCoverage=true

# Run specific tests (now testing CQRS handlers, not services)
dotnet test --filter "FullyQualifiedName~CreateGameCommandHandlerTests"
```

**Framework**: xUnit, Moq, Testcontainers (PostgreSQL, Qdrant, Unstructured, SmolDocling)

### Frontend (Web)

```bash
cd apps/web

# Run unit tests
pnpm test

# Run with coverage
pnpm test:coverage

# Run E2E tests
pnpm test:e2e
```

**Framework**: Jest, React Testing Library, Playwright (E2E with Page Object Model)

**Coverage Requirements**: 90% minimum (branches, functions, lines, statements) - Currently 90.03%

### Coverage Automation

```bash
# Measure coverage for all projects
pwsh tools/measure-coverage.ps1

# API only with HTML report
pwsh tools/measure-coverage.ps1 -Project api -GenerateHtml

# Track coverage trends over time
bash tools/coverage-trends.sh          # Bash/Linux/macOS
pwsh tools/coverage-trends.ps1         # PowerShell/Windows
```

## 🔧 Development Commands

### Backend

```bash
# Build
dotnet build

# Run tests
dotnet test

# Apply migrations
dotnet ef database update --project src/Api

# Create migration
dotnet ef migrations add <MigrationName> --project src/Api
```

### Frontend

```bash
# Development server
pnpm dev

# Production build
pnpm build

# Lint
pnpm lint

# Type checking
pnpm typecheck
```

### Infrastructure

```bash
# Start all services
docker compose up -d

# View logs
docker compose logs -f api

# Restart service
docker compose restart api

# Stop all services
docker compose down
```

## 🤖 CI/CD Workflows

- **Main CI (`.github/workflows/ci.yml`)** – change-aware build that runs schema validation, web type-check/tests, API tests, infra config validation, and RAG evaluation only when the relevant paths change.
- **Security Scan (`.github/workflows/security-scan.yml`)** – CodeQL, dependency audit, SecurityCodeScan, and Semgrep now trigger only when code/infrastructure files change (or on the weekly schedule) to reduce noise on docs-only PRs.
- **Migration Guard (`.github/workflows/migration-guard.yml`)** – prevents accidental EF Core migration deletions on PRs touching `apps/api/src/Api/Migrations`.
- **Cache Cleanup (`.github/workflows/cleanup-caches.yml`)** – scheduled monthly maintenance that runs `tools/cleanup-caches.sh`, generates a report artifact, and no longer opens noisy PRs/commits.

The legacy `test-automation-mvp.yml` and duplicate `semgrep.yml` workflows have been retired; their responsibilities now live inside the main CI and security pipelines above.

## 🌐 Environment Variables

Create environment files from templates:

```bash
cp infra/env/api.env.dev.example infra/env/api.env.dev
cp infra/env/web.env.dev.example infra/env/web.env.dev
cp infra/env/n8n.env.dev.example infra/env/n8n.env.dev
```

**Required Configuration**:
- `OPENROUTER_API_KEY` - OpenRouter API key for embeddings/LLM
- `QDRANT_URL` - Qdrant endpoint (default: http://qdrant:6333)
- `REDIS_URL` - Redis endpoint (default: redis:6379)
- `ConnectionStrings__Postgres` - **REQUIRED** PostgreSQL connection string (application will fail to start if not configured)
  ```bash
  # Example
  export CONNECTIONSTRINGS__POSTGRES="Host=localhost;Port=5432;Database=meepleai;Username=meepleai_user;Password=your_secure_password"
  ```
- `NEXT_PUBLIC_API_BASE` - API base URL for frontend

**Security**: See [docs/SECURITY.md](./docs/SECURITY.md) for secret management, required environment variables, and key rotation procedures.

**Important**: As of version 1.2, hardcoded database credentials have been removed. The application will **fail fast** with a clear error message if `CONNECTIONSTRINGS__POSTGRES` is not configured. This prevents accidental use of insecure default credentials.

## 🤝 Contributing

1. **Install pre-commit hooks** (first time setup):
   ```bash
   pip install pre-commit
   pre-commit install
   ```
2. Follow [BDD-style test naming conventions](./docs/guide/testing-guide.md)
3. Ensure tests pass: `dotnet test` (API) and `pnpm test` (Web)
4. Maintain 90% coverage for frontend
5. Run linting: `pnpm lint` (Web)
6. Create meaningful commits following conventional commits
7. **Never commit secrets** - hooks will block commits with detected secrets

## 📊 CI/CD

**GitHub Actions Workflow**: `.github/workflows/ci.yml`

**Jobs**:
- `ci-web`: Lint → Typecheck → Test with Coverage → Codecov Upload
- `ci-api`: Build → Test with Coverage → Codecov Upload

**Coverage Tracking**: Automated via Codecov with PR comments

## 🏆 Code Quality

- **Backend**: 162 tests, 90%+ coverage (enforced)
- **Frontend**: 4,033 tests, 90.03% coverage (enforced)
- **E2E**: 30+ Playwright tests with Page Object Model
- **Architecture**: DDD with 7 Bounded Contexts, CQRS/MediatR pattern
- **Linting**: ESLint (Web), Roslyn Analyzers (API)
- **Type Safety**: TypeScript strict mode, C# nullable reference types
- **Testing**: Comprehensive unit, integration, and E2E tests
- **CI Performance**: ~14min (38% faster, optimized)

## 📦 Project Structure

```
meepleai-monorepo/
├── apps/
│   ├── api/               # ASP.NET Core backend
│   │   ├── src/Api/       # Application code
│   │   └── tests/         # xUnit tests
│   └── web/               # Next.js frontend
│       ├── src/           # Application code
│       └── __tests__/     # Jest tests
├── infra/
│   ├── docker-compose.yml # Service orchestration
│   └── env/               # Environment templates
├── docs/                  # Technical documentation
├── tools/                 # PowerShell scripts
├── schemas/               # JSON schemas
├── CLAUDE.md              # Development guide
└── README.test.md         # Testing guidelines
```

## 🛠️ Tech Details

### Backend Architecture (DDD/CQRS)

**7 Bounded Contexts** with Commands/Queries/Handlers:
- **Authentication**: Login, register, OAuth, 2FA, API keys
- **GameManagement**: CRUD operations, play sessions
- **KnowledgeBase**: Hybrid RAG (vector+keyword), chat, search
- **DocumentProcessing**: 3-stage PDF pipeline (Unstructured → SmolDocling → Docnet)
- **WorkflowIntegration**: n8n workflows, error logging
- **SystemConfiguration**: Runtime config, feature flags
- **Administration**: User management, alerts, audit, analytics

**Key Services** (Infrastructure/Orchestration):
- **RagService**: Hybrid RAG orchestration (Vector + Keyword RRF fusion)
- **ConfigurationService**: 3-tier fallback config (DB → appsettings → defaults)
- **AdminStatsService**: Analytics aggregation
- **AlertingService**: Email/Slack/PagerDuty alerts

**PDF Processing Pipeline**:
- **Stage 1**: Unstructured (≥0.80 quality) - 80% success, 1.3s avg
- **Stage 2**: SmolDocling VLM (≥0.70 quality) - 15% fallback, 3-5s avg
- **Stage 3**: Docnet (best effort) - 5% fallback, fast

### Frontend Pages

- `/` - Dashboard
- `/chat` - AI chat interface
- `/upload` - PDF upload and processing
- `/editor` - Rule specification editor
- `/versions` - Version comparison
- `/admin` - Admin dashboard
- `/n8n` - Workflow management
- `/logs` - Activity logs

## 📝 License

This project is private and proprietary. All rights reserved.

See [LICENSE](./LICENSE) for full terms.

## 🔗 Resources

- **API Documentation**: OpenAPI/Swagger at `/api/docs`
- **Code Coverage**: [Codecov Dashboard](https://codecov.io/gh/DegrassiAaron/meepleai-monorepo)
- **CI/CD**: [GitHub Actions](https://github.com/DegrassiAaron/meepleai-monorepo/actions)
- **Issues**: [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)

---

## 🎯 Project Status

**Phase**: Alpha (pre-production)
**DDD Migration**: **99% complete** (7/7 contexts, 72+ handlers, 2,070 lines removed)
**Next Milestone**: Final polish (1%) → Beta testing (2-4 weeks) → Production
**Target**: 10,000 MAU by Phase 4, >99.5% uptime SLA

---

**Maintained by**: MeepleAI Team
**Version**: 1.0-rc (DDD 99%)
**Last Updated**: 2025-12-04
