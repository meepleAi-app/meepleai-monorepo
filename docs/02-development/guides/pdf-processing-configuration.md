# PDF Processing Configuration Guide

**Version**: 1.2
**Last Updated**: 2025-12-13T10:59:23.970Z
**Context**: DocumentProcessing Bounded Context (BGAI-086: Startup validation added)

---

## 📋 Overview

Complete configuration reference for PDF processing pipeline (3-stage fallback architecture).

**Configuration Layers**:
1. **appsettings.json** - Default values, development
2. **appsettings.Production.json** - Production overrides
3. **Environment Variables** - Docker, secrets
4. **Feature Flags (Database)** - Runtime toggles

---

## ⚙️ appsettings.json Configuration

### PDF Processing Section (Actually Implemented)

```json
{
  "PdfProcessing": {
    "MaxFileSizeBytes": 104857600,
    "MaxPageCount": 500,
    "MinPageCount": 1,
    "MinPdfVersion": "1.4",
    "AllowedContentTypes": [ "application/pdf" ],
    "Quality": {
      "MinimumThreshold": 0.80,
      "WarningThreshold": 0.70,
      "MinCharsPerPage": 500
    },
    "Extractor": {
      "Provider": "Orchestrator",
      "Unstructured": {
        "ApiUrl": "http://unstructured-service:8001",
        "TimeoutSeconds": 35,
        "MaxRetries": 3,
        "Strategy": "fast",
        "Language": "ita"
      },
      "SmolDocling": {
        "ApiUrl": "http://smoldocling-service:8002",
        "TimeoutSeconds": 30,
        "MaxRetries": 3
      }
    }
  }
}
```

**✅ BGAI-086: Configuration Validation** - All settings are validated on startup using `IValidateOptions<PdfProcessingOptions>`. Invalid configuration causes immediate application failure with clear error messages.

---

## 🔧 Configuration Reference

### PdfProcessing:Extractor

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Provider` | string | `"Unstructured"` | Extractor provider: `"Unstructured"`, `"SmolDocling"`, `"Docnet"`, `"Orchestrator"` |

**Values**:
- `"Orchestrator"` - 3-stage pipeline with quality-based fallback (✅ default)
- `"Unstructured"` - Fast, RAG-optimized (Stage 1)
- `"SmolDocling"` - VLM for complex layouts (Stage 2)
- `"Docnet"` - Local fallback (Stage 3)

**Use Cases**:
- **Production**: `"Orchestrator"` (recommended - automatic quality routing)
- **Development**: `"Orchestrator"` or `"Unstructured"` (fast iteration)
- **Testing**: Switch providers to compare quality

**Validation**: Provider must match configured extractors in DI container.

---

### PdfProcessing:Extractor:UnstructuredService

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `BaseUrl` | string | `"http://unstructured-service:8001"` | Unstructured Python service URL |
| `TimeoutSeconds` | int | `35` | HTTP request timeout |
| `MaxRetries` | int | `3` | Number of retries on failure (Polly policy) |
| `Strategy` | string | `"fast"` | Extraction strategy: `"fast"` or `"hi_res"` |
| `Language` | string | `"ita"` | Document language hint |

**Strategy Comparison**:

| Strategy | Speed | Quality | Use Case |
|----------|-------|---------|----------|
| `fast` | 1.3s | 0.85 | **Recommended** - Fast, good quality |
| `hi_res` | 4s | 0.90 | Complex layouts, tables, diagrams |

---

### PdfProcessing:Quality

**✅ BGAI-086: Now in appsettings.json with startup validation**

| Setting | Type | Default | Valid Range | Description |
|---------|------|---------|-------------|-------------|
| `MinimumThreshold` | double | `0.80` | 0.0-1.0 | Quality acceptance threshold - extraction fails below |
| `WarningThreshold` | double | `0.70` | 0.0-1.0 | Quality warning threshold - triggers warnings |
| `MinCharsPerPage` | int | `500` | ≥100 | Text coverage threshold (chars/page) |

**Validation Rules** (enforced at startup):
- Thresholds must be between 0.0 and 1.0
- MinCharsPerPage must be ≥100
- Invalid values cause application startup failure with clear error message

**Quality Score Calculation**:
```
TotalScore = (TextCoverage × 0.40) + (Structure × 0.20) + (Tables × 0.20) + (PageCoverage × 0.20)
```

**Thresholds**:
- `≥0.80`: Excellent quality - ~800+ chars/page
- `0.70-0.79`: Good quality - ~700 chars/page
- `0.50-0.69`: Acceptable - ~500 chars/page
- `<0.50`: Poor quality

**Tuning Guide**:
- **High accuracy needed**: `PdfProcessing__Quality__MinimumThreshold=0.85`
- **Accept lower quality**: `PdfProcessing__Quality__MinimumThreshold=0.75`
- **Scanned PDFs**: `PdfProcessing__Quality__MinCharsPerPage=300`

---

### PdfProcessing:Extractor:SmolDocling

**✅ BGAI-086: New configuration with startup validation**

| Setting | Type | Default | Valid Range | Description |
|---------|------|---------|-------------|-------------|
| `ApiUrl` | string | `"http://smoldocling-service:8002"` | Valid HTTP/HTTPS URL | SmolDocling VLM service URL |
| `TimeoutSeconds` | int | `30` | 1-300 | HTTP request timeout |
| `MaxRetries` | int | `3` | 0-10 | Number of retries on failure |

**Validation Rules**:
- ApiUrl must be valid absolute HTTP/HTTPS URL
- TimeoutSeconds must be between 1 and 300
- MaxRetries must be between 0 and 10

---

### PdfProcessing:MaxFileSizeBytes

| Setting | Type | Default | Valid Range | Description |
|---------|------|---------|-------------|-------------|
| `MaxFileSizeBytes` | long | `104857600` | 1KB-500MB | Maximum PDF file size |

**Validation Rules** (BGAI-086):
- **Minimum**: 1 KB (1,024 bytes)
- **Maximum**: 500 MB (524,288,000 bytes)
- Invalid values cause startup failure

---

### PdfProcessing:LargePdfThresholdBytes & UseTempFileForLargePdfs

**✅ BGAI-087: Memory optimization for large PDFs**

| Setting | Type | Default | Valid Range | Description |
|---------|------|---------|-------------|-------------|
| `LargePdfThresholdBytes` | long | `52428800` | 1KB-MaxFileSizeBytes | Threshold for temp file strategy (50 MB) |
| `UseTempFileForLargePdfs` | bool | `true` | true/false | Enable temp file for PDFs ≥threshold |

**Behavior**:
- **Small PDFs** (<50MB): Loaded into memory for fast multi-stage processing
- **Large PDFs** (≥50MB): Saved to temp file to reduce memory pressure
- **Temp File Cleanup**: Automatic cleanup via `IDisposable` pattern

**Use Cases**:
- **Default** (true): Automatic optimization for large rulebook PDFs
- **Memory-Rich Environments** (false): Disable temp files for maximum performance
- **Testing**: Adjust threshold to test memory vs file strategies

**Performance Impact**:
- Memory usage: 50-80% reduction for PDFs ≥50MB
- Extraction speed: Minimal overhead (<5% for temp file I/O)
- Disk usage: Temporary (cleaned up immediately after extraction)

---

## 🐳 Docker Configuration

### docker-compose.yml

```yaml
version: '3.8'

services:
  api:
    build: ./apps/api
    ports: ["8080:8080"]
    environment:
      - ConnectionStrings__Postgres=${POSTGRES_CONNECTION_STRING}
      - PdfProcessing__Extractor__UnstructuredService__BaseUrl=http://unstructured-service:8001
      - PdfProcessing__Quality__MinimumThreshold=0.80
      - PdfProcessing__Quality__MinCharsPerPage=500
    depends_on:
      - unstructured-service
      - postgres
      - qdrant

  unstructured-service:
    build: ./apps/unstructured-service
    ports: ["8001:8001"]
    environment:
      - UNSTRUCTURED_STRATEGY=fast
      - LOG_LEVEL=INFO
      - MAX_WORKERS=3
    volumes:
      - ./data/temp:/tmp

  postgres:
    image: postgres:16
    ports: ["5432:5432"]
    environment:
      - POSTGRES_DB=meepleai
      - POSTGRES_USER=meepleai_user
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}

  qdrant:
    image: qdrant/qdrant:latest
    ports: ["6333:6333"]
    volumes:
      - qdrant-data:/qdrant/storage
```

---

## 🔑 Environment Variables

### Required Variables

```bash
# PostgreSQL Connection
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=meepleai;Username=meepleai_user;Password=<secret>"

# Qdrant Vector Store
QDRANT_URL="http://localhost:6333"

# Feature Flags (optional, defaults to true)
Features__PdfUpload=true
Features__PdfIndexing=true
```

### Optional Variables (Quality Tuning)

```bash
# Override quality thresholds
PdfProcessing__Quality__MinimumThreshold=0.80
PdfProcessing__Quality__MinCharsPerPage=500

# Override extractor URLs
PdfProcessing__Extractor__UnstructuredService__BaseUrl="http://custom-host:8001"
PdfProcessing__Extractor__UnstructuredService__TimeoutSeconds=60

# Performance tuning
PdfProcessing__Extractor__UnstructuredService__Strategy=hi_res
PdfProcessing__Extractor__UnstructuredService__MaxRetries=5
```

---

## 🎛️ Feature Flags (Database-driven)

### Runtime Configuration

Feature flags stored in `SystemConfiguration` table, configurable via Admin UI (`/admin/configuration`).

```sql
-- Check current feature flags
SELECT category, key, value, description
FROM system_configurations
WHERE category = 'Features'
  AND key LIKE 'Pdf%';
```

**Example Output**:
| Category | Key | Value | Description |
|----------|-----|-------|-------------|
| Features | PdfUpload | `true` | Enable PDF upload endpoint |
| Features | PdfIndexing | `true` | Enable PDF indexing workflow |

### Toggle Feature Flags

```bash
# Via Admin UI (recommended)
# Navigate to: http://localhost:3000/admin/configuration
# Find: Features.PdfUpload
# Toggle: ON/OFF

# Via API (programmatic)
curl -X PUT http://localhost:8080/api/v1/admin/configuration \
  -H "Cookie: meepleai-session=<admin-session>" \
  -d '{"category":"Features","key":"PdfUpload","value":"false"}'
```

**Effect**: Immediate (no restart required)

**Use Cases**:
- Maintenance: Disable uploads during database migration
- Load control: Disable indexing during high traffic
- Testing: Enable features in staging, disable in production

---

## 🔬 Quality Threshold Tuning

### Current Default Thresholds

```
MinimumThreshold: 0.80 (80% quality score)
MinCharsPerPage: 500 (text coverage per page)
```

### Tuning Scenarios

**Scenario 1: Low Quality Extractions**

Problem: PDFs consistently fail quality validation

Solution (via environment variables):
```bash
export PdfProcessing__Quality__MinimumThreshold=0.75
export PdfProcessing__Quality__MinCharsPerPage=450
```

**Scenario 2: Users Report Poor Results**

Problem: Quality threshold too lenient, accepting poor extractions

Solution:
```bash
export PdfProcessing__Quality__MinimumThreshold=0.85
export PdfProcessing__Quality__MinCharsPerPage=600
```

**Scenario 3: Scanned PDFs Failing**

Problem: Scanned PDFs have lower text density

Solution:
```bash
# Lower text density requirement
export PdfProcessing__Quality__MinCharsPerPage=300

# Use hi_res strategy for better OCR
export PdfProcessing__Extractor__UnstructuredService__Strategy=hi_res
```

---

## 📈 Example Configurations

### Development (Fast Iteration)

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Unstructured",
      "UnstructuredService": {
        "BaseUrl": "http://localhost:8001",
        "Strategy": "fast",
        "TimeoutSeconds": 15
      }
    },
    "MaxFileSizeBytes": 52428800
  }
}
```

**Quality Overrides** (via environment):
```bash
PdfProcessing__Quality__MinimumThreshold=0.70
PdfProcessing__Quality__MinCharsPerPage=400
```

---

### Staging (Pre-Production Testing)

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Unstructured",
      "UnstructuredService": {
        "BaseUrl": "http://unstructured-service:8001",
        "Strategy": "hi_res",
        "TimeoutSeconds": 60,
        "MaxRetries": 3
      }
    },
    "MaxFileSizeBytes": 104857600
  }
}
```

**Quality Overrides**:
```bash
PdfProcessing__Quality__MinimumThreshold=0.80
PdfProcessing__Quality__MinCharsPerPage=500
```

---

### Production (Optimized)

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Unstructured",
      "UnstructuredService": {
        "BaseUrl": "http://unstructured-prod.internal:8001",
        "Strategy": "fast",
        "TimeoutSeconds": 30,
        "MaxRetries": 5
      }
    },
    "MaxFileSizeBytes": 104857600
  }
}
```

**Quality Overrides**:
```bash
PdfProcessing__Quality__MinimumThreshold=0.85
PdfProcessing__Quality__MinCharsPerPage=500
```

---

## 🛠️ Configuration Troubleshooting

### Issue: "Unstructured service unavailable"

**Check Configuration**:
```bash
# Verify URL is reachable
curl http://unstructured-service:8001/health

# Check configured URL
echo $PdfProcessing__Extractor__UnstructuredService__BaseUrl
```

**Fix**: Update `BaseUrl` in appsettings.json or environment variable

---

### Issue: "Quality threshold never met"

**Check Configuration**:
```bash
# View current thresholds (defaults if not set)
curl http://localhost:8080/api/v1/admin/configuration \
  | jq '.[] | select(.category == "PdfProcessing" and .key contains "Quality")'
```

**Fix**: Set environment variables:
```bash
export PdfProcessing__Quality__MinimumThreshold=0.70
export PdfProcessing__Quality__MinCharsPerPage=400
```

---

### Issue: "File size exceeded"

**Check Configuration**:
```bash
# Check max file size from appsettings.json
grep -A5 "PdfProcessing" apps/api/src/Api/appsettings.json | grep MaxFileSizeBytes
```

**Fix**: Update `MaxFileSizeBytes` in appsettings.json (up to 500 MB recommended)

---

## 📚 References

- [DocumentProcessing README](../../apps/api/src/Api/BoundedContexts/DocumentProcessing/README.md)
- [PDF Processing API](../03-api/pdf-processing-api.md)
- [ADR-003b: Unstructured PDF](../../docs/01-architecture/adr/adr-003b-unstructured-pdf.md)
- [Troubleshooting Guide](./pdf-processing-troubleshooting.md)

---

**Version**: 1.1
**Last Updated**: 2025-12-13T10:59:23.970Z
**Maintainer**: Backend Team
