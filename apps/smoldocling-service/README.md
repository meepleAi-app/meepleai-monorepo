# SmolDocling PDF Extraction Service

FastAPI microservice for VLM-based PDF extraction using SmolDocling (256M parameters).

**Purpose**: Stage 2 of MeepleAI's 3-stage PDF pipeline - fallback for complex layouts (BGAI-005).

---

## Features

- ✅ **Vision-Language Model**: SmolDocling 256M (Apache 2.0)
- ✅ **Complex Layouts**: Multi-column, tables, equations detection
- ✅ **GPU Optional**: Works on CPU (3-5s/page) or GPU (0.5s/page)
- ✅ **Markdown Output**: Structured Markdown with DocTags
- ✅ **Quality Scoring**: VLM-specific 4-metric assessment
- ✅ **Italian Support**: Latin script languages (ita, eng, spa, fra, deu)

---

## Quick Start

### Docker Compose (Recommended)

```bash
# Start service (CPU-only, no GPU required)
cd infra
docker compose up smoldocling-service -d

# Check logs (model download ~500MB on first start)
docker compose logs -f smoldocling-service

# Verify health (wait 2-3 min for model download)
curl http://localhost:8002/health
```

**First Startup**: Model downloads ~513MB from Hugging Face (cached for future use).

### Local Development

```bash
cd apps/smoldocling-service

# Create virtual environment
python3.11 -m venv venv
source venv/bin/activate  # Linux/Mac
venv\Scripts\activate     # Windows

# Install dependencies
pip install -r requirements.txt

# Install system dependencies (Ubuntu/Debian)
sudo apt-get install poppler-utils libgl1

# Run development server
uvicorn src.main:app --reload --port 8002
```

---

## API Reference

### POST /api/v1/extract

Extract text from PDF using SmolDocling VLM.

**Request**:
```http
POST /api/v1/extract HTTP/1.1
Content-Type: multipart/form-data

file: <binary PDF file>
```

**Response** (200 OK):
```json
{
  "text": "Plain text extracted...",
  "markdown": "# Markdown with structure\n\n## Section 1\n...",
  "chunks": [
    {
      "text": "Page 1 content...",
      "page_number": 1,
      "element_type": "Page",
      "metadata": {
        "char_count": 850,
        "has_tables": true,
        "has_equations": false,
        "confidence": 0.85
      }
    }
  ],
  "quality_score": 0.78,
  "page_count": 3,
  "metadata": {
    "extraction_duration_ms": 12500,
    "has_tables": true,
    "has_equations": false,
    "quality_breakdown": {
      "text_coverage_score": 0.38,
      "layout_detection_score": 0.28,
      "confidence_score": 0.85,
      "page_coverage_score": 0.10
    }
  }
}
```

### GET /health

Health check with GPU status.

**Response**:
```json
{
  "status": "healthy",
  "timestamp": "2025-01-15T12:00:00Z",
  "checks": {
    "model_initialized": "ok",
    "device_available": "ok",
    "disk_space": "ok"
  },
  "gpu_info": {
    "device_count": 1,
    "device_name": "NVIDIA RTX 3060",
    "memory_allocated_mb": 450.3,
    "memory_reserved_mb": 512.0
  }
}
```

---

## Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `DEVICE` | `auto` | Device (cuda, cpu, auto) |
| `MODEL_NAME` | `docling-project/SmolDocling-256M-preview` | Hugging Face model |
| `MAX_NEW_TOKENS` | `2048` | Max tokens per page |
| `TORCH_DTYPE` | `bfloat16` | Model precision |
| `IMAGE_DPI` | `300` | PDF→Image DPI |
| `QUALITY_THRESHOLD` | `0.70` | Min quality (lower than Stage 1) |
| `TIMEOUT` | `60` | Extraction timeout (seconds) |
| `ENABLE_MODEL_WARMUP` | `true` | Warmup on startup |

---

## Performance

### Benchmarks (Estimated)

| Hardware | Time/Page | Total (20 pages) |
|----------|-----------|------------------|
| **CPU (4 cores)** | 3-5s | 60-100s (~1.5 min) |
| **GPU (RTX 3060)** | 0.5-0.8s | 10-16s |
| **GPU (A100)** | 0.35s | 7s |

### Resource Requirements

- **CPU**: 2-4 cores (3-5s/page)
- **GPU**: Optional, 500MB VRAM (0.5s/page)
- **RAM**: 2-3GB
- **Disk**: 1GB (model cache: 513MB)

---

## Quality Score Breakdown

Stage 2 scoring (VLM-specific):

| Metric | Weight | Description |
|--------|--------|-------------|
| **Text Coverage** | 40% | Chars per page (300+ = good) |
| **Layout Detection** | 30% | Tables, equations, structure |
| **Confidence** | 20% | VLM inference confidence |
| **Page Coverage** | 10% | All pages processed |

**Threshold**: ≥0.70 for Stage 2 acceptance (lower than Stage 1 due to fallback nature)

---

## Architecture

```
apps/smoldocling-service/
├── src/
│   ├── api/                # FastAPI controllers
│   │   └── schemas.py     # Pydantic DTOs
│   ├── application/        # Business logic
│   │   ├── pdf_extraction_service.py
│   │   └── quality_calculator.py
│   ├── domain/             # Domain models
│   │   └── models.py
│   ├── infrastructure/     # External adapters
│   │   ├── pdf_converter.py       # PDF→Image
│   │   ├── smoldocling_adapter.py # VLM inference
│   │   └── file_storage.py        # Temp files
│   ├── config/             # Configuration
│   │   └── settings.py
│   └── main.py             # FastAPI app
├── tests/                  # (Future: pytest tests)
├── Dockerfile              # CPU + GPU support
└── requirements.txt
```

---

## Troubleshooting

### "Model download slow"

```bash
# Pre-download model
docker exec meepleai-smoldocling python -c "
from transformers import AutoProcessor, AutoModelForVision2Seq
AutoProcessor.from_pretrained('docling-project/SmolDocling-256M-preview')
AutoModelForVision2Seq.from_pretrained('docling-project/SmolDocling-256M-preview')
print('Model cached!')
"
```

### "CUDA out of memory"

```bash
# Switch to CPU mode
docker compose stop smoldocling-service
# Edit infra/docker-compose.yml: DEVICE=cpu
docker compose up smoldocling-service -d
```

### "Processing too slow (>10s/page)"

- **Use GPU**: Set `DEVICE=cuda` and uncomment deploy section
- **Reduce DPI**: `IMAGE_DPI=200` (faster but lower quality)
- **Reduce tokens**: `MAX_NEW_TOKENS=1024` (less detailed output)

---

## Integration with MeepleAI

**3-Stage Pipeline**:
1. **Stage 1**: Unstructured (fast, 1.3s) - Primary
2. **Stage 2**: SmolDocling (VLM, 3-5s CPU) - **This Service** (complex layouts fallback)
3. **Stage 3**: Docnet (basic) - Final fallback

**Trigger**: Stage 1 quality < 0.80 → Route to Stage 2

---

## License

Apache 2.0 (matches SmolDocling model license)

---

## Links

- [SmolDocling on Hugging Face](https://huggingface.co/docling-project/SmolDocling-256M-preview)
- [Docling Documentation](https://github.com/docling-project/docling)
- [Issue #945 (BGAI-005)](https://github.com/DegrassiAaron/meepleai-monorepo/issues/945)

---

**Version**: 1.0.0
**Status**: MVP Implementation
**Maintainer**: MeepleAI Team
