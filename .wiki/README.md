# MeepleAI Wiki

Welcome to the MeepleAI Wiki - your comprehensive guide to understanding, using, developing, and maintaining the MeepleAI board game rules assistant.

## 📚 Wiki Navigation

### For Different Roles

| Role | Guide | Description |
|------|-------|-------------|
| **End Users** | [User Guide](./01-user-guide.md) | How to use MeepleAI to get game rules help |
| **Developers** | [Developer Guide](./02-developer-guide.md) | Setup, development workflow, and coding standards |
| **QA/Testers** | [Testing Guide](./03-testing-guide.md) | Testing procedures, coverage requirements, and quality gates |
| **DevOps** | [Deployment Guide](./04-deployment-guide.md) | Deployment procedures, CI/CD, and infrastructure |
| **Administrators** | [Administrator Guide](./05-administrator-guide.md) | System maintenance, monitoring, and troubleshooting |
| **Technical Leads** | [Architecture Guide](./06-architecture-guide.md) | System architecture, design decisions, and patterns |
| **Contributors** | [Contributing Guide](./07-contributing-guide.md) | How to contribute to the project |

### Quick Links

- **[Home/Overview](./00-home.md)** - Project overview and quick start
- **[Main Documentation](../docs/INDEX.md)** - Complete technical documentation (115+ docs)
- **[CLAUDE.md](../CLAUDE.md)** - AI assistant instructions and quick reference
- **[SECURITY.md](../SECURITY.md)** - Security policies and procedures

## 🎯 Quick Start by Role

### "I want to use MeepleAI"
→ Start with [User Guide](./01-user-guide.md)

### "I want to develop features"
→ Read [Developer Guide](./02-developer-guide.md) → [Architecture Guide](./06-architecture-guide.md)

### "I want to test the application"
→ Follow [Testing Guide](./03-testing-guide.md)

### "I want to deploy to production"
→ Review [Deployment Guide](./04-deployment-guide.md)

### "I need to maintain the system"
→ Consult [Administrator Guide](./05-administrator-guide.md)

### "I want to contribute"
→ Read [Contributing Guide](./07-contributing-guide.md)

## 📖 About MeepleAI

**MeepleAI** is an AI-powered board game rules assistant designed to help players quickly find accurate answers to game rules questions.

**Key Features:**
- 🎲 Hybrid RAG search (vector + keyword fusion)
- 📄 Advanced PDF processing with 3-stage fallback pipeline
- 🔐 Multi-method authentication (Cookie, API Key, OAuth, 2FA)
- 🌍 Italian-first with multilingual support
- ⚡ High performance with caching and optimization
- 📊 Full observability (logs, traces, metrics, alerts)

**Tech Stack:**
- Backend: ASP.NET 9, PostgreSQL, Qdrant, Redis
- Frontend: Next.js 16, React 19, Shadcn/UI, Tailwind CSS 4
- AI/ML: OpenRouter, Ollama, Unstructured, SmolDocling
- Observability: Seq, Jaeger, Prometheus, Grafana

## 🏗️ Project Status

- **Phase**: Alpha (DDD refactoring 99% complete)
- **Test Coverage**: 90%+ (4,225 tests)
- **Architecture**: 7 bounded contexts with CQRS/MediatR
- **Next Milestone**: Beta testing → Production release

## 📞 Support & Resources

- **Issues**: [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
- **Discussions**: [GitHub Discussions](https://github.com/DegrassiAaron/meepleai-monorepo/discussions)
- **Documentation**: See [docs/INDEX.md](../docs/INDEX.md)
- **Health Check**: `http://localhost:8080/health`

## 🔄 Wiki Maintenance

This wiki is maintained alongside the codebase. When making changes:
1. Keep guides in sync with code changes
2. Update version information when releasing
3. Add new guides as needed for new roles/processes
4. Cross-reference with main documentation in `/docs`

---

**Last Updated**: 2025-11-15
**Version**: 1.0-rc
**Maintainer**: Engineering Team
