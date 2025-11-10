# Agent Lightning Integration Guide for MeepleAI Development

## Overview

This guide explains how to use Microsoft Agent Lightning as a **development tool** to optimize MeepleAI's AI agents through Reinforcement Learning (RL). Agent Lightning is used in a **separate training environment** to improve prompts, behaviors, and model performance, which are then deployed to the production MeepleAI system.

## What is Agent Lightning?

Agent Lightning is a Python-based RL training framework from Microsoft Research that enables:
- **Zero-code RL training** for any AI agent
- **Framework-agnostic optimization** (works with LangChain, OpenAI SDK, AutoGen, etc.)
- **Selective agent optimization** in multi-agent systems
- **Decoupled training/execution** architecture

**Key Paper**: [Agent Lightning: Train ANY AI Agents with Reinforcement Learning (arXiv)](https://arxiv.org/abs/2508.03680)

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                  DEVELOPMENT ENVIRONMENT                     │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         Agent Lightning (Python)                      │  │
│  │  ┌────────────┐         ┌──────────────┐            │  │
│  │  │  Training  │────────▶│  Optimized   │            │  │
│  │  │  Server    │         │  Prompts/    │            │  │
│  │  │  (VERL)    │         │  Models      │            │  │
│  │  └────────────┘         └──────────────┘            │  │
│  │         ▲                       │                     │  │
│  │         │                       │                     │  │
│  │  ┌──────┴───────┐              │                     │  │
│  │  │ Test Agents  │              │                     │  │
│  │  │ (Python)     │              │                     │  │
│  │  └──────────────┘              │                     │  │
│  └────────────────────────────────┼─────────────────────┘  │
│                                   │                         │
│                                   ▼                         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         Evaluation & Analysis                         │  │
│  │  - A/B Testing                                        │  │
│  │  - Confidence Scoring                                 │  │
│  │  - Hallucination Detection                            │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          │ Deploy Optimized Artifacts
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                  PRODUCTION ENVIRONMENT                      │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         MeepleAI (.NET/ASP.NET Core)                 │  │
│  │  ┌────────────┐  ┌──────────────┐  ┌──────────────┐ │  │
│  │  │ RagService │  │ SetupGuide   │  │ ChatService  │ │  │
│  │  │            │  │ Service      │  │              │ │  │
│  │  └────────────┘  └──────────────┘  └──────────────┘ │  │
│  │         │                │                 │         │  │
│  │         └────────────────┴─────────────────┘         │  │
│  │                          │                            │  │
│  │                          ▼                            │  │
│  │                  ┌──────────────┐                    │  │
│  │                  │ Prompt DB    │                    │  │
│  │                  │ (Updated)    │                    │  │
│  │                  └──────────────┘                    │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Use Cases for MeepleAI

### 1. RAG System Optimization
**Goal**: Improve retrieval quality and answer accuracy

**Agent Lightning Application**:
- Train agent to select better search parameters (TopK, MinScore)
- Optimize query expansion strategies
- Learn to filter irrelevant chunks
- Improve citation accuracy

**Reward Signal**: Precision@K, MRR, citation correctness

### 2. Setup Guide Agent Optimization
**Goal**: Generate more helpful, accurate setup instructions

**Agent Lightning Application**:
- Optimize prompt templates for clarity
- Learn to structure multi-step instructions
- Improve context selection from rules
- Reduce hallucinations in edge cases

**Reward Signal**: User feedback, instruction completeness, error rate

### 3. Streaming QA Optimization
**Goal**: Faster, more accurate responses with better confidence scores

**Agent Lightning Application**:
- Optimize token generation patterns
- Learn when to stop generation early
- Improve confidence calibration
- Reduce irrelevant token generation

**Reward Signal**: Latency, confidence accuracy, user satisfaction

### 4. Chess Agent Training (Future)
**Goal**: Optimize chess move suggestions and FEN analysis

**Agent Lightning Application**:
- Train agent on legal move validation
- Optimize position evaluation prompts
- Learn better move explanation generation

**Reward Signal**: Move legality, position accuracy, explanation quality

## Development Workflow

### Phase 1: Setup Training Environment

```bash
# 1. Create Python virtual environment (separate from MeepleAI)
python3.10 -m venv ~/agent-lightning-env
source ~/agent-lightning-env/bin/activate

# 2. Install Agent Lightning + dependencies
pip install torch==2.7.0 torchvision==0.22.0 torchaudio==2.7.0 --index-url https://download.pytorch.org/whl/cu128
pip install flash-attn --no-build-isolation
pip install vllm==0.9.2
pip install verl==0.5.0
pip install agentlightning

# 3. Install agent frameworks (optional)
pip install "autogen-agentchat" "autogen-ext[openai]"
pip install langgraph "langchain[openai]"
pip install openai-agents
```

### Phase 2: Create Test Agent (Python)

Create a Python agent that mimics your MeepleAI service behavior:

```python
# File: meepleai_rag_agent.py
"""
Test agent that simulates MeepleAI's RAG service for RL training.
"""
from openai import OpenAI
import agentlightning as al

class MeepleAIRagAgent:
    """Simulates MeepleAI RAG behavior for training."""

    def __init__(self, llm_client: OpenAI):
        self.client = llm_client
        self.system_prompt = """You are a board game rules expert assistant.
Use the provided context to answer questions accurately.
Always cite the page numbers of your sources."""

    def search_rules(self, game_id: str, query: str) -> list[dict]:
        """Simulate Qdrant vector search (mock)."""
        # In training, this would fetch from test dataset
        # For real training, connect to actual Qdrant instance
        return [
            {"text": "Rule context 1...", "page": 5},
            {"text": "Rule context 2...", "page": 12}
        ]

    def answer_question(self, game_id: str, question: str) -> str:
        """Main RAG pipeline."""
        # 1. Retrieve context
        context_chunks = self.search_rules(game_id, question)
        context_text = "\n\n".join([
            f"[Page {c['page']}]: {c['text']}"
            for c in context_chunks
        ])

        # 2. Generate answer with LLM
        messages = [
            {"role": "system", "content": self.system_prompt},
            {"role": "user", "content": f"""Context:
{context_text}

Question: {question}

Answer with citations (e.g., [Page X])."""}
        ]

        response = self.client.chat.completions.create(
            model="gpt-4",
            messages=messages,
            temperature=0.7
        )

        return response.choices[0].message.content

# Training script
def train_rag_agent():
    """Train RAG agent with Agent Lightning."""
    import agentlightning as al

    # 1. Setup LLM client (connects to Agent Lightning server)
    client = OpenAI(
        base_url="http://localhost:9997/v1",  # Agent Lightning server
        api_key="dummy"
    )

    # 2. Create agent
    agent = MeepleAIRagAgent(client)

    # 3. Define reward function
    def compute_reward(question: str, answer: str, ground_truth: dict) -> float:
        """Reward based on answer quality."""
        reward = 0.0

        # Check citation presence
        if "[Page" in answer:
            reward += 0.3

        # Check answer relevance (simplified)
        if any(kw in answer.lower() for kw in ground_truth["keywords"]):
            reward += 0.5

        # Penalize hallucinations
        if "I don't know" in answer and not ground_truth["answerable"]:
            reward += 0.2

        return reward

    # 4. Load training dataset
    train_data = load_rag_dataset("data/train_board_game_qa.parquet")

    # 5. Run training loop
    for sample in al.sample_iterator(train_data):
        question = sample["question"]
        game_id = sample["game_id"]
        ground_truth = sample["ground_truth"]

        # Agent generates answer
        answer = agent.answer_question(game_id, question)

        # Compute reward
        reward = compute_reward(question, answer, ground_truth)

        # Report to Agent Lightning for RL
        al.report(reward=reward)

if __name__ == "__main__":
    train_rag_agent()
```

### Phase 3: Prepare Training Dataset

Create dataset from MeepleAI's existing data:

```python
# File: prepare_training_data.py
"""
Convert MeepleAI data to Agent Lightning format.
"""
import pandas as pd
import json

def prepare_rag_training_data():
    """Create training dataset from MeepleAI audit logs."""

    # 1. Extract from MeepleAI database (via SQL export)
    # SELECT question, answer, game_id, confidence, feedback
    # FROM ai_request_logs WHERE created_at > '2025-01-01'

    logs = pd.read_csv("exports/ai_request_logs.csv")

    # 2. Filter high-quality samples
    quality_samples = logs[
        (logs["confidence"] > 0.7) &
        (logs["feedback"].isin(["helpful", "very_helpful"]))
    ]

    # 3. Create training format
    training_data = []
    for _, row in quality_samples.iterrows():
        training_data.append({
            "question": row["question"],
            "game_id": row["game_id"],
            "ground_truth": {
                "answer": row["answer"],
                "keywords": extract_keywords(row["question"]),
                "answerable": True
            }
        })

    # 4. Save as parquet (Agent Lightning format)
    df = pd.DataFrame(training_data)
    df.to_parquet("data/train_board_game_qa.parquet")

    print(f"Created training dataset: {len(training_data)} samples")

def extract_keywords(text: str) -> list[str]:
    """Extract key terms from question."""
    # Simplified - use NLP library in production
    stopwords = {"what", "how", "when", "where", "is", "the", "a", "an"}
    words = text.lower().split()
    return [w for w in words if w not in stopwords]

if __name__ == "__main__":
    prepare_rag_training_data()
```

### Phase 4: Run Training

```bash
# Terminal 1: Start Agent Lightning training server
python -m agentlightning.verl \
    agentlightning.port=9997 \
    algorithm.adv_estimator=grpo \
    data.train_files=data/train_board_game_qa.parquet \
    data.val_files=data/val_board_game_qa.parquet \
    actor_rollout_ref.model.path=Qwen/Qwen2.5-Coder-3B-Instruct \
    trainer.total_epochs=3 \
    trainer.save_freq=100 \
    trainer.logger=['console','wandb'] \
    trainer.project_name=MeepleAI-RAG-Optimization

# Terminal 2: Run agent clients (parallel workers)
python meepleai_rag_agent.py --n-workers 8
```

### Phase 5: Evaluate Results

```python
# File: evaluate_trained_agent.py
"""
Evaluate trained agent against baseline.
"""
import pandas as pd
from sklearn.metrics import precision_score, recall_score

def evaluate_agent(model_path: str):
    """Compare trained vs baseline agent."""

    # Load test dataset
    test_data = pd.read_parquet("data/test_board_game_qa.parquet")

    # Setup baseline and trained agents
    baseline_agent = MeepleAIRagAgent(
        OpenAI(base_url="https://api.openai.com/v1")
    )
    trained_agent = MeepleAIRagAgent(
        OpenAI(base_url="http://localhost:9997/v1")  # Trained model
    )

    baseline_metrics = compute_metrics(baseline_agent, test_data)
    trained_metrics = compute_metrics(trained_agent, test_data)

    # Compare results
    print("Evaluation Results:")
    print(f"Baseline - Accuracy: {baseline_metrics['accuracy']:.2%}")
    print(f"Trained  - Accuracy: {trained_metrics['accuracy']:.2%}")
    print(f"Improvement: {trained_metrics['accuracy'] - baseline_metrics['accuracy']:+.2%}")

    return trained_metrics

def compute_metrics(agent, test_data):
    """Calculate performance metrics."""
    correct = 0
    total = len(test_data)

    for _, sample in test_data.iterrows():
        answer = agent.answer_question(
            sample["game_id"],
            sample["question"]
        )

        # Check correctness (simplified)
        if is_correct_answer(answer, sample["ground_truth"]):
            correct += 1

    return {
        "accuracy": correct / total,
        "total_samples": total
    }

if __name__ == "__main__":
    evaluate_agent("checkpoints/epoch_3")
```

### Phase 6: Deploy to MeepleAI Production

After training, deploy optimized artifacts:

#### Option A: Export Optimized Prompts

```python
# File: export_optimized_prompts.py
"""
Extract best-performing prompts from training.
"""

def extract_best_prompts():
    """Analyze training traces to find optimal prompts."""

    # 1. Load training logs from Agent Lightning
    traces = load_training_traces("checkpoints/epoch_3/traces.jsonl")

    # 2. Find highest-reward prompt variations
    best_prompts = []
    for trace in traces:
        if trace["reward"] > 0.85:  # High-quality threshold
            best_prompts.append({
                "system_prompt": trace["system_message"],
                "reward": trace["reward"],
                "sample_count": 1
            })

    # 3. Cluster and select top performers
    optimized_prompt = aggregate_top_prompts(best_prompts)

    # 4. Export to SQL for MeepleAI prompt_templates table
    export_to_sql(optimized_prompt, "rag-system-prompt")

    print("Exported optimized prompt to database")

def export_to_sql(prompt_data: dict, template_name: str):
    """Generate SQL INSERT for MeepleAI database."""
    sql = f"""
    -- Insert optimized prompt from Agent Lightning training
    INSERT INTO prompt_templates (name, category, content, is_active, version)
    VALUES (
        '{template_name}',
        'rag',
        '{prompt_data["system_prompt"]}',
        false,  -- Requires manual activation after review
        2  -- Increment version
    );
    """

    with open("migration_optimized_prompts.sql", "w") as f:
        f.write(sql)
```

#### Option B: Fine-tuned Model via OpenRouter

If you trained a custom model:

```bash
# 1. Export model checkpoint
python -m agentlightning.export \
    --checkpoint checkpoints/epoch_3 \
    --output models/meepleai-rag-optimized

# 2. Upload to model hosting (e.g., Hugging Face)
huggingface-cli upload \
    meepleai/rag-optimized-qwen2.5-3b \
    models/meepleai-rag-optimized

# 3. Update MeepleAI configuration
# In appsettings.json or dynamic config:
# "AI:Model": "meepleai/rag-optimized-qwen2.5-3b"
```

## Best Practices

### 1. Start Small
- Begin with single agent (RAG or Setup Guide)
- Use small model (Qwen 3B) for faster iteration
- Validate on 100-500 samples before scaling

### 2. Reward Engineering
- Combine multiple signals (accuracy + latency + citations)
- Use existing MeepleAI metrics (confidence scores, feedback)
- Penalize hallucinations heavily

### 3. Data Quality
- Filter training data by confidence > 0.7
- Include both positive and negative examples
- Balance dataset across game types

### 4. Monitoring
- Track training metrics in W&B or TensorBoard
- Compare against baseline after each epoch
- Stop early if overfitting detected

### 5. Deployment Safety
- Always A/B test in staging first
- Use feature flags for gradual rollout
- Monitor production metrics closely

## Integration with Existing MeepleAI Features

### Prompt Management (ADMIN-01)
Agent Lightning optimized prompts → `prompt_templates` table:

```csharp
// Use PromptEvaluationService to compare
var evaluation = await _promptEvaluationService.EvaluatePromptAsync(
    promptId: templateId,
    versionId: newVersionId,
    datasetPath: "test_board_game_qa.json"
);

// If evaluation passes thresholds, activate
if (evaluation.Accuracy >= 0.80 && evaluation.HallucinationRate <= 0.10)
{
    await _promptTemplateService.ActivateVersionAsync(templateId, newVersionId, userId);
}
```

### RAG Evaluation (AI-06)
Use Agent Lightning to optimize RAG metrics:

```csharp
// Measure improvement post-training
var beforeMetrics = await _ragEvaluationService.EvaluateAsync(
    querySet: testQueries,
    promptVersion: "baseline"
);

var afterMetrics = await _ragEvaluationService.EvaluateAsync(
    querySet: testQueries,
    promptVersion: "agent-lightning-optimized"
);

// Compare Precision@K, MRR improvements
var improvement = afterMetrics.PrecisionAtK - beforeMetrics.PrecisionAtK;
_logger.LogInformation($"RAG improvement: {improvement:P2}");
```

### Dynamic Configuration (CONFIG-01)
Store training hyperparameters:

```sql
-- Track Agent Lightning training configuration
INSERT INTO system_configurations (key, value, value_type, category)
VALUES
    ('AgentLightning:LastTrainingDate', '2025-11-01', 'string', 'AI'),
    ('AgentLightning:ModelVersion', 'epoch_3_20251101', 'string', 'AI'),
    ('AgentLightning:TrainingAccuracy', '0.87', 'double', 'AI');
```

## Example: End-to-End RAG Optimization

Complete walkthrough for optimizing MeepleAI RAG:

```bash
# 1. Export training data from MeepleAI
cd D:/Repositories/meepleai-monorepo/tools
pwsh export-training-data.ps1 -Days 90 -MinConfidence 0.7

# 2. Setup Agent Lightning environment
cd ~/agent-lightning-projects/meepleai-rag
python3.10 -m venv venv
source venv/bin/activate
pip install -r requirements-agent-lightning.txt

# 3. Prepare dataset
python prepare_training_data.py \
    --input ../../meepleai-monorepo/exports/ai_logs_90days.csv \
    --output data/train_rag.parquet

# 4. Start training (overnight, 8-12 hours)
./train_rag_agent.sh

# 5. Evaluate results next morning
python evaluate_trained_agent.py \
    --checkpoint checkpoints/epoch_3 \
    --test-data data/test_rag.parquet

# 6. Export best prompts
python export_optimized_prompts.py \
    --checkpoint checkpoints/epoch_3 \
    --output ../meepleai-monorepo/migrations/optimized_prompts.sql

# 7. Deploy to MeepleAI
cd ../../meepleai-monorepo/apps/api
dotnet ef database update  # Apply migration
# Activate new prompt via admin UI at /admin/prompts
```

## Troubleshooting

### Issue: CUDA Out of Memory
**Solution**: Reduce batch size or use smaller model
```bash
actor_rollout_ref.actor.ppo_micro_batch_size_per_gpu=2  # Default: 4
```

### Issue: Agent Timeout During Training
**Solution**: Increase timeout and limit response length
```python
data.max_response_length=1024  # Default: 2048
```

### Issue: Poor Reward Signal
**Solution**: Refine reward function with domain knowledge
```python
# Add game-specific rewards
if "rules on page" in answer and correct_page_cited:
    reward += 0.4  # Bonus for correct citations
```

## Resources

- **Agent Lightning Docs**: https://microsoft.github.io/agent-lightning/
- **GitHub Repo**: https://github.com/microsoft/agent-lightning
- **Research Paper**: https://arxiv.org/abs/2508.03680
- **SQL Agent Example**: https://medium.com/@yugez/training-ai-agents-to-write-and-self-correct-sql-with-reinforcement-learning-571ed31281ad
- **MeepleAI Prompt Management**: `docs/issue/admin-01-phase4-implementation-tracker.md`

## Next Steps

1. **Start Simple**: Optimize RAG system prompt first (highest impact)
2. **Collect Baseline**: Run evaluation on current prompts
3. **Setup Environment**: Install Agent Lightning in separate Python env
4. **Prepare Data**: Export 3 months of high-quality AI logs
5. **Train & Evaluate**: Run training loop, compare metrics
6. **Deploy Safely**: Use feature flags and A/B testing

**Estimated Timeline**:
- Setup (Day 1): 4 hours
- Data preparation (Day 2): 4 hours
- Training (Day 3-4): 24 hours (overnight runs)
- Evaluation & deployment (Day 5): 6 hours

**Total**: 1 week for first optimization cycle
