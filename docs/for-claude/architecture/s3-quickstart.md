# S3 Storage Quick Start Guide

## Setup Cloudflare R2 (5 minuti)

### Step 1: Crea Account Cloudflare R2

1. **Registrati**: https://dash.cloudflare.com/sign-up
2. **Vai a R2**: Dashboard → R2 Object Storage
3. **Acquista piano**: $0.015/GB/mese (~$0.30/20GB)

### Step 2: Crea Bucket

1. **Create bucket** → Nome: `meepleai-uploads`
2. **Location**: EU (GDPR compliance)
3. **Settings**: Default (encryption automatica)

### Step 3: Genera API Token

1. **Manage R2 API Tokens** → Create API Token
2. **Permissions**: Object Read & Write
3. **Copia**: Access Key ID + Secret Access Key

### Step 4: Configura MeepleAI

```bash
# 1. Crea storage.secret da template
cd D:\Repositories\meepleai-monorepo-dev\infra\secrets
Copy-Item storage.secret.example storage.secret

# 2. Modifica storage.secret (notepad/vscode)
notepad storage.secret
```

**Contenuto `storage.secret`**:
```bash
# STORAGE_PROVIDER: "local" (default) o "s3"
STORAGE_PROVIDER=s3

# Account ID (da dashboard: https://dash.cloudflare.com/?to=/:account/r2)
S3_ACCOUNT_ID=your_cloudflare_account_id_here

# Credentials (da Step 3)
S3_ACCESS_KEY=your_r2_access_key_id_here
S3_SECRET_KEY=your_r2_secret_access_key_here

# Bucket (da Step 2)
S3_BUCKET_NAME=meepleai-uploads

# Endpoint (sostituisci {account_id} con il tuo Account ID)
S3_ENDPOINT=https://your_cloudflare_account_id_here.r2.cloudflarestorage.com

# Region (usa "auto" per R2)
S3_REGION=auto

# Pre-signed URL expiry (secondi)
S3_PRESIGNED_URL_EXPIRY=3600

# Force path style (false per R2)
S3_FORCE_PATH_STYLE=false
```

### Step 5: Riavvia API

```powershell
# Se in Docker
cd D:\Repositories\meepleai-monorepo-dev\infra
docker compose restart api

# Se dotnet run locale
cd D:\Repositories\meepleai-monorepo-dev\apps\api\src\Api
# Ctrl+C per stop
dotnet run
```

---

## Setup MinIO (Testing Locale - Opzionale)

### Step 1: Avvia MinIO Container

```powershell
cd D:\Repositories\meepleai-monorepo-dev\infra

# Avvia MinIO con profilo storage-test
docker compose -f docker-compose.yml -f docker-compose.test.yml --profile storage-test up -d minio

# Verifica container attivo
docker ps | Select-String minio
```

### Step 2: Crea Bucket

1. **Apri console**: http://localhost:9001
2. **Login**: minioadmin / minioadmin
3. **Create Bucket**: `meepleai-uploads`

### Step 3: Configura storage.secret

```bash
STORAGE_PROVIDER=s3
S3_ENDPOINT=http://localhost:9000
S3_ACCESS_KEY=minioadmin
S3_SECRET_KEY=minioadmin
S3_BUCKET_NAME=meepleai-uploads
S3_REGION=us-east-1
S3_FORCE_PATH_STYLE=true  # IMPORTANTE per MinIO
```

---

## Verifica Funzionamento

### Test 1: Health Check (30 secondi)

```powershell
# 1. Avvia API
cd apps/api/src/Api
dotnet run

# 2. Verifica health endpoint (nuovo terminale)
curl http://localhost:8080/health | ConvertFrom-Json | Select-Object -ExpandProperty checks | Select-Object -ExpandProperty s3storage

# ✅ Output atteso:
# status      : Healthy
# description : S3 storage accessible (endpoint: https://..., bucket: meepleai-uploads)

# ❌ Se Unhealthy:
# - Verifica storage.secret esista
# - Controlla credentials (S3_ACCESS_KEY, S3_SECRET_KEY)
# - Verifica bucket esista
```

### Test 2: Upload PDF (1 minuto)

```powershell
# 1. Crea test PDF
"Test content" | Out-File test.pdf

# 2. Upload via API (usa Postman/curl/frontend)
# POST http://localhost:8080/api/v1/documents/upload
# Headers: Authorization: Bearer <your-jwt>
# Body (multipart): file=test.pdf, gameId=<game-uuid>

# 3. Verifica in S3
# Per R2: Dashboard → R2 → meepleai-uploads → pdf_uploads/
# Per MinIO: http://localhost:9001 → Buckets → meepleai-uploads

# ✅ Dovresti vedere: pdf_uploads/{gameId}/{fileId}_test.pdf
```

### Test 3: AWS CLI (Opzionale)

```powershell
# 1. Installa AWS CLI
# Windows: choco install awscli
# Verifica: aws --version

# 2. Configura credentials
$env:AWS_ACCESS_KEY_ID = "your_access_key"
$env:AWS_SECRET_ACCESS_KEY = "your_secret_key"

# 3. List bucket contents
aws s3 ls s3://meepleai-uploads/pdf_uploads/ --endpoint-url https://your-account.r2.cloudflarestorage.com

# ✅ Output: lista di file o vuoto (OK)
# ❌ Error 403: credentials errate
# ❌ Error 404: bucket non esiste
```

### Test 4: Automated Tests

```powershell
cd D:\Repositories\meepleai-monorepo-dev\apps\api

# Run tutti gli S3 unit tests
dotnet test --filter "FullyQualifiedName~S3BlobStorageServiceTests"

# ✅ Output atteso:
# Superato! - Superati: 15. Totale: 15
```

---

## Troubleshooting Rapido

| Problema | Causa | Soluzione |
|----------|-------|-----------|
| **Health check Unhealthy** | Credentials invalide | Verifica S3_ACCESS_KEY, S3_SECRET_KEY in storage.secret |
| **Bucket not found** | Bucket non creato | Crea bucket in R2 dashboard / MinIO console |
| **Upload fallisce** | Endpoint sbagliato | Verifica S3_ENDPOINT (format: https://{account}.r2.cloudflarestorage.com) |
| **403 Forbidden** | Token permissions insufficienti | R2 token deve avere "Object Read & Write" |
| **MinIO connection refused** | Container non avviato | `docker ps` → verifica minio running |
| **Path style error** | MinIO senza force-path | Aggiungi S3_FORCE_PATH_STYLE=true |

---

## Migrazione Dati Esistenti

```powershell
# Se hai già PDF in locale (pdf_uploads/)

# 1. Dry run (preview)
cd D:\Repositories\meepleai-monorepo-dev
.\tools\migrate-local-to-s3.ps1 -DryRun

# 2. Esegui migrazione
.\tools\migrate-local-to-s3.ps1

# 3. Verifica (confronta count)
.\tools\migrate-local-to-s3.ps1 -Verify

# 4. Elimina locale dopo verifica (OPZIONALE)
.\tools\migrate-local-to-s3.ps1 -DeleteLocal -Confirm
```

---

## Switching Provider

### Da Local → S3

```bash
# 1. storage.secret: STORAGE_PROVIDER=s3
# 2. Restart: docker compose restart api
# 3. Migra: .\tools\migrate-local-to-s3.ps1
```

### Da S3 → Local

```bash
# 1. storage.secret: STORAGE_PROVIDER=local
# 2. Restart: docker compose restart api
# 3. Download: aws s3 sync s3://bucket/pdf_uploads/ ./pdf_uploads/ --endpoint-url ...
```

---

## Quick Reference

```powershell
# Setup
cd infra/secrets && cp storage.secret.example storage.secret
notepad storage.secret  # Fill in credentials
cd ../.. && docker compose restart api

# Verify
curl http://localhost:8080/health | jq '.checks.s3storage'

# Test upload (via frontend or Postman)
# POST http://localhost:8080/api/v1/documents/upload

# Monitor
docker compose logs api | Select-String "S3"
```

**Docs completi**: `docs/04-infrastructure/s3-storage-operations-runbook.md`
