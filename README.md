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

## 🏗️ Architecture

**Monorepo Structure**:
- `apps/api` - ASP.NET Core 9.0 backend
- `apps/web` - Next.js 14 frontend
- `infra` - Docker Compose infrastructure
- `docs` - Technical documentation
- `tools` - Automation scripts

**Tech Stack**:
- **Backend**: C# / .NET 9, EF Core, Minimal APIs
- **Frontend**: TypeScript, React 18, Next.js 14
- **Databases**: PostgreSQL, Qdrant (vector DB), Redis (cache)
- **AI/ML**: OpenRouter API (embeddings & LLM)
- **Automation**: n8n workflows
- **Infrastructure**: Docker Compose

## 🚀 Quick Start

### Prerequisites

- Docker & Docker Compose
- .NET 8.0 SDK
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

- **[AI Agents Guide](./docs/guide/agents-guide.md)** - Guide for AI coding assistants (conventions, workflow, prompts)
- **[CLAUDE.md](./CLAUDE.md)** - Comprehensive development guide
- **[Testing Guidelines](./docs/guide/testing-guide.md)** - BDD-style test naming conventions
- **[Code Coverage](./docs/code-coverage.md)** - Coverage measurement and tracking
- **[Codecov Setup](./docs/codecov-setup.md)** - CI/CD coverage integration
- **[Architecture Docs](./docs/)** - Technical documentation

## 🧪 Testing

### Backend (API)

```bash
cd apps/api

# Run all tests
dotnet test

# Run with coverage
dotnet test -p:CollectCoverage=true

# Run specific tests
dotnet test --filter "FullyQualifiedName~GameServiceTests"
```

**Framework**: xUnit, Moq, Testcontainers

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

**Framework**: Jest, React Testing Library, Playwright

**Coverage Requirements**: 90% minimum (branches, functions, lines, statements)

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

- **Backend**: 313 tests, estimated 75-85% coverage
- **Frontend**: 90% coverage threshold (enforced)
- **Linting**: ESLint (Web), Roslyn Analyzers (API)
- **Type Safety**: TypeScript strict mode, C# nullable reference types
- **Testing**: Comprehensive unit, integration, and E2E tests

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

### Backend Services

- **EmbeddingService**: Generate text embeddings via OpenRouter
- **QdrantService**: Manage vector database collections
- **TextChunkingService**: Split documents (512 chars, 50 overlap)
- **RagService**: Semantic search and RAG
- **LlmService**: LLM interactions
- **PdfStorageService**: PDF uploads and storage
- **PdfTextExtractionService**: Extract text from PDFs
- **GameService**: Game metadata management
- **RuleSpecService**: Rule specification CRUD
- **AuthService**: Session-based authentication

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

**Maintained by**: MeepleAI Team
**Last Updated**: 2025-10-09
