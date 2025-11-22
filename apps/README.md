# MeepleAI Applications

**Microservices Architecture** - Backend API, web frontend, and specialized AI/ML services for MeepleAI board game rules assistant.

---

## 📁 Directory Structure

```
apps/
├── api/                           # ASP.NET Core 9 Backend API
├── web/                           # Next.js 16 + React 19 Frontend
├── embedding-service/             # BGE-M3 Multilingual Embedding Service
├── smoldocling-service/           # SmolDocling VLM PDF Extraction (Stage 2)
└── unstructured-service/          # Unstructured PDF Extraction (Stage 1)
```

---

## 🏗️ Architecture Overview

### Services Dependency Graph

```
┌─────────────┐
│   Web       │  Next.js 16 Frontend (Port 3000)
│  (React 19) │  ↓
└─────────────┘  │
                 │ HTTP/REST
                 ↓
┌─────────────────────────────────────────────────┐
│              API (ASP.NET Core 9)                │  Port 8080
│  7 Bounded Contexts (DDD + CQRS/MediatR)        │
│  - Authentication, GameManagement,               │
│  - KnowledgeBase, DocumentProcessing, etc.       │
└─────────────────────────────────────────────────┘
        │         │           │
        │         │           │ HTTP
        ↓         ↓           ↓
┌──────────┐  ┌─────────┐  ┌────────────────┐
│Embedding │  │Unstruct │  │  SmolDocling   │
│ Service  │  │ Service │  │   Service      │
│(BGE-M3)  │  │(Stage 1)│  │  (Stage 2 VLM) │
│Port 8000 │  │Port 8001│  │   Port 8002    │
└──────────┘  └─────────┘  └────────────────┘
```

---

## 📦 Services

### 1. API (Backend)

**ASP.NET Core 9 REST API** - Domain-Driven Design with CQRS/MediatR

**Port**: 8080

**Key Features**:
- 7 Bounded Contexts (DDD architecture)
- CQRS pattern with MediatR
- Dual authentication (Cookie + API Key)
- OAuth 2.0 (Google, Discord, GitHub)
- 2FA with TOTP
- Hybrid RAG (Vector + Keyword search)
- 3-stage PDF processing pipeline
- PostgreSQL + EF Core
- Qdrant vector database
- Redis caching

**Tech Stack**: ASP.NET Core 9, C# 13, EF Core 9, MediatR, Serilog, OpenTelemetry

**Documentation**: [api/README.md](./api/README.md)

**Quick Start**:
```bash
cd apps/api/src/Api
dotnet run
# API: http://localhost:8080
# Swagger: http://localhost:8080/swagger
```

---

### 2. Web (Frontend)

**Next.js 16 + React 19** - Modern, accessible, Italian-first web application

**Port**: 3000

**Key Features**:
- Server-side rendering (SSR)
- Static site generation (SSG)
- Shadcn/UI design system (Radix + Tailwind CSS 4)
- React Query for server state
- React Hook Form + Zod validation
- Cookie + API key authentication
- Real-time chat (SSE streaming)
- PDF upload with drag & drop
- Settings page (4-tab interface)
- Admin dashboard

**Tech Stack**: Next.js 16, React 19, TypeScript 5.6+, Tailwind CSS 4, Shadcn/UI, TanStack Query

**Documentation**: [web/README.md](./web/README.md)

**Quick Start**:
```bash
cd apps/web
pnpm install
pnpm dev
# Web: http://localhost:3000
```

---

### 3. Embedding Service

**BGE-M3 Multilingual Embedding Service** - High-quality embeddings for RAG

**Port**: 8000

**Key Features**:
- BGE-M3 model (1024-dimensional embeddings)
- Multilingual support (100+ languages)
- Optimized for Italian + English
- REST API endpoint
- Batch processing support
- GPU acceleration (optional)

**Model**: BAAI/bge-m3 (Hugging Face)

**Tech Stack**: Python 3.11+, FastAPI, Transformers, PyTorch

**API Endpoint**:
```bash
POST http://localhost:8000/embed
Content-Type: application/json

{
  "texts": ["Il gioco inizia con...", "The game starts with..."]
}

Response:
{
  "embeddings": [[0.123, -0.456, ...], [0.789, -0.012, ...]]
}
```

**Quick Start**:
```bash
cd apps/embedding-service
pip install -r requirements.txt
python app.py
# Service: http://localhost:8000
```

**Container**:
```bash
docker compose up meepleai-embedding
```

---

### 4. Unstructured Service (PDF Stage 1)

**Unstructured PDF Text Extraction** - Primary PDF processing (80% success rate)

**Port**: 8001

**Key Features**:
- Stage 1 of 3-stage PDF pipeline
- Unstructured library (Apache 2.0)
- RAG-optimized extraction
- Table detection and extraction
- Layout analysis
- Metadata extraction
- Quality threshold: ≥0.80

**Tech Stack**: Python 3.11+, FastAPI, Unstructured, Tesseract OCR

**Processing Flow**:
```
PDF Upload → Unstructured API → Text + Metadata → Quality Check
                                                    ↓
                                        Quality ≥ 0.80? → Success
                                        Quality < 0.80? → Stage 2 (SmolDocling)
```

**API Endpoint**:
```bash
POST http://localhost:8001/extract
Content-Type: multipart/form-data

file: <PDF binary>

Response:
{
  "text": "Extracted text...",
  "quality": 0.85,
  "metadata": {
    "pages": 12,
    "tables": 3,
    "images": 5
  }
}
```

**Quick Start**:
```bash
cd apps/unstructured-service
pip install -r requirements.txt
python app.py
# Service: http://localhost:8001
```

**Container**:
```bash
docker compose up unstructured
```

**Documentation**: See [ADR-003b: Unstructured PDF](../docs/01-architecture/adr/adr-003b-unstructured-pdf.md)

---

### 5. SmolDocling Service (PDF Stage 2)

**SmolDocling VLM PDF Extraction** - Fallback for complex layouts (15% usage)

**Port**: 8002

**Key Features**:
- Stage 2 of 3-stage PDF pipeline (fallback)
- Vision-Language Model (256M parameters)
- Complex layout handling (tables, diagrams, multi-column)
- Image-based extraction
- Quality threshold: ≥0.70
- Slower but more accurate for complex PDFs

**Model**: SmolDocling VLM (Hugging Face)

**Tech Stack**: Python 3.11+, FastAPI, Transformers, PyTorch, torchvision

**Processing Flow**:
```
Stage 1 Failed (Quality < 0.80)
    ↓
SmolDocling VLM → Text + Layout → Quality Check
                                      ↓
                          Quality ≥ 0.70? → Success
                          Quality < 0.70? → Stage 3 (Docnet)
```

**API Endpoint**:
```bash
POST http://localhost:8002/extract
Content-Type: multipart/form-data

file: <PDF binary>

Response:
{
  "text": "Extracted text with layout...",
  "quality": 0.75,
  "layout": {
    "tables": [...],
    "images": [...],
    "columns": 2
  }
}
```

**Quick Start**:
```bash
cd apps/smoldocling-service
pip install -r requirements.txt
python src/app.py
# Service: http://localhost:8002
```

**Container**:
```bash
docker compose up smoldocling
```

**Documentation**: See [ADR-003b: Unstructured PDF](../docs/01-architecture/adr/adr-003b-unstructured-pdf.md)

---

## 🚀 Running All Services

### Development Mode

**Option 1: Docker Compose (Recommended)**
```bash
# From project root
cd infra
docker compose up -d

# All services start:
# - api: http://localhost:8080
# - web: http://localhost:3000
# - embedding: http://localhost:8000
# - unstructured: http://localhost:8001
# - smoldocling: http://localhost:8002
# - postgres: localhost:5432
# - qdrant: http://localhost:6333
# - redis: localhost:6379
# - seq: http://localhost:8081
# - jaeger: http://localhost:16686
# - prometheus: http://localhost:9090
# - grafana: http://localhost:3001
# - n8n: http://localhost:5678
```

**Option 2: Run Individually**
```bash
# Terminal 1: Infrastructure
cd infra && docker compose up meepleai-postgres meepleai-qdrant meepleai-redis

# Terminal 2: Embedding Service
cd apps/embedding-service && python app.py

# Terminal 3: Unstructured Service
cd apps/unstructured-service && python app.py

# Terminal 4: SmolDocling Service
cd apps/smoldocling-service && python src/app.py

# Terminal 5: API
cd apps/api/src/Api && dotnet run

# Terminal 6: Web
cd apps/web && pnpm dev
```

### Production Mode

```bash
# Build all services
docker compose -f infra/docker-compose.yml build

# Start production stack
docker compose -f infra/docker-compose.yml up -d
```

---

## 🧪 Testing

### Run All Tests

```bash
# Backend tests (from project root)
cd apps/api && dotnet test

# Frontend tests
cd apps/web && pnpm test

# E2E tests (requires all services running)
cd apps/web && pnpm test:e2e
```

### Test Coverage

| Service | Coverage | Target |
|---------|----------|--------|
| API | 90%+ | 90%+ |
| Web | 90.03% | 90%+ |
| Embedding | N/A | N/A |
| Unstructured | 85%+ | 80%+ |
| SmolDocling | 85%+ | 80%+ |

---

## 📊 Service Health

### Health Check Endpoints

```bash
# API
curl http://localhost:8080/health

# Web (Next.js internal)
curl http://localhost:3000/api/health

# Embedding Service
curl http://localhost:8000/health

# Unstructured Service
curl http://localhost:8001/health

# SmolDocling Service
curl http://localhost:8002/health
```

### Service Dependencies

**API depends on**:
- PostgreSQL (required)
- Qdrant (required for RAG)
- Redis (required for caching)
- Embedding Service (required for RAG)
- Unstructured Service (required for PDF processing)
- SmolDocling Service (optional, fallback)

**Web depends on**:
- API (required)

**All services**:
- Run independently in Docker containers
- Health checks configured
- Auto-restart on failure (Docker restart policy)

---

## 🔧 Configuration

### Environment Variables

Each service has its own configuration:

**API** (`.env` in `infra/env/`):
```bash
OPENROUTER_API_KEY=sk-or-***
ConnectionStrings__Postgres=Host=meepleai-postgres;...
QDRANT_URL=http://meepleai-qdrant:6333
REDIS_URL=meepleai-redis:6379
EMBEDDING_SERVICE_URL=http://embedding:8000
UNSTRUCTURED_SERVICE_URL=http://unstructured:8001
SMOLDOCLING_SERVICE_URL=http://smoldocling:8002
```

**Web** (`.env.local`):
```bash
NEXT_PUBLIC_API_BASE=http://localhost:8080
```

**Embedding Service** (`apps/embedding-service/.env`):
```bash
MODEL_NAME=BAAI/bge-m3
PORT=8000
```

**Unstructured Service** (`apps/unstructured-service/.env`):
```bash
PORT=8001
TESSERACT_PATH=/usr/bin/tesseract
```

**SmolDocling Service** (`apps/smoldocling-service/.env`):
```bash
PORT=8002
MODEL_NAME=smoldocling-vlm
```

---

## 📚 Documentation

### Service-Specific Docs

- **[API Documentation](./api/README.md)** - Backend API guide
- **[Web Documentation](./web/README.md)** - Frontend guide
- **[Embedding Service](./embedding-service/README.md)** - Embedding service (if exists)
- **[Unstructured Service](./unstructured-service/README.md)** - PDF extraction (if exists)
- **[SmolDocling Service](./smoldocling-service/README.md)** - VLM extraction (if exists)

### Architecture Docs

- **[System Architecture](../docs/01-architecture/overview/system-architecture.md)** - Overall architecture
- **[ADR-001: Hybrid RAG](../docs/01-architecture/adr/adr-001-hybrid-rag.md)** - RAG architecture
- **[ADR-003b: Unstructured PDF](../docs/01-architecture/adr/adr-003b-unstructured-pdf.md)** - PDF processing pipeline
- **[Infrastructure Overview](../docs/01-architecture/diagrams/infrastructure-overview.md)** - Infrastructure diagram

### Project Guide

- **[CLAUDE.md](../CLAUDE.md)** - Complete development guide

---

## 🐛 Troubleshooting

### Service Won't Start

```bash
# Check logs
docker compose logs <service-name>

# Restart service
docker compose restart <service-name>

# Rebuild service
docker compose up -d --build <service-name>
```

### API Can't Connect to Services

```bash
# Check network
docker network ls
docker network inspect infra_default

# Check service health
docker compose ps

# Test connectivity
docker compose exec meepleai-api curl http://meepleai-embedding:8000/health
docker compose exec meepleai-api curl http://unstructured:8001/health
```

### PDF Processing Fails

```bash
# Check Unstructured service logs
docker compose logs unstructured

# Check SmolDocling service logs
docker compose logs smoldocling

# Test extraction manually
curl -X POST http://localhost:8001/extract \
  -F "file=@test.pdf"
```

---

## 🚀 Deployment

### Production Deployment

See [Deployment Guide](../docs/05-operations/deployment/board-game-ai-deployment-guide.md) for complete instructions.

**Quick overview**:
1. Build all Docker images
2. Push to container registry
3. Deploy to Kubernetes/Docker Swarm
4. Configure ingress/load balancer
5. Set up monitoring (Prometheus, Grafana)
6. Configure secrets (Vault/Infisical)

---

## 🤝 Contributing

### Adding a New Service

1. **Create service directory** in `apps/<service-name>/`
2. **Add Dockerfile** for containerization
3. **Add to docker-compose.yml** in `infra/`
4. **Document service** in this README
5. **Add health check endpoint** (`/health`)
6. **Write tests** (unit + integration)
7. **Update architecture diagrams**

### Pull Request Checklist

- [ ] Service documented in this README
- [ ] Dockerfile created and tested
- [ ] Added to `docker-compose.yml`
- [ ] Health check endpoint implemented
- [ ] Tests written (if applicable)
- [ ] Environment variables documented
- [ ] Architecture diagrams updated
- [ ] PR has `[APPS]` prefix

---

**Last Updated**: 2025-11-15
**Maintainer**: Engineering Team
**Total Services**: 5 (API, Web, Embedding, Unstructured, SmolDocling)

