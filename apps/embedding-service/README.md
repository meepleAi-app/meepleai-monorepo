# MeepleAI Embedding Service

Local multilingual embedding service using sentence-transformers.

## Overview

FastAPI microservice providing embeddings for 5 languages (EN, IT, DE, FR, ES) using the `intfloat/multilingual-e5-large` model.

**AI-09**: Multi-language embeddings support (LOCAL implementation)

## Features

- **Multilingual Support**: EN, IT, DE, FR, ES
- **High Quality**: Uses intfloat/multilingual-e5-large (1024-dimensional embeddings)
- **Fast**: GPU support (CUDA) with CPU fallback
- **Production Ready**: Health checks, structured logging, error handling
- **Docker Support**: Containerized with pre-downloaded model

## API Endpoints

### `POST /embeddings`

Generate embeddings for input texts.

**Request**:
```json
{
  "texts": ["Text to embed", "Another text"],
  "language": "en",
  "purpose": "passage"
}
```

**`purpose`** (optional, default `"passage"`) — the e5 instruction prefix to apply. The
`multilingual-e5` family is trained asymmetrically: the retrieval question and the indexed
text must be encoded with **different** prefixes.

| `purpose` | prefix applied | use for |
|---|---|---|
| `"query"` | `query: ` | a search question |
| `"passage"` | `passage: ` | a document chunk being indexed |

Both sides go through this one endpoint, so the caller has to declare which it is — the
service cannot tell from the text. Sending the wrong side degrades retrieval silently: on the
real corpus (56.367 chunk, 127 manuali) encoding a question as a passage pushed the expected
manual's best chunk from cosine rank 1 down to rank 10 ([#3737](https://github.com/meepleAi-app/meepleai-monorepo/issues/3737)).

The default is `"passage"` because that is what the service applied unconditionally before
#3737 — an older client keeps its behaviour, and chunks already indexed stay valid.

**Response**:
```json
{
  "embeddings": [[0.123, -0.456, ...], [0.789, -0.012, ...]],
  "model": "intfloat/multilingual-e5-large",
  "dimension": 1024,
  "count": 2
}
```

**Languages**: `en`, `it`, `de`, `fr`, `es`

### `GET /health`

Health check endpoint for readiness probes.

**Response**:
```json
{
  "status": "healthy",
  "model": "intfloat/multilingual-e5-large",
  "device": "cpu",
  "supported_languages": ["en", "it", "de", "fr", "es"]
}
```

### `GET /`

Service information endpoint.

### `GET /docs`

Interactive API documentation (Swagger UI).

## Local Development

### Prerequisites

- Python 3.11+
- 8GB+ RAM (model is ~2GB)
- Optional: CUDA-capable GPU

### Setup

```bash
# Create virtual environment
python -m venv venv
source venv/bin/activate  # Windows: venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Run server
python main.py
```

Service will be available at `http://localhost:8000`

### Testing

```bash
# Health check
curl http://localhost:8000/health

# Generate embeddings for a document chunk (indexing side)
curl -X POST http://localhost:8000/embeddings \
  -H "Content-Type: application/json" \
  -d '{
    "texts": ["The quick brown fox jumps over the lazy dog"],
    "language": "en",
    "purpose": "passage"
  }'

# Generate an embedding for a search question (retrieval side)
curl -X POST http://localhost:8000/embeddings \
  -H "Content-Type: application/json" \
  -d '{
    "texts": ["How do I set up the board in Catan?"],
    "language": "en",
    "purpose": "query"
  }'
```

## Docker

### Build

```bash
cd apps/embedding-service
docker build -t meepleai-embedding-service:latest .
```

### Run

```bash
docker run -p 8000:8000 meepleai-embedding-service:latest
```

### Docker Compose

See `infra/docker-compose.yml` for integration with the full MeepleAI stack.

## Model Information

**Model**: `intfloat/multilingual-e5-large`
- **Dimension**: 1024
- **Languages**: 100+ (we support EN, IT, DE, FR, ES)
- **Training**: Multilingual E5 trained on 1B+ text pairs
- **Paper**: [Text Embeddings by Weakly-Supervised Contrastive Pre-training](https://arxiv.org/abs/2212.03533)
- **Hugging Face**: [intfloat/multilingual-e5-large](https://huggingface.co/intfloat/multilingual-e5-large)

## Architecture

```
┌──────────────────┐
│   API Request    │
│ POST /embeddings │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  FastAPI Router  │
│  Validation      │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ SentenceTransf.  │
│ multilingual-e5  │
│  (1024-dim)      │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  L2 Normalize    │
│  JSON Serialize  │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  Response        │
│  Embeddings[]    │
└──────────────────┘
```

## Configuration

Environment variables:

- `MODEL_NAME`: Model to load (default: `intfloat/multilingual-e5-large`)
- `DEVICE`: Compute device (default: auto-detect CUDA/CPU)
- `LOG_LEVEL`: Logging level (default: `INFO`)

## Performance

**Latency** (CPU, 10 texts):
- ~500ms for batch of 10 short texts
- ~2s for batch of 10 long texts (500+ chars)

**GPU**: 5-10x faster with CUDA

**Memory**: ~2GB RAM for model + overhead

## Troubleshooting

**Model download fails**:
- Check internet connection
- Hugging Face may be rate-limiting, retry later
- Model is cached in `~/.cache/huggingface/`

**Out of memory**:
- Reduce batch size (max 100 texts per request)
- Use CPU instead of GPU
- Increase Docker memory limit

**Slow startup**:
- Model download + load takes 30-60s on first run
- Subsequent runs are faster (cached)
- Use Docker image with pre-downloaded model

## Related

- **AI-09**: Multi-language embeddings support
- **EmbeddingService** (`apps/api/src/Api/Services/EmbeddingService.cs`): .NET client with fallback chain
- **QdrantService** (`apps/api/src/Api/Services/QdrantService.cs`): Vector storage with language metadata
