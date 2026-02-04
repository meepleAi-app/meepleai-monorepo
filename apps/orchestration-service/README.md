# MeepleAI Orchestration Service

**Issue**: #3495 - LangGraph Orchestrator Foundation
**Epic**: #3490 - Multi-Agent Game AI System

LangGraph-based orchestration service for coordinating Tutor, Arbitro, and Decisore agents in the MeepleAI multi-agent system.

## Architecture

### 3-Agent System

```
User Query → Intent Classification → Agent Router → Agent Execution → Response
                                            ↓
                                    Tutor / Arbitro / Decisore
```

**Agents**:
- **Tutor**: Setup and rules questions, tutorials
- **Arbitro**: Move validation, rules arbitration
- **Decisore**: Strategic move suggestions

### LangGraph Workflow

```python
StateGraph(GameAgentState)
    ├─ classify_intent (keyword-based, will use LLM in #3496)
    ├─ tutor_agent (placeholder, real impl in Phase 2)
    ├─ arbitro_agent (placeholder, real impl in Phase 3)
    ├─ decisore_agent (placeholder, real impl in Phase 4)
    └─ format_response
```

## Quick Start

### Development

```bash
# Install dependencies
cd apps/orchestration-service
python -m venv venv
source venv/bin/activate  # Windows: venv\Scripts\activate
pip install -r requirements.txt

# Configure environment
cp .env.example .env
# Edit .env with your configuration

# Run locally
python main.py
# Or with uvicorn
uvicorn main:app --reload --port 8004
```

### Docker

```bash
# Start with docker-compose (from infra/)
cd infra
docker compose --profile ai up orchestration-service

# Check health
curl http://localhost:8004/health

# Test workflow
curl -X POST http://localhost:8004/execute \
  -H "Content-Type: application/json" \
  -d '{
    "game_id": "123e4567-e89b-12d3-a456-426614174000",
    "session_id": "123e4567-e89b-12d3-a456-426614174001",
    "query": "How do I set up chess?"
  }'
```

## API Endpoints

### `GET /`
Service information

**Response**:
```json
{
  "service": "MeepleAI Orchestration Service",
  "version": "0.1.0",
  "agents": ["tutor", "arbitro", "decisore"]
}
```

### `GET /health`
Health check with dependency status

**Response**:
```json
{
  "status": "healthy",
  "version": "0.1.0",
  "dependencies": {
    "embedding": "healthy",
    "reranker": "healthy",
    "orchestrator": "healthy"
  }
}
```

### `POST /execute`
Execute multi-agent workflow

**Request**:
```json
{
  "game_id": "uuid",
  "session_id": "uuid",
  "query": "user question",
  "board_state": {...},  // optional
  "pending_move": "e2-e4"  // optional
}
```

**Response**:
```json
{
  "agent_type": "tutor",
  "response": "agent response text",
  "confidence": 0.85,
  "citations": ["source1", "source2"],
  "execution_time_ms": 245.3,
  "session_id": "uuid",
  "error": null
}
```

### `GET /metrics`
Prometheus metrics

**Response**: Prometheus text format
```
workflow_executions_total 42
workflow_failures_total 2
workflow_duration_ms_avg 312.45
```

## Configuration

Environment variables (see `.env.example`):

| Variable | Default | Description |
|----------|---------|-------------|
| `LOG_LEVEL` | `INFO` | Logging level |
| `PORT` | `8004` | Service port |
| `EMBEDDING_SERVICE_URL` | `http://embedding-service:8000` | Embedding service |
| `RERANKER_SERVICE_URL` | `http://reranker-service:8003` | Reranker service |
| `DATABASE_URL` | `postgresql://...` | PostgreSQL connection |
| `REDIS_URL` | `redis://...` | Redis connection |
| `QDRANT_URL` | `http://qdrant:6333` | Qdrant vector store |
| `OPENROUTER_API_KEY` | - | OpenRouter API key (for LLM) |
| `LANGGRAPH_TIMEOUT` | `30` | Workflow timeout (seconds) |
| `MAX_WORKFLOW_DEPTH` | `10` | Max recursion depth |

## Testing

```bash
# Run all tests
pytest

# Run with coverage
pytest --cov=src --cov-report=html

# Run specific test file
pytest tests/test_orchestrator.py -v

# Run integration tests only
pytest -m integration
```

**Coverage Target**: ≥85%

## Project Structure

```
orchestration-service/
├── src/
│   ├── api/              # FastAPI schemas
│   │   ├── schemas.py
│   │   └── __init__.py
│   ├── application/      # LangGraph orchestrator
│   │   ├── orchestrator.py
│   │   └── __init__.py
│   ├── domain/           # State models
│   │   ├── state.py
│   │   └── __init__.py
│   ├── config/           # Settings
│   │   ├── settings.py
│   │   └── __init__.py
│   └── __init__.py
├── tests/
│   ├── test_orchestrator.py  # Unit tests
│   ├── test_api.py            # API tests
│   └── __init__.py
├── main.py               # FastAPI app
├── requirements.txt
├── Dockerfile
├── .env.example
├── .dockerignore
├── pytest.ini
└── README.md
```

## Dependencies

**Python Services** (must be running):
- embedding-service (port 8000)
- reranker-service (port 8003)

**Infrastructure** (must be running):
- PostgreSQL (port 5432)
- Redis (port 6379)
- Qdrant (port 6333)

## Current Implementation Status

### ✅ Phase 1: Foundation (Issue #3495)
- [x] LangGraph state definition (GameAgentState)
- [x] Basic orchestrator with routing logic
- [x] FastAPI server with /health, /execute, /metrics
- [x] Docker integration
- [x] Placeholder agent nodes
- [x] Unit and integration tests

### ⏳ Future Phases
- **Phase 2 (#3496-#3502)**: Tutor agent with intent classification, hybrid search, conversation memory
- **Phase 3**: Arbitro agent with move validation
- **Phase 4**: Decisore agent with strategic analysis

## Performance Targets

| Metric | Target | Current (Placeholder) |
|--------|--------|----------------------|
| Workflow P95 | < 2s | ~100ms (no LLM) |
| Intent Classification | < 500ms | ~10ms (keywords) |
| Agent Response | < 1.5s | ~50ms (placeholder) |

## Monitoring

Metrics exported at `/metrics` in Prometheus format:
- `workflow_executions_total` - Total workflow runs
- `workflow_failures_total` - Failed workflows
- `workflow_duration_ms_avg` - Average execution time
- `health_checks_total` - Health check requests

## Troubleshooting

### Service won't start
- Check logs: `docker logs meepleai-orchestrator`
- Verify dependencies: embedding-service, reranker-service running
- Check network: `docker network inspect meepleai`

### Health check fails
- Verify dependent services are healthy
- Check port 8004 is not in use: `netstat -ano | findstr :8004`

### Workflow execution errors
- Check LangGraph timeout (default: 30s)
- Verify OpenRouter API key if using LLM
- Review logs for detailed error traces

## References

- **Issue**: #3495
- **Epic**: #3490 - Multi-Agent Game AI System
- **Architecture Docs**: `docs/02-development/agent-architecture/`
