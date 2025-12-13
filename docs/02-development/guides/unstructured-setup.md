# Unstructured PDF Extraction - Developer Setup Guide

Quick start guide for the Unstructured PDF extraction microservice (Issue #952 / BGAI-001-v2).

---

## Prerequisites

- Docker & Docker Compose
- Python 3.11+ (for local development)
- .NET 9 SDK (for C# backend)

---

## Quick Start (Docker Compose)

### 1. Start Unstructured Service

```bash
# From project root
cd infra
docker compose up unstructured-service -d

# Check logs
docker compose logs -f unstructured-service

# Verify health
curl http://localhost:8001/health
```

**Expected Response**:
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

### 2. Test Extraction

```bash
# Test with sample PDF
curl -X POST http://localhost:8001/api/v1/extract \
  -F "file=@/path/to/test.pdf" \
  -F "strategy=fast" \
  -F "language=ita"
```

**Expected**: JSON response with extracted text, chunks, and quality score ≥0.80

---

## Backend Integration

### Configuration (appsettings.json)

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Unstructured",
      "UnstructuredService": {
        "BaseUrl": "http://unstructured-service:8001",
        "TimeoutSeconds": 35,
        "MaxRetries": 3,
        "Strategy": "fast",
        "Language": "ita"
      }
    }
  }
}
```

### Switch to Docnet (Fallback)

Change `Provider` to `"Docnet"` to use existing Docnet extractor:

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Docnet"
    }
  }
}
```

**No code changes needed** - dependency injection handles the switch automatically.

---

## Local Development (Python)

### 1. Install Dependencies

```bash
cd apps/unstructured-service

# Create virtual environment
python3.11 -m venv venv
source venv/bin/activate  # Linux/Mac
venv\Scripts\activate     # Windows

# Install packages
pip install -r requirements.txt

# Install system dependencies (Ubuntu/Debian)
sudo apt-get update
sudo apt-get install -y \
  tesseract-ocr \
  tesseract-ocr-ita \
  poppler-utils \
  libmagic-dev
```

### 2. Configure Environment

```bash
# Copy example env file
cp .env.example .env

# Edit configuration (optional)
nano .env
```

### 3. Run Development Server

```bash
# Run with uvicorn (auto-reload)
uvicorn src.main:app --reload --port 8001

# Or run main.py directly
python -m src.main
```

**Server**: http://localhost:8001
**Docs**: http://localhost:8001/docs (Swagger UI)

### 4. Run Tests

```bash
# All tests
pytest

# With coverage
pytest --cov=src --cov-report=html

# Open coverage report
open htmlcov/index.html  # Mac
xdg-open htmlcov/index.html  # Linux
start htmlcov/index.html  # Windows
```

---

## Testing the Integration

### 1. Start Full Stack

```bash
# Terminal 1: Infrastructure
cd infra
docker compose up postgres qdrant redis unstructured-service

# Terminal 2: Backend API
cd apps/api/src/Api
dotnet run

# Verify API started
curl http://localhost:8080/health
```

### 2. Upload Test PDF

```bash
# Create test user
curl -X POST http://localhost:8080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "displayName": "Test User"
  }'

# Login
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }' \
  -c cookies.txt

# Upload PDF (uses Unstructured behind the scenes)
curl -X POST http://localhost:8080/api/v1/pdfs/upload \
  -H "Cookie: $(cat cookies.txt)" \
  -F "file=@/path/to/italian-rulebook.pdf" \
  -F "gameId=123e4567-e89b-12d3-a456-426614174000"
```

### 3. Verify Extraction

Check logs for quality score:

```bash
# Unstructured service logs
docker compose logs unstructured-service | grep "quality"

# Backend API logs
cd apps/api/src/Api && dotnet run | grep "Unstructured"
```

**Expected**:
```
INFO: Extraction completed: quality=0.85
INFO: Unstructured extraction completed. Pages=20, Quality=High (0.85)
```

---

## Troubleshooting

### Issue: "Unstructured library check failed"

**Solution**:
```bash
# Reinstall Unstructured
pip uninstall unstructured unstructured-inference
pip install "unstructured[pdf]==0.18.18" unstructured-inference>=0.7.1

# Verify
python -c "from unstructured.partition.pdf import partition_pdf; print('OK')"
```

### Issue: "tesseract-ocr-ita not found"

**Solution**:
```bash
# Install Italian language pack
sudo apt-get install tesseract-ocr-ita

# Verify
tesseract --list-langs | grep ita
```

### Issue: "Connection refused to unstructured-service:8001"

**Solution**:
```bash
# Check service is running
docker compose ps unstructured-service

# Check health
curl http://localhost:8001/health

# View logs
docker compose logs unstructured-service

# Restart service
docker compose restart unstructured-service
```

### Issue: "Quality score always low (<0.60)"

**Possible Causes**:
1. PDF is scanned image (needs OCR)
2. PDF is encrypted/password-protected
3. Complex multi-column layout

**Solutions**:
```json
// Try hi_res strategy (better for complex layouts)
{
  "PdfProcessing": {
    "Extractor": {
      "UnstructuredService": {
        "Strategy": "hi_res"  // Changed from "fast"
      }
    }
  }
}
```

Or fallback to Stage 2 (SmolDocling, Issue #945)

---

## Performance Tuning

### Fast Strategy (Default)

```json
{
  "PdfProcessing": {
    "Extractor": {
      "UnstructuredService": {
        "Strategy": "fast"
      }
    }
  }
}
```

**Pros**: ~1.1-1.3s processing
**Cons**: Lower quality on complex layouts
**Use For**: Simple rulebooks, text-heavy documents

### Hi-Res Strategy

```json
{
  "PdfProcessing": {
    "Extractor": {
      "UnstructuredService": {
        "Strategy": "hi_res"
      }
    }
  }
}
```

**Pros**: Better structure detection, table accuracy
**Cons**: ~3-5s processing
**Use For**: Complex layouts, multi-column text, heavy tables

---

## Monitoring

### Prometheus Metrics (Future)

Service exposes `/metrics` endpoint for Prometheus:

- `pdf_extraction_requests_total` - Total requests
- `pdf_extraction_duration_seconds` - Histogram
- `pdf_extraction_quality_score` - Histogram
- `pdf_extraction_errors_total` - Error counter

### Health Check Integration

Add to monitoring stack:

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'unstructured-service'
    static_configs:
      - targets: ['unstructured-service:8001']
    metrics_path: '/metrics'
```

---

## Development Workflow

### Making Changes to Python Service

```bash
# 1. Make code changes in apps/unstructured-service/src/

# 2. Run tests
cd apps/unstructured-service
pytest

# 3. Rebuild Docker image
cd ../../infra
docker compose build unstructured-service

# 4. Restart service
docker compose up unstructured-service -d

# 5. Verify changes
curl http://localhost:8001/health
```

### Making Changes to C# Adapter

```bash
# 1. Edit UnstructuredPdfTextExtractor.cs

# 2. Build backend
cd apps/api
dotnet build

# 3. Run tests
dotnet test

# 4. Restart API
dotnet run --project src/Api
```

---

## Next Steps

After Unstructured integration:

1. **Issue #945** (BGAI-005): Implement SmolDocling service (Stage 2)
2. **Issue #949** (BGAI-010): Enhanced orchestrator (3-stage routing)
3. **Issue #950** (BGAI-011): End-to-end tests (all 3 stages)

---

## References

- [README](../../apps/unstructured-service/README.md) - Service documentation
- [ADR-003](../architecture/adr-003-unstructured-pdf-extraction.md) - Architecture decision
- [Unstructured Docs](https://unstructured-io.github.io/unstructured/)
- [Issue #952](https://github.com/DegrassiAaron/meepleai-monorepo/issues/952)

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Status**: Production Ready

