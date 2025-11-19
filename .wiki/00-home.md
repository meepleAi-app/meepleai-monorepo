# MeepleAI - Home

## 🎲 Welcome to MeepleAI

MeepleAI is your intelligent board game rules assistant, designed to provide quick, accurate answers to game rules questions. Whether you're in the middle of a game and need clarification, learning a new game, or settling a debate, MeepleAI has you covered.

## 🚀 Quick Start

### For Users
```bash
# Access the web application
https://meepleai.dev  # Production
http://localhost:3000  # Local development
```

### For Developers
```bash
# Clone and start local environment
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
cd meepleai-monorepo/infra
docker compose up -d  # Start all services

# Terminal 2: Start API
cd apps/api/src/Api
dotnet run  # http://localhost:5080

# Terminal 3: Start Web
cd apps/web
pnpm dev  # http://localhost:3000
```

## 🎯 What Can MeepleAI Do?

### Core Features

1. **📝 Ask Questions**
   - Natural language questions about game rules
   - Context-aware answers from official rulebooks
   - Citation references to specific rule sections

2. **🔍 Smart Search**
   - Hybrid search (vector + keyword)
   - Finds relevant rules across multiple games
   - Ranked results by relevance and confidence

3. **📚 Game Library**
   - Browse supported board games
   - View game metadata and complexity
   - Upload custom rulebooks (PDF)

4. **💬 Chat Sessions**
   - Conversational interface
   - Maintains context across questions
   - Follow-up question support

5. **🔐 Multi-Auth Support**
   - Email/password registration
   - OAuth (Google, GitHub, Discord)
   - 2FA with TOTP
   - API keys for programmatic access

## 🏗️ System Architecture

### High-Level Overview

```
┌─────────────┐
│   User/UI   │
└──────┬──────┘
       │
┌──────▼──────────────────────────────┐
│      Next.js Frontend (3000)        │
│  React 19 + Shadcn/UI + Tailwind 4  │
└──────┬──────────────────────────────┘
       │ HTTP/REST
┌──────▼──────────────────────────────┐
│    ASP.NET API (8080)               │
│  7 Bounded Contexts + CQRS/MediatR │
└──┬───┬───┬───┬───┬──┬──┬────────────┘
   │   │   │   │   │  │  │
   ▼   ▼   ▼   ▼   ▼  ▼  ▼
  PG  QD  RD  n8n OL EM SD

PG: PostgreSQL (data)
QD: Qdrant (vectors)
RD: Redis (cache)
n8n: Workflows
OL: Ollama (local LLM)
EM: Embedding service
SD: SmolDocling (PDF VLM)
```

### Technology Stack

**Backend:**
- ASP.NET 9 (C# 13)
- PostgreSQL 17
- Qdrant (vector DB)
- Redis (cache)
- OpenRouter (LLM proxy)

**Frontend:**
- Next.js 16 (App Router)
- React 19
- Shadcn/UI (Radix + Tailwind)
- TypeScript 5

**AI/ML:**
- GPT-4 + Claude (consensus)
- Qdrant embeddings
- Unstructured (PDF extraction)
- SmolDocling (VLM fallback)

**Observability:**
- Seq (logs)
- Jaeger (traces)
- Prometheus (metrics)
- Grafana (dashboards)
- AlertManager (alerts)

## 📊 Key Metrics & Goals

| Metric | Target | Current |
|--------|--------|---------|
| **Accuracy** | >95% | ~92% (alpha) |
| **Hallucination** | <3% | ~5% (alpha) |
| **Response Time** | <2s P95 | ~1.8s (alpha) |
| **Test Coverage** | >90% | 90.03% (4,225 tests) |
| **Uptime** | >99.5% | N/A (pre-prod) |
| **MAU** | 10K (Phase 4) | N/A (alpha) |

## 🗺️ Project Phases

### Current: Alpha (DDD Refactoring)
- ✅ 99% DDD migration complete
- ✅ 7 bounded contexts operational
- ✅ 72+ CQRS handlers
- ✅ 2,070 lines legacy code removed
- 🔄 Final polish (1% remaining)

### Next: Beta Testing (2-4 weeks)
- Invite beta testers
- Gather feedback
- Performance tuning
- Bug fixes

### Then: Production Launch
- Public release
- Marketing campaign
- SLA commitments
- Support infrastructure

## 🧩 Bounded Contexts (DDD)

MeepleAI uses Domain-Driven Design with 7 bounded contexts:

1. **Authentication** - User auth, sessions, API keys, OAuth, 2FA
2. **GameManagement** - Games catalog, play sessions
3. **KnowledgeBase** - RAG, vectors, chat (hybrid search)
4. **DocumentProcessing** - PDF upload, extraction, validation
5. **WorkflowIntegration** - n8n workflows, error logging
6. **SystemConfiguration** - Runtime config, feature flags
7. **Administration** - Users, alerts, audit, analytics

Each context follows CQRS pattern:
```
Domain (pure logic)
  → Application (Commands/Queries + Handlers)
    → Infrastructure (Repositories/Adapters)
      → HTTP Endpoints (MediatR)
```

## 🔗 Important Links

### Documentation
- **[Wiki Home](./README.md)** - Wiki navigation
- **[Main Docs](../docs/INDEX.md)** - Complete documentation (115+ docs)
- **[CLAUDE.md](../CLAUDE.md)** - Quick reference
- **[API Spec](../docs/03-api/board-game-ai-api-specification.md)** - REST API

### Guides by Role
- **[User Guide](./01-user-guide.md)** - Using the application
- **[Developer Guide](./02-developer-guide.md)** - Development workflow
- **[Testing Guide](./03-testing-guide.md)** - Testing procedures
- **[Deployment Guide](./04-deployment-guide.md)** - Deployment process
- **[Admin Guide](./05-administrator-guide.md)** - System maintenance
- **[Architecture Guide](./06-architecture-guide.md)** - Technical deep dive
- **[Contributing Guide](./07-contributing-guide.md)** - How to contribute

### External Resources
- **GitHub**: [DegrassiAaron/meepleai-monorepo](https://github.com/DegrassiAaron/meepleai-monorepo)
- **Issues**: [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
- **Discussions**: [GitHub Discussions](https://github.com/DegrassiAaron/meepleai-monorepo/discussions)

## 🆘 Getting Help

### Health Checks
```bash
# API health
curl http://localhost:5080/health

# Service status
docker compose ps
```

### Common Issues
See the [Troubleshooting section in CLAUDE.md](../CLAUDE.md#troubleshooting)

### Support Channels
1. Check the [User Guide](./01-user-guide.md) for usage questions
2. Check the [Developer Guide](./02-developer-guide.md) for development questions
3. Search [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
4. Post in [GitHub Discussions](https://github.com/DegrassiAaron/meepleai-monorepo/discussions)
5. Review [Main Documentation](../docs/INDEX.md)

## 📅 Version Information

- **Version**: 1.0-rc
- **Phase**: Alpha
- **Last Updated**: 2025-11-15
- **DDD Progress**: 99% complete
- **Test Suite**: 4,225 tests (90%+ coverage)

---

**Next Steps**: Choose your role from the [Wiki Home](./README.md) and dive into the appropriate guide!
