# Agent Lightning + OpenRouter Integration Guide

**Purpose**: Optimize MeepleAI agents using Agent Lightning with OpenRouter API - no code changes required

---

## Overview

Agent Lightning provides **two training approaches** for MeepleAI, both deploy identically to production:

| Approach | GPU Required | Cost | Time | Optimization |
|----------|--------------|------|------|--------------|
| **GPU Local** | RTX 3090+ | $0-26/train | 6-24h | GRPO (RL) + fine-tuning |
| **OpenRouter API** | No | $30-50/train | 4-6h | APO (prompt-only) |

**Key Insight**: Both approaches deploy **only optimized prompts** to MeepleAI - no code changes to `LlmService.cs` required.

---

## Architecture

### Scenario 1: GPU Local → OpenRouter Production (Recommended)

```
TRAINING (Agent Lightning - GPU)
├── vLLM local model server
├── GRPO algorithm (RL optimization)
├── Optional: Model fine-tuning
└── Export: Optimized prompts + weights

         ↓ Deploy artifacts

PRODUCTION (MeepleAI → OpenRouter)
├── LlmService.cs calls OpenRouter API
├── Uses optimized prompts from training
└── NO CODE CHANGES
```

**Pros**: Maximum quality (RL + fine-tuning), training cost only in dev, production uses OpenRouter (cheap, scalable)
**Cons**: Requires GPU (RTX 3090+ or cloud), complex setup, longer training

### Scenario 2: OpenRouter Training → OpenRouter Production

```
TRAINING (Agent Lightning + OpenRouter)
├── Calls OpenRouter API during training
├── APO algorithm (prompt optimization only)
├── No model fine-tuning
└── Export: Optimized prompts

         ↓ Deploy artifacts

PRODUCTION (MeepleAI → OpenRouter)
├── LlmService.cs calls OpenRouter API
├── Uses optimized prompts
└── SAME MODEL as training
```

**Pros**: No GPU, simple setup, fast training (4-6h), same model in train/prod
**Cons**: Prompt-only optimization (no fine-tuning), $30-50 API cost per training, latency-dependent

---

## Implementation

### Scenario 1: GPU Local Setup

```bash
# 1. Install Agent Lightning (standard quickstart)
cd ~/agent-lightning-meepleai
python3.10 -m venv venv && source venv/bin/activate
pip install torch flash-attn vllm verl agentlightning

# 2. Prepare data (see agent-lightning-quickstart.md)
python prepare_data.py

# 3. Training (GPU)
# Terminal 1: Training server
python -m agentlightning.verl \
    algorithm.adv_estimator=grpo \
    data.train_files=data/train_rag.parquet \
    actor_rollout_ref.model.path=Qwen/Qwen2.5-Coder-3B-Instruct \
    trainer.total_epochs=5

# Terminal 2: Agent workers
python simple_rag_agent.py --n-workers 8

# 4. Export optimized prompt
python export_for_meepleai.py  # Generates SQL migration

# 5. Deploy to MeepleAI
cp optimized_prompt.sql ../../meepleai-monorepo/migrations/
cd ../../meepleai-monorepo/apps/api
dotnet ef database update

# 6. Activate in Admin UI: /admin/prompts
```

### Scenario 2: OpenRouter Training

```bash
# 1. Simple setup (no GPU)
cd ~/agent-lightning-openrouter
python3.10 -m venv venv && source venv/bin/activate
pip install openai pandas agentlightning

# 2. Configure
export OPENROUTER_API_KEY="sk-or-v1-..."
MODEL="anthropic/claude-3.5-sonnet"  # Same as MeepleAI production

# 3. Prepare data (same as GPU scenario)
python prepare_data.py

# 4. Run APO training (4-6 hours)
python train_rag_with_openrouter.py --iterations 10

# Output:
# ✅ TRAINING COMPLETE
# Baseline: 0.723 | Optimized: 0.851 | Improvement: +17.7%

# 5. Deploy (identical to GPU scenario)
cp deploy_optimized_prompt.sql ../../meepleai-monorepo/migrations/
cd ../../meepleai-monorepo/apps/api
dotnet ef database update
```

**Training Script** (OpenRouter): See full script in original doc lines 212-571 - implements APO algorithm with LLM-guided prompt variations.

---

## Production Deployment (Both Scenarios)

**MeepleAI LlmService** (NO CHANGES):
```csharp
public async Task<string> GenerateCompletionAsync(string userMessage)
{
    // 1. Get prompt (may be Agent Lightning optimized)
    var systemPrompt = await _promptTemplateService.GetActivePromptAsync("rag-system-prompt");

    // 2. Call OpenRouter (unchanged)
    var response = await _httpClient.PostAsJsonAsync(
        "https://openrouter.ai/api/v1/chat/completions",
        new { model = _config["AI:Model"], messages = [...], temperature = 0.7 }
    );

    return result.Choices[0].Message.Content;
}
```

**A/B Testing**:
```csharp
var promptVersion = await _featureFlagService.IsEnabledAsync("AgentLightningPrompt")
    ? "rag-system-prompt"           // Optimized version
    : "rag-system-prompt-baseline"; // Original

var systemPrompt = await _promptTemplateService.GetActivePromptAsync(promptVersion);
```

**Dynamic Config Tracking**:
```sql
INSERT INTO system_configurations (key, value)
VALUES ('AI:ActivePromptSource', 'agent-lightning-apo'),
       ('AI:PromptImprovement', '0.177'),
       ('AI:TrainingMethod', 'openrouter-apo');
```

---

## Cost Analysis

### GPU Local Scenario

| Item | Cost | Frequency |
|------|------|-----------|
| RTX 3090 workstation | $2,000-3,000 | One-time |
| Cloud GPU (A100, 8h) | $8-26 | Per training |
| Electricity (local) | $2-5 | Per training |
| **Year 1 Total (local hardware)** | $2,000-3,000 + $100/year | - |
| **Year 1 Total (cloud GPU)** | ~$300/year | Monthly training |

### OpenRouter Scenario

| Item | Cost | Frequency |
|------|------|-----------|
| Setup | $0 | One-time |
| Training (10 iter, 500 samples) | $15-20 | Per training |
| Model variations: Claude 3.5 Sonnet | $15-20 | - |
| Model variations: GPT-4o | $10-15 | - |
| Model variations: GPT-4o-mini | $3-5 | Reduced performance |
| **Year 1 Total** | $180-240 | Monthly training |

**ROI Comparison**:

| Aspect | GPU Local | OpenRouter |
|--------|-----------|-----------|
| Initial investment | $2,000-3,000 | $0 |
| Annual cost | $100-300 | $180-240 |
| Break-even | 8-15 months | Immediate |
| Flexibility | Medium | High |
| Scalability | Limited | Unlimited |

**Recommendation**: Startup/test → OpenRouter | Production (>2x/month training) → GPU local

---

## Comparison

| Aspect | GPU Local | OpenRouter API |
|--------|-----------|----------------|
| GPU | ✅ RTX 3090+ | ❌ None |
| Setup | Complex (1-2h) | Simple (10min) |
| Algorithm | GRPO (RL) | APO (prompt) |
| Fine-tuning | ✅ Possible | ❌ No |
| Cost/training | $0-26 | $30-50 |
| Time | 6-24h | 4-6h |
| Improvement | +20-25% | +15-20% |
| Deployment | Identical | Identical |

---

## Monitoring Post-Deploy

### Prometheus Metrics

```csharp
PromptPerformance
    .WithLabels(promptVersion, "accuracy").Observe(accuracy);
PromptPerformance
    .WithLabels(promptVersion, "citation_correctness").Observe(citationCorrectness);
PromptPerformance
    .WithLabels(promptVersion, "hallucination_rate").Observe(hallucinationRate);
```

### Grafana Dashboard Queries

```promql
# Accuracy comparison
avg(meepleai_prompt_performance{metric_type='accuracy', prompt_version='baseline'})
avg(meepleai_prompt_performance{metric_type='accuracy', prompt_version='agent-lightning'})

# Citation correctness
avg(meepleai_prompt_performance{metric_type='citation_correctness'})

# Hallucination rate
avg(meepleai_prompt_performance{metric_type='hallucination_rate'})
```

---

## Continuous Improvement Workflow

### Monthly Training Automation

```bash
# Cron: First Saturday 2 AM
0 2 1-7 * 6 cd /home/user/agent-lightning-openrouter && ./monthly_training.sh

# monthly_training.sh:
# 1. Export new data from MeepleAI (last 30 days, min confidence 0.7)
# 2. Augment training dataset
# 3. Run APO training (15 iterations)
# 4. Evaluate improvement
# 5. If improvement > 10%: Deploy to staging, notify team
```

**Auto-Deploy Criteria**:
- Improvement > 10% baseline
- Validation confidence > 0.85
- No hallucination increase
- Manual approval for production

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **OpenRouter rate limits** | Exponential backoff retry (1s, 2s, 4s, 8s, 16s) |
| **High training costs** | Use GPT-4o-mini ($3-5 vs $15-20) with 5x cost reduction |
| **Prompt not improving** | Increase iterations (20+), larger sample size (100+), advanced reward function |
| **Low baseline performance** | Improve data quality, add more training samples, refine reward function |

---

## Best Practices

1. **Start Small**: 1K samples, 5 iterations, GPT-4o-mini → validate before scaling
2. **Always Validate**: A/B test in staging 1-2 weeks, monitor P@K/MRR/citations, rollback ready with feature flags
3. **Document Everything**: Save training logs, version prompts in git, track metrics in Grafana, maintain changelog
4. **Iterate Regularly**: Monthly training with new data, analyze user feedback, update reward function, scale dataset progressively

---

## Next Steps

### Week 1 (Setup)
1. Choose: GPU local vs OpenRouter
2. Setup training environment
3. Export 3K samples from MeepleAI
4. First test (2 iter, 100 samples)

### Month 1 (Validation)
1. Full training (10 iter, full dataset)
2. Deploy to staging
3. A/B test 2 weeks
4. Production deploy if >10% improvement

### Quarter 1 (Scale)
1. Automate monthly training
2. Expand to other services (SetupGuide, Chess)
3. Continuous learning pipeline
4. Consider full model fine-tuning

---

## Resources

- **Agent Lightning**: https://microsoft.github.io/agent-lightning/
- **OpenRouter**: https://openrouter.ai/docs
- **MeepleAI Guides**:
  - [Quick Start](./agent-lightning-quickstart.md)
  - [Examples](./agent-lightning-examples.md)
  - [Architecture](./agent-lightning-architecture.md)

---

**Conclusion**: Agent Lightning is **fully compatible with OpenRouter**. Both approaches deploy optimized prompts to MeepleAI production without code changes. Choose OpenRouter for low-risk fast validation, GPU local for maximum quality at scale.
