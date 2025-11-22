# Agent Lightning Quick Start for MeepleAI

Get started with Agent Lightning to optimize MeepleAI agents in under 1 hour.

## Prerequisites

- Python 3.10+ installed
- NVIDIA GPU with 16GB+ VRAM (RTX 3090 or better)
- 50GB free disk space
- Access to MeepleAI production database exports

## 5-Step Quick Start

### Step 1: Install Agent Lightning (10 minutes)

```bash
# Create dedicated environment
mkdir ~/agent-lightning-meepleai
cd ~/agent-lightning-meepleai
python3.10 -m venv venv
source venv/bin/activate  # Windows: venv\Scripts\activate

# Install core dependencies
pip install torch==2.7.0 --index-url https://download.pytorch.org/whl/cu128
pip install flash-attn --no-build-isolation
pip install vllm==0.9.2
pip install verl==0.5.0
pip install agentlightning

# Install optional agent frameworks
pip install "langgraph" "langchain[openai]"
pip install pandas qdrant-client httpx
```

**Verify installation**:
```bash
python -c "import agentlightning; print(agentlightning.__version__)"
# Expected: 0.1.x
```

### Step 2: Prepare Training Data (15 minutes)

Export data from MeepleAI database:

```bash
# In MeepleAI repo
cd D:/Repositories/meepleai-monorepo/apps/api

# Run SQL export
psql -h localhost -U postgres -d meepleai -c "
COPY (
    SELECT
        prompt AS question,
        response AS answer,
        metadata->>'game_id' AS game_id,
        confidence_score,
        created_at
    FROM ai_request_logs
    WHERE confidence_score > 0.7
        AND created_at > NOW() - INTERVAL '90 days'
        AND service_name = 'RagService'
    ORDER BY created_at DESC
    LIMIT 3000
) TO '/tmp/rag_training_data.csv' WITH CSV HEADER;
"

# Copy to Agent Lightning project
cp /tmp/rag_training_data.csv ~/agent-lightning-meepleai/exports/
```

**Convert to Parquet format**:

```python
# File: prepare_data.py
import pandas as pd
import json

# Load CSV
df = pd.read_csv("exports/rag_training_data.csv")

# Create training format
def create_sample(row):
    return {
        "game_id": row["game_id"],
        "question": row["question"],
        "ground_truth": {
            "answer": row["answer"],
            "keywords": row["question"].lower().split()[:5],  # Simplified
            "answerable": True
        }
    }

samples = [create_sample(row) for _, row in df.iterrows()]

# Split train/val (85/15)
split_idx = int(len(samples) * 0.85)
train_samples = samples[:split_idx]
val_samples = samples[split_idx:]

# Save as Parquet
pd.DataFrame(train_samples).to_parquet("data/train_rag.parquet")
pd.DataFrame(val_samples).to_parquet("data/val_rag.parquet")

print(f"Created training dataset: {len(train_samples)} train, {len(val_samples)} val")
```

Run preparation:
```bash
mkdir data exports
python prepare_data.py
# Output: Created training dataset: 2550 train, 450 val
```

### Step 3: Create Test Agent (15 minutes)

```python
# File: simple_rag_agent.py
"""
Minimal RAG agent for Agent Lightning training.
Tests prompt optimization without full MeepleAI stack.
"""
from openai import OpenAI
import agentlightning as al

class SimpleRagAgent:
    """Simplified RAG agent for quick testing."""

    def __init__(self, llm_client: OpenAI):
        self.client = llm_client

        # Initial prompt (will be optimized via RL)
        self.system_prompt = """You are a board game rules expert.
Answer questions based on the provided context.
Always cite page numbers using [Page X] format."""

    def answer(self, game_id: str, question: str, context: str) -> str:
        """Generate answer from context."""

        messages = [
            {"role": "system", "content": self.system_prompt},
            {"role": "user", "content": f"""Context:
{context}

Question: {question}

Answer with citations:"""}
        ]

        response = self.client.chat.completions.create(
            model="gpt-4o-mini",
            messages=messages,
            temperature=0.7,
            max_tokens=256
        )

        return response.choices[0].message.content


# Simple reward function
def compute_reward(answer: str, ground_truth: dict) -> float:
    """Basic reward based on citations and keyword matching."""
    reward = 0.0

    # Citation bonus
    if "[Page" in answer:
        reward += 0.5

    # Keyword matching
    keywords = ground_truth.get("keywords", [])
    matched = sum(1 for kw in keywords if kw in answer.lower())
    reward += 0.5 * (matched / max(len(keywords), 1))

    return reward


# Training loop
def train():
    """Run training with Agent Lightning."""

    # Connect to training server
    client = OpenAI(
        base_url="http://localhost:9997/v1",
        api_key="dummy"
    )

    agent = SimpleRagAgent(client)

    # Load data
    import pandas as pd
    train_data = pd.read_parquet("data/train_rag.parquet")

    print(f"Starting training on {len(train_data)} samples")

    # Training loop (managed by Agent Lightning)
    for idx, sample in al.sample_iterator(train_data.to_dict('records')):

        # Mock context (in real training, fetch from Qdrant)
        context = f"[Page 5]: Example rule text about {sample['question'][:30]}..."

        # Generate answer
        answer = agent.answer(
            game_id=sample["game_id"],
            question=sample["question"],
            context=context
        )

        # Compute reward
        reward = compute_reward(answer, sample["ground_truth"])

        # Report to Agent Lightning
        al.report(reward=reward)

        # Progress logging
        if idx % 100 == 0:
            print(f"Sample {idx}: reward={reward:.2f}")


if __name__ == "__main__":
    train()
```

### Step 4: Run Training (20 minutes for first test)

**Terminal 1 - Start Training Server**:
```bash
cd ~/agent-lightning-meepleai

# Quick test with small model (3B params)
python -m agentlightning.verl \
    agentlightning.port=9997 \
    algorithm.adv_estimator=grpo \
    data.train_files=data/train_rag.parquet \
    data.val_files=data/val_rag.parquet \
    actor_rollout_ref.model.path=Qwen/Qwen2.5-Coder-3B-Instruct \
    trainer.total_epochs=2 \
    trainer.n_gpus_per_node=1 \
    data.train_batch_size=16 \
    data.max_prompt_length=2048 \
    data.max_response_length=512 \
    trainer.logger=['console'] \
    trainer.save_freq=100

# Expected output:
# [INFO] Loading model Qwen/Qwen2.5-Coder-3B-Instruct...
# [INFO] Starting training server on port 9997
# [INFO] Waiting for workers...
```

**Terminal 2 - Run Agent Workers**:
```bash
cd ~/agent-lightning-meepleai
source venv/bin/activate

python simple_rag_agent.py --n-workers 4

# Expected output:
# Starting training on 2550 samples
# Sample 0: reward=0.75
# Sample 100: reward=0.82
# Sample 200: reward=0.85
# ...
```

**Monitor progress** (Terminal 3):
```bash
# Watch training metrics
watch -n 5 'curl -s http://localhost:9997/v1/status | jq .'
```

### Step 5: Evaluate & Deploy (10 minutes)

After training completes (~20 minutes for 2 epochs):

```python
# File: evaluate.py
"""
Quick evaluation of trained agent.
"""
import pandas as pd
from simple_rag_agent import SimpleRagAgent, compute_reward
from openai import OpenAI

def evaluate():
    """Compare baseline vs trained agent."""

    # Load test data
    test_data = pd.read_parquet("data/val_rag.parquet")

    # Setup agents
    baseline_client = OpenAI(api_key="your-openai-key")
    trained_client = OpenAI(
        base_url="http://localhost:9997/v1",
        api_key="dummy"
    )

    baseline_agent = SimpleRagAgent(baseline_client)
    trained_agent = SimpleRagAgent(trained_client)

    # Evaluate both
    baseline_rewards = []
    trained_rewards = []

    for _, sample in test_data.head(50).iterrows():  # Quick test on 50 samples
        context = f"[Page 5]: Example context..."

        # Baseline
        baseline_answer = baseline_agent.answer(
            sample["game_id"],
            sample["question"],
            context
        )
        baseline_reward = compute_reward(baseline_answer, sample["ground_truth"])
        baseline_rewards.append(baseline_reward)

        # Trained
        trained_answer = trained_agent.answer(
            sample["game_id"],
            sample["question"],
            context
        )
        trained_reward = compute_reward(trained_answer, sample["ground_truth"])
        trained_rewards.append(trained_reward)

    # Compare
    import numpy as np
    print("\n📊 Evaluation Results (50 samples):")
    print(f"Baseline - Avg Reward: {np.mean(baseline_rewards):.3f}")
    print(f"Trained  - Avg Reward: {np.mean(trained_rewards):.3f}")
    print(f"Improvement: {np.mean(trained_rewards) - np.mean(baseline_rewards):+.3f}")

if __name__ == "__main__":
    evaluate()
```

Run evaluation:
```bash
python evaluate.py

# Expected output:
# 📊 Evaluation Results (50 samples):
# Baseline - Avg Reward: 0.723
# Trained  - Avg Reward: 0.851
# Improvement: +0.128 (+17.7%)
```

**Deploy to MeepleAI**:

If results are good, export optimized prompt:

```python
# File: export_prompt.py
"""Extract best prompt from training."""
import json

# Load training traces
with open("checkpoints/epoch_2/traces.jsonl") as f:
    traces = [json.loads(line) for line in f]

# Find highest-reward prompts
high_reward = [t for t in traces if t.get("reward", 0) > 0.85]

# Extract common system prompt (simplified)
best_prompt = high_reward[0]["system_message"] if high_reward else None

if best_prompt:
    # Generate SQL for MeepleAI
    sql = f"""
-- Optimized RAG prompt from Agent Lightning (2025-11-01)
UPDATE prompt_templates
SET content = '{best_prompt}'
WHERE name = 'rag-system-prompt';
"""

    with open("deploy_prompt.sql", "w") as f:
        f.write(sql)

    print("✅ Exported prompt to deploy_prompt.sql")
    print(f"Preview:\n{best_prompt[:200]}...")
```

## Verification Checklist

Before deploying to production:

- [ ] Training completed successfully (2+ epochs)
- [ ] Evaluation shows improvement > 10%
- [ ] No hallucination increase detected
- [ ] Tested on 50+ validation samples
- [ ] SQL migration created
- [ ] Reviewed optimized prompt manually
- [ ] Backup current production prompt

## Next Steps

### Quick Wins (Week 1)
1. ✅ Complete this quickstart
2. Run overnight training (5 epochs, ~6 hours)
3. A/B test in MeepleAI staging environment
4. Deploy if improvement > 15%

### Deep Optimization (Week 2-3)
1. Add real Qdrant integration to training agent
2. Expand training dataset to 10K samples
3. Fine-tune reward function with citation metrics
4. Train larger model (7B params)

### Production Pipeline (Month 2)
1. Automate weekly training on new logs
2. Setup continuous evaluation dashboard
3. Implement auto-deployment pipeline
4. Monitor production metrics

## Troubleshooting

### Issue: GPU Out of Memory
```bash
# Reduce batch size
data.train_batch_size=8
actor_rollout_ref.actor.ppo_micro_batch_size_per_gpu=2
```

### Issue: Training Server Won't Start
```bash
# Check CUDA availability
python -c "import torch; print(torch.cuda.is_available())"

# If False, install CUDA toolkit:
# https://developer.nvidia.com/cuda-downloads
```

### Issue: Workers Not Connecting
```bash
# Check server is listening
netstat -an | grep 9997

# Test connection
curl http://localhost:9997/v1/models
```

## Resources

- **Full Integration Guide**: `agent-lightning-integration-guide.md`
- **Detailed Examples**: `agent-lightning-examples.md`
- **Architecture Deep Dive**: `agent-lightning-architecture.md`
- **Agent Lightning Docs**: https://microsoft.github.io/agent-lightning/
- **GitHub**: https://github.com/microsoft/agent-lightning

## Estimated Timeline

| Phase | Duration | Outcome |
|-------|----------|---------|
| **Setup** | 30 min | Environment ready |
| **Data Prep** | 15 min | 3K samples prepared |
| **First Training** | 20 min | Quick test (2 epochs) |
| **Evaluation** | 10 min | +10-15% improvement |
| **Deploy** | 10 min | SQL migration ready |
| **Total** | **1.5 hours** | **Ready for production testing** |

For overnight training (recommended):
- **Setup**: 1 hour (one-time)
- **Training**: 6-8 hours (overnight)
- **Evaluation**: 30 minutes
- **Deploy**: 30 minutes
- **Total**: **8-10 hours** (mostly unattended)

**Expected improvement**: +20-25% on RAG metrics
