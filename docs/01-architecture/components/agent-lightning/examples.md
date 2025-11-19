# Agent Lightning Use Case Examples for MeepleAI

Practical examples showing how to use Agent Lightning to optimize specific MeepleAI agents.

## Example 1: RAG System Optimization

### Goal
Improve answer accuracy, citation correctness, and reduce hallucinations in the RAG-powered Q&A system.

### Current Baseline (MeepleAI Production)

**Service**: `RagService.cs` (apps/api/src/Api/Services/RagService.cs)
**Metrics** (from AI-06 evaluation):
- Precision@5: 0.72
- Citation Correctness: 0.68
- Hallucination Rate: 0.15

### Training Setup

#### 1. Create Python Agent Wrapper

```python
# File: agents/meepleai_rag_agent.py
"""
RAG agent for Agent Lightning training.
Simulates MeepleAI's RagService behavior.
"""
from typing import List, Dict, Optional
from openai import OpenAI
import agentlightning as al
import json

class MeepleAIRagAgent:
    """Board game rules Q&A agent using RAG."""

    def __init__(
        self,
        llm_client: OpenAI,
        vector_db_url: str = "http://localhost:6333"
    ):
        self.client = llm_client
        self.vector_db_url = vector_db_url
        self.collection_name = "board_game_rules"

        # System prompt (will be optimized via RL)
        self.system_prompt = self._load_initial_prompt()

    def _load_initial_prompt(self) -> str:
        """Load baseline prompt from MeepleAI."""
        # Exported from MeepleAI prompt_templates table
        return """You are an expert board game rules assistant.

**Your Task**:
Answer questions about board game rules using the provided context.

**Instructions**:
1. Base your answer ONLY on the provided context
2. Always cite page numbers using [Page X] format
3. If the answer isn't in the context, say "I don't have enough information"
4. Be concise but complete
5. Use game-specific terminology correctly

**Context Format**:
Each context chunk includes a [Page X] reference.
"""

    def search_vector_db(
        self,
        game_id: str,
        query: str,
        top_k: int = 5
    ) -> List[Dict]:
        """
        Simulate Qdrant vector search.
        In production training, connect to actual Qdrant instance.
        """
        from qdrant_client import QdrantClient

        client = QdrantClient(url=self.vector_db_url)

        # Search (same logic as MeepleAI RagService)
        results = client.search(
            collection_name=self.collection_name,
            query_vector=self._embed_query(query),
            query_filter={
                "must": [
                    {"key": "game_id", "match": {"value": game_id}}
                ]
            },
            limit=top_k,
            score_threshold=0.5
        )

        return [
            {
                "text": hit.payload["text"],
                "page": hit.payload["page_number"],
                "score": hit.score
            }
            for hit in results
        ]

    def _embed_query(self, text: str) -> List[float]:
        """Generate embedding (call same endpoint as MeepleAI)."""
        import httpx

        response = httpx.post(
            "http://localhost:5080/api/v1/internal/embeddings",
            json={"text": text}
        )
        return response.json()["embedding"]

    def answer_question(
        self,
        game_id: str,
        question: str,
        conversation_history: Optional[List[Dict]] = None
    ) -> Dict:
        """
        Main RAG pipeline.
        Returns: {answer: str, sources: List[Dict], confidence: float}
        """
        # 1. Retrieve relevant context
        context_chunks = self.search_vector_db(game_id, question)

        # 2. Build context string
        context_text = "\n\n".join([
            f"[Page {chunk['page']}] (Relevance: {chunk['score']:.2f})\n{chunk['text']}"
            for chunk in context_chunks
        ])

        # 3. Build messages
        messages = [
            {"role": "system", "content": self.system_prompt}
        ]

        # Add conversation history if available
        if conversation_history:
            messages.extend(conversation_history)

        # Add current query
        messages.append({
            "role": "user",
            "content": f"""**Context from Rules**:
{context_text}

**Question**: {question}

**Answer** (with citations):"""
        })

        # 4. Call LLM (via Agent Lightning server during training)
        response = self.client.chat.completions.create(
            model="gpt-4o-mini",  # Will use trained model during RL
            messages=messages,
            temperature=0.7,
            max_tokens=512
        )

        answer = response.choices[0].message.content

        # 5. Extract confidence (simplified)
        confidence = self._estimate_confidence(answer, context_chunks)

        return {
            "answer": answer,
            "sources": context_chunks,
            "confidence": confidence
        }

    def _estimate_confidence(
        self,
        answer: str,
        sources: List[Dict]
    ) -> float:
        """Estimate answer confidence based on heuristics."""
        confidence = 0.5  # Base

        # Increase if citations present
        if "[Page" in answer:
            confidence += 0.2

        # Increase if answer overlaps with high-score chunks
        if sources and sources[0]["score"] > 0.8:
            confidence += 0.2

        # Decrease if uncertainty phrases present
        uncertainty_phrases = [
            "I'm not sure",
            "I don't have enough",
            "might be",
            "possibly"
        ]
        if any(phrase in answer.lower() for phrase in uncertainty_phrases):
            confidence -= 0.3

        return max(0.0, min(1.0, confidence))


# Reward function for RL training
def compute_rag_reward(
    question: str,
    answer: str,
    sources: List[Dict],
    ground_truth: Dict
) -> float:
    """
    Multi-component reward function.

    Components:
    1. Answer correctness (0-0.5)
    2. Citation accuracy (0-0.3)
    3. No hallucination (0-0.2)
    """
    reward = 0.0

    # 1. Answer correctness (keyword matching)
    gt_keywords = set(ground_truth.get("keywords", []))
    answer_lower = answer.lower()
    matched_keywords = sum(1 for kw in gt_keywords if kw in answer_lower)
    keyword_ratio = matched_keywords / max(len(gt_keywords), 1)
    reward += 0.5 * keyword_ratio

    # 2. Citation accuracy
    cited_pages = extract_page_numbers(answer)
    correct_pages = set(ground_truth.get("pages", []))

    if cited_pages:
        citation_precision = len(cited_pages & correct_pages) / len(cited_pages)
        reward += 0.3 * citation_precision
    elif not correct_pages:
        # No citations needed and none provided
        reward += 0.3

    # 3. No hallucination bonus
    forbidden_phrases = [
        "I think", "probably", "I assume",
        "it seems like", "I guess"
    ]
    if not any(phrase in answer.lower() for phrase in forbidden_phrases):
        reward += 0.2

    # 4. Penalty for "don't know" when answer exists
    if ground_truth.get("answerable", True):
        if "don't have enough information" in answer.lower():
            reward -= 0.5

    return max(0.0, min(1.0, reward))


def extract_page_numbers(text: str) -> set:
    """Extract page numbers from [Page X] citations."""
    import re
    pattern = r'\[Page (\d+)\]'
    matches = re.findall(pattern, text)
    return set(int(page) for page in matches)


# Training script
def train_rag_agent():
    """Train RAG agent with Agent Lightning."""

    # 1. Setup LLM client (connects to Agent Lightning training server)
    client = OpenAI(
        base_url="http://localhost:9997/v1",
        api_key="dummy"
    )

    # 2. Create agent
    agent = MeepleAIRagAgent(
        llm_client=client,
        vector_db_url="http://localhost:6333"
    )

    # 3. Load training dataset
    import pandas as pd
    train_data = pd.read_parquet("data/train_rag_qa.parquet")

    print(f"Training on {len(train_data)} samples")

    # 4. Training loop (managed by Agent Lightning)
    for idx, sample in al.sample_iterator(train_data.to_dict('records')):

        # Get sample data
        game_id = sample["game_id"]
        question = sample["question"]
        ground_truth = sample["ground_truth"]

        # Agent generates answer
        result = agent.answer_question(game_id, question)

        # Compute reward
        reward = compute_rag_reward(
            question=question,
            answer=result["answer"],
            sources=result["sources"],
            ground_truth=ground_truth
        )

        # Report to Agent Lightning for RL optimization
        al.report(
            reward=reward,
            metadata={
                "confidence": result["confidence"],
                "num_sources": len(result["sources"]),
                "has_citations": "[Page" in result["answer"]
            }
        )

        # Log progress every 100 samples
        if idx % 100 == 0:
            print(f"Sample {idx}: reward={reward:.3f}")


if __name__ == "__main__":
    train_rag_agent()
```

#### 2. Prepare Training Data

```python
# File: data_preparation/prepare_rag_dataset.py
"""
Convert MeepleAI audit logs to Agent Lightning training format.
"""
import pandas as pd
import json
from typing import List, Dict

def prepare_rag_training_dataset():
    """
    Create training dataset from MeepleAI ai_request_logs.

    Data Sources:
    1. ai_request_logs table (positive samples)
    2. Manual test cases (edge cases)
    3. User feedback logs (hard negatives)
    """

    # 1. Load MeepleAI audit logs (export via SQL)
    logs_df = pd.read_csv("exports/ai_request_logs_3months.csv")

    # 2. Filter high-quality samples
    # - Confidence > 0.7
    # - User feedback positive or neutral
    # - Response length reasonable (50-500 tokens)
    quality_logs = logs_df[
        (logs_df["confidence_score"] > 0.7) &
        (logs_df["response_length"] > 50) &
        (logs_df["response_length"] < 500) &
        (logs_df["user_feedback"].isin(["helpful", "very_helpful", None]))
    ]

    print(f"Filtered {len(quality_logs)} high-quality samples")

    # 3. Extract ground truth keywords
    training_samples = []

    for _, row in quality_logs.iterrows():
        # Parse RAG metadata
        metadata = json.loads(row["metadata"])

        sample = {
            "game_id": row["game_id"],
            "question": row["prompt"],
            "ground_truth": {
                "keywords": extract_keywords_from_answer(row["response"]),
                "pages": extract_page_numbers_from_metadata(metadata),
                "answerable": True,
                "expected_confidence": row["confidence_score"]
            }
        }

        training_samples.append(sample)

    # 4. Add hard negatives (unanswerable questions)
    hard_negatives = load_hard_negative_samples()
    training_samples.extend(hard_negatives)

    # 5. Split train/val/test
    from sklearn.model_selection import train_test_split

    train, temp = train_test_split(
        training_samples,
        test_size=0.3,
        random_state=42
    )
    val, test = train_test_split(temp, test_size=0.5, random_state=42)

    # 6. Save as parquet
    pd.DataFrame(train).to_parquet("data/train_rag_qa.parquet")
    pd.DataFrame(val).to_parquet("data/val_rag_qa.parquet")
    pd.DataFrame(test).to_parquet("data/test_rag_qa.parquet")

    print(f"Dataset created:")
    print(f"  Train: {len(train)} samples")
    print(f"  Val: {len(val)} samples")
    print(f"  Test: {len(test)} samples")


def extract_keywords_from_answer(answer: str) -> List[str]:
    """Extract key terms from answer using NLP."""
    # Use spaCy or similar for better extraction
    import re
    from collections import Counter

    # Remove citations
    text = re.sub(r'\[Page \d+\]', '', answer)

    # Simple keyword extraction (improve with NLP)
    words = re.findall(r'\b[a-z]{4,}\b', text.lower())

    # Filter stopwords
    stopwords = {"this", "that", "with", "from", "your", "have", "will"}
    keywords = [w for w in words if w not in stopwords]

    # Get top 5 most common
    return [word for word, _ in Counter(keywords).most_common(5)]


def extract_page_numbers_from_metadata(metadata: Dict) -> List[int]:
    """Extract page numbers from RAG sources."""
    if "sources" in metadata:
        return [src["page_number"] for src in metadata["sources"]]
    return []


def load_hard_negative_samples() -> List[Dict]:
    """
    Load manually curated unanswerable questions.
    These test if agent correctly identifies when it lacks information.
    """
    return [
        {
            "game_id": "catan",
            "question": "What is the average game duration for experienced players?",
            "ground_truth": {
                "keywords": [],
                "pages": [],
                "answerable": False  # Not in rulebook
            }
        },
        {
            "game_id": "chess",
            "question": "How many chess tournaments are held annually?",
            "ground_truth": {
                "keywords": [],
                "pages": [],
                "answerable": False
            }
        }
        # Add 50-100 more hard negatives
    ]


if __name__ == "__main__":
    prepare_rag_training_dataset()
```

#### 3. Training Configuration

```bash
# File: train_rag.sh
#!/bin/bash
# Train RAG agent with Agent Lightning

# Start training server
python -m agentlightning.verl \
    agentlightning.port=9997 \
    algorithm.adv_estimator=grpo \
    data.train_files=data/train_rag_qa.parquet \
    data.val_files=data/val_rag_qa.parquet \
    \
    # Model configuration
    actor_rollout_ref.model.path=Qwen/Qwen2.5-Coder-3B-Instruct \
    actor_rollout_ref.rollout.tensor_model_parallel_size=1 \
    \
    # Training parameters
    trainer.n_gpus_per_node=1 \
    trainer.total_epochs=5 \
    trainer.save_freq=200 \
    trainer.test_freq=50 \
    \
    # Batch sizes (adjust for GPU memory)
    data.train_batch_size=32 \
    actor_rollout_ref.actor.ppo_mini_batch_size=32 \
    actor_rollout_ref.actor.ppo_micro_batch_size_per_gpu=4 \
    \
    # Context limits (match MeepleAI)
    data.max_prompt_length=4096 \
    data.max_response_length=1024 \
    data.truncation='error' \
    \
    # Learning rate
    actor_rollout_ref.actor.optim.lr=1e-6 \
    \
    # RL algorithm settings
    actor_rollout_ref.actor.use_kl_loss=False \
    actor_rollout_ref.actor.clip_ratio_low=0.2 \
    actor_rollout_ref.actor.clip_ratio_high=0.3 \
    algorithm.use_kl_in_reward=False \
    \
    # Logging
    trainer.logger=['console','wandb'] \
    trainer.project_name=MeepleAI-RAG \
    trainer.experiment_name=rag_optimization_v1 \
    \
    # Evaluation
    trainer.val_before_train=True \
    trainer.critic_warmup=0
```

#### 4. Run Training

```bash
# Terminal 1: Start training server
cd ~/agent-lightning-projects/meepleai-rag
source venv/bin/activate
./train_rag.sh

# Terminal 2: Run agent workers (parallel)
python agents/meepleai_rag_agent.py \
    --n-workers 16 \
    --daemon true
```

### Expected Results

**Baseline** (before training):
- Precision@5: 0.72
- Citation Correctness: 0.68
- Hallucination Rate: 0.15
- Avg Confidence: 0.65

**After 5 Epochs** (expected improvements):
- Precision@5: 0.82 (+14%)
- Citation Correctness: 0.85 (+25%)
- Hallucination Rate: 0.08 (-47%)
- Avg Confidence: 0.78 (+20%)

### Deployment

```python
# File: deploy_optimized_rag.py
"""
Deploy trained RAG agent artifacts to MeepleAI.
"""
import json

def export_optimized_prompt():
    """Extract best system prompt from training."""

    # 1. Analyze training traces
    traces = load_training_traces("checkpoints/epoch_5/traces.jsonl")

    # 2. Find highest-reward prompts
    high_reward_traces = [
        t for t in traces if t["reward"] > 0.85
    ]

    # 3. Extract common prompt patterns
    optimized_prompt = analyze_prompt_patterns(high_reward_traces)

    # 4. Generate SQL migration for MeepleAI
    sql = f"""
-- Optimized RAG system prompt from Agent Lightning training (2025-11-01)
-- Performance: P@5=0.82, Citation=0.85, Hallucination=0.08

INSERT INTO prompt_versions (
    prompt_template_id,
    version_number,
    content,
    change_summary,
    created_by_user_id
)
SELECT
    id,
    (SELECT MAX(version_number) + 1 FROM prompt_versions WHERE prompt_template_id = pt.id),
    '{optimized_prompt}',
    'Agent Lightning RL optimization - 5 epochs, 3200 samples, +25% citation accuracy',
    1  -- System user
FROM prompt_templates pt
WHERE pt.name = 'rag-system-prompt';
"""

    with open("../../meepleai-monorepo/migrations/rag_optimized_prompt_v2.sql", "w") as f:
        f.write(sql)

    print("✅ Exported optimized prompt to migrations/")


def analyze_prompt_patterns(traces: List[Dict]) -> str:
    """
    Find common patterns in high-reward prompts.
    Returns optimized prompt template.
    """
    # Extract prompt variations that led to high rewards
    # Use LLM to synthesize best practices

    # Simplified version:
    return """You are an expert board game rules assistant with access to official rulebooks.

**Critical Instructions**:
1. ONLY answer based on the provided rule context - never guess or use external knowledge
2. ALWAYS cite specific page numbers using [Page X] format
3. If the context doesn't contain the answer, respond: "I don't have this information in the rulebook"
4. Use precise game terminology from the rules (don't paraphrase)
5. For multi-step questions, break down the answer clearly

**Response Format**:
- Direct answer first
- Supporting details with citations
- Relevant rule references

**Forbidden**:
- Never say "I think" or "probably"
- Never mix information from multiple games
- Never add clarifications not in the rules"""


if __name__ == "__main__":
    export_optimized_prompt()
```

---

## Example 2: Setup Guide Agent Optimization

### Goal
Improve clarity, completeness, and accuracy of generated setup instructions.

### Current Baseline

**Service**: `SetupGuideService.cs` (apps/api/src/Api/Services/SetupGuideService.cs)
**Metrics**:
- Completeness: 0.75 (missing edge cases)
- Clarity: User feedback "confusing" in 12% of cases
- Accuracy: 0.92

### Training Setup

```python
# File: agents/setup_guide_agent.py
"""
Setup guide generation agent for Agent Lightning training.
"""
from openai import OpenAI
import agentlightning as al
from typing import List, Dict

class SetupGuideAgent:
    """Generate game setup instructions from rules."""

    def __init__(self, llm_client: OpenAI):
        self.client = llm_client
        self.system_prompt = """You are a game setup instruction generator.

**Task**: Create clear, step-by-step setup instructions for board games.

**Instructions**:
1. Use numbered steps
2. Be specific about component placement
3. Include player count variations
4. Mention common mistakes to avoid
5. Keep language simple and direct

**Output Format**:
## Setup Instructions

1. [First step]
2. [Second step]
...

**Tips**:
- [Common mistake 1]
- [Common mistake 2]
"""

    def generate_setup_guide(
        self,
        game_id: str,
        player_count: int,
        rule_context: List[str]
    ) -> str:
        """Generate setup instructions."""

        # Build context from rules
        context_text = "\n\n".join([
            f"**Rule Section {i+1}**:\n{section}"
            for i, section in enumerate(rule_context)
        ])

        messages = [
            {"role": "system", "content": self.system_prompt},
            {"role": "user", "content": f"""**Game**: {game_id}
**Player Count**: {player_count}

**Rules Context**:
{context_text}

Generate complete setup instructions:"""}
        ]

        response = self.client.chat.completions.create(
            model="gpt-4o-mini",
            messages=messages,
            temperature=0.3  # Lower temp for consistent instructions
        )

        return response.choices[0].message.content


def compute_setup_guide_reward(
    generated_guide: str,
    ground_truth: Dict
) -> float:
    """
    Reward function for setup guide quality.

    Components:
    1. Completeness (0-0.4): All required steps present
    2. Clarity (0-0.3): Numbered, clear language
    3. Accuracy (0-0.3): No incorrect information
    """
    reward = 0.0

    # 1. Completeness: Check for required components
    required_components = ground_truth.get("required_components", [])
    mentioned_count = sum(
        1 for comp in required_components
        if comp.lower() in generated_guide.lower()
    )
    completeness = mentioned_count / max(len(required_components), 1)
    reward += 0.4 * completeness

    # 2. Clarity: Structured with numbered steps
    import re
    numbered_steps = len(re.findall(r'^\d+\.', generated_guide, re.MULTILINE))
    expected_steps = ground_truth.get("expected_steps", 5)

    if numbered_steps >= expected_steps:
        reward += 0.3
    elif numbered_steps > 0:
        reward += 0.15

    # 3. Accuracy: No forbidden phrases (guessing)
    forbidden = ["I think", "probably", "maybe", "I'm not sure"]
    if not any(phrase in generated_guide for phrase in forbidden):
        reward += 0.3

    return reward


def train_setup_guide_agent():
    """Train setup guide agent."""

    client = OpenAI(
        base_url="http://localhost:9998/v1",  # Different port from RAG
        api_key="dummy"
    )

    agent = SetupGuideAgent(client)

    # Load training data
    import pandas as pd
    train_data = pd.read_parquet("data/train_setup_guides.parquet")

    for idx, sample in al.sample_iterator(train_data.to_dict('records')):

        # Generate guide
        guide = agent.generate_setup_guide(
            game_id=sample["game_id"],
            player_count=sample["player_count"],
            rule_context=sample["rule_sections"]
        )

        # Compute reward
        reward = compute_setup_guide_reward(
            generated_guide=guide,
            ground_truth=sample["ground_truth"]
        )

        # Report to Agent Lightning
        al.report(reward=reward)

        if idx % 50 == 0:
            print(f"Sample {idx}: reward={reward:.3f}")


if __name__ == "__main__":
    train_setup_guide_agent()
```

---

## Example 3: Multi-Agent System (Chess + RAG)

For future multi-agent scenarios:

```python
# File: agents/multi_agent_system.py
"""
Multi-agent system with selective optimization.
Example: Chess agent + RAG agent working together.
"""
import agentlightning as al
from openai import OpenAI

class ChessRagSystem:
    """
    Combines chess move validation with rules Q&A.
    Optimize ONLY the rules agent, keep chess frozen.
    """

    def __init__(self, llm_client: OpenAI):
        self.chess_agent = ChessAgent(llm_client)  # Frozen
        self.rag_agent = MeepleAIRagAgent(llm_client)  # Optimized

    def handle_query(self, query: str, fen: Optional[str] = None):
        """Route query to appropriate agent."""

        # 1. Classify intent
        if fen or "move" in query.lower():
            # Chess query
            return self.chess_agent.suggest_move(fen, query)
        else:
            # Rules query (optimized via RL)
            return self.rag_agent.answer_question("chess", query)


# Only RAG agent gets reward signal during training
def train_selective():
    client = OpenAI(base_url="http://localhost:9997/v1")
    system = ChessRagSystem(client)

    for sample in al.sample_iterator(train_data):
        if sample["agent_type"] == "rag":
            # Train this agent
            result = system.rag_agent.answer_question(
                sample["game_id"],
                sample["question"]
            )
            reward = compute_rag_reward(result, sample["ground_truth"])
            al.report(reward=reward)
        else:
            # Chess agent: execute but don't train
            _ = system.chess_agent.suggest_move(sample["fen"], sample["query"])
            al.report(reward=None, skip_training=True)
```

---

## Summary: Quick Reference

| Use Case | Training Time | Expected Improvement | Deploy Artifact |
|----------|---------------|---------------------|-----------------|
| **RAG Optimization** | 24h (5 epochs) | +25% citation accuracy | Optimized prompt |
| **Setup Guide** | 12h (3 epochs) | +15% completeness | Prompt template |
| **Streaming QA** | 18h (4 epochs) | -20% latency | Model checkpoint |
| **Multi-Agent** | 36h (7 epochs) | Selective gains | Prompt + model |

**Recommended Order**:
1. Start with RAG (highest impact, clearest metrics)
2. Then Setup Guide (simpler reward function)
3. Finally Streaming QA (requires more tuning)
