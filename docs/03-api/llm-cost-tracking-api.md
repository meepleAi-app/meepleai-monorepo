# LLM Cost Tracking API

**Issue**: #960 (BGAI-018)
**Status**: ✅ Production Ready
**Version**: 1.0
**Updated**: 2025-11-12

---

## Overview

Comprehensive LLM cost tracking system providing:
- Real-time cost calculation for every LLM request
- Per-user and per-tier cost attribution
- Historical cost analytics and reporting
- Multi-threshold alerting ($100/day, $500/week, $3000/month)

---

## Architecture

```
LLM Request → HybridLlmService
              ├─ LlmCostCalculator (calculate cost)
              ├─ LlmClient (execute request)
              └─ LlmCostLogRepository (persist cost - fire & forget)
                     ↓
              llm_cost_logs table
                     ↓
              Cost Analytics & Alerts
```

---

## Endpoints

### 1. GET /admin/llm-costs/report

**Purpose**: Comprehensive cost report for date range

**Auth**: Admin session required

**Query Parameters**:
- `startDate` (optional): Start date (YYYY-MM-DD). Default: 30 days ago
- `endDate` (optional): End date (YYYY-MM-DD). Default: today
- `userId` (optional): Filter by specific user UUID

**Response** (200 OK):
```json
{
  "startDate": "2025-11-01",
  "endDate": "2025-11-12",
  "totalCost": 12.456789,
  "costsByProvider": {
    "OpenRouter": 10.123456,
    "Ollama": 0.0
  },
  "costsByRole": {
    "Anonymous": 2.345678,
    "User": 3.456789,
    "Editor": 4.567890,
    "Admin": 2.086432
  },
  "dailyCost": 1.234567,
  "exceedsThreshold": false,
  "thresholdAmount": 100.0
}
```

**Example**:
```bash
# Last 30 days (default)
curl -H "Cookie: session_id=..." http://localhost:8080/admin/llm-costs/report

# Specific date range
curl -H "Cookie: session_id=..." \
  "http://localhost:8080/admin/llm-costs/report?startDate=2025-11-01&endDate=2025-11-12"

# User-specific costs
curl -H "Cookie: session_id=..." \
  "http://localhost:8080/admin/llm-costs/report?userId=550e8400-e29b-41d4-a716-446655440000"
```

---

### 2. GET /admin/llm-costs/daily

**Purpose**: Daily cost monitoring with threshold status

**Auth**: Admin session required

**Query Parameters**:
- `date` (optional): Target date (YYYY-MM-DD). Default: today

**Response** (200 OK):
```json
{
  "date": "2025-11-12",
  "totalCost": 45.678901,
  "exceedsThreshold": false,
  "threshold": 100.0,
  "costsByProvider": {
    "OpenRouter": 45.678901,
    "Ollama": 0.0
  },
  "costsByRole": {
    "Anonymous": 10.123456,
    "User": 15.234567,
    "Editor": 12.345678,
    "Admin": 7.975200
  }
}
```

**Example**:
```bash
# Today (default)
curl -H "Cookie: session_id=..." http://localhost:8080/admin/llm-costs/daily

# Specific date
curl -H "Cookie: session_id=..." \
  "http://localhost:8080/admin/llm-costs/daily?date=2025-11-10"
```

---

### 3. POST /admin/llm-costs/check-alerts

**Purpose**: Manual trigger for cost threshold checks

**Auth**: Admin session required

**Body**: None

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Cost threshold checks completed"
}
```

**Behavior**:
- Checks daily cost vs $100 threshold
- Checks weekly cost vs $500 threshold
- Projects monthly cost vs $3000 budget
- Sends alerts via AlertingService if thresholds exceeded

**Example**:
```bash
curl -X POST \
  -H "Cookie: session_id=..." \
  http://localhost:8080/admin/llm-costs/check-alerts
```

---

## Cost Calculation

### Pricing (Per 1M Tokens)

| Model | Provider | Input Cost | Output Cost |
|-------|----------|------------|-------------|
| **openai/gpt-4o-mini** | OpenRouter | $0.15 | $0.60 |
| **anthropic/claude-3.5-haiku** | OpenRouter | $0.80 | $4.00 |
| **anthropic/claude-3.5-sonnet** | OpenRouter | $3.00 | $15.00 |
| **anthropic/claude-3-opus** | OpenRouter | $15.00 | $75.00 |
| **deepseek/deepseek-chat** | OpenRouter | $0.27 | $1.10 |
| **meta-llama/llama-3.3-70b:free** | OpenRouter | $0 | $0 |
| **meta-llama/llama-3.1-70b:free** | OpenRouter | $0 | $0 |
| **llama3:8b** | Ollama | $0 | $0 |
| **llama3:70b** | Ollama | $0 | $0 |
| **mistral** | Ollama | $0 | $0 |

### Formula
```
InputCost = (PromptTokens / 1,000,000) × InputCostPer1M
OutputCost = (CompletionTokens / 1,000,000) × OutputCostPer1M
TotalCost = InputCost + OutputCost
```

### Precision
- **6 decimal places** (micro-dollar precision: $0.000001)
- Database storage: `DECIMAL(18,6)`

---

## Cost Attribution

### By User
- **Authenticated Users**: Tracked by `user_id` (UUID)
- **Anonymous Users**: `user_id = NULL`, `user_role = "Anonymous"`

### By Tier
- **Anonymous**: 80% free tier (Llama 3.3 70B free), 20% GPT-4o-mini
- **User**: Same as Anonymous
- **Editor**: 50% local Ollama, 50% GPT-4o-mini
- **Admin**: 20% local Ollama, 80% Claude Haiku

---

## Alert Thresholds

| Period | Threshold | Action |
|--------|-----------|--------|
| **Daily** | >$100 | Send warning alert with provider breakdown |
| **Weekly** | >$500 | Send warning alert with 7-day summary |
| **Monthly** | >$3000 projected | Send warning with current spend + projection |

**Alert Channels**: Email, Slack, PagerDuty (configured via AlertingService)

---

## Database Schema

### llm_cost_logs Table

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| user_id | UUID | User (nullable for anonymous) |
| user_role | VARCHAR(50) | User tier (Anonymous, User, Editor, Admin) |
| model_id | VARCHAR(100) | Model identifier |
| provider | VARCHAR(50) | Provider (OpenRouter, Ollama) |
| prompt_tokens | INT | Input token count |
| completion_tokens | INT | Output token count |
| total_tokens | INT | Total tokens |
| input_cost_usd | DECIMAL(18,6) | Input cost in USD |
| output_cost_usd | DECIMAL(18,6) | Output cost in USD |
| total_cost_usd | DECIMAL(18,6) | Total cost in USD |
| endpoint | VARCHAR(100) | Endpoint (completion, chat, qa, explain) |
| success | BOOLEAN | Request success status |
| error_message | VARCHAR(500) | Error message if failed |
| latency_ms | INT | Request latency |
| ip_address | VARCHAR(45) | Client IP (nullable) |
| user_agent | VARCHAR(500) | Client user agent (nullable) |
| created_at | TIMESTAMPTZ | Request timestamp |
| request_date | DATE | Request date (for fast aggregation) |

**Indexes**:
- `ix_llm_cost_logs_user_id` (user_id)
- `ix_llm_cost_logs_request_date` (request_date)
- `ix_llm_cost_logs_provider_date` (provider, request_date)
- `ix_llm_cost_logs_role_date` (user_role, request_date)
- `ix_llm_cost_logs_created_at` (created_at)

---

## Cost Examples

### Typical Request Costs

```
# GPT-4o-mini (1000 prompt, 500 completion)
Input:  1000 / 1M × $0.15  = $0.000150
Output:  500 / 1M × $0.60  = $0.000300
Total: $0.000450

# Claude 3.5 Haiku (10000 prompt, 2000 completion)
Input:  10000 / 1M × $0.80 = $0.008000
Output:  2000 / 1M × $4.00 = $0.008000
Total: $0.016000

# Llama 3.3 70B Free Tier (any usage)
Total: $0.000000

# Ollama llama3:8b (self-hosted, any usage)
Total: $0.000000
```

### Monthly Cost Projection (10K MAU, 80/20 split)

| Scenario | Free Tier % | Paid % | Monthly Cost |
|----------|-------------|--------|--------------|
| **Target (ADR-004)** | 80% | 20% | ~$3,000 |
| **OpenRouter Only** | 0% | 100% | ~$15,000 |
| **Ollama Only** | 100% | 0% | $0 (but 33% accuracy) |

---

## Usage Patterns

### Get Monthly Cost Report
```bash
#!/bin/bash
# Get current month costs
MONTH_START=$(date +"%Y-%m-01")
TODAY=$(date +"%Y-%m-%d")

curl -H "Cookie: session_id=admin-session" \
  "http://localhost:8080/admin/llm-costs/report?startDate=$MONTH_START&endDate=$TODAY" \
  | jq '.totalCost'
```

### Check Daily Threshold
```bash
#!/bin/bash
# Check if today exceeded $100 threshold
curl -H "Cookie: session_id=admin-session" \
  "http://localhost:8080/admin/llm-costs/daily" \
  | jq '.exceedsThreshold, .totalCost'
```

### Trigger Manual Alert Check
```bash
#!/bin/bash
# Run threshold checks manually
curl -X POST \
  -H "Cookie: session_id=admin-session" \
  http://localhost:8080/admin/llm-costs/check-alerts
```

---

## Monitoring & Analytics

### Key Metrics
- **Total Cost**: Sum of all LLM requests
- **Cost by Provider**: OpenRouter vs Ollama breakdown
- **Cost by Tier**: Anonymous vs User vs Editor vs Admin
- **Daily Average**: Total cost / days in period
- **Projected Monthly**: Daily avg × days in month

### Alert Triggers
1. **Daily >$100**: Immediate warning to admin channels
2. **Weekly >$500**: Weekly review alert
3. **Monthly projection >$3000**: Budget warning alert

### Recommended Monitoring
- **Daily**: Check dashboard for cost trends
- **Weekly**: Review cost by provider/tier breakdown
- **Monthly**: Validate projection vs budget

---

## Integration

### Auto-Tracking
- **All LLM requests** automatically logged (fire-and-forget)
- **Zero performance impact**: Async background logging
- **Non-blocking**: Request returns immediately, logging happens separately

### LlmCompletionResult
Every LLM completion now includes:
```csharp
result.Cost.TotalCost     // Decimal
result.Cost.InputCost     // Decimal
result.Cost.OutputCost    // Decimal
result.Cost.ModelId       // string
result.Cost.Provider      // string
```

---

## Security & Privacy

- **Admin Only**: All cost endpoints require admin authentication
- **Data Retention**: No automatic cleanup (manual archiving recommended)
- **PII Handling**: IP address and user agent stored for analytics
- **Cost Data Sensitivity**: Treat as business confidential

---

## Performance

- **Non-blocking**: Cost logging doesn't slow LLM responses
- **Indexed Queries**: Optimized for date range and aggregation
- **Efficient Storage**: ~100 bytes per log entry
- **Scalability**: Handles 10K+ requests/day easily

---

## Testing

### Unit Tests (12)
- `LlmCostCalculatorTests`: All models, edge cases, precision

### Integration Tests (7)
- `LlmCostLogRepositoryTests`: Persistence, grouping, filtering

### Coverage
- **100%** for cost calculation logic
- **100%** for repository operations

---

## Troubleshooting

| Issue | Check | Fix |
|-------|-------|-----|
| Costs not logging | Check DI registration | Verify ILlmCostLogRepository registered |
| Incorrect costs | Verify model pricing | Update LlmCostCalculator pricing database |
| No alerts | Check threshold values | Verify daily cost >$100 |
| Missing attribution | Check user context | Ensure User passed to HybridLlmService |

---

## Related Documentation

- [ADR-004: Hybrid LLM Architecture](../architecture/adr-004-hybrid-llm-architecture.md)
- [Issue #960: BGAI-018](https://github.com/DegrassiAaron/meepleai-monorepo/issues/960)
- [OpenRouter Models Reference](../wiki/openrouter-models-reference.wiki)

---

**Version**: 1.0
**Last Updated**: 2025-11-12
**Owner**: Engineering Lead
