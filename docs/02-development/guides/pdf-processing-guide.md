# PDF Processing Guide

**Comprehensive guide for PDF processing configuration, setup, and troubleshooting**

---

## Table of Contents

1. [Overview](#overview)
2. [Configuration](#configuration)
3. [Unstructured Setup](#unstructured-setup)
4. [Troubleshooting](#troubleshooting)

---

## Overview

MeepleAI uses a **3-stage fallback PDF processing pipeline**:

```
Stage 1: Unstructured (≥0.80 quality) - 80% success, 1.3s avg
Stage 2: SmolDocling VLM (≥0.70 quality) - 15% fallback, 3-5s avg
Stage 3: Docnet (best effort) - 5% fallback, fast
```

See **ADR-003b** (`docs/01-architecture/adr/adr-003b-unstructured-pdf.md`) for architecture details.

---

## Configuration

### appsettings.json

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Orchestrator",  // Use 3-stage pipeline (recommended)
      "TimeoutSeconds": 30
    },
    "Quality": {
      "MinimumThreshold": 0.80,   // Minimum acceptable quality score
      "MinCharsPerPage": 500,      // Minimum characters per page
      "EnableDetailedReports": true
    },
    "Unstructured": {
      "BaseUrl": "http://localhost:8001",
      "Strategy": "hi_res",         // Options: hi_res, fast, auto
      "Languages": ["ita", "eng"],  // OCR languages
      "ChunkingStrategy": "by_title",
      "MaxChunkSize": 1000
    },
    "SmolDocling": {
      "BaseUrl": "http://localhost:8002",
      "Model": "smoldocling-256m",
      "EnableVisionEnhancement": true
    },
    "Docnet": {
      "DpiResolution": 300,
      "EnableOCR": true
    }
  }
}
```

### Environment Variables

```bash
# Docker Compose (infra/.env)
UNSTRUCTURED_VERSION=0.10.30
SMOLDOCLING_VERSION=latest
PDF_PROCESSING_TIMEOUT=30
PDF_QUALITY_THRESHOLD=0.80
```

---

## Unstructured Setup

### Docker Deployment (Recommended)

**Already included in `docker-compose.yml`**:

```yaml
services:
  unstructured:
    image: quay.io/unstructured-io/unstructured-api:0.10.30
    ports:
      - "8001:8000"
    environment:
      - UNSTRUCTURED_ALLOWED_ORIGINS=*
    volumes:
      - ./temp:/app/temp
```

**Start**:
```bash
cd infra
docker compose up -d unstructured
```

**Verify**:
```bash
curl http://localhost:8001/general/v0/general
# Should return: {"status": "ok"}
```

### Local Development (Alternative)

**Install with pip**:
```bash
pip install "unstructured[all-docs]==0.10.30"
pip install "unstructured-api-tools==0.10.11"
```

**Run API server**:
```bash
uvicorn unstructured_api.main:app --host 0.0.0.0 --port 8001
```

### Testing

```bash
# Test Unstructured endpoint
curl -X POST http://localhost:8001/general/v0/general \
  -F "files=@test-rulebook.pdf" \
  -F "strategy=hi_res" \
  -F "languages=ita"
```

---

## Troubleshooting

### Common Issues

#### 1. Unstructured Container Not Starting

**Symptom**: `docker compose up unstructured` fails

**Diagnosis**:
```bash
docker logs unstructured
```

**Solutions**:
- **Memory limit**: Increase Docker memory (8GB+ recommended)
```yaml
# docker-compose.yml
services:
  unstructured:
    deploy:
      resources:
        limits:
          memory: 8G
```

- **Port conflict**: Check if port 8001 already in use
```bash
lsof -i :8001  # macOS/Linux
netstat -ano | findstr :8001  # Windows
```

---

#### 2. PDF Processing Timeout

**Symptom**: `PdfProcessingException: Timeout after 30s`

**Diagnosis**:
```bash
# Check PDF size
ls -lh /path/to/rulebook.pdf

# Check processing logs
docker logs api | grep "PDF processing"
```

**Solutions**:
- **Increase timeout**: Update `appsettings.json`
```json
{
  "PdfProcessing": {
    "Extractor": {
      "TimeoutSeconds": 60  // Increase for large PDFs
    }
  }
}
```

- **Reduce DPI**: Lower resolution for faster processing
```json
{
  "PdfProcessing": {
    "Docnet": {
      "DpiResolution": 150  // Default: 300
    }
  }
}
```

---

#### 3. Low Quality Score (< 0.70)

**Symptom**: `Quality score 0.45 below threshold 0.70`

**Diagnosis**:
```csharp
// Check quality report
GET /api/v1/pdf/{documentId}/quality

// Response
{
  "score": 0.45,
  "metrics": {
    "textCoverage": 0.30,      // LOW: Scanned PDF?
    "structureDetection": 0.50, // MEDIUM
    "tableDetection": 0.60,
    "pageCoverage": 0.40        // LOW: Missing pages?
  },
  "recommendations": [
    "Consider re-scanning PDF at higher DPI",
    "Some pages appear to be images without OCR"
  ]
}
```

**Solutions**:
- **Enable hi-res strategy**:
```json
{
  "PdfProcessing": {
    "Unstructured": {
      "Strategy": "hi_res",  // Use high-resolution mode
      "EnableOCR": true
    }
  }
}
```

- **Add missing languages**:
```json
{
  "PdfProcessing": {
    "Unstructured": {
      "Languages": ["ita", "eng", "fra", "deu"]  // Add all languages in PDF
    }
  }
}
```

- **Lower threshold** (temporary workaround):
```json
{
  "PdfProcessing": {
    "Quality": {
      "MinimumThreshold": 0.60  // Temporarily accept lower quality
    }
  }
}
```

---

#### 4. SmolDocling VLM Fallback Always Triggered

**Symptom**: All PDFs fall back to SmolDocling (Stage 2)

**Diagnosis**:
```bash
# Check Unstructured health
curl http://localhost:8001/health

# Check logs
docker logs unstructured | tail -50
```

**Solutions**:
- **Restart Unstructured**:
```bash
docker restart unstructured
```

- **Check API key** (if using cloud Unstructured):
```bash
# Verify environment variable
echo $UNSTRUCTURED_API_KEY
```

---

#### 5. Empty Text Extraction

**Symptom**: `Extracted text is empty` or very short

**Diagnosis**:
- **Check PDF type**: Scanned image vs. native text
```bash
# Install pdfinfo
sudo apt install poppler-utils  # Linux
brew install poppler            # macOS

# Check PDF info
pdfinfo rulebook.pdf
```

**Solutions**:
- **Scanned PDF**: Enable OCR
```json
{
  "PdfProcessing": {
    "Unstructured": {
      "Strategy": "hi_res",
      "EnableOCR": true,
      "Languages": ["ita"]
    }
  }
}
```

- **Encrypted PDF**: Remove password protection
```bash
# Install qpdf
sudo apt install qpdf

# Remove password
qpdf --password=PASSWORD --decrypt input.pdf output.pdf
```

---

#### 6. Performance Degradation

**Symptom**: PDF processing takes >10s for small files

**Diagnosis**:
```bash
# Check container resource usage
docker stats unstructured

# Check disk I/O
docker exec unstructured df -h
```

**Solutions**:
- **Clear temp files**:
```bash
docker exec unstructured rm -rf /app/temp/*
```

- **Restart container**:
```bash
docker restart unstructured
```

- **Use fast strategy** for simple PDFs:
```json
{
  "PdfProcessing": {
    "Unstructured": {
      "Strategy": "fast"  // Faster, lower quality
    }
  }
}
```

---

### Monitoring & Debugging

#### Enable Detailed Logging

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "MeepleAi.Api.BoundedContexts.DocumentProcessing": "Debug"
    }
  }
}
```

#### Check Processing Metrics

```bash
# View metrics in Prometheus
curl http://localhost:9090/api/v1/query?query=pdf_processing_duration_seconds

# View logs in HyperDX
# http://localhost:8180
# Query: @Message like '%PDF processing%'
```

---

## See Also

- **ADR-003b**: `docs/01-architecture/adr/adr-003b-unstructured-pdf.md` - Architecture decision
- **API Spec**: `docs/03-api/board-game-ai-api-specification.md` - PDF upload endpoints
- **Testing**: `docs/02-development/testing/testing-specialized.md` - PDF processing tests
