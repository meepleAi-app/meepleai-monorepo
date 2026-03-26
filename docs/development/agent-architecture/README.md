# Multi-Agent Game AI System Architecture

**MeepleAI's Intelligent Game Assistant Ecosystem**

> 📖 **Complete Research**: [Multi-Agent RAG Architecture Research](../../claudedocs/research_multiagent_rag_architecture_20260202.md)

## Overview

MeepleAI's multi-agent system consists of three specialized AI agents that work together to provide comprehensive board game assistance:

1. **🎓 Tutor Agent**: Interactive tutorial system for onboarding and rules QA
2. **⚖️ Arbitro Agent**: Real-time rules arbitration and move validation
3. **🎲 Decisore Agent**: Strategic AI for move suggestions and gameplay assistance

All agents share a unified **Context Engineering framework** that assembles dynamic context from multiple sources (knowledge base, conversation memory, game state, tool metadata) for intelligent decision-making.

## Architecture at a Glance

```
┌─────────────────────────────────────────────────────────────┐
│                    LangGraph Orchestrator                    │
│                  (Event-Driven Coordination)                 │
└────────┬──────────────────┬───────────────────┬─────────────┘
         │                  │                   │
    ┌────▼────┐       ┌─────▼──────┐     ┌─────▼──────┐
    │  Tutor  │       │  Arbitro   │     │ Decisore   │
    │  Agent  │       │   Agent    │     │   Agent    │
    └────┬────┘       └─────┬──────┘     └─────┬──────┘
         │                  │                   │
         └──────────────────┼───────────────────┘
                            │
                ┌───────────▼────────────┐
                │ Context Engineering     │
                │   Framework             │
                ├─────────────────────────┤
                │ • Static Knowledge      │
                │ • Dynamic Memory        │
                │ • Agent State           │
                │ • Tool Metadata         │
                └─────────┬───────────────┘
                          │
         ┌────────────────┼────────────────┐
         │                │                │
    ┌────▼─────┐   ┌─────▼──────┐  ┌─────▼──────┐
    │  Qdrant  │   │PostgreSQL  │  │   Redis    │
    │ (Vector) │   │  (Memory)  │  │  (Cache)   │
    └──────────┘   └────────────┘  └────────────┘
```

## Quick Navigation

- [Context Engineering Framework](./01-context-engineering.md) - Multi-source context assembly
- [Tutor Agent](./02-tutor-agent.md) - Interactive tutorial system
- [Arbitro Agent](./03-arbitro-agent.md) - Rules arbitration engine
- [Decisore Agent](./04-decisore-agent.md) - Strategic AI
- [LangGraph Orchestration](./05-orchestration.md) - Multi-agent coordination
- [Integration Guide](./06-integration.md) - Connecting with existing stack
- [Testing Strategy](./07-testing.md) - Validation and quality assurance

## Key Features

### Context Engineering (2025-2026 Evolution)

Traditional RAG retrieves documents. **Context Engineering** assembles multi-source context:

| Source | Type | Example |
|--------|------|---------|
| **Knowledge Base** | Static | Game rules, FAQs, strategy guides |
| **Conversation Memory** | Dynamic | User's previous questions, dialogue history |
| **Agent State** | Real-time | Current game board position, turn number |
| **Tool Metadata** | Contextual | Available actions, component inventory |

### Hybrid Search Strategy

Balance exact rule matching with semantic understanding:

```
User Query: "Can my knight move backwards in chess?"
     ↓
Stage 1: Parallel Retrieval
├─ Keyword Search (BM25): Exact "knight" + "move" matches
└─ Semantic Search (Vector): Conceptually similar rules
     ↓
Stage 2: Reciprocal Rank Fusion (0.4 keyword, 0.6 semantic)
     ↓
Stage 3: Cross-Encoder Reranking (Top-10 → Top-3)
     ↓
Result: Most relevant knight movement rules
```

### Agent Specializations

| Agent | Purpose | Priority | Complexity | Timeline |
|-------|---------|----------|------------|----------|
| **Tutor** | Onboarding, QA, Setup | 1 (PoC) | Medium | 6 weeks |
| **Arbitro** | Move validation, Rules | 2 | High | 6 weeks |
| **Decisore** | Strategy suggestions | 3 | Very High | 8 weeks |

## Implementation Roadmap

### Phase 1: Foundation (Weeks 1-4)
- ✅ Research completed (2026-02-02)
- [ ] Enhance Qdrant with hybrid search
- [ ] Integrate cross-encoder reranker
- [ ] Extend PostgreSQL schema for memory
- [ ] Implement Redis 3-tier caching
- [ ] Create LangGraph orchestrator base

### Phase 2: Tutor Agent (Weeks 5-10)
- [ ] Intent classification (setup/rules/general)
- [ ] Multi-turn dialogue state machine
- [ ] Setup validation workflows
- [ ] Hybrid search + reranking integration
- [ ] Conversation memory (temporal RAG)
- [ ] REST API endpoint deployment

### Phase 3: Arbitro Agent (Weeks 11-16)
- [ ] Rule precedence graph design
- [ ] Redis hot cache for frequent rules
- [ ] Conflict resolution engine
- [ ] Event-driven message bus
- [ ] Move validation API
- [ ] Arbitration decision logging

### Phase 4: Decisore Agent (Weeks 17-24)
- [ ] MCTS engine with UCB1
- [ ] LLM position evaluator
- [ ] Strategy pattern RAG
- [ ] Difficulty scaling system
- [ ] Move explanation generation
- [ ] Computational budget management

### Phase 5: Integration (Weeks 25-28)
- [ ] Complete LangGraph orchestrator
- [ ] Cross-agent event routing
- [ ] Comprehensive monitoring
- [ ] Integration tests
- [ ] Production deployment

## Performance Targets

| Metric | Target | Rationale |
|--------|--------|-----------|
| **Tutor Response Time** | <2s (P95) | User expects conversational speed |
| **Arbitro Validation** | <100ms (P95) | Real-time gameplay requirement |
| **Decisore Move Gen** | <10s (expert) | Strategic thinking perception |
| **Cache Hit Rate** | >80% | Reduce redundant retrievals |
| **Test Coverage** | >90% | Production quality assurance |

## Technology Stack

### Core Infrastructure
- **Vector Store**: Qdrant (existing)
- **Embeddings**: sentence-transformers/all-MiniLM-L6-v2
- **Reranker**: cross-encoder/ms-marco-MiniLM-L-6-v2
- **Cache**: Redis (3-tier: memory → Redis → Qdrant)
- **Database**: PostgreSQL (conversation memory, state snapshots)

### Agent Orchestration
- **Framework**: LangGraph (stateful workflows)
- **LLM**: GPT-4 / Claude (via OpenRouter)
- **Runtime**: Python service (containerized)
- **Integration**: Event-driven message bus

### Specialized Components
- **Tutor**: Multi-turn dialogue, intent classification
- **Arbitro**: Rule precedence graph, conflict resolver
- **Decisore**: MCTS engine, LLM position evaluator

## Cost Considerations

### Estimated LLM API Costs
- **Per Conversation**: ~$0.60 (GPT-4 for Tutor)
- **Monthly** (10K users, 3 conversations avg): ~$18,000
- **Optimization Strategies**:
  - Caching (-40%)
  - Prompt compression (-30%)
  - Fine-tuning (future, self-hosted)

### Infrastructure Costs
- Qdrant, Redis, PostgreSQL: Self-hosted (existing)
- LangGraph: Python runtime (containerized, minimal overhead)

## Security & Privacy

### User Data Protection
- **Encryption at rest**: PostgreSQL conversation memory
- **Retention policy**: 90-day automatic deletion (GDPR)
- **Access control**: Session-based isolation

### Prompt Injection Defense
- Pattern detection for malicious inputs
- Sanitization of user queries
- Suspicious activity logging

### Rate Limiting
- 30 requests/minute per IP (Tutor)
- 100 requests/minute per IP (Arbitro)
- Adaptive limits based on usage patterns

## Getting Started

### For Developers
1. Read [Context Engineering Framework](./01-context-engineering.md)
2. Start with [Tutor Agent Implementation](./02-tutor-agent.md)
3. Follow [Integration Guide](./06-integration.md) for existing stack

### For Architects
1. Review [Complete Research Report](../../claudedocs/research_multiagent_rag_architecture_20260202.md)
2. Understand [LangGraph Orchestration](./05-orchestration.md)
3. Evaluate [Testing Strategy](./07-testing.md)

### For Product Managers
1. See [Implementation Roadmap](#implementation-roadmap)
2. Review [Performance Targets](#performance-targets)
3. Understand [Cost Considerations](#cost-considerations)

## Related Documentation

### Internal Resources
- [Complete Research Report](../../claudedocs/research_multiagent_rag_architecture_20260202.md) (70 pages)
- [MeepleAI Architecture](../../architecture/README.md)
- [API Documentation](../../api/README.md)
- [Testing Guide](../../testing/README.md)

### External References
- [LangGraph Documentation](https://python.langchain.com/docs/langgraph)
- [Qdrant Hybrid Search](https://qdrant.tech/documentation/concepts/hybrid-queries/)
- [Cross-Encoder Reranking](https://www.sbert.net/examples/applications/cross-encoder/README.html)
- [MCTS Algorithm](https://en.wikipedia.org/wiki/Monte_Carlo_tree_search)

## Support & Contribution

### Questions or Issues?
- Check [GitHub Issues](https://github.com/meepleai/meepleai-monorepo-dev/issues) with label `area/ai`
- Review existing [Agent Creation Epic #3386](https://github.com/meepleai/meepleai-monorepo-dev/issues/3386)
- See [Plugin-Based RAG Epic #3413](https://github.com/meepleai/meepleai-monorepo-dev/issues/3413)

### Contributing
1. Follow [Development Workflow](../workflow.md)
2. Ensure [Test Coverage](../../testing/backend/backend-testing-patterns.md) >90%
3. Update documentation for architectural changes

---

**Last Updated**: 2026-02-02
**Status**: Architecture Defined, Implementation Pending
**Next Milestone**: Phase 1 Foundation (4 weeks)
