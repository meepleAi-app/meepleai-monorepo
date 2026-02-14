# Agent Lightning Examples for MeepleAI

Practical use cases for optimizing MeepleAI agents with Agent Lightning.

---

## Example 1: RAG System Optimization

### Goal
Improve answer accuracy, citation correctness, reduce hallucinations

### Current Baseline
**Service**: `RagService.cs`
**Metrics**:
- Precision@5: 0.72
- Citation Correctness: 0.68
- Hallucination Rate: 0.15

### Target Improvements
- Precision@5: 0.82 (+14%)
- Citation Correctness: 0.85 (+25%)
- Hallucination Rate: 0.08 (-47%)

### Reward Function
```python
def compute_rag_reward(question, answer, sources, ground_truth) -> float:
    reward = 0.0

    # 1. Answer correctness (0-0.5)
    gt_keywords = set(ground_truth["keywords"])
    matched = sum(1 for kw in gt_keywords if kw in answer.lower())
    reward += 0.5 * (matched / len(gt_keywords))

    # 2. Citation accuracy (0-0.3)
    cited_pages = extract_page_numbers(answer)
    correct_pages = set(ground_truth["pages"])
    if cited_pages:
        precision = len(cited_pages & correct_pages) / len(cited_pages)
        reward += 0.3 * precision

    # 3. No hallucination (0-0.2)
    forbidden = ["I think", "probably", "I assume"]
    if not any(p in answer.lower() for p in forbidden):
        reward += 0.2

    return max(0.0, min(1.0, reward))
```

### Training Configuration
```bash
python -m agentlightning.verl \
    agentlightning.port=9997 \
    algorithm.adv_estimator=grpo \
    data.train_files=data/train_rag_qa.parquet \
    actor_rollout_ref.model.path=Qwen/Qwen2.5-Coder-3B-Instruct \
    trainer.total_epochs=5 \
    data.train_batch_size=32 \
    data.max_prompt_length=4096 \
    data.max_response_length=1024 \
    actor_rollout_ref.actor.optim.lr=1e-6
```

### Data Preparation
```python
def prepare_rag_training_dataset():
    # 1. Load MeepleAI audit logs (ai_request_logs table)
    logs_df = pd.read_csv("exports/ai_request_logs_3months.csv")

    # 2. Filter quality (confidence > 0.7, positive feedback)
    quality = logs_df[
        (logs_df["confidence_score"] > 0.7) &
        (logs_df["user_feedback"].isin(["helpful", "very_helpful", None]))
    ]

    # 3. Extract ground truth
    samples = []
    for _, row in quality.iterrows():
        samples.append({
            "game_id": row["game_id"],
            "question": row["prompt"],
            "ground_truth": {
                "keywords": extract_keywords(row["response"]),
                "pages": extract_pages(row["metadata"]),
                "answerable": True
            }
        })

    # 4. Add hard negatives (unanswerable)
    samples.extend(load_hard_negatives())

    # 5. Split train/val/test (70/15/15)
    train, temp = train_test_split(samples, test_size=0.3)
    val, test = train_test_split(temp, test_size=0.5)

    # 6. Save
    pd.DataFrame(train).to_parquet("data/train_rag_qa.parquet")
```

### Deployment
```python
def export_optimized_prompt():
    # Analyze training traces
    traces = load_training_traces("checkpoints/epoch_5/")
    high_reward = [t for t in traces if t["reward"] > 0.85]

    # Extract best prompt pattern
    optimized = analyze_prompt_patterns(high_reward)

    # Generate SQL migration
    sql = f"""
    INSERT INTO prompt_versions (prompt_template_id, version_number, content)
    SELECT id, MAX(version_number) + 1, '{optimized}'
    FROM prompt_templates WHERE name = 'rag-system-prompt';
    """

    with open("migrations/rag_optimized_v2.sql", "w") as f:
        f.write(sql)
```

---

## Example 2: Setup Guide Agent

### Goal
Improve clarity, completeness, accuracy of setup instructions

### Current Baseline
- Completeness: 0.75 (missing edge cases)
- Clarity: "confusing" in 12% feedback
- Accuracy: 0.92

### Reward Function
```python
def compute_setup_guide_reward(generated, ground_truth) -> float:
    reward = 0.0

    # 1. Completeness (0-0.4)
    required = ground_truth["required_components"]
    mentioned = sum(1 for c in required if c.lower() in generated.lower())
    reward += 0.4 * (mentioned / len(required))

    # 2. Clarity (0-0.3): Numbered steps
    numbered_steps = len(re.findall(r'^\d+\.', generated, re.MULTILINE))
    if numbered_steps >= ground_truth["expected_steps"]:
        reward += 0.3

    # 3. Accuracy (0-0.3): No guessing
    forbidden = ["I think", "probably", "maybe"]
    if not any(p in generated for p in forbidden):
        reward += 0.3

    return reward
```

---

## Example 3: Multi-Agent System

### Selective Training
```python
class ChessRagSystem:
    def __init__(self, llm_client):
        self.chess_agent = ChessAgent(llm_client)  # Frozen
        self.rag_agent = RagAgent(llm_client)      # Optimized

    def handle_query(self, query, fen=None):
        if fen or "move" in query.lower():
            return self.chess_agent.suggest_move(fen, query)
        else:
            return self.rag_agent.answer_question("chess", query)

# Training: Only RAG gets reward signal
for sample in train_data:
    if sample["agent_type"] == "rag":
        result = system.rag_agent.answer_question(...)
        al.report(reward=compute_reward(result))
    else:
        _ = system.chess_agent.suggest_move(...)
        al.report(reward=None, skip_training=True)
```

---

## Training Time & Impact Summary

| Use Case | Training Time | Expected Improvement | Deploy Artifact |
|----------|---------------|---------------------|-----------------|
| **RAG** | 24h (5 epochs) | +25% citation accuracy | Optimized prompt |
| **Setup Guide** | 12h (3 epochs) | +15% completeness | Prompt template |
| **Streaming QA** | 18h (4 epochs) | -20% latency | Model checkpoint |
| **Multi-Agent** | 36h (7 epochs) | Selective gains | Prompt + model |

**Recommended Order**:
1. RAG (highest impact, clear metrics)
2. Setup Guide (simpler reward)
3. Streaming QA (more tuning needed)

---

**See Also**: [Agent Lightning Architecture](./architecture.md) | [Training Guide](./training-guide.md)
