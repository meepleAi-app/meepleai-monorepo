#!/bin/bash
# Create all remaining Board Game AI issues (batch script)
set -e

echo "🚀 Creating All Remaining BGAI Issues..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Month 2: LLM Integration (remaining 10 issues)
echo "📋 Month 2: LLM Integration (11 issues)"

gh issue create --title "[BGAI-018] OpenRouterClient implementation (if Option B chosen)" \
  --body "**Goal**: C# client for OpenRouter API (conditional on BGAI-016 decision)

**Tasks**:
- [ ] Create OpenRouterClient class
- [ ] Implement IHttpClientFactory
- [ ] Add model selection (GPT-4, Claude)
- [ ] Add retry + timeout
- [ ] Add cost tracking

**Acceptance**: Client functional, costs tracked
**Dependencies**: #958 (BGAI-016)
**References**: Week 5" \
  --label "board-game-ai,month-2,backend,llm" \
  --milestone "Month 2: LLM Integration"

gh issue create --title "[BGAI-019] Unit tests for LLM clients (Ollama + optional OpenRouter)" \
  --body "**Goal**: Comprehensive testing for LLM integration

**Test Cases** (12 minimum):
1. Ollama successful query
2. Ollama timeout
3. Ollama model not found
4. Ollama streaming
5. OpenRouter successful (if used)
6. OpenRouter API error (if used)
7. Cost tracking validation
8. Model selection logic
9. Retry logic
10. Response parsing
11. Italian language queries
12. Concurrent requests

**Acceptance**: All tests pass, coverage ≥90%
**Dependencies**: #959 (BGAI-017), BGAI-018
**References**: Week 5" \
  --label "board-game-ai,month-2,backend,testing" \
  --milestone "Month 2: LLM Integration"

gh issue create --title "[BGAI-020] AdaptiveLlmService (routing logic between LLM providers)" \
  --body "**Goal**: Smart routing service for LLM provider selection

**Routing Logic**:
- Default: Ollama (free, local)
- Fallback: OpenRouter (if configured and Ollama fails)
- Override: Feature flag AI:PreferredProvider

**Tasks**:
- [ ] Create AdaptiveLlmService class
- [ ] Implement provider selection logic
- [ ] Add circuit breaker for failed providers
- [ ] Add cost tracking per provider
- [ ] Add latency tracking
- [ ] Add provider health checks

**Acceptance**: Routing works, providers switchable
**Dependencies**: #959, BGAI-018, BGAI-019
**References**: Week 6" \
  --label "board-game-ai,month-2,backend,llm,orchestration" \
  --milestone "Month 2: LLM Integration"

gh issue create --title "[BGAI-021] Feature flag AI:Provider configuration" \
  --body "**Goal**: Runtime configuration for LLM provider selection

**Configuration**:
\`\`\`json
{
  \"AI\": {
    \"PreferredProvider\": \"Ollama\",
    \"Providers\": {
      \"Ollama\": { \"Enabled\": true, \"Models\": [\"mistral\", \"llama3\"] },
      \"OpenRouter\": { \"Enabled\": false, \"ApiKey\": \"\" }
    },
    \"FallbackChain\": [\"Ollama\", \"OpenRouter\"]
  }
}
\`\`\`

**Tasks**:
- [ ] Add AI:Provider config section
- [ ] Add validation on startup
- [ ] Add runtime toggle via admin UI
- [ ] Add config tests

**Acceptance**: Config flexible, runtime switchable
**Dependencies**: BGAI-020
**References**: Week 6" \
  --label "board-game-ai,month-2,backend,configuration" \
  --milestone "Month 2: LLM Integration"

gh issue create --title "[BGAI-022] Integration tests for adaptive LLM routing" \
  --body "**Goal**: E2E testing of LLM provider switching

**Test Scenarios**:
1. Ollama primary success
2. Ollama fails → OpenRouter fallback (if configured)
3. Both providers down → error handling
4. Feature flag toggle
5. Cost tracking accuracy
6. Latency comparison

**Acceptance**: All scenarios pass, routing reliable
**Dependencies**: BGAI-020, BGAI-021
**References**: Week 6" \
  --label "board-game-ai,month-2,backend,testing,integration" \
  --milestone "Month 2: LLM Integration"

gh issue create --title "[BGAI-023] Replace existing LLM calls in RagService with AdaptiveLlmService" \
  --body "**Goal**: Migrate RagService to use new adaptive LLM client

**Tasks**:
- [ ] Identify all LLM calls in RagService
- [ ] Replace with AdaptiveLlmService
- [ ] Maintain backward compatibility
- [ ] Update dependency injection
- [ ] Update configuration
- [ ] Test with existing queries

**Acceptance**: RagService uses adaptive client, tests pass
**Dependencies**: BGAI-022
**References**: Week 7" \
  --label "board-game-ai,month-2,backend,refactoring" \
  --milestone "Month 2: LLM Integration"

gh issue create --title "[BGAI-024] Backward compatibility testing for RAG with new LLM" \
  --body "**Goal**: Ensure RAG functionality unchanged with new LLM client

**Tests**:
- [ ] Existing unit tests still pass
- [ ] Integration tests pass
- [ ] Response quality equivalent
- [ ] Latency comparable
- [ ] Error handling preserved

**Acceptance**: All existing tests pass, no regressions
**Dependencies**: BGAI-023
**References**: Week 7" \
  --label "board-game-ai,month-2,backend,testing" \
  --milestone "Month 2: LLM Integration"

gh issue create --title "[BGAI-025] Performance testing (latency baseline <3s P95)" \
  --body "**Goal**: Establish performance baseline for LLM integration

**Metrics**:
- P50, P95, P99 latency
- Per-provider comparison (Ollama vs OpenRouter)
- Throughput (queries/second)
- Resource usage (CPU, RAM, GPU)

**Tasks**:
- [ ] Create load test suite
- [ ] Test with 100 concurrent queries
- [ ] Measure all metrics
- [ ] Create Grafana dashboard
- [ ] Document baseline

**Acceptance**: P95 <3s, baseline documented
**Dependencies**: BGAI-024
**References**: Week 8" \
  --label "board-game-ai,month-2,backend,testing,performance" \
  --milestone "Month 2: LLM Integration"

gh issue create --title "[BGAI-026] Cost tracking (Ollama vs OpenRouter comparison)" \
  --body "**Goal**: Track and compare LLM provider costs

**Metrics**:
- Cost per query
- Cost per month (projected)
- Provider usage ratio
- Model usage breakdown

**Tasks**:
- [ ] Implement cost tracking in AdaptiveLlmService
- [ ] Store costs in database
- [ ] Create cost report endpoint
- [ ] Add cost alerts (budget exceeded)
- [ ] Create cost dashboard (Grafana)

**Acceptance**: Costs tracked accurately, dashboard functional
**Dependencies**: BGAI-025
**References**: Week 8" \
  --label "board-game-ai,month-2,backend,monitoring" \
  --milestone "Month 2: LLM Integration"

gh issue create --title "[BGAI-027] LLM integration documentation and ADR" \
  --body "**Goal**: Document LLM integration architecture and decisions

**Documentation**:
1. ADR-004: LLM Provider Strategy
2. API documentation for AdaptiveLlmService
3. Configuration guide
4. Cost optimization guide
5. Troubleshooting guide

**Tasks**:
- [ ] Write ADR-004
- [ ] Document API
- [ ] Write config guide
- [ ] Write optimization guide
- [ ] Update README

**Acceptance**: All docs complete, easy to understand
**Dependencies**: BGAI-026
**References**: Week 8" \
  --label "board-game-ai,month-2,documentation" \
  --milestone "Month 2: LLM Integration"

echo "✅ Month 2 complete (12 issues)"
