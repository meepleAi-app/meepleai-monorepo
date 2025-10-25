# OPS-07: Alerting System - Implementation Specification

**Issue**: #425 OPS-07 - Alerting system for critical errors and anomalies
**Status**: Ready for Implementation
**Effort**: L (2 weeks / 80h)
**Priority**: 🔴 HIGH (CRITICAL OPS)
**Dependencies**: OPS-02 (OpenTelemetry metrics), ADMIN-02 (Analytics)

---

## Executive Summary

Implement proactive alerting system integrating with Prometheus metrics (OPS-02) to notify operations team of critical errors, performance anomalies, and system health issues via Email, Slack, and PagerDuty.

**Complexity**: HIGH (multi-channel alerting, Prometheus integration, throttling)
**Risk**: MEDIUM (false positives, alert fatigue)
**Impact**: HIGH (reduced incident response time, proactive monitoring)

---

## Optimal Agent/Tool Selection

### Phase 1: Architecture Design (Days 1-2)
**LEAD**: devops-architect
**Support**: backend-architect, system-architect
**MCP**: Sequential (alerting architecture), Context7 (Prometheus patterns)

**Why devops-architect LEADS**:
- Prometheus/Grafana expertise
- Alert rule design and tuning
- Multi-channel notification patterns
- Infrastructure monitoring experience

### Phase 2: Backend Service (Days 3-6)
**Primary**: backend-architect, devops-architect
**MCP**: Serena (pattern discovery), Sequential
**Tasks**: AlertingService, alert channels, throttling logic

### Phase 3: Prometheus Integration (Days 7-9)
**LEAD**: devops-architect
**Tasks**: AlertManager config, alert rules, Grafana dashboards

### Phase 4: Testing (Days 10-12)
**Primary**: quality-engineer, devops-architect
**MCP**: Sequential (test strategy)
**Tasks**: 50+ tests (unit, integration, alert simulation)

### Phase 5: Documentation (Days 13-14)
**Primary**: technical-writer, devops-architect
**Tasks**: Runbooks, alert configuration guide, troubleshooting

---

## Backend Implementation

### AlertingService (~300 lines)
**File**: `Services/AlertingService.cs`

**Methods**:
- `SendAlertAsync(type, severity, message, metadata)`
- `ResolveAlertAsync(alertId)`
- `GetActiveAlertsAsync()`
- `GetAlertHistoryAsync(fromDate, toDate)`
- `CheckThrottleAsync(alertType)` → bool (1/hour limit)

**Alert Channels** (Strategy pattern):
- EmailAlertChannel
- SlackAlertChannel
- PagerDutyAlertChannel

### Database Schema
```sql
CREATE TABLE alerts (
    id UUID PRIMARY KEY,
    alert_type VARCHAR(50),
    severity VARCHAR(20),
    message TEXT,
    metadata JSONB,
    triggered_at TIMESTAMPTZ,
    resolved_at TIMESTAMPTZ,
    is_active BOOLEAN,
    channel_sent JSONB -- {"email": true, "slack": true, "pagerduty": false}
);

CREATE INDEX idx_alerts_active ON alerts(is_active) WHERE is_active = TRUE;
CREATE INDEX idx_alerts_type_time ON alerts(alert_type, triggered_at);
```

### Prometheus AlertManager
**File**: `infra/prometheus/alertmanager.yml`

```yaml
route:
  receiver: 'meepleai-webhook'
  group_by: ['alertname']
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 1h

receivers:
  - name: 'meepleai-webhook'
    webhook_configs:
      - url: 'http://api:8080/api/v1/alerts/prometheus'
        send_resolved: true
```

**File**: `infra/prometheus/rules/meepleai-alerts.yml`

```yaml
groups:
  - name: meepleai_critical
    interval: 30s
    rules:
      - alert: HighErrorRate
        expr: rate(meepleai_rag_errors_total[5m]) > 10
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High AI request error rate"
          description: "{{ $value }} errors/sec in last 5 minutes"

      - alert: HighLatency
        expr: histogram_quantile(0.95, meepleai_rag_request_duration_bucket) > 2000
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High RAG request latency"

      - alert: DatabaseDown
        expr: up{job="postgres"} == 0
        for: 1m
        labels:
          severity: critical

      - alert: QdrantDown
        expr: up{job="qdrant"} == 0
        for: 1m
        labels:
          severity: critical
```

### API Endpoints
```csharp
// POST /api/v1/alerts/prometheus - Webhook from AlertManager
v1Api.MapPost("/alerts/prometheus", async (
    PrometheusAlertWebhook webhook,
    IAlertingService alertingService) =>
{
    foreach (var alert in webhook.Alerts)
    {
        if (alert.Status == "firing")
        {
            await alertingService.SendAlertAsync(
                alertType: alert.Labels["alertname"],
                severity: alert.Labels["severity"],
                message: alert.Annotations["summary"],
                metadata: alert
            );
        }
        else if (alert.Status == "resolved")
        {
            await alertingService.ResolveAlertAsync(alert.Labels["alertname"]);
        }
    }
    return Results.Ok();
});

// GET /api/v1/admin/alerts - List active alerts
v1Api.MapGet("/admin/alerts", async (
    IAlertingService alertingService,
    bool? activeOnly = true) =>
{
    var alerts = await alertingService.GetActiveAlertsAsync();
    return Results.Ok(alerts);
})
.RequireAuthorization(policy => policy.RequireRole("Admin"));
```

---

## Testing Strategy (50+ tests)

### Unit Tests (25 tests)
- Alert throttling logic (1/hour enforcement)
- Multi-channel sending (email, slack, pagerduty)
- Alert resolution detection
- Severity level handling

### Integration Tests (15 tests)
- Prometheus webhook endpoint
- Alert database storage
- Email sending (mocked SMTP)
- Slack webhook (mocked)
- PagerDuty API (mocked)

### E2E Tests (10 tests)
- Simulate database down → Alert triggered → Email/Slack sent
- Simulate high error rate → Alert fired → Resolved when normal
- Alert throttling: 2 alerts within 1h → Only 1 sent

---

## Configuration

### appsettings.json
```json
{
  "Alerting": {
    "Enabled": true,
    "ThrottleMinutes": 60,
    "Email": {
      "Enabled": true,
      "SmtpHost": "${SMTP_HOST}",
      "SmtpPort": 587,
      "From": "alerts@meepleai.dev",
      "To": ["ops@meepleai.dev"],
      "UseTls": true,
      "Username": "${SMTP_USERNAME}",
      "Password": "${SMTP_PASSWORD}"
    },
    "Slack": {
      "Enabled": true,
      "WebhookUrl": "${SLACK_WEBHOOK_URL}",
      "Channel": "#alerts"
    },
    "PagerDuty": {
      "Enabled": false,
      "IntegrationKey": "${PAGERDUTY_INTEGRATION_KEY}"
    }
  }
}
```

---

## Effort: 80h (2 weeks)

| Phase | Hours | Agent |
|-------|-------|-------|
| Architecture | 12h | devops-architect |
| Backend Service | 24h | backend-architect + devops |
| Prometheus Config | 16h | devops-architect |
| Testing | 20h | quality-engineer |
| Documentation | 8h | technical-writer |

---

## Dependencies
- ✅ OPS-02: OpenTelemetry + Prometheus (metrics source)
- ✅ ADMIN-02: Analytics (alert dashboard)
- 📋 Prometheus AlertManager (need to add to docker-compose)

---

**Status**: ✅ SPECIFICATION COMPLETE
**Effort**: 80h (2 weeks)
**Agent LEAD**: devops-architect

🤖 Generated with Claude Code
