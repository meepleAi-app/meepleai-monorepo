#!/bin/bash

# Phase 3: Arbitro Agent - 6 issues, 29 SP total

gh issue create --title "[Arbitro Agent] Rules Arbitration Engine - Real-Time Validation" \
  --body "## Overview
Implement core rules arbitration engine for real-time move and action validation.

**Parent Epic**: #3490 (Phase 3 - Arbitro Agent)
**Story Points**: 5

## Requirements
- Real-time rules validation API
- Game state parser
- Rule conflict detection
- Natural language explanation of violations

## Technical Design
- Domain: KnowledgeBase/Agents/Arbitro
- Pattern: LangGraph + RAG hybrid
- Strategy: BALANCED (precision critical)

## DoD
- [ ] Rules validation endpoint functional
- [ ] <100ms P95 latency
- [ ] Natural language explanations
- [ ] Unit tests >90%
- [ ] Integration with Tutor Agent

## Estimate: 1 week" \
  --label "area/ai,backend,priority: high" \
  --assignee "@me"

gh issue create --title "[Arbitro Agent] Move Validation Logic with Game State Analysis" \
  --body "## Overview
Validate player moves against current game state and rules.

**Parent Epic**: #3490 (Phase 3)
**Story Points**: 5

## Requirements
- Parse game state from session
- Validate move legality
- Check turn order and player permissions
- Resource validation (cards, tokens, etc)

## DoD
- [ ] Move validation working
- [ ] State analysis accurate
- [ ] Error messages clear
- [ ] Tests >90%

## Estimate: 1 week" \
  --label "area/ai,backend,priority: high"

gh issue create --title "[Arbitro Agent] Conflict Resolution & Edge Cases" \
  --body "## Overview
Handle rule conflicts, ambiguities, and edge cases.

**Parent Epic**: #3490 (Phase 3)
**Story Points**: 5

## Requirements
- Detect rule ambiguities
- Priority resolution system
- FAQ for common conflicts
- Escalation to human arbitrator

## DoD
- [ ] Conflict detection works
- [ ] Resolution logic documented
- [ ] Edge cases covered
- [ ] Tests >85%

## Estimate: 1 week" \
  --label "area/ai,backend,priority: medium"

gh issue create --title "[Arbitro Agent] REST API Endpoint - /api/v1/agents/arbitro/validate" \
  --body "## Overview
Expose Arbitro Agent via REST API with SSE streaming.

**Parent Epic**: #3490 (Phase 3)
**Story Points**: 3

## Requirements
- POST /api/v1/agents/arbitro/validate
- SSE streaming for real-time feedback
- Request/response DTOs
- OpenAPI documentation

## DoD
- [ ] Endpoint functional
- [ ] SSE streaming works
- [ ] API docs complete
- [ ] Integration tests

## Estimate: 3-4 days" \
  --label "area/ai,backend,area/api,priority: high"

gh issue create --title "[Arbitro Agent] Performance Optimization - <100ms Target" \
  --body "## Overview
Optimize Arbitro to meet <100ms P95 latency target.

**Parent Epic**: #3490 (Phase 3)
**Story Points**: 5

## Requirements
- Cache frequent validations
- Optimize rule lookup
- Parallel validation where possible
- Performance benchmarking

## DoD
- [ ] <100ms P95 achieved
- [ ] Load testing passed
- [ ] Cache hit rate >70%
- [ ] Performance report

## Estimate: 1 week" \
  --label "area/ai,backend,area/performance,priority: high"

gh issue create --title "[Arbitro Agent] Testing & User Feedback Iteration" \
  --body "## Overview
Beta testing with real users and iteration based on feedback.

**Parent Epic**: #3490 (Phase 3)
**Story Points**: 6

## Requirements
- Beta user cohort (20-30 users)
- Feedback collection
- Issue triage and fixes
- Documentation updates

## DoD
- [ ] Beta testing complete
- [ ] Feedback incorporated
- [ ] Known issues documented
- [ ] User satisfaction >4.0

## Estimate: 1-2 weeks" \
  --label "area/ai,kind/test,priority: medium"

echo "✓ Created 6 Arbitro Agent issues"
