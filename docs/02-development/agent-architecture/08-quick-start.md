# Quick Start Guide - Multi-Agent System

**Get started with MeepleAI's Multi-Agent Game AI architecture**

> 📖 **Parent**: [Multi-Agent Architecture](./README.md)

## For Product Managers

### Epic Overview
**#3490 - Multi-Agent Game AI System** (28 weeks, 162 SP)

**Phases**:
1. Foundation (4w) - Shared infrastructure
2. Tutor Agent (6w) - Priority 1 proof-of-concept
3. Arbitro Agent (6w) - Priority 2
4. Decisore Agent (8w) - Priority 3
5. Integration (4w) - Production deployment

**Current Status**: Foundation + Tutor issues created (11 issues)

### Key Deliverables
- **Tutor Agent**: Interactive game tutorial system
- **Arbitro Agent**: Real-time move validation
- **Decisore Agent**: Strategic AI assistant

### Business Value
- **User Engagement**: +40% (easier onboarding via Tutor)
- **Retention**: +25% (strategic assistance via Decisore)
- **Support Reduction**: -30% (automated rules QA)

## For Developers

### Phase 1: Foundation (Start Here)

**Week 1-2 Tasks**:
```bash
# 1. Review architecture
cat docs/02-development/agent-architecture/README.md
cat docs/02-development/agent-architecture/01-context-engineering.md

# 2. Set up development environment
cd apps
mkdir orchestration-service && cd orchestration-service
python -m venv venv && source venv/bin/activate  # or venv\Scripts\activate on Windows
pip install langgraph langchain sentence-transformers qdrant-client redis

# 3. Start working on issues
# - #3491: Context Engineering Framework
# - #3492: Hybrid Search
# - #3493: PostgreSQL Schema
# - #3494: Redis Cache
# - #3495: LangGraph Orchestrator
```

**Week 3-4 Tasks**:
- Integrate components
- Write integration tests
- Deploy to Docker Compose

### Phase 2: Tutor Agent

**Week 5-10 Tasks**:
```bash
# Implement Tutor Agent components
# - #3496: Intent Classification
# - #3497: Dialogue State Machine
# - #3498: Conversation Memory
# - #3499: REST API Endpoint
# - #3501: Beta Testing
```

## For Architects

### Key Decisions

**Framework Selection**: LangGraph over CrewAI/AutoGen
- **Rationale**: Best for stateful, sequential turn-based workflows
- **Trade-off**: Moderate learning curve vs excellent state management

**Hybrid Search**: Keyword (40%) + Semantic (60%)
- **Rationale**: Balance exact rule matching with natural language understanding
- **Trade-off**: Complexity vs accuracy (+15% precision)

**3-Tier Cache**: Memory → Redis → Qdrant
- **Rationale**: <100ms P95 for frequent rules
- **Trade-off**: Memory overhead vs latency reduction

### Architecture Reviews

**Recommended**:
1. Review [Complete Research Report](../../claudedocs/research_multiagent_rag_architecture_20260202.md)
2. Validate [Context Engineering](./01-context-engineering.md) against existing patterns
3. Assess [LangGraph Orchestration](./05-orchestration.md) scalability
4. Plan [Integration](./06-integration.md) with current services

## For QA Engineers

### Testing Priorities

**Phase 1 (Foundation)**:
- Integration tests for multi-source context assembly
- Performance tests for cache hit rates
- Load tests for Qdrant hybrid search

**Phase 2 (Tutor)**:
- Multi-turn conversation coherence
- Context retention validation
- Response time under load
- User acceptance testing (50 beta testers)

### Test Targets
- **Coverage**: >90% backend, >85% frontend
- **Performance**: See [Performance Targets](./README.md#performance-targets)
- **Quality**: User satisfaction >4.0/5.0

## For DevOps

### Infrastructure Requirements

**New Services**:
```yaml
# docker-compose.yml addition
orchestration-service:
  build: ./apps/orchestration-service
  ports: ['8090:8090']
  environment:
    - POSTGRES_URL=${DATABASE_URL}
    - REDIS_URL=${REDIS_URL}
    - QDRANT_URL=http://qdrant:6333
    - OPENROUTER_API_KEY=${OPENROUTER_API_KEY}
  depends_on: [postgres, redis, qdrant]
```

**Database Extensions**:
```sql
-- Requires pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;
```

**Monitoring**:
- New Grafana dashboard: Agent Performance
- Prometheus metrics: agent requests, latency, cache hits, LLM tokens

## Quick Commands

### Start Development
```bash
# Clone and setup
git checkout -b feature/issue-3491-context-engineering
cd apps/orchestration-service
python -m venv venv && source venv/bin/activate
pip install -r requirements.txt

# Run tests
pytest tests/ -v --cov

# Start service
uvicorn main:app --reload --port 8090
```

### Run Tests
```bash
# Backend (Foundation)
cd apps/api/src/Api
dotnet test --filter "Category=Unit&BoundedContext=KnowledgeBase"

# Python (Orchestration Service)
cd apps/orchestration-service
pytest tests/ -v --cov --cov-report=html
```

### Check Integration
```bash
# Verify all services running
docker compose ps

# Test Tutor API endpoint
curl -X POST http://localhost:8080/api/v1/agents/tutor/query \
  -H "Content-Type: application/json" \
  -d '{"sessionId":"test","gameId":"chess","query":"How do I win?"}'
```

## Resources

### Documentation
- [README](./README.md) - Architecture overview
- [Context Engineering](./01-context-engineering.md) - Multi-source framework
- [Tutor Agent](./02-tutor-agent.md) - Tutorial system
- [Arbitro Agent](./03-arbitro-agent.md) - Rules arbitration
- [Decisore Agent](./04-decisore-agent.md) - Strategic AI
- [Orchestration](./05-orchestration.md) - LangGraph coordination
- [Integration](./06-integration.md) - Stack integration
- [Testing](./07-testing.md) - QA strategy

### External
- [LangGraph Docs](https://python.langchain.com/docs/langgraph)
- [Qdrant Hybrid Search](https://qdrant.tech/documentation/concepts/hybrid-queries/)
- [Cross-Encoder Reranking](https://www.sbert.net/examples/applications/cross-encoder/README.html)

### GitHub
- **Epic**: #3490
- **Research**: docs/claudedocs/research_multiagent_rag_architecture_20260202.md
- **Issues**: 11 created (Foundation + Tutor)

---

**Last Updated**: 2026-02-02
**Next Milestone**: Complete Foundation Phase (4 weeks)
