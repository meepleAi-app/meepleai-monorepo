# Cross-Encoder Reranking Service

ADR-016 Phase 4: Semantic reranking service for RAG retrieval optimization.

## Overview

This service provides cross-encoder based reranking using the BAAI/bge-reranker-v2-m3 model. It takes initial retrieval results and reorders them based on semantic relevance to the query, significantly improving precision for RAG applications.

## Features

- **High-Quality Reranking**: Uses BGE-reranker-v2-m3, a state-of-the-art multilingual cross-encoder
- **Batch Processing**: Efficient batch inference for multiple chunks
- **Configurable**: Environment-based configuration for model, batch size, device
- **Health Monitoring**: Health endpoint for container orchestration
- **Graceful Degradation**: C# client handles service unavailability gracefully

## API Endpoints

### POST /rerank

Rerank chunks based on query relevance.

**Request:**
```json
{
  "query": "How do I set up the game?",
  "chunks": [
    {
      "id": "chunk-1",
      "content": "Place all tokens in the center...",
      "score": 0.85,
      "metadata": {"page": 5}
    },
    {
      "id": "chunk-2",
      "content": "Each player takes 3 cards...",
      "score": 0.82,
      "metadata": {"page": 3}
    }
  ],
  "top_k": 5
}
```

**Response:**
```json
{
  "results": [
    {
      "id": "chunk-2",
      "content": "Each player takes 3 cards...",
      "original_score": 0.82,
      "rerank_score": 0.94,
      "metadata": {"page": 3}
    },
    {
      "id": "chunk-1",
      "content": "Place all tokens in the center...",
      "original_score": 0.85,
      "rerank_score": 0.78,
      "metadata": {"page": 5}
    }
  ],
  "model": "BAAI/bge-reranker-v2-m3",
  "processing_time_ms": 45.2
}
```

### GET /health

Health check endpoint.

**Response:**
```json
{
  "status": "healthy",
  "model_loaded": true,
  "model_name": "BAAI/bge-reranker-v2-m3",
  "device": "cpu",
  "uptime_seconds": 3600.5
}
```

## Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `MODEL_NAME` | `BAAI/bge-reranker-v2-m3` | Cross-encoder model to use |
| `BATCH_SIZE` | `32` | Inference batch size |
| `MAX_LENGTH` | `512` | Maximum sequence length |
| `DEVICE` | `cpu` | Device (cpu/cuda) |
| `ENABLE_WARMUP` | `true` | Warmup model on startup |
| `PORT` | `8003` | Service port |
| `HOST` | `0.0.0.0` | Service host |

## Local Development

```bash
# Install dependencies
pip install -r requirements.txt

# Run the service
python main.py

# Run tests
pytest tests/ -v
```

## Docker

```bash
# Build
docker build -t meepleai-reranker .

# Run
docker run -p 8003:8003 meepleai-reranker

# With GPU support
docker run --gpus all -e DEVICE=cuda -p 8003:8003 meepleai-reranker
```

## Performance

- **Cold Start**: ~30-60s (model download on first run, cached thereafter)
- **Warmup**: ~2-5s (initial inference warmup)
- **Inference**: ~30-100ms for 10-50 chunks (CPU), ~10-30ms (GPU)
- **Memory**: ~1.5GB RAM (model + inference)

## Integration

The C# `CrossEncoderRerankerClient` calls this service via HTTP. The `ResilientRetrievalService` orchestrates:

1. Hybrid search (vector + keyword)
2. Call reranker service for top candidates
3. Resolve parent chunks for expanded context
4. Return final ranked results

Graceful degradation: If reranker unavailable, falls back to RRF-fused results.

## Model Information

**BAAI/bge-reranker-v2-m3**:
- Multilingual cross-encoder (100+ languages)
- Optimized for retrieval reranking
- 568M parameters
- Apache 2.0 license

## Related

- ADR-016: Advanced PDF Embedding Pipeline
- Issue #1906: Phase 4 Implementation
- Parent Issue #1901: Full Pipeline Scope
