# MeepleAI Operational Runbooks

Comprehensive troubleshooting guides for responding to production alerts and incidents.

## Quick Reference

**Emergency? Start here:**
- 🔴 **Critical alerts** → Respond in < 5 minutes → See Critical Runbooks below
- 🟡 **Warning alerts** → Respond in < 30 minutes → See Warning Runbooks below
- 🔍 **Not sure?** → Check [Alert to Runbook Mapping](#alert-to-runbook-mapping)

## Runbook Index

### Critical Runbooks (< 5 min response)

| Runbook | Alert(s) | Threshold | What It Means |
|---------|----------|-----------|---------------|
| [High Error Rate](./high-error-rate.md) | `HighErrorRate` | > 1 error/sec for 2 min | API experiencing major failures, users seeing 500 errors |
| [Error Spike](./error-spike.md) | `ErrorSpike` | > 3x baseline for 2 min | Sudden error increase, likely deployment issue |
| [Dependency Down](./dependency-down.md) | `DatabaseDown`<br>`RedisDown`<br>`QdrantDown` | Service unreachable 1-2 min | Critical dependency offline, service degraded/down |
| [Quality Metrics Unavailable](./quality-metrics-unavailable.md) | `QualityMetricsUnavailable` | No metrics for 15 min | Quality scoring broken or no AI requests |

### Warning Runbooks (< 30 min response)

| Runbook | Alert(s) | Threshold | What It Means |
|---------|----------|-----------|---------------|
| [RAG Errors](./rag-errors.md) | `RagErrorsDetected` | > 0.5 errors/sec for 3 min | RAG pipeline failing, Q&A degraded |
| [Slow Performance](./slow-performance.md) | `SlowResponseTime` | P95 > 5s for 5 min | API responses slow, poor user experience |
| [AI Quality Low](./ai-quality-low.md) | `LowOverallConfidence`<br>`HighLowQualityRate`<br>`LowRagConfidence`<br>`LowLlmConfidence`<br>`DegradedOverallConfidence`<br>`ElevatedLowQualityRate` | Various quality thresholds | AI response quality degraded, poor answers |
| [High Memory Usage](./high-memory-usage.md) | `HighMemoryUsage` | > 80% for 5 min | Memory pressure, risk of OOMKill |

## Alert to Runbook Mapping

### API Performance Alerts
- `HighErrorRate` → [high-error-rate.md](./high-error-rate.md)
- `ErrorSpike` → [error-spike.md](./error-spike.md)
- `SlowResponseTime` → [slow-performance.md](./slow-performance.md)

### Dependency Health Alerts
- `DatabaseDown` → [dependency-down.md](./dependency-down.md)
- `RedisDown` → [dependency-down.md](./dependency-down.md)
- `QdrantDown` → [dependency-down.md](./dependency-down.md)

### AI/RAG Quality Alerts
- `RagErrorsDetected` → [rag-errors.md](./rag-errors.md)
- `LowOverallConfidence` → [ai-quality-low.md](./ai-quality-low.md)
- `HighLowQualityRate` → [ai-quality-low.md](./ai-quality-low.md)
- `LowRagConfidence` → [ai-quality-low.md](./ai-quality-low.md)
- `LowLlmConfidence` → [ai-quality-low.md](./ai-quality-low.md)
- `DegradedOverallConfidence` → [ai-quality-low.md](./ai-quality-low.md)
- `ElevatedLowQualityRate` → [ai-quality-low.md](./ai-quality-low.md)
- `QualityMetricsUnavailable` → [quality-metrics-unavailable.md](./quality-metrics-unavailable.md)

### Infrastructure Alerts
- `HighMemoryUsage` → [high-memory-usage.md](./high-memory-usage.md)

## Severity Matrix

### By Service Component

| Component | Critical Alerts | Warning Alerts |
|-----------|----------------|----------------|
| **API** | HighErrorRate, ErrorSpike | SlowResponseTime |
| **Database** | DatabaseDown | _(queries in slow-performance)_ |
| **Cache** | RedisDown | _(hit rate in slow-performance)_ |
| **Vector DB** | QdrantDown | RagErrorsDetected |
| **AI/RAG** | LowRagConfidence, LowLlmConfidence | DegradedOverallConfidence |
| **Quality** | LowOverallConfidence, HighLowQualityRate, QualityMetricsUnavailable | ElevatedLowQualityRate |
| **Infrastructure** | _(OOMKill in dependency-down)_ | HighMemoryUsage |

### By Impact

| Impact Level | Runbooks | Response Time |
|--------------|----------|---------------|
| **Complete Outage** | [Dependency Down (Postgres)](./dependency-down.md) | < 5 min |
| **Major Degradation** | [High Error Rate](./high-error-rate.md), [Error Spike](./error-spike.md) | < 5 min |
| **Feature Failure** | [Dependency Down (Qdrant)](./dependency-down.md), [RAG Errors](./rag-errors.md) | < 15 min |
| **Performance Issues** | [Slow Performance](./slow-performance.md), [High Memory](./high-memory-usage.md) | < 30 min |
| **Quality Issues** | [AI Quality Low](./ai-quality-low.md), [Quality Metrics Unavailable](./quality-metrics-unavailable.md) | < 30 min (critical) <br> < 2 hours (warning) |

## Common Investigation Patterns

### Quick Health Check (2 minutes)

```bash
# Check all service status
docker compose ps

# Check API health
curl http://localhost:8080/health | jq '.'

# Check dependencies
curl http://localhost:6333/healthz  # Qdrant
export REDIS_PASS=$(cat infra/secrets/redis-password.txt)
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning ping  # Redis
docker compose exec postgres psql -U meeple -d meepleai -c "SELECT 1;"  # Postgres

# Check dashboards
open http://localhost:3001/d/meepleai-error-monitoring
open http://localhost:9093  # Alertmanager
```

### Standard Troubleshooting Flow

1. **Verify alert** (is it really firing? false positive?)
2. **Identify scope** (which component, endpoints, users affected?)
3. **Check recent changes** (deployment, config, infrastructure)
4. **Check dependencies** (Postgres, Redis, Qdrant health)
5. **Analyze logs** (HyperDX error logs, correlation IDs)
6. **Check traces** (HyperDX distributed tracing, slow operations)
7. **Check resources** (CPU, memory, disk, network)

### Emergency Quick Fixes

```bash
# Restart API (clears most transient issues)
docker compose restart api

# Restart all services (nuclear option)
docker compose down && docker compose up -d

# Check logs for errors
docker compose logs api --tail 100 | grep -i error
docker compose logs postgres --tail 50
docker compose logs redis --tail 50
docker compose logs qdrant --tail 50
```

## Dashboard URLs

| Dashboard | URL | Purpose |
|-----------|-----|---------|
| **Error Monitoring** | http://localhost:3001/d/meepleai-error-monitoring | Error rates, dependency health |
| **API Performance** | http://localhost:3001/d/api-performance | Response times, throughput |
| **Quality Metrics** | http://localhost:3001/d/quality-metrics | AI quality scores, confidence |
| **AI/RAG Operations** | http://localhost:3001/d/ai-rag-operations | RAG pipeline health |
| **Infrastructure** | http://localhost:3001/d/infrastructure | CPU, memory, disk usage |
| **Prometheus** | http://localhost:9090 | Metrics and alerts |
| **Alertmanager** | http://localhost:9093 | Active alerts, silences |
| **HyperDX** | http://localhost:8180 | Logs and traces |

## Prometheus Alert Queries

Quick reference for common Prometheus queries used in runbooks:

### Error Metrics
```promql
# Current error rate (errors/second)
meepleai:api:error_rate:5m

# Error spike ratio (current vs baseline)
meepleai:api:error_spike_ratio

# Error rate by endpoint
topk(5, sum by (http_route) (rate(meepleai_api_errors_total[5m])))
```

### Performance Metrics
```promql
# P95 response time
meepleai:api:response_time_p95:5m

# Active concurrent requests
http_server_active_requests

# Request rate
rate(http_server_request_duration_count[5m])
```

### Quality Metrics
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

### Resource Metrics
```promql
# CPU usage
rate(process_cpu_seconds_total{job="meepleai-api"}[5m]) * 100

# Memory usage
process_working_set_bytes / process_memory_limit_bytes

# Disk usage
node_filesystem_avail_bytes / node_filesystem_size_bytes
```

## Escalation Guidelines

### When to Escalate

**Time-based**:
- Cannot resolve critical alert in 15 minutes → Escalate
- Cannot resolve warning alert in 1 hour → Escalate
- Issue getting worse despite mitigation → Escalate immediately

**Impact-based**:
- Complete service outage → Escalate immediately
- Data corruption suspected → Escalate immediately
- Security incident → Escalate immediately
- Critical business endpoint affected → Escalate

**Complexity-based**:
- Root cause unknown after standard investigation → Escalate
- Fix requires expertise you don't have → Escalate
- External dependency issue (out of control) → Escalate

### Escalation Channels

| Channel | When to Use | Response Time |
|---------|-------------|---------------|
| **#incidents** | All active incidents | Real-time |
| **#engineering** | Code/architecture questions | < 30 min |
| **#ops** | Infrastructure issues | < 15 min |
| **#ai-engineering** | AI/ML/quality issues | < 1 hour |
| **On-call engineer** | Critical incidents | Immediate |
| **Team lead (phone)** | Major outage | Immediate |
| **CTO (phone)** | Business-critical emergency | Immediate |

## Post-Incident Procedures

### Required for All Incidents

1. **Document incident** (GitHub issue with `incident` label):
   - Timeline (when started, when resolved, duration)
   - Root cause (what happened and why)
   - Fix applied (temporary or permanent)
   - User impact (how many users, what functionality)

2. **Update runbook** (if new scenario):
   - Add new failure mode to Common Root Causes
   - Improve investigation steps if gaps found
   - Update resolution time estimates

3. **Create preventive tasks** (GitHub issues):
   - Fix root cause (if workaround was applied)
   - Improve monitoring (add metrics, adjust alerts)
   - Improve testing (add tests for failure scenario)

### Optional (Major Incidents >30 min or high impact)

1. **Post-mortem meeting**:
   - Schedule within 48 hours
   - Blameless discussion (focus on systems)
   - Identify systemic issues
   - Create action items

2. **Incident report** (detailed):
   - Executive summary (for stakeholders)
   - Detailed timeline
   - Root cause analysis
   - Remediation plan
   - Preventive measures

## Testing Runbooks

All runbooks include a "Testing This Runbook" section with:
- Commands to simulate alert condition
- Expected behavior (what should happen)
- Cleanup steps (return to normal)

**Recommended testing schedule**:
- **Monthly**: Test 2 runbooks (rotate through all 8 over 4 months)
- **Quarterly**: Test all critical runbooks (high-error-rate, error-spike, dependency-down)
- **Pre-production**: Test all runbooks before major releases

## Runbook Maintenance

### Review Schedule

- **Monthly**: Review 2 runbooks, update based on recent incidents
- **Quarterly**: Full audit of all runbooks, update thresholds and commands
- **After incidents**: Update runbook immediately with lessons learned
- **After architecture changes**: Update affected runbooks (new services, new tools)

### Quality Standards

All runbooks follow uniform template with:
- **Alert details**: Name, severity, threshold, response time
- **Symptoms**: Observable indicators when alert fires
- **Impact**: Effect on users, data, business, system
- **Investigation steps**: 7-step systematic troubleshooting (8-10 minutes total)
- **Common root causes**: Top 5-6 causes with symptoms, fixes, verification
- **Mitigation steps**: Immediate, short-term, medium-term actions
- **Escalation**: When to escalate, who to contact
- **Prevention**: Monitoring, configuration, code quality improvements
- **Testing**: How to simulate alert, expected behavior, cleanup
- **Related runbooks**: Cross-references to related scenarios
- **Related dashboards**: Dashboard links for investigation
- **Changelog**: Version history

### Continuous Improvement

**After each incident**:
1. Did runbook help? (what worked, what didn't)
2. Missing information? (add to runbook)
3. Incorrect information? (fix runbook)
4. New scenario? (add to Common Root Causes)

**Track runbook effectiveness**:
- Time to resolution (is runbook helping achieve target RTO?)
- Escalation rate (do runbooks reduce escalations?)
- Recurring incidents (are preventive measures working?)

## Templates

Use the [runbook template](./templates/runbook-template.md) when creating new runbooks.

**Template structure**:
- Uniform format across all runbooks
- Comprehensive investigation workflow
- Actionable mitigation steps
- Prevention strategies
- Testing procedures

## Related Documentation

| Document | Purpose |
|----------|---------|
| [General Troubleshooting](./general-troubleshooting.md) | Non-alert troubleshooting techniques |
| [RAG Evaluation Pipeline](./rag-evaluation-pipeline.md) | RAG performance benchmarking |
| [K6 Performance Troubleshooting](./k6-performance-troubleshooting.md) | Load testing issues |
| [LLM Budget Alerts](./llm-budget-alerts.md) | Cost monitoring and alerts |
| [Infrastructure Monitoring](./infrastructure-monitoring.md) | cAdvisor and node-exporter setup |
| [HyperDX Alert Configuration](./hyperdx-alert-configuration.md) | HyperDX alert setup |
| [Disaster Recovery](../deployment/disaster-recovery.md) | Backup and restore procedures |
| [Deployment Guide](../deployment-guide.md) | Deployment procedures |

## Monitoring Stack

MeepleAI uses the following tools for observability:

| Tool | Purpose | URL | Runbook Usage |
|------|---------|-----|---------------|
| **Prometheus** | Metrics collection | http://localhost:9090 | Query metrics, verify alert conditions |
| **Alertmanager** | Alert routing | http://localhost:9093 | View active alerts, create silences |
| **Grafana** | Dashboards | http://localhost:3001 | Visualize metrics, investigate trends |
| **HyperDX** | Logs & Traces | http://localhost:8180 | Search logs, analyze traces, find correlation IDs |

## Contributing to Runbooks

### When to Create New Runbook

Create a new runbook when:
- New Prometheus alert added (every alert needs a runbook)
- Recurring incident without runbook (>3 occurrences)
- Complex troubleshooting procedure (>5 steps)
- Critical system component added (new dependency, new feature)

### How to Create Runbook

1. Copy [runbook template](./templates/runbook-template.md)
2. Fill in all sections (don't skip any)
3. Test runbook (follow your own steps)
4. Have teammate review (ensure clarity)
5. Add to this README index
6. Link from alert annotations (Prometheus alerts)

### Runbook Review Checklist

Before committing new/updated runbook:
- ✅ All commands tested and work correctly
- ✅ All dashboard URLs valid (links work)
- ✅ All Prometheus queries return expected results
- ✅ Investigation steps take ≤ 10 minutes total
- ✅ Common root causes cover >80% of scenarios
- ✅ Escalation criteria clear and appropriate
- ✅ Testing section allows runbook validation
- ✅ Related runbooks linked correctly

## Best Practices

### During Incident Response

1. **Follow the runbook** (don't skip steps, even if you think you know the issue)
2. **Document actions** (take notes, useful for post-mortem)
3. **Communicate often** (update #incidents every 5-10 minutes)
4. **Silence alerts** (reduce noise while actively working)
5. **Verify fixes** (don't assume, test and measure)

### Post-Incident

1. **Update runbook immediately** (while details fresh)
2. **Document incident** (create GitHub issue within 1 hour)
3. **Create preventive tasks** (fix root cause, improve monitoring)
4. **Share learnings** (post-mortem, team meeting, slack update)

### Communication

**Good incident updates**:
- ✅ "🚨 HighErrorRate firing - error rate 2.5/sec on /api/v1/games - restarting Redis"
- ✅ "⏳ Investigating database slow queries - ETA 10 min"
- ✅ "✅ Resolved - added database index - P95 now 1.2s - monitoring"

**Poor incident updates**:
- ❌ "Looking into it"
- ❌ "Still broken"
- ❌ "Fixed" (no details on what was fixed)

## Metrics and SLAs

### Response Time Targets

| Severity | Target Response Time | Target Resolution Time |
|----------|---------------------|----------------------|
| **CRITICAL** | < 5 minutes | < 30 minutes |
| **WARNING** | < 30 minutes | < 2 hours |
| **INFO** | < 2 hours | < 8 hours |

### Service Level Objectives (SLOs)

| Metric | Target | Alert Threshold |
|--------|--------|----------------|
| **Uptime** | 99.9% (43 min downtime/month) | 99.5% |
| **Error Rate** | < 0.1% | > 1 error/sec |
| **P95 Latency** | < 3s | > 5s |
| **AI Quality** | > 75% confidence | < 70% |

## Changelog

- **2025-12-08**: Complete rewrite for uniform template compliance (Issue #706)
  - Standardized all 8 runbooks with master template
  - Added comprehensive index with severity matrix
  - Added alert-to-runbook mapping
  - Added quick reference and common patterns
- **2025-10-16**: Initial runbook collection