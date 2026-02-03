# Docker Documentation

**Last Updated**: 2026-02-02

Comprehensive Docker Compose guides for MeepleAI local development.

---

## 📚 Documentation Structure

### 🚀 Quick Start
**[quick-start.md](./quick-start.md)** - 5-minute setup guide
- Prerequisites check
- Auto-generate secrets
- Start services (minimal, full, or native)
- Verify endpoints
- Next steps

**Best for**: First-time setup, quick reference

---

### 🌐 Service Endpoints
**[service-endpoints.md](./service-endpoints.md)** - Complete endpoint reference
- Frontend & API endpoints
- Database & storage connections (PostgreSQL, Redis, Qdrant)
- AI & ML services (Embedding, Reranker, Unstructured, SmolDocling, Ollama)
- Monitoring & observability (Grafana, Prometheus, Alertmanager, cAdvisor, HyperDX)
- Development tools (Mailpit, n8n)
- API example requests
- Health check scripts

**Best for**: Testing services, debugging connections, API integration

---

### 🧹 Clean Builds
**[clean-builds.md](./clean-builds.md)** - Build strategies & data management
- **Restart**: Quick fix (no data loss)
- **Rebuild**: Code changes (preserves volumes)
- **Medium Clean**: Fresh containers (preserves volumes) ⭐
- **Full Clean**: Complete reset (destroys all data) ⚠️
- Selective cleaning strategies
- Backup procedures

**Best for**: Fixing stuck services, switching branches, complete resets

---

### ⚙️ Common Commands
**[common-commands.md](./common-commands.md)** - Daily command cheatsheet
- Service management (start/stop/restart)
- Logs & debugging
- Container operations (exec, cp, inspect)
- Volume management (backup, restore, clean)
- Network operations
- Image management (build, pull, clean)
- Resource monitoring
- Database operations (PostgreSQL, Redis, Qdrant)
- Development workflows

**Best for**: Daily development, quick reference, operations

---

### 🔧 Troubleshooting
**[troubleshooting.md](./troubleshooting.md)** - Problem-solving guide
- **Port Conflicts** ⭐ (8080, 3000, 5432, etc.)
- **Memory & CPU Issues** ⭐ (resource limits, OOM, leaks)
- Service won't start
- Network problems
- Database issues
- Volume & permission errors
- Performance problems
- General debugging

**Best for**: Solving issues, debugging, optimization

---

### 📦 Docker Profiles
**[docker-profiles.md](./docker-profiles.md)** - Service profiles guide
- **minimal**: Core services (5 services, ~4 GB RAM)
- **dev**: Development stack (8 services, ~6 GB RAM)
- **ai**: ML services (10 services, ~12 GB RAM)
- **observability**: Monitoring (11 services, ~8 GB RAM)
- **automation**: n8n workflows (6 services, ~5 GB RAM)
- **full**: Complete stack (17+ services, ~18 GB RAM)
- Profile combinations & switching
- Use case scenarios

**Best for**: Optimizing resource usage, selective services

---

### 🔬 Advanced Features
**[advanced-features.md](./advanced-features.md)** - Power user guide
- Custom Docker Compose overrides
- Docker profiles customization
- Build optimizations (BuildKit, caching)
- Layer caching strategies
- Multi-stage builds
- VS Code integration (Dev Containers, debugging)
- JetBrains Rider integration
- Performance tuning
- Production best practices
- CI/CD integration

**Best for**: Advanced users, IDE integration, optimization

---

## 🎯 Quick Navigation

### By Task

| Task | Document |
|------|----------|
| **First-time setup** | [quick-start.md](./quick-start.md) |
| **Find service URL** | [service-endpoints.md](./service-endpoints.md) |
| **Clean rebuild** | [clean-builds.md](./clean-builds.md) (Medium Clean section) |
| **Fix port conflict** | [troubleshooting.md](./troubleshooting.md) (Port Conflicts section) |
| **High memory usage** | [troubleshooting.md](./troubleshooting.md) (Memory & CPU section) |
| **Daily commands** | [common-commands.md](./common-commands.md) |
| **Reduce resource usage** | [docker-profiles.md](./docker-profiles.md) (use minimal profile) |
| **IDE setup** | [advanced-features.md](./advanced-features.md) (VS Code/Rider sections) |

### By User Level

**Beginner** (just getting started):
1. [quick-start.md](./quick-start.md) - Setup environment
2. [service-endpoints.md](./service-endpoints.md) - Test connections
3. [troubleshooting.md](./troubleshooting.md) - Fix issues

**Intermediate** (daily development):
1. [docker-profiles.md](./docker-profiles.md) - Optimize resource usage
2. [common-commands.md](./common-commands.md) - Efficient workflows
3. [clean-builds.md](./clean-builds.md) - Maintain environment

**Advanced** (power users):
1. [advanced-features.md](./advanced-features.md) - IDE integration, optimization
2. [docker-profiles.md](./docker-profiles.md) - Custom profiles
3. [clean-builds.md](./clean-builds.md) - Advanced cleanup strategies

---

## 📖 Additional Resources

### Main Documentation
- **Main Guide**: [../local-environment-startup-guide.md](../local-environment-startup-guide.md) - Comprehensive startup guide
- **Development README**: [../README.md](../README.md) - Development overview
- **CLAUDE.md**: [../../CLAUDE.md](../../CLAUDE.md) - Project root guide

### External Resources
- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Reference](https://docs.docker.com/compose/compose-file/)
- [Docker BuildKit](https://docs.docker.com/build/buildkit/)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)

---

## 🤝 Contributing

**Found an issue?** Please update the relevant document.

**Adding new services?** Update:
1. [service-endpoints.md](./service-endpoints.md) - Add endpoint information
2. [docker-profiles.md](./docker-profiles.md) - Assign to appropriate profile(s)
3. [troubleshooting.md](./troubleshooting.md) - Add common issues

---

## ⏱️ Document Reading Times

| Document | Reading Time | Best For |
|----------|-------------|----------|
| quick-start.md | 5 min | First-time setup |
| service-endpoints.md | 10 min | Reference lookup |
| clean-builds.md | 8 min | Problem solving |
| common-commands.md | 12 min | Learning workflows |
| troubleshooting.md | 10 min | Debugging |
| docker-profiles.md | 8 min | Optimization |
| advanced-features.md | 15 min | Deep dive |

**Total Time**: ~1 hour (read all) | ~30 min (selective reading)

---

**Last Updated**: 2026-02-02
**Maintainer**: MeepleAI Development Team
**License**: Proprietary
