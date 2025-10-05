# CLAUDE.md - MeepleAI Monorepo Development Guide

This file provides essential information for Claude Code instances working on the MeepleAI monorepo. It covers the project architecture, development workflows, and key commands needed for effective development.

## 🏗️ Project Architecture

### Overview
MeepleAI is a board game rules assistant platform built as a microservices architecture with:
- **Frontend**: Next.js/TypeScript web application
- **Backend**: .NET 8 Web API (C#)
- **Infrastructure**: Docker Compose orchestration
- **Data Storage**: PostgreSQL, Qdrant (vector DB), Redis (cache)
- **Automation**: n8n workflows

### Directory Structure
```
meepleai-monorepo/
├── apps/
│   ├── web/                  # Next.js frontend application
│   │   ├── src/              # UI source code
│   │   ├── package.json      # Frontend dependencies
│   │   └── Dockerfile        # Frontend container config
│   └── api/                  # .NET 8 Web API backend
│       ├── src/Api/          # Main API project
│       │   ├── Models/       # Data contracts and DTOs
│       │   ├── Services/     # Business logic services
│       │   └── Program.cs    # API entry point
│       ├── tests/            # Unit tests
│       └── MeepleAI.Api.sln  # .NET solution file
├── infra/
│   ├── docker-compose.yml    # Main orchestration file
│   ├── env/                  # Environment presets for compose
│   └── init/                 # Database initialization scripts
├── schemas/                  # JSON schemas for data validation
├── scripts/                  # PowerShell development scripts
├── tools/                    # Utility scripts and tools
└── meepleai_backlog/        # Project backlog and planning
```

## 🛠️ Technology Stack

### Frontend (`apps/web`)
- **Framework**: Next.js 14.2.12
- **Language**: TypeScript 5.5.4
- **Runtime**: React 18.3.1
- **Styling**: CSS-in-JS (inline styles currently)
- **Build Tool**: Next.js built-in

### Backend (`apps/api`)
- **Framework**: .NET 8 Web API
- **Language**: C# with nullable reference types enabled
- **Database**: PostgreSQL via Npgsql
- **Cache**: Redis via StackExchange.Redis
- **Vector DB**: Qdrant for RAG functionality
- **Serialization**: System.Text.Json

### Infrastructure
- **Container Orchestration**: Docker Compose
- **Database**: PostgreSQL 16 Alpine
- **Vector Database**: Qdrant v1.12.4
- **Cache**: Redis 7 Alpine
- **Workflow Engine**: n8n 1.60.0

## 🚀 Development Commands

### Quick Start
```bash
# Start all services (from root directory)
./scripts/dev-up.ps1

# Start with rebuild
./scripts/dev-up.ps1 -Rebuild

# View logs
./scripts/dev-logs.ps1

# Stop all services
./scripts/dev-down.ps1

# Seed demo data
./scripts/seed-demo.ps1
```

### Frontend Development
```bash
cd apps/web

# Install dependencies (if needed)
npm install

# Development server (port 3000)
npm run dev

# Build for production
npm run build

# Start production server
npm run start

# Run tests (smoke test currently)
npm test
```

### Backend Development
```bash
cd apps/api

# Restore packages
dotnet restore

# Build solution
dotnet build

# Run API (development)
dotnet run --project src/Api

# Run tests
dotnet test

# Build Docker image
docker build -f src/Api/Dockerfile .
```

### Repository Safety Hooks & Security
```bash
# Install Python tooling (requires Python 3.9+)
python -m pip install --user -r requirements-dev.txt

# Register git hooks (blocks commits with secrets)
pre-commit install

# Run the full suite on demand
pre-commit run --all-files
```
> Windows tip: if `python -m pre_commit` reports `No module named pre_commit`, rerun the installation command using `py -3 -m pip install --user -r requirements-dev.txt` to ensure the package is available on PATH.

**Security Note**: Pre-commit hooks will automatically:
- Detect and block API keys, tokens, and private keys
- Prevent commits of `.env.dev` and `.env.local` files with secrets
- Check for large files, merge conflicts, and code quality issues

See [SECURITY.md](SECURITY.md) for complete security guidelines and secrets management.

## 🌐 Service Endpoints

### Development URLs
- **Frontend**: http://localhost:3000
- **Backend API**: http://localhost:8080
- **n8n Workflows**: http://localhost:5678
- **Qdrant**: http://localhost:6333
- **PostgreSQL**: localhost:5432
- **Redis**: localhost:6379

### API Endpoints
- `GET /` - Health check
- `POST /agents/qa` - Question answering endpoint
- `POST /ingest/pdf` - PDF ingestion (placeholder)
- `POST /admin/seed` - Seed demo data

## 🔧 Environment Configuration

### Required Environment Variables

**⚠️ Security**: Never commit real secrets to git. Use `.env.dev` files locally (ignored by git) and inject secrets via CI/CD for production.

#### Frontend (`apps/web`)
```bash
NEXT_PUBLIC_API_BASE=http://localhost:8080
```

#### Backend (`apps/api`)
```bash
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__Postgres=Host=postgres;Database=meepleai;Username=meeple;Password=meeplepass
QDRANT_URL=http://qdrant:6333
REDIS_URL=redis:6379
OPENROUTER_API_KEY=changeme  # ⚠️ Replace with your key in .env.dev
JWT_ISSUER=http://localhost:8080
ALLOW_ORIGIN=http://localhost:3000
```

#### Infrastructure
```bash
POSTGRES_USER=meeple
POSTGRES_PASSWORD=meeplepass  # ⚠️ Use strong password in production
POSTGRES_DB=meepleai
```

**Setup**: Run `.\scripts\dev-up.ps1` to auto-create `.env.dev` files from templates, then update secrets locally.

## 📋 Development Workflow

### 1. Starting Development
1. Clone the repository
2. Copy environment templates: `cp infra/env/*.env.dev.example infra/env/*.env.dev`
3. Start services: `cd infra && docker compose up -d --build`
4. Verify services are running: `docker compose ps`

### 2. Making Changes
1. Create feature branch: `git checkout -b feature/your-feature`
2. Make changes to frontend or backend
3. Test locally using the running Docker services
4. Commit changes with conventional commits format

### 3. Testing
- **Frontend**: Basic smoke test via `npm test`
- **Backend**: xUnit tests via `dotnet test`
- **Integration**: Test against running Docker services
- **Manual**: Use the web interface at localhost:3000

## 🏗️ Key Architectural Components

### Frontend Components
- **Main Page** (`src/pages/index.tsx`): Simple UI for testing QA functionality
- **API Client** (`src/lib/api.ts`): HTTP client for backend communication
- **Health Check** (`src/pages/api/health.ts`): Next.js API route for health monitoring

### Backend Services
- **RagService**: Handles question-answering using RAG (Retrieval-Augmented Generation)
- **RuleSpecService**: Manages game rule specifications and demo data
- **Program.cs**: Main API entry point with endpoint definitions

### Data Flow
1. User asks question via frontend (localhost:3000)
2. Frontend calls backend API (localhost:8080)
3. Backend processes query using RagService
4. RagService queries Qdrant for relevant context
5. Response returned through the chain back to user

## 🔍 Common Development Patterns

### Backend Patterns
- **Dependency Injection**: Services registered in Program.cs
- **Minimal APIs**: Direct endpoint mapping without controllers
- **Async/Await**: All service methods are asynchronous
- **Result Pattern**: JSON responses with consistent structure

### Frontend Patterns
- **React Hooks**: useState for local state management
- **Async Functions**: API calls with async/await
- **Environment Variables**: Next.js public env vars for configuration
- **Component-based**: Functional React components

## 🐛 Troubleshooting

### Common Issues
1. **Services not starting**: Check Docker Desktop is running
2. **Database connection issues**: Verify PostgreSQL is healthy in `docker compose ps`
3. **CORS errors**: Check ALLOW_ORIGIN environment variable
4. **API not responding**: Verify backend is running on port 8080

### Debugging Commands
```bash
# Check service status
docker compose ps

# View logs for specific service
docker compose logs api
docker compose logs web

# Restart specific service
docker compose restart api

# Rebuild and restart
docker compose up --build -d
```

## 📚 Additional Resources

### Documentation Files
- `agents.md`: Comprehensive agent development guidelines
- `agents.monorepo.md`: Monorepo-specific development patterns
- `infra/init/n8n/README.md`: n8n workflow setup instructions

### Key Scripts
- `tools/create-issues.ps1`: GitHub issue creation automation
- `scripts/seed-demo.ps1`: Demo data seeding
- All scripts are PowerShell-based for Windows development

## 🎯 Development Focus Areas

When working on this codebase, pay attention to:

1. **Microservices Communication**: Frontend ↔ Backend API communication
2. **Data Persistence**: PostgreSQL for structured data, Qdrant for vectors
3. **Containerization**: Docker-first development approach
4. **Type Safety**: TypeScript frontend, C# backend with nullable reference types
5. **RAG Implementation**: Question-answering system using vector search

This architecture supports a board game rules assistant where users can ask questions about game rules and receive AI-powered answers based on ingested rule documents.