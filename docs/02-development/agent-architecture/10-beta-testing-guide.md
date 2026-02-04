# Tutor Agent Beta Testing Guide

**Issue**: #3501
**Epic**: #3490 - Multi-Agent Game AI System
**Date**: 2026-02-04

## Prerequisites - ALL COMPLETE ✅

| Dependency | Issue | Status | PR |
|------------|-------|--------|----|
| Intent Classification | #3496 | ✅ Merged | #3540 |
| Dialogue State Machine | #3497 | ✅ Merged | #3540 |
| Conversation Memory | #3498 | ✅ Merged | #3540 |
| REST API Endpoint | #3499 | ✅ Merged | #3540 |
| Hybrid Search | #3502 | ✅ Merged | #3540 |
| PostgreSQL Schema | #3493 | ✅ Merged | #3545 |
| Context Engineering | #3491 | ✅ Merged | #3546 |

**All technical dependencies satisfied!** System ready for deployment.

## Beta Testing Plan

### Phase 1: Recruitment (Week 1)

**Target**: 50 beta testers with diverse game preferences

**Recruitment Channels**:
- BoardGameGeek forum announcement
- Existing MeepleAI users (if any)
- Gaming communities (Reddit r/boardgames)
- Discord servers (board game focused)

**Selection Criteria**:
- Mix of casual and hardcore gamers
- Variety of favorite games (strategy, party, cooperative)
- Different experience levels (beginner to expert)

### Phase 2: Onboarding (Week 1-2)

**Setup**:
1. Deploy orchestration-service to staging
2. Enable Tutor Agent endpoint in production API
3. Create beta tester accounts with increased quotas
4. Provide onboarding guide with test scenarios

**Test Scenarios**:
```markdown
1. Setup Questions: "How do I set up Catan for 4 players?"
2. Rules Questions: "Can I move my knight backwards in chess?"
3. Multi-Turn Dialogue: Ask follow-up questions referencing previous context
4. Ambiguous Queries: "What about the queen?" (requires context)
5. Edge Cases: Very long questions, rapid-fire queries
```

### Phase 3: Data Collection (Week 2-4)

**Telemetry Metrics** (already implemented in orchestration-service):
```python
# Prometheus metrics exported at /metrics
workflow_executions_total
workflow_failures_total
workflow_duration_ms_avg
intent_classification_accuracy
context_retention_turns_avg
```

**Feedback Survey** (Google Forms / Typeform):
```
1. How helpful were the responses? (1-5 stars)
2. Were responses accurate? (1-5 stars)
3. Did context from previous questions persist? (Yes/No)
4. Response time acceptable? (1-5 stars)
5. What improvements would you suggest? (Free text)
6. What worked well? (Free text)
```

**A/B Testing Configuration**:
```python
# In orchestration-service settings
RERANKING_WEIGHTS_VARIANT = "CONTROL" | "SEMANTIC_HEAVY" | "BALANCED" | "KEYWORD_HEAVY"

Variants:
- CONTROL: 0.4 keyword, 0.6 semantic (default)
- SEMANTIC_HEAVY: 0.2 keyword, 0.8 semantic
- BALANCED: 0.5 keyword, 0.5 semantic
- KEYWORD_HEAVY: 0.6 keyword, 0.4 semantic
```

### Phase 4: Analysis (Week 4-5)

**Key Metrics to Analyze**:
```
User Satisfaction:
- Target: >4.0/5.0 average
- Breakdown by: intent type, game type, user level

Performance:
- Response time P50, P95, P99
- Target: <2s P95
- Identify slow queries

Context Retention:
- Multi-turn coherence score
- Average turns before context loss
- Summarization trigger frequency

Intent Classification:
- Accuracy per intent type (SETUP, RULES, GENERAL)
- Misclassification patterns
- Confidence score distribution

Hybrid Search:
- Citation relevance scores
- Keyword vs semantic contribution
- Reranking impact on quality
```

**Analysis Tools**:
```sql
-- Query analytics from PostgreSQL
SELECT
  intent,
  AVG(confidence_score) as avg_confidence,
  AVG(response_time_ms) as avg_response_time,
  COUNT(*) as query_count
FROM conversation_memory
WHERE timestamp > NOW() - INTERVAL '2 weeks'
GROUP BY intent;
```

### Phase 5: Iteration (Week 5-6)

**Common Improvements** (based on feedback):

1. **Prompt Engineering**:
   - Adjust TUTOR_PROMPT for better responses
   - Improve few-shot examples in intent classification
   - Refine summarization strategy

2. **Search Tuning**:
   - Adjust hybrid search weights based on A/B results
   - Fine-tune reranking threshold
   - Optimize top-k values

3. **Context Management**:
   - Adjust max_turns_before_summary threshold
   - Improve temporal scoring weights
   - Optimize context window size

**Implementation Cycle**:
```
Feedback → Analysis → Hypothesis → Implementation → Deploy → Measure → Repeat
```

## Deployment Checklist

### Staging Deployment
- [ ] Deploy orchestration-service to staging
- [ ] Configure OpenRouter API key
- [ ] Verify PostgreSQL schema applied
- [ ] Verify Redis cache accessible
- [ ] Verify Qdrant collections ready
- [ ] Run smoke tests on staging
- [ ] Enable metrics collection

### Production Deployment (Post-Beta)
- [ ] Review beta results and implement improvements
- [ ] Update production configuration
- [ ] Deploy with feature flag (gradual rollout)
- [ ] Monitor error rates and performance
- [ ] Gradual rollout: 10% → 50% → 100%

## Success Criteria

**Quantitative**:
- ✅ User satisfaction ≥4.0/5.0
- ✅ Response time P95 <2s
- ✅ Context retention ≥10 turns
- ✅ Intent classification accuracy ≥85%

**Qualitative**:
- ✅ Users report helpful responses
- ✅ Multi-turn conversations feel natural
- ✅ Citations are relevant and accurate
- ✅ No critical bugs or crashes

## Beta Testing Report Template

```markdown
# Tutor Agent Beta Testing Report

**Period**: [Start Date] - [End Date]
**Participants**: 50 users
**Total Queries**: [N]

## Results

### User Satisfaction
- Overall: [X.X]/5.0
- Helpfulness: [X.X]/5.0
- Accuracy: [X.X]/5.0
- Response Time: [X.X]/5.0

### Performance Metrics
- Response Time P50: [X]ms
- Response Time P95: [X]ms
- Intent Classification Accuracy: [X]%
- Context Retention: [X] turns average

### A/B Testing Results
| Variant | Users | Satisfaction | P95 Time |
|---------|-------|--------------|----------|
| CONTROL | 12 | 4.2 | 1.8s |
| SEMANTIC_HEAVY | 13 | 4.3 | 1.9s |
| BALANCED | 12 | 4.1 | 1.7s |
| KEYWORD_HEAVY | 13 | 4.0 | 1.6s |

**Winner**: [Variant] with [reasoning]

### Improvements Implemented
1. [Improvement 1]
2. [Improvement 2]

### Recommendations
1. [Recommendation 1]
2. [Recommendation 2]
```

## Next Steps

1. **Deploy to Staging**: Coordinate with DevOps team
2. **Recruit Testers**: Use channels listed above
3. **Run Beta**: 2-4 weeks with feedback collection
4. **Analyze Results**: Use metrics and survey data
5. **Iterate**: Implement improvements
6. **Production Release**: Gradual rollout with monitoring

## Contact

**Technical Lead**: [Name]
**Beta Coordinator**: [Name]
**Feedback Email**: beta@meepleai.app

---

**Status**: Ready for deployment
**Last Updated**: 2026-02-04
