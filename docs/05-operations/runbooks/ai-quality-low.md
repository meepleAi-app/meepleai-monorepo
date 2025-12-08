# Runbook: Low AI Quality

**Alerts**: `LowOverallConfidence`, `HighLowQualityRate`, `LowRagConfidence`, `LowLlmConfidence`, `DegradedOverallConfidence`, `ElevatedLowQualityRate`
**Severity**: CRITICAL (Low*/High*) or WARNING (Degraded*/Elevated*)
**Threshold**: Various (see Alert Reference below)
**Expected Response Time**: CRITICAL < 30 min, WARNING < 2 hours

## Symptoms

**Observable indicators when these alerts fire:**
- Alerts firing in Alertmanager (LowOverallConfidence, LowRagConfidence, LowLlmConfidence, etc.)
- AI response quality below acceptable thresholds
- Users reporting poor answer quality, incorrect information, missing citations
- Quality metrics dashboard showing low confidence scores (<0.70)
- High percentage of responses failing quality validation

## Impact

**Effect on system and users:**
- **User Experience**: Incorrect answers, low confidence responses, poor citations, user distrust
- **Data Integrity**: May provide wrong game rules, potentially affecting gameplay
- **Business Impact**: Core feature (AI rules assistant) providing low value, user churn risk, reputation damage
- **System Health**: AI pipeline degraded (RAG retrieval, LLM generation, or citation extraction failing)

## Alert Reference

### Critical Alerts (< 30 min response)

#### LowOverallConfidence
- **Trigger**: Average overall confidence < 0.60 for 1 hour
- **Impact**: AI responses consistently failing quality standards (>40% below threshold)
- **Urgency**: High - core feature severely degraded

#### HighLowQualityRate
- **Trigger**: >30% of responses are low quality for 1 hour
- **Impact**: More than 1 in 3 responses failing quality checks
- **Urgency**: High - systemic quality failure

#### LowRagConfidence
- **Trigger**: RAG confidence < 0.50 for 30 minutes
- **Impact**: Semantic search not finding relevant context (retrieval failing)
- **Urgency**: High - RAG pipeline broken

#### LowLlmConfidence
- **Trigger**: LLM confidence < 0.50 for 30 minutes
- **Impact**: Language model producing low-quality responses (generation failing)
- **Urgency**: High - LLM pipeline broken

### Warning Alerts (< 2 hour response)

#### DegradedOverallConfidence
- **Trigger**: Average overall confidence < 0.70 for 30 minutes
- **Impact**: Response quality degraded but not critical (60-70% range)
- **Urgency**: Medium - monitor closely, may escalate

#### ElevatedLowQualityRate
- **Trigger**: >15% of responses are low quality for 30 minutes
- **Impact**: Higher than normal rate of quality failures (15-30% range)
- **Urgency**: Medium - trending toward critical

## Investigation Steps

### 1. Verify Alert (30 seconds)

**Dashboard Check**:
```
http://localhost:3001/d/quality-metrics
```

**Verification checklist**:
- ✅ Which quality dimension is low? (overall, RAG, LLM, citation)
- ✅ What is current value vs threshold?
- ✅ Is this sustained or temporary spike?
- ✅ How many responses affected? (percentage)

**Prometheus Queries**:
```promql
# Overall confidence
meepleai:quality:overall_confidence:5m

# RAG confidence
meepleai:quality:rag_confidence:5m

# LLM confidence
meepleai:quality:llm_confidence:5m

# Low quality rate (percentage)
meepleai:quality:low_quality_rate:5m
```

**If false alarm**:
- Silence alert for 1 hour
- Document if quality drop was temporary (difficult queries)
- Create issue to adjust thresholds if recurring

### 2. Identify Failing Dimension (1 minute)

**Questions to answer**:
1. **Which dimension failing?** (RAG < 0.50, LLM < 0.50, Citation < 0.50)
2. **All queries or specific queries?** (topic-dependent, game-dependent)
3. **When did it start?** (deployment, config change, data change)
4. **Patterns in failures?** (specific games, query types, time of day)

**HyperDX - Quality scoring logs**:
```
http://localhost:8180
Filter: quality_score AND low_quality=true AND @timestamp:[now-30m TO now]
```

**Group by dimension**:
```
Group by: quality_dimension
# Shows: rag_confidence, llm_confidence, citation_quality, overall
```

**Check failing examples**:
```
HyperDX: quality_score.overall_confidence < 0.60 AND @timestamp:[now-10m TO now]
# Review specific low-quality responses
# Identify patterns (same game, similar questions, etc.)
```

### 3. Check RAG Component (if LowRagConfidence) (2 minutes)

**Qdrant health**:
```bash
# Check Qdrant status
docker compose ps qdrant
curl http://localhost:6333/healthz

# Check collection health
curl http://localhost:6333/collections/meepleai | jq '.result.status'

# Check vector count (should be >0)
curl http://localhost:6333/collections/meepleai | jq '.result.vectors_count'
```

**Embedding service**:
```bash
# Test embedding generation
# Check OpenRouter API for embedding model
curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  https://openrouter.ai/api/v1/models

# Check embedding logs
# HyperDX: embedding AND error
```

**RAG configuration**:
```bash
# Check retrieval settings
cat apps/api/src/Api/appsettings.json | grep -A 15 "Rag"

# Verify:
# - TopK: 5-10 (retrieval count)
# - MinScore: 0.70 (relevance threshold)
# - QueryExpansion: enabled
```

### 4. Check LLM Component (if LowLlmConfidence) (2 minutes)

**OpenRouter API health**:
```bash
# Test OpenRouter API
curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  https://openrouter.ai/api/v1/models

# Check API status
curl https://openrouter.ai/api/v1/status

# Check for rate limits or errors
# HyperDX: openrouter AND (error OR rate_limit)
```

**LLM configuration**:
```bash
# Check model selection
cat apps/api/src/Api/appsettings.json | grep -A 10 "OpenRouter"

# Verify:
# - Model: anthropic/claude-3-sonnet (or appropriate model)
# - Temperature: 0.0-0.3 (for factual responses)
# - MaxTokens: 1024 (sufficient for responses)
```

**Prompt quality**:
```bash
# Review prompt templates in code
grep -r "prompt" apps/api/src/Api/BoundedContexts/KnowledgeBase/

# Check recent prompt changes
git log --oneline --since="1 week ago" -- "**/KnowledgeBase/**"
```

### 5. Check Citation Extraction (2 minutes)

**Citation metrics**:
```promql
# Citation quality score
avg(meepleai_quality_score{dimension="citation_quality"})

# Responses without citations
rate(meepleai_quality_no_citations_total[5m])
```

**Citation extraction logs**:
```
HyperDX: citation AND (error OR missing OR not_found)
```

**Common citation issues**:
- PDF parsing failures (citations not extracted from source)
- LLM not following citation format (prompt issue)
- Citation validation failing (overly strict rules)

### 6. Analyze Quality Patterns (2 minutes)

**Quality distribution**:
```promql
# Quality score histogram
histogram_quantile(0.50, meepleai_quality_score_bucket{dimension="overall_confidence"})
histogram_quantile(0.90, meepleai_quality_score_bucket{dimension="overall_confidence"})
```

**HyperDX - Quality patterns**:
```
quality_score is not null
Group by: game_id
Sort by: avg(quality_score.overall_confidence) asc
# Shows which games have lowest quality
```

**Check for patterns**:
- Specific games with low quality (data quality issue)
- Specific query types (complex rules, edge cases)
- Time-based patterns (night vs day, traffic correlation)

### 7. Check Data Quality (1 minute)

**Vector index quality**:
```bash
# Check indexed document count
docker compose exec postgres psql -U meeple -d meepleai -c "
SELECT COUNT(*) FROM vector_documents;"

# Check Qdrant vector count
curl http://localhost:6333/collections/meepleai | jq '.result.vectors_count'

# Counts should match (all documents indexed)
```

**PDF processing quality**:
```bash
# Check recent PDF processing quality
docker compose exec postgres psql -U meeple -d meepleai -c "
SELECT id, filename, quality_score, extraction_stage
FROM pdf_documents
WHERE uploaded_at > NOW() - INTERVAL '7 days'
ORDER BY quality_score ASC
LIMIT 10;"

# Look for low quality scores (<0.80)
```

## Common Root Causes & Fixes

### Cause 1: Qdrant Down or Unhealthy (RAG Confidence Low)

**Symptoms**:
- RAG confidence < 0.50 (retrieval failing)
- All queries returning low RAG scores
- Logs: "Qdrant connection failed" or "Search failed"
- Quality dashboard shows RAG dimension red

**Fix**:
```bash
# Option A: Restart Qdrant
docker compose restart qdrant
# Wait 30 seconds for startup

# Option B: Verify collection exists
curl http://localhost:6333/collections/meepleai
# If missing, re-create and re-index

# Option C: Check collection health
curl http://localhost:6333/collections/meepleai | jq '.result.status'
# Should be "green", if "yellow" or "red", investigate

# Refer to: rag-errors.md runbook for detailed RAG troubleshooting
```

**Resolution time**: 5-15 minutes

**Related**: See [RAG Errors](./rag-errors.md) for detailed RAG troubleshooting

### Cause 2: OpenRouter API Issues (LLM Confidence Low)

**Symptoms**:
- LLM confidence < 0.50 (generation failing)
- Logs: "OpenRouter API error" or "LLM generation failed"
- Quality dashboard shows LLM dimension red
- Some responses completely missing or truncated

**Fix**:
```bash
# Option A: Verify API key
docker compose exec api printenv | grep OPENROUTER_API_KEY
# Ensure key is valid and not expired

# Option B: Check rate limits
# Visit OpenRouter dashboard: https://openrouter.ai/account
# Verify quota not exceeded

# Option C: Switch to fallback model
# Edit appsettings.json:
# "OpenRouter": {
#   "Model": "anthropic/claude-3-haiku",  # Faster, cheaper
#   "FallbackModel": "openai/gpt-3.5-turbo"
# }
docker compose restart api

# Option D: Check OpenRouter status
curl https://openrouter.ai/api/v1/status
# If provider issue, wait or switch provider
```

**Verification**:
```bash
# OpenRouter API responds
curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  https://openrouter.ai/api/v1/models

# LLM confidence improved
curl http://localhost:9090/api/v1/query?query=meepleai:quality:llm_confidence:5m
# Should return > 0.60

# No API errors in logs
# HyperDX: openrouter AND error AND @timestamp:[now-5m TO now]
```

**Resolution time**: 5-15 minutes

### Cause 3: Poor Data Quality (Low Index Quality)

**Symptoms**:
- Consistently low quality for specific games
- RAG retrieving irrelevant context (low relevance scores)
- Citations missing or incorrect
- Quality varies significantly by game (not all games affected)

**Investigation**:
```bash
# Check PDF processing quality
docker compose exec postgres psql -U meeple -d meepleai -c "
SELECT game_id, AVG(quality_score) as avg_quality
FROM pdf_documents
GROUP BY game_id
HAVING AVG(quality_score) < 0.80
ORDER BY avg_quality ASC;"

# Check vector embedding quality
# (Requires manual review of retrieval results)
```

**Fix**:
```bash
# Option A: Re-process low-quality PDFs
# 1. Identify low-quality PDFs (< 0.80 score)
# 2. Delete from database
# 3. Re-upload with better extraction settings
# Edit appsettings.json:
# "PdfProcessing": {
#   "Extractor": {
#     "Provider": "Orchestrator"  # 3-stage pipeline
#   }
# }

# Option B: Improve chunking strategy
# Edit appsettings.json:
# "Chunking": {
#   "Strategy": "Sentence",  # Better than Fixed
#   "MaxTokens": 512
# }
# Re-index all documents

# Option C: Add manual quality review
# Review low-quality game PDFs manually
# Add better source documents if available

# Option D: Tune retrieval parameters
# Edit appsettings.json:
# "Rag": {
#   "TopK": 10,  # Increased from 5
#   "MinScore": 0.65  # Reduced from 0.70 (more lenient)
# }
docker compose restart api
```

**Verification**:
```bash
# Quality scores improved for affected games
# Test queries for previously low-quality games

# RAG confidence improved
curl http://localhost:9090/api/v1/query?query=meepleai:quality:rag_confidence:5m
# Should return > 0.60

# Overall quality improved
curl http://localhost:9090/api/v1/query?query=meepleai:quality:overall_confidence:5m
# Should return > 0.70
```

**Prevention**:
- Validate PDF quality before indexing (quality gate)
- Monitor per-game quality metrics
- Regular quality audits (quarterly)
- User feedback loop for quality issues

**Resolution time**: 30 minutes to several hours (re-indexing time)

### Cause 4: Prompt Quality Issues

**Symptoms**:
- LLM confidence low but RAG confidence acceptable
- LLM responses don't follow expected format
- Missing citations even when RAG provides good context
- Responses verbose or off-topic

**Investigation**:
```bash
# Review prompt templates
cat apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Prompts/*.txt

# Check recent prompt changes
git log --oneline --since="1 week ago" -- "**/Prompts/**"

# Review low-quality responses in HyperDX
# HyperDX: quality_score.llm_confidence < 0.50
# Compare prompt vs response quality
```

**Fix**:
```bash
# Option A: Revert recent prompt changes
git log --oneline --since="1 week ago" -- "**/Prompts/**"
git checkout <commit-sha> -- "**/Prompts/**"
git commit -m "revert: rollback prompt changes causing quality drop"
git push origin main

# Option B: Improve prompt engineering
# 1. Add clearer instructions for citation format
# 2. Add examples in prompt (few-shot learning)
# 3. Adjust temperature (lower = more deterministic)
# Edit prompt templates, deploy

# Option C: A/B test prompt variants
# Implement prompt experimentation framework
# Test multiple prompts, choose best performer
```

**Verification**:
```bash
# LLM confidence improved
curl http://localhost:9090/api/v1/query?query=meepleai:quality:llm_confidence:5m

# Test responses follow format
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"How do I win?","gameId":1}' | jq '.citations'
# Should have citations array

# Overall quality improved
curl http://localhost:9090/api/v1/query?query=meepleai:quality:overall_confidence:5m
```

**Prevention**:
- Version control prompt templates (track changes)
- A/B test prompt changes before deploying
- Monitor quality after prompt updates
- Document prompt engineering best practices

**Resolution time**: 15-60 minutes

### Cause 5: Quality Threshold Misconfigured

**Symptoms**:
- Quality metrics seem reasonable (0.65-0.70) but alerts firing
- Users not reporting quality issues
- Alert threshold may be too strict
- Quality stable but below arbitrary threshold

**Investigation**:
```bash
# Check current thresholds
cat apps/api/src/Api/appsettings.json | grep -A 10 "QualityThresholds"

# Check actual quality distribution
# HyperDX: quality_score is not null
# Group by: quality_tier (high, medium, low)
# Analyze distribution: is 0.65 actually acceptable?

# Review user feedback
# Check support tickets, user ratings
# Is quality actually poor or just below threshold?
```

**Fix**:
```bash
# Option A: Adjust quality thresholds (if appropriate)
# Edit appsettings.json:
# "Quality": {
#   "MinOverallConfidence": 0.60,  # Reduced from 0.70
#   "MinRagConfidence": 0.50,  # Reduced from 0.60
#   "MinLlmConfidence": 0.50   # Reduced from 0.60
# }
docker compose restart api

# Option B: Adjust alert thresholds in Prometheus
# Edit infra/prometheus/alerts/quality-metrics.yml:
# - alert: LowOverallConfidence
#   expr: meepleai:quality:overall_confidence:5m < 0.55  # Reduced from 0.60
docker compose restart prometheus

# Option C: Implement tiered quality system
# High: >0.80, Medium: 0.60-0.80, Low: <0.60
# Adjust alerts based on business requirements
```

**Verification**:
```bash
# Alerts stop firing (if thresholds adjusted)
curl http://localhost:9093 | jq '.data.alerts[]'

# Quality metrics still monitored
# Dashboard continues tracking scores

# Quality remains acceptable for users
# Monitor user feedback, support tickets
```

**Prevention**:
- Establish quality baselines from user feedback
- Regular quality calibration (quarterly review)
- Balance strictness with user satisfaction
- Document quality tier definitions

**Resolution time**: 10-30 minutes

## Mitigation Steps

### Immediate (< 5 minutes)

1. **Check which dimension failing**:
   ```bash
   # Quick check all dimensions
   curl http://localhost:9090/api/v1/query?query=meepleai:quality:rag_confidence:5m
   curl http://localhost:9090/api/v1/query?query=meepleai:quality:llm_confidence:5m
   curl http://localhost:9090/api/v1/query?query=meepleai:quality:overall_confidence:5m
   ```

2. **Silence alert** (if actively working):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=LowOverallConfidence, duration=1h
   Comment: "Investigating RAG component, Qdrant restart in progress"
   ```

3. **Notify team**:
   ```
   #incidents: "🚨 LowOverallConfidence alert - avg 0.55 - investigating RAG"
   ```

### Short-term (< 30 minutes CRITICAL, < 2 hours WARNING)

1. **Identify root cause** (use investigation steps 1-7 above)

2. **Apply fix** (use appropriate fix based on failing dimension):
   - RAG low → Check Qdrant, embeddings, retrieval config
   - LLM low → Check OpenRouter, prompts, model selection
   - Citation low → Check extraction, prompt format, validation rules

3. **Verify fix**:
   - Quality scores return to acceptable range (>0.70 overall)
   - Test multiple queries (various games, topics)
   - Check quality metrics dashboard shows improvement

4. **Update incident channel**:
   - Post resolution: "✅ Fixed by restarting Qdrant - quality now 0.75"
   - Or post ETA: "⏳ Re-indexing low-quality PDFs - ETA 1 hour"

### Medium-term (< 4 hours)

1. **Monitor for recurrence**:
   - Watch Quality Metrics dashboard for 1-2 hours
   - Test various query types (simple, complex, edge cases)
   - Ensure quality stable (not degrading again)

2. **Root cause analysis**:
   - Why did quality drop? (data, config, code, external)
   - Is fix permanent or temporary? (restart vs code fix)
   - Pattern in failures? (specific games, query types)

3. **Create follow-up tasks**:
   - GitHub issue for permanent fix (if workaround applied)
   - Quality improvement initiatives (better data, prompts)
   - Monitoring improvements (add per-game quality tracking)

## Escalation

### When to Escalate

Escalate if:
- ✅ Quality continues degrading despite fixes (>1 hour)
- ✅ Multiple dimensions failing simultaneously (systemic issue)
- ✅ External dependency down (OpenRouter, provider issue)
- ✅ Requires AI/ML expertise (prompt engineering, model selection)
- ✅ Data quality issues requiring manual review

### Escalation Contacts

**AI/ML team**:
- #ai-engineering Slack channel
- ML engineer (for quality, embeddings, prompts)
- AI lead (for strategic decisions)

**Backend team**:
- #engineering Slack channel
- Backend lead (for RAG pipeline, integration)

**Emergency contacts**:
- AI Lead: [name] - [phone]
- Team Lead: [name] - [phone]

## Prevention

### Monitoring

1. **Quality metrics** (comprehensive):
   ```promql
   # Overall confidence (target >0.75)
   avg(meepleai_quality_score{dimension="overall_confidence"}) > 0.75

   # RAG confidence (target >0.70)
   avg(meepleai_quality_score{dimension="rag_confidence"}) > 0.70

   # LLM confidence (target >0.70)
   avg(meepleai_quality_score{dimension="llm_confidence"}) > 0.70

   # Citation quality (target >0.80)
   avg(meepleai_quality_score{dimension="citation_quality"}) > 0.80

   # Low quality rate (target <10%)
   meepleai:quality:low_quality_rate:5m < 10
   ```

2. **Per-game quality tracking**:
   - Monitor quality by game_id
   - Alert on individual game quality drop
   - Identify data quality issues early

3. **Quality trend alerts**:
   - Quality decreasing >10% over 24 hours
   - Indicates gradual degradation

### Configuration

1. **Quality thresholds** (appsettings.json):
   ```json
   "Quality": {
     "MinOverallConfidence": 0.70,
     "MinRagConfidence": 0.60,
     "MinLlmConfidence": 0.60,
     "MinCitationQuality": 0.70,
     "MaxLowQualityRate": 0.15
   }
   ```

2. **RAG optimization** (appsettings.json):
   ```json
   "Rag": {
     "TopK": 10,
     "MinRelevanceScore": 0.70,
     "QueryExpansion": {
       "Enabled": true,
       "MaxExpansions": 3
     },
     "Reranking": {
       "Enabled": true
     }
   }
   ```

3. **LLM optimization** (appsettings.json):
   ```json
   "OpenRouter": {
     "Model": "anthropic/claude-3-sonnet",
     "Temperature": 0.1,
     "MaxTokens": 1024,
     "TopP": 0.9
   }
   ```

### Quality Assurance

1. **Automated testing**:
   - Benchmark dataset (100 Q&A pairs)
   - Weekly quality regression tests
   - Alert on quality degradation >10%

2. **Manual review**:
   - Sample 10 responses daily
   - Review low-quality responses
   - Identify improvement opportunities

3. **User feedback**:
   - Implement response rating (thumbs up/down)
   - Collect user feedback on quality
   - Correlate ratings with quality scores

## Testing This Runbook

**Simulate low quality**:
```bash
# Option A: Stop Qdrant (causes RAG confidence drop)
docker compose stop qdrant
# Wait for RAG requests (will have low RAG confidence)
# Wait 30 minutes for LowRagConfidence alert

# Option B: Use invalid OpenRouter key (causes LLM failures)
docker compose up -d -e OPENROUTER_API_KEY=invalid_key api
# Test chat endpoint, will have low LLM confidence
```

**Expected behavior**:
- Quality alerts fire after threshold duration
- Dashboard shows confidence scores drop
- Alerts auto-resolve when dependencies restored

## Related Runbooks

- [RAG Errors](./rag-errors.md): RAG-specific troubleshooting
- [Dependency Down](./dependency-down.md): Qdrant outage scenarios
- [Quality Metrics Unavailable](./quality-metrics-unavailable.md): Quality scoring pipeline failure

## Related Dashboards

- [Quality Metrics](http://localhost:3001/d/quality-metrics): Primary quality monitoring
- [AI/RAG Operations](http://localhost:3001/d/ai-rag-operations): RAG pipeline health
- [Error Monitoring](http://localhost:3001/d/meepleai-error-monitoring): Quality-related errors

## Changelog

- **2025-12-08**: Rewritten for uniform template compliance (Issue #706)
- **2025-11-XX**: Initial version
