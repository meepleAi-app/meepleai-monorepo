# PDF Processing Configuration Guide

**Version**: 1.0
**Last Updated**: 2025-11-13
**Context**: DocumentProcessing Bounded Context

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

### PDF Processing Section

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Orchestrator",
      "TimeoutSeconds": 120
    },
    "Quality": {
      "MinimumThreshold": 0.80,
      "WarningThreshold": 0.70,
      "CriticalThreshold": 0.50,
      "MinCharsPerPage": 500
    },
    "MaxFileSizeBytes": 104857600,
    "Unstructured": {
      "ApiUrl": "http://unstructured-service:8001",
      "TimeoutSeconds": 30,
      "Strategy": "fast",
      "RetryCount": 3,
      "RetryDelaySeconds": 2
    },
    "SmolDocling": {
      "ApiUrl": "http://smoldocling-service:8002",
      "TimeoutSeconds": 60,
      "Device": "cpu",
      "RetryCount": 2,
      "RetryDelaySeconds": 5
    },
    "Docnet": {
      "EnableOcr": true,
      "OcrLanguage": "ita",
      "TesseractDataPath": "/usr/share/tesseract-ocr/tessdata"
    }
  }
}
```

---

## 🔧 Configuration Reference

### PdfProcessing:Extractor

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Provider` | string | `"Orchestrator"` | Which extractor to use: `"Unstructured"`, `"SmolDocling"`, `"Docnet"`, `"Orchestrator"` (3-stage) |
| `TimeoutSeconds` | int | `120` | Max time for entire extraction pipeline |

**Values**:
- `"Orchestrator"` (recommended): 3-stage pipeline with quality-based fallback
- `"Unstructured"`: Stage 1 only (fast, RAG-optimized)
- `"SmolDocling"`: Stage 2 only (VLM, complex layouts)
- `"Docnet"`: Stage 3 only (local fallback, best effort)

**Use Cases**:
- **Production**: `"Orchestrator"` (highest success rate)
- **Development**: `"Unstructured"` (fast iteration)
- **GPU Testing**: `"SmolDocling"` (test VLM quality)
- **Offline/Fallback**: `"Docnet"` (no external services)

---

### PdfProcessing:Quality

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MinimumThreshold` | double | `0.80` | Stage 1 acceptance threshold (0.0-1.0) |
| `WarningThreshold` | double | `0.70` | Stage 2 acceptance threshold |
| `CriticalThreshold` | double | `0.50` | Stage 3 minimum quality |
| `MinCharsPerPage` | int | `500` | Text coverage threshold (chars/page) |

**Quality Score Calculation**:
```
TotalScore = (TextCoverage × 0.40) + (Structure × 0.20) + (Tables × 0.20) + (PageCoverage × 0.20)
```

**Thresholds**:
- `≥0.80`: Excellent (Stage 1 acceptance) - ~800+ chars/page
- `0.70-0.79`: Good (Stage 2 acceptance) - ~700 chars/page
- `0.50-0.69`: Acceptable (Stage 3 minimum) - ~500 chars/page
- `<0.50`: Poor (fallback to next stage)

**Tuning Guide**:
- **High accuracy needed**: Increase `MinimumThreshold` to `0.85` (fewer fallbacks)
- **Accept lower quality**: Decrease to `0.75` (more Stage 1 acceptances)
- **Scanned PDFs**: Lower `MinCharsPerPage` to `300` (OCR produces less text)

---

### PdfProcessing:MaxFileSizeBytes

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MaxFileSizeBytes` | long | `104857600` | Maximum PDF file size (100 MB) |

**Size Limits**:
- **Default**: 100 MB (104,857,600 bytes)
- **Minimum**: 1 KB (1024 bytes)
- **Maximum**: 500 MB (recommended upper limit for performance)

**Memory Impact**:
- Orchestrator loads entire PDF in memory for retries (`byte[]`)
- Large PDFs (>50 MB): Consider streaming optimization (BGAI-017)

---

### PdfProcessing:Unstructured (Stage 1)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `ApiUrl` | string | `"http://unstructured-service:8001"` | Unstructured Python service URL |
| `TimeoutSeconds` | int | `30` | HTTP request timeout |
| `Strategy` | string | `"fast"` | Extraction strategy: `"fast"` (1.3s) or `"hi_res"` (4s, higher accuracy) |
| `RetryCount` | int | `3` | Number of retries on failure |
| `RetryDelaySeconds` | int | `2` | Delay between retries (exponential backoff) |

**Strategy Comparison**:

| Strategy | Speed | Quality | Use Case |
|----------|-------|---------|----------|
| `fast` | 1.3s | 0.85 | **Recommended** - Fast, good quality |
| `hi_res` | 4s | 0.90 | Complex layouts, tables, diagrams |

**Docker Environment Variables**:
```yaml
services:
  unstructured-service:
    environment:
      - UNSTRUCTURED_STRATEGY=fast  # or "hi_res"
      - LOG_LEVEL=INFO
```

---

### PdfProcessing:SmolDocling (Stage 2)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `ApiUrl` | string | `"http://smoldocling-service:8002"` | SmolDocling Python service URL |
| `TimeoutSeconds` | int | `60` | HTTP request timeout (longer for VLM) |
| `Device` | string | `"cpu"` | Processing device: `"cpu"` or `"cuda"` (GPU) |
| `RetryCount` | int | `2` | Number of retries |
| `RetryDelaySeconds` | int | `5` | Delay between retries |

**Device Selection**:

| Device | Speed | Cost | Use Case |
|--------|-------|------|----------|
| `cpu` | 3-5s/page | €0 | **MVP** - Sufficient for fallback (15% of PDFs) |
| `cuda` (GPU) | 0.5s/page | $50/month | **Phase 2** - If Stage 2 becomes primary (>30% usage) |

**GPU Setup** (Phase 2):
```yaml
services:
  smoldocling-service:
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    environment:
      - DEVICE=cuda
      - MODEL_NAME=smoldocling-256m
```

---

### PdfProcessing:Docnet (Stage 3)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `EnableOcr` | bool | `true` | Enable Tesseract OCR for scanned PDFs |
| `OcrLanguage` | string | `"ita"` | Tesseract language code (Italian) |
| `TesseractDataPath` | string | `/usr/share/tesseract-ocr/tessdata` | Tesseract data files location |

**OCR Languages**:
- `"ita"` - Italian (default)
- `"eng"` - English
- `"ita+eng"` - Both (fallback)

**Installation Verification**:
```bash
# Check Tesseract languages installed
tesseract --list-langs

# Should show:
# ita
# eng
```

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
      - PdfProcessing__Unstructured__ApiUrl=http://unstructured-service:8001
      - PdfProcessing__SmolDocling__ApiUrl=http://smoldocling-service:8002
    depends_on:
      - unstructured-service
      - smoldocling-service
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
      - ./data/temp:/tmp  # Temp files for processing

  smoldocling-service:
    build: ./apps/smoldocling-service
    ports: ["8002:8002"]
    environment:
      - DEVICE=cpu  # or "cuda"
      - MODEL_NAME=smoldocling-256m
      - LOG_LEVEL=INFO
    volumes:
      - ./data/models:/models  # Model cache

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

# Embedding Service (if external)
EMBEDDING_SERVICE_URL="http://embedding-service:8003"

# Feature Flags (optional, defaults to true)
Features__PdfUpload=true
Features__PdfIndexing=true
```

### Optional Variables

```bash
# Override PDF processing config
PdfProcessing__Quality__MinimumThreshold=0.80
PdfProcessing__MaxFileSizeBytes=104857600

# Override extractor URLs (if not using Docker Compose)
PdfProcessing__Unstructured__ApiUrl="http://custom-host:8001"
PdfProcessing__SmolDocling__ApiUrl="http://custom-host:8002"

# Performance tuning
PdfProcessing__Unstructured__Strategy=hi_res
PdfProcessing__SmolDocling__Device=cuda
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

### Current Thresholds (Production)

```
Stage 1 (Unstructured): ≥0.80 (800+ chars/page, high quality)
Stage 2 (SmolDocling):  ≥0.70 (700 chars/page, good quality)
Stage 3 (Docnet):       ≥0.50 (500 chars/page, acceptable)
```

### Tuning Scenarios

**Scenario 1: Too Many Fallbacks (>30% Stage 2/3 usage)**

Problem: Stage 1 threshold too strict, unnecessary fallbacks

Solution:
```json
"PdfProcessing": {
  "Quality": {
    "MinimumThreshold": 0.75,  // Lower from 0.80
    "MinCharsPerPage": 450      // Lower from 500
  }
}
```

**Scenario 2: Low Quality Accepted (users report bad results)**

Problem: Quality threshold too lenient

Solution:
```json
"PdfProcessing": {
  "Quality": {
    "MinimumThreshold": 0.85,  // Raise from 0.80
    "MinCharsPerPage": 600      // Raise from 500
  }
}
```

**Scenario 3: Scanned PDFs Failing**

Problem: Scanned PDFs consistently fall to Stage 3 (poor quality)

Solution:
```json
"PdfProcessing": {
  "Unstructured": {
    "Strategy": "hi_res"  // Change from "fast" (better OCR accuracy)
  },
  "Quality": {
    "MinCharsPerPage": 300  // Lower for scanned PDFs
  }
}
```

---

## 📊 Monitoring Quality Thresholds

### Check Stage Usage Distribution

```sql
-- Query: Which stage is used most?
SELECT
  processing_stage,
  COUNT(*) as count,
  ROUND(AVG(quality_score), 2) as avg_quality,
  ROUND(AVG(character_count::float / page_count), 0) as avg_chars_per_page
FROM pdf_documents
WHERE processing_status = 'completed'
  AND processed_at > NOW() - INTERVAL '7 days'
GROUP BY processing_stage
ORDER BY count DESC;
```

**Expected Distribution** (healthy):
| Stage | Count | % | Avg Quality | Avg Chars/Page |
|-------|-------|---|-------------|----------------|
| Stage 1 (Unstructured) | 80 | 80% | 0.85 | 820 |
| Stage 2 (SmolDocling) | 15 | 15% | 0.72 | 715 |
| Stage 3 (Docnet) | 5 | 5% | 0.55 | 530 |

**Action Thresholds**:
- ⚠️ **Stage 2 >30%**: Lower Stage 1 threshold or switch to `hi_res` strategy
- ⚠️ **Stage 3 >10%**: Investigate Stage 2 failures, consider GPU for SmolDocling
- 🚨 **Stage 1 <70%**: Critical - Review quality thresholds or PDF quality

---

## 🚀 Performance Tuning

### Unstructured Strategy Selection

**fast** (Default):
```json
"Unstructured": {
  "Strategy": "fast"
}
```
- Speed: 1.3s per document
- Quality: 0.85 avg
- Use: **Production** (80% PDFs, good quality)

**hi_res** (High Accuracy):
```json
"Unstructured": {
  "Strategy": "hi_res"
}
```
- Speed: 4s per document
- Quality: 0.90 avg
- Use: Complex layouts, tables, scanned PDFs

**Trade-off**:
- `fast` → `hi_res`: 3x slower, +5% quality
- Recommendation: Use `fast` by default, `hi_res` for specific games if needed

---

### SmolDocling Device Selection

**CPU** (Default):
```json
"SmolDocling": {
  "Device": "cpu"
}
```
- Speed: 3-5s per page (15% PDFs use Stage 2 = ~15s avg)
- Cost: €0
- Use: **MVP** (Stage 2 is fallback, infrequent)

**GPU** (Phase 2):
```json
"SmolDocling": {
  "Device": "cuda"
}
```
- Speed: 0.5s per page (15% PDFs = ~2s avg)
- Cost: $50/month GPU instance
- Use: **Production** (if Stage 2 usage >30%, or Stage 2 becomes primary)

**Decision Matrix**:
| Stage 2 Usage | Device | Rationale |
|---------------|--------|-----------|
| <20% | CPU | Fallback only, cost not justified |
| 20-40% | CPU | Monitor, consider GPU if user complaints |
| >40% | GPU | Significant usage, GPU improves UX |

---

### Retry Policies

**Polly Retry Configuration** (already implemented):

```csharp
// UnstructuredPdfTextExtractor.cs
services.AddHttpClient<UnstructuredPdfTextExtractor>()
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientsHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

**Behavior**:
- Retry Count: 3 attempts
- Delay: Exponential backoff (2s, 4s, 8s)
- Transient Errors: HTTP 5xx, timeout, connection refused

**Tuning**:
```json
"PdfProcessing": {
  "Unstructured": {
    "RetryCount": 5,        // Increase retries for flaky network
    "RetryDelaySeconds": 1  // Faster retries (1s, 2s, 4s, 8s, 16s)
  }
}
```

---

## 🔐 Secrets Management

### Local Development (.env)

```bash
# infra/env/.env.dev
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=meepleai_dev;Username=dev_user;Password=dev_pass"
QDRANT_URL="http://localhost:6333"
EMBEDDING_SERVICE_URL="http://localhost:8003"

# Optional: Override default configs
PdfProcessing__Quality__MinimumThreshold=0.75
PdfProcessing__MaxFileSizeBytes=52428800  # 50 MB for dev testing
```

**Load in API**:
```bash
cd apps/api/src/Api
dotnet run --environment Development
```

---

### Production (.env.prod)

```bash
# infra/env/.env.prod (NEVER commit this file!)
ConnectionStrings__Postgres="Host=prod-db.example.com;Port=5432;Database=meepleai_prod;Username=meepleai_prod;Password=<secret-from-vault>"
QDRANT_URL="http://qdrant-prod.internal:6333"
EMBEDDING_SERVICE_URL="http://embedding-prod.internal:8003"

# Production thresholds (stricter)
PdfProcessing__Quality__MinimumThreshold=0.85
PdfProcessing__MaxFileSizeBytes=104857600
```

**Load in API**:
```bash
export $(cat infra/env/.env.prod | xargs)
dotnet run --environment Production
```

---

### Docker Secrets

```yaml
# docker-compose.prod.yml
services:
  api:
    environment:
      - ConnectionStrings__Postgres=${POSTGRES_CONNECTION_STRING}
    secrets:
      - postgres_password
      - qdrant_api_key

secrets:
  postgres_password:
    file: ./secrets/postgres_password.txt
  qdrant_api_key:
    file: ./secrets/qdrant_api_key.txt
```

**Secrets Files**:
```bash
# infra/secrets/postgres_password.txt
my-secure-postgres-password

# infra/secrets/qdrant_api_key.txt (if Qdrant Cloud)
qdrant-prod-api-key-xyz
```

**Gitignore**:
```
# .gitignore
infra/secrets/*.txt
!infra/secrets/*.txt.example
```

---

## 🧪 Configuration Validation

### Startup Validation (Recommended - BGAI-016)

**Future Implementation**:
```csharp
public class PdfProcessingConfigurationValidator : IValidateOptions<PdfProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, PdfProcessingOptions options)
    {
        // Validate thresholds
        if (options.Quality.MinimumThreshold < 0 || options.Quality.MinimumThreshold > 1)
            return ValidateOptionsResult.Fail("Quality.MinimumThreshold must be 0.0-1.0");

        if (options.Quality.MinCharsPerPage < 100)
            return ValidateOptionsResult.Fail("Quality.MinCharsPerPage must be ≥100");

        // Validate URLs
        if (string.IsNullOrWhiteSpace(options.Unstructured.ApiUrl))
            return ValidateOptionsResult.Fail("Unstructured.ApiUrl is required");

        if (!Uri.TryCreate(options.Unstructured.ApiUrl, UriKind.Absolute, out _))
            return ValidateOptionsResult.Fail("Unstructured.ApiUrl must be a valid URL");

        // Validate file size
        if (options.MaxFileSizeBytes < 1024 || options.MaxFileSizeBytes > 524288000) // 1KB-500MB
            return ValidateOptionsResult.Fail("MaxFileSizeBytes must be 1KB-500MB");

        return ValidateOptionsResult.Success;
    }
}

// Register in Program.cs
builder.Services.AddOptionsWithValidateOnStart<PdfProcessingOptions>()
    .Bind(builder.Configuration.GetSection("PdfProcessing"))
    .ValidateOnStart();
```

**Benefit**: Fails fast on startup with clear error messages vs runtime failures

---

## 📈 Example Configurations

### Development (Fast Iteration)

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Unstructured"  // Stage 1 only, skip fallbacks
    },
    "Quality": {
      "MinimumThreshold": 0.70,  // Lower threshold (accept more)
      "MinCharsPerPage": 400
    },
    "MaxFileSizeBytes": 52428800,  // 50 MB (smaller test files)
    "Unstructured": {
      "ApiUrl": "http://localhost:8001",
      "Strategy": "fast",
      "TimeoutSeconds": 15  // Shorter timeout for dev
    }
  }
}
```

---

### Staging (Pre-Production Testing)

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Orchestrator"  // Full 3-stage pipeline
    },
    "Quality": {
      "MinimumThreshold": 0.80,  // Production thresholds
      "MinCharsPerPage": 500
    },
    "MaxFileSizeBytes": 104857600,  // 100 MB
    "Unstructured": {
      "ApiUrl": "http://unstructured-service:8001",
      "Strategy": "hi_res",  // Higher quality for validation
      "TimeoutSeconds": 60
    },
    "SmolDocling": {
      "Device": "cuda"  // Test GPU performance
    }
  }
}
```

---

### Production (Optimized)

```json
{
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Orchestrator",
      "TimeoutSeconds": 120
    },
    "Quality": {
      "MinimumThreshold": 0.85,  // Stricter for production quality
      "WarningThreshold": 0.75,
      "MinCharsPerPage": 500
    },
    "MaxFileSizeBytes": 104857600,
    "Unstructured": {
      "ApiUrl": "http://unstructured-prod.internal:8001",
      "Strategy": "fast",  // Optimize for speed
      "TimeoutSeconds": 30,
      "RetryCount": 5  // More retries for reliability
    },
    "SmolDocling": {
      "ApiUrl": "http://smoldocling-prod.internal:8002",
      "Device": "cuda",  // GPU for production (if Stage 2 >30%)
      "TimeoutSeconds": 60
    }
  }
}
```

---

## 🛠️ Configuration Troubleshooting

### Issue: "Unstructured service unavailable"

**Check Configuration**:
```bash
# Verify URL is reachable
curl http://unstructured-service:8001/health

# Check API URL in config
dotnet run -- --urls="http://localhost:8080" | grep "Unstructured__ApiUrl"
```

**Fix**: Update `ApiUrl` to correct service URL

---

### Issue: "Quality threshold never met"

**Check Configuration**:
```bash
# View current thresholds
curl http://localhost:8080/api/v1/admin/configuration \
  | jq '.[] | select(.category == "PdfProcessing" and .key == "Quality")'
```

**Fix**: Lower `MinimumThreshold` or `MinCharsPerPage`

---

### Issue: "File size exceeded"

**Check Configuration**:
```bash
# Check max file size
curl http://localhost:8080/api/v1/admin/configuration \
  | jq '.[] | select(.key == "MaxFileSizeBytes")'
```

**Fix**: Increase `MaxFileSizeBytes` (up to 500 MB max recommended)

---

## 📚 References

- [DocumentProcessing README](../../apps/api/src/Api/BoundedContexts/DocumentProcessing/README.md)
- [PDF Processing API](../03-api/pdf-processing-api.md)
- [ADR-003b: Unstructured PDF](../../docs/01-architecture/adr/adr-003b-unstructured-pdf.md)
- [Troubleshooting Guide](./pdf-processing-troubleshooting.md)

---

**Version**: 1.0
**Last Updated**: 2025-11-13
**Maintainer**: Backend Team
