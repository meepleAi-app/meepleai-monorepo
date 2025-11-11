# PDF Extraction Microservice (Unstructured)

FastAPI microservice for PDF text extraction using the Unstructured library (Apache 2.0).

**Purpose**: Stage 1 of MeepleAI's 3-stage PDF processing pipeline (BGAI-001-v2).

---

## Features

- ✅ **Semantic Chunking**: Text chunks optimized for RAG workflows
- ✅ **Italian Language**: Native support via tesseract-ocr-ita
- ✅ **Quality Scoring**: 4-metric quality assessment (0.0-1.0)
- ✅ **Table Detection**: Automatic table structure inference
- ✅ **Fast Performance**: <2s for 20-page PDFs (fast strategy)
- ✅ **Health Checks**: Monitoring-ready with /health endpoint
- ✅ **Docker Ready**: Production Dockerfile with multi-stage build

---

## Quick Start

### Local Development

```bash
# Install dependencies
pip install -r requirements.txt

# Install system dependencies (Ubuntu/Debian)
sudo apt-get install tesseract-ocr tesseract-ocr-ita poppler-utils libmagic-dev

# Run development server
uvicorn src.main:app --reload --port 8001

# Run tests
pytest

# Run with coverage
pytest --cov=src --cov-report=html
```

### Docker Deployment

```bash
# Build image
docker build -t meepleai-unstructured:latest .

# Run container
docker run -p 8001:8001 \
  -e LOG_LEVEL=INFO \
  -e UNSTRUCTURED_STRATEGY=fast \
  -e LANGUAGE=ita \
  meepleai-unstructured:latest

# Check health
curl http://localhost:8001/health
```

### Docker Compose

```bash
# From project root
cd infra
docker compose up unstructured-service

# Check logs
docker compose logs -f unstructured-service

# Stop service
docker compose down unstructured-service
```

---

## API Reference

### POST /api/v1/extract

Extract text and metadata from PDF file.

**Request**:
```http
POST /api/v1/extract HTTP/1.1
Content-Type: multipart/form-data

file: <binary PDF file>
strategy: fast | hi_res (default: fast)
language: ita | eng | ... (default: ita)
```

**Response** (200 OK):
```json
{
  "text": "Full extracted text...",
  "chunks": [
    {
      "text": "Chunk text content",
      "page_number": 1,
      "element_type": "Title",
      "metadata": {"char_count": 150}
    }
  ],
  "quality_score": 0.85,
  "page_count": 20,
  "metadata": {
    "extraction_duration_ms": 1250,
    "strategy_used": "fast",
    "language": "ita",
    "detected_tables": 3,
    "detected_structures": ["Title", "Paragraph", "Table"],
    "quality_breakdown": {
      "text_coverage_score": 0.40,
      "structure_detection_score": 0.18,
      "table_detection_score": 0.15,
      "page_coverage_score": 0.20
    }
  }
}
```

**Error Responses**:
- `400`: Invalid request
- `413`: File too large (>50MB)
- `415`: Unsupported media type (not PDF)
- `422`: Unprocessable PDF (corrupted/encrypted)
- `500`: Internal server error

### GET /health

Health check endpoint for monitoring.

**Response** (200 OK):
```json
{
  "status": "healthy",
  "timestamp": "2025-01-15T10:30:00Z",
  "checks": {
    "unstructured_library": "ok",
    "disk_space": "ok",
    "memory": "ok"
  }
}
```

---

## Configuration

Environment variables (see `.env.example`):

| Variable | Default | Description |
|----------|---------|-------------|
| `LOG_LEVEL` | `INFO` | Logging level (DEBUG, INFO, WARNING, ERROR) |
| `MAX_FILE_SIZE` | `52428800` | Max file size in bytes (50MB) |
| `TIMEOUT` | `30` | Extraction timeout in seconds |
| `UNSTRUCTURED_STRATEGY` | `fast` | Extraction strategy (fast or hi_res) |
| `LANGUAGE` | `ita` | Default document language |
| `CHUNK_MAX_CHARACTERS` | `2000` | Max characters per chunk |
| `CHUNK_OVERLAP` | `200` | Character overlap between chunks |
| `QUALITY_THRESHOLD` | `0.80` | Minimum quality score threshold |
| `MIN_CHARS_PER_PAGE` | `500` | Minimum expected chars per page |
| `WORKERS` | `4` | Number of Gunicorn workers |

---

## Quality Score Breakdown

Quality score is calculated from 4 metrics (total 0.0-1.0):

| Metric | Weight | Description |
|--------|--------|-------------|
| **Text Coverage** | 40% | Sufficient text extracted (chars per page) |
| **Structure Detection** | 20% | Titles, headers, lists detected |
| **Table Detection** | 20% | Tables correctly identified |
| **Page Coverage** | 20% | All pages processed |

**Thresholds**:
- ≥0.80: High quality (acceptable for RAG)
- 0.60-0.79: Medium quality (consider fallback)
- <0.60: Low quality (fallback to Stage 2)

---

## Architecture

Clean Architecture layers:

```
src/
├── api/              # FastAPI controllers (HTTP layer)
│   └── schemas.py   # Request/response DTOs
├── application/      # Business logic
│   ├── pdf_extraction_service.py
│   └── quality_calculator.py
├── domain/           # Domain models
│   └── models.py
├── infrastructure/   # External adapters
│   ├── unstructured_adapter.py
│   └── file_storage.py
├── config/           # Configuration
│   └── settings.py
└── main.py           # FastAPI app entry point
```

---

## Testing

```bash
# Run all tests
pytest

# Run with coverage
pytest --cov=src --cov-report=html

# Run specific test file
pytest tests/test_quality_calculator.py -v

# Run unit tests only
pytest -m unit

# Run integration tests only
pytest -m integration
```

**Test Coverage**: ≥80% required

---

## Performance

### Benchmarks (20-page Italian PDFs)

| Strategy | Avg Time | Quality | Use Case |
|----------|----------|---------|----------|
| `fast` | ~1.1-1.3s | High | Default (RAG-optimized) |
| `hi_res` | ~3-5s | Excellent | Complex layouts |

### Resource Requirements

- **CPU**: 2+ cores recommended
- **Memory**: 2GB+ per worker
- **Disk**: 1GB+ free space (temp files)

---

## Troubleshooting

### Service won't start

```bash
# Check Tesseract installation
tesseract --list-langs
# Should include: eng, ita

# Check Unstructured library
python -c "from unstructured.partition.pdf import partition_pdf; print('OK')"

# Check disk space
df -h /tmp
```

### Low quality scores

- Check PDF quality (scanned images need OCR)
- Try `hi_res` strategy for complex layouts
- Verify Italian language pack installed
- Check logs for extraction warnings

### Timeout errors

- Increase `TIMEOUT` environment variable
- Reduce `WORKERS` count (less concurrency = more memory per worker)
- Use `fast` strategy instead of `hi_res`

---

## Integration with MeepleAI Backend

C# ASP.NET Core backend calls this service via HttpClient:

```csharp
// appsettings.json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Unstructured",
      "UnstructuredService": {
        "BaseUrl": "http://unstructured-service:8001",
        "TimeoutSeconds": 35,
        "MaxRetries": 3
      }
    }
  }
}
```

Service automatically selected when `Provider = "Unstructured"`.

---

## License

Apache 2.0 (matches Unstructured library license)

---

## Links

- [Unstructured Documentation](https://unstructured-io.github.io/unstructured/)
- [FastAPI Documentation](https://fastapi.tiangolo.com/)
- [MeepleAI Architecture](../../docs/architecture/pdf-extraction-opensource-alternatives.md)
- [Issue #952 (BGAI-001-v2)](https://github.com/DegrassiAaron/meepleai-monorepo/issues/952)

---

**Version**: 1.0.0
**Status**: Production Ready
**Maintainer**: MeepleAI Team
