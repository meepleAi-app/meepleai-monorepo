# S3 Object Storage - Complete Guide

**Implementazione completa**: S3-compatible storage per PDF uploads, backups, document processing

---

## Quick Start (5 minuti)

### Cloudflare R2 (Production)

```powershell
# 1. Setup credentials
cd infra/secrets
Copy-Item storage.secret.example storage.secret
notepad storage.secret  # Fill: STORAGE_PROVIDER=s3, S3_ENDPOINT, keys, bucket

# 2. Restart API
cd .. && docker compose restart api

# 3. Verify
curl http://localhost:8080/health | jq '.checks.s3storage'
# ✅ Expected: status="Healthy"
```

**Full guide**: `docs/04-infrastructure/s3-quickstart.md`

---

## Architecture

### Service Hierarchy

```
IBlobStorageService (interface)
    ↓ (factory selection via STORAGE_PROVIDER env var)
    ├─ BlobStorageService (STORAGE_PROVIDER=local)
    │   └─ Filesystem: pdf_uploads/{gameId}/{fileId}_{filename}
    └─ S3BlobStorageService (STORAGE_PROVIDER=s3)
        └─ S3: s3://{bucket}/pdf_uploads/{gameId}/{fileId}_{filename}
```

### Factory Pattern

```csharp
// ApplicationServiceExtensions.cs:195
services.AddScoped<IBlobStorageService>(sp =>
    BlobStorageServiceFactory.Create(sp));

// BlobStorageServiceFactory.cs
public static IBlobStorageService Create(IServiceProvider sp)
{
    var storageProvider = config["STORAGE_PROVIDER"] ?? "local";
    return storageProvider switch
    {
        "s3" => CreateS3StorageService(sp, config),
        _ => CreateLocalStorageService(sp, config)
    };
}
```

### Handlers Using Storage (7)

1. `UploadPdfCommandHandler` - PDF uploads
2. `UploadPrivatePdfCommandHandler` - Private PDFs
3. `CompleteChunkedUploadCommandHandler` - Chunked uploads
4. `DeletePdfCommandHandler` - PDF deletion
5. `DownloadPdfQueryHandler` - PDF downloads
6. `ExtractPdfTextCommandHandler` - Text extraction
7. `UploadGameImageCommandHandler` - Game images

**Zero code changes needed** - all use `IBlobStorageService` interface.

---

## Configuration

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `STORAGE_PROVIDER` | No | `local` | Storage backend: `local` or `s3` |
| `S3_ENDPOINT` | Yes* | - | S3 endpoint URL (provider-specific) |
| `S3_ACCESS_KEY` | Yes* | - | S3 access key ID |
| `S3_SECRET_KEY` | Yes* | - | S3 secret access key |
| `S3_BUCKET_NAME` | Yes* | - | S3 bucket name |
| `S3_REGION` | No | `auto` | AWS region or "auto" for R2 |
| `S3_PRESIGNED_URL_EXPIRY` | No | `3600` | Pre-signed URL lifetime (seconds) |
| `S3_FORCE_PATH_STYLE` | No | `false` | Use path-style URLs (MinIO needs `true`) |
| `S3_DISABLE_ENCRYPTION` | No | `false` | Disable server-side encryption |

*Required only when `STORAGE_PROVIDER=s3`

### Provider Examples

**Cloudflare R2** (Recommended):
```bash
S3_ENDPOINT=https://abc123.r2.cloudflarestorage.com
S3_REGION=auto
S3_FORCE_PATH_STYLE=false
```

**AWS S3**:
```bash
S3_ENDPOINT=https://s3.amazonaws.com
S3_REGION=eu-west-1
S3_FORCE_PATH_STYLE=false
```

**MinIO (Local)**:
```bash
S3_ENDPOINT=http://localhost:9000
S3_REGION=us-east-1
S3_FORCE_PATH_STYLE=true  # REQUIRED
```

**Backblaze B2**:
```bash
S3_ENDPOINT=https://s3.us-west-004.backblazeb2.com
S3_REGION=us-west-004
S3_FORCE_PATH_STYLE=false
```

**DigitalOcean Spaces**:
```bash
S3_ENDPOINT=https://nyc3.digitaloceanspaces.com
S3_REGION=nyc3
S3_FORCE_PATH_STYLE=false
```

---

## Local Testing con MinIO

### Setup (2 minuti)

```powershell
# 1. Avvia MinIO
cd infra
docker compose -f docker-compose.yml -f docker-compose.test.yml --profile storage-test up -d minio

# 2. Apri console
start http://localhost:9001

# 3. Login: minioadmin / minioadmin

# 4. Crea bucket "meepleai-uploads"

# 5. Configura storage.secret
STORAGE_PROVIDER=s3
S3_ENDPOINT=http://localhost:9000
S3_ACCESS_KEY=minioadmin
S3_SECRET_KEY=minioadmin
S3_BUCKET_NAME=meepleai-uploads
S3_REGION=us-east-1
S3_FORCE_PATH_STYLE=true

# 6. Restart API
docker compose restart api
```

### Verify MinIO

```powershell
# Health check
curl http://localhost:8080/health | jq '.checks.s3storage'

# Console
start http://localhost:9001  # View uploaded files

# Stop MinIO when done
docker compose --profile storage-test down
```

---

## Features

### 1. Multi-Provider Support

**Supportati**:
- ✅ Cloudflare R2 (zero egress fees, EU jurisdiction)
- ✅ AWS S3 (standard S3, massima compatibilità)
- ✅ Backblaze B2 (low cost, $0.005/GB)
- ✅ MinIO (self-hosted, testing locale)
- ✅ DigitalOcean Spaces (semplice setup)

**Configurazione**: Cambia solo `S3_ENDPOINT` e `S3_REGION`

### 2. Security

**Path Traversal Protection**:
```csharp
PathSecurity.ValidateIdentifier(gameId, nameof(gameId));
// Blocca: ../, ../../, /etc/passwd, ecc.
```

**Server-Side Encryption** (AES256):
```csharp
request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
```

**Stream Disposal**:
```csharp
// IBlobStorageService.RetrieveAsync XML doc:
// IMPORTANT: Caller MUST dispose returned stream
using var stream = await storageService.RetrieveAsync(fileId, gameId);
```

### 3. Pre-Signed URLs

```csharp
// S3BlobStorageService only (not in IBlobStorageService interface)
var s3Service = (S3BlobStorageService)storageService;
var url = await s3Service.GetPresignedDownloadUrlAsync(fileId, gameId, expirySeconds: 7200);
// Returns: https://bucket.s3.com/key?X-Amz-Signature=...&X-Amz-Expires=7200
```

**Use cases**:
- Temporary download links (expire dopo X secondi)
- Direct browser downloads (bypass API)
- CDN integration (Cloudflare)

### 4. Health Monitoring

**Endpoint**: `GET /health`

```json
{
  "status": "Healthy",
  "checks": {
    "s3storage": {
      "status": "Healthy",
      "description": "S3 storage accessible (endpoint: https://..., bucket: meepleai-uploads)"
    }
  }
}
```

**States**:
- `Healthy`: S3 accessibile
- `Unhealthy`: Credentials invalide, bucket non esiste
- `Degraded`: Timeout connectivity

---

## Migration

### Script Usage

```powershell
cd D:\Repositories\meepleai-monorepo-dev

# Preview migration
.\tools\migrate-local-to-s3.ps1 -DryRun

# Execute migration
.\tools\migrate-local-to-s3.ps1

# Verify + delete local after success
.\tools\migrate-local-to-s3.ps1 -Verify -DeleteLocal -Confirm
```

### Manual Migration (AWS CLI)

```powershell
# Install AWS CLI
choco install awscli

# Set credentials
$env:AWS_ACCESS_KEY_ID = "your-key"
$env:AWS_SECRET_ACCESS_KEY = "your-secret"

# Sync local → S3
aws s3 sync pdf_uploads/ s3://meepleai-uploads/pdf_uploads/ --endpoint-url https://your-account.r2.cloudflarestorage.com

# Verify
aws s3 ls s3://meepleai-uploads/pdf_uploads/ --recursive --endpoint-url https://your-account.r2.cloudflarestorage.com
```

---

## Testing

### Unit Tests (15 tests)

```powershell
cd apps/api
dotnet test --filter "FullyQualifiedName~S3BlobStorageServiceTests"

# Tests cover:
# - StoreAsync (success + errors)
# - RetrieveAsync (found + not found + S3 errors)
# - DeleteAsync (success + not found)
# - GetStoragePath (valid + path traversal)
# - Exists (found + not found + path traversal)
# - GetPresignedDownloadUrlAsync (success + not found + custom expiry)
```

### Integration Testing

**Con MinIO locale**:
```powershell
# 1. Avvia MinIO
docker compose -f docker-compose.yml -f docker-compose.test.yml --profile storage-test up -d minio

# 2. Configura storage.secret per MinIO (vedi sopra)

# 3. Test manuale upload PDF via frontend/Postman

# 4. Verifica in console: http://localhost:9001

# 5. Cleanup
docker compose --profile storage-test down -v
```

---

## Monitoring

### Health Check Monitoring

```powershell
# Check periodico
while ($true) {
    $health = curl http://localhost:8080/health -s | ConvertFrom-Json
    $s3Status = $health.checks.s3storage.status
    Write-Host "$(Get-Date -Format 'HH:mm:ss') - S3: $s3Status"
    Start-Sleep -Seconds 30
}
```

### Application Logs

```powershell
# S3 operations
docker compose logs api | Select-String "S3"

# Storage operations
docker compose logs api | Select-String "Stored file|Retrieved file|Deleted file"

# Errors
docker compose logs api | Select-String "S3 error"
```

### Bucket Metrics

**Cloudflare R2**:
- Dashboard → R2 → `meepleai-uploads` → Metrics
- Storage GB, Requests (Class A/B), Egress (gratis)

**AWS CLI**:
```powershell
aws s3 ls s3://meepleai-uploads/pdf_uploads/ --recursive --summarize --human-readable --endpoint-url $S3_ENDPOINT

# Output:
# Total Objects: 1234
# Total Size: 4.5 GB
```

---

## Troubleshooting

### 🔴 Health Check Fails

**Error**: `s3storage: Unhealthy - S3 configuration incomplete`

```powershell
# Verify storage.secret exists
Test-Path infra/secrets/storage.secret  # Must be True

# Check required vars
Get-Content infra/secrets/storage.secret | Select-String "S3_"

# Must have: S3_ENDPOINT, S3_ACCESS_KEY, S3_SECRET_KEY, S3_BUCKET_NAME
```

**Error**: `s3storage: Unhealthy - S3 bucket does not exist`

```powershell
# Create bucket in provider console
# R2: https://dash.cloudflare.com → R2 → Create bucket
# MinIO: http://localhost:9001 → Buckets → Create

# Or via AWS CLI
aws s3 mb s3://meepleai-uploads --endpoint-url $S3_ENDPOINT
```

**Error**: `s3storage: Unhealthy - S3 access denied`

```powershell
# Test credentials manually
aws s3 ls s3://meepleai-uploads --endpoint-url $S3_ENDPOINT

# If fails:
# - Regenerate API token in R2 dashboard
# - Update S3_ACCESS_KEY + S3_SECRET_KEY in storage.secret
# - Restart API
```

### 🔴 Upload Fails

**Symptom**: PDF upload returns error

```powershell
# 1. Check API logs
docker compose logs api --tail 50 | Select-String "S3 error"

# Common errors:
# - NoSuchBucket: Bucket doesn't exist → create bucket
# - InvalidAccessKeyId: Wrong ACCESS_KEY → regenerate token
# - SignatureDoesNotMatch: Wrong SECRET_KEY → verify secret
```

**Symptom**: Upload succeeds but file not in S3

```powershell
# Verify STORAGE_PROVIDER
docker compose exec api printenv | Select-String STORAGE_PROVIDER
# Must be: STORAGE_PROVIDER=s3

# Check factory selection (API logs on startup)
docker compose logs api | Select-String "Initialized S3 storage"
# Expected: "endpoint=..., bucket=..., region=..., encryption=True"
```

### 🔴 MinIO Connection Issues

```powershell
# 1. Verify container running
docker ps | Select-String minio

# 2. Check MinIO logs
docker compose logs minio

# 3. Test API access
curl http://localhost:9000/minio/health/live

# 4. Verify FORCE_PATH_STYLE
# MinIO REQUIRES: S3_FORCE_PATH_STYLE=true
```

---

## Cost Comparison

| Provider | Storage (20GB) | Egress (100GB/mo) | Total/mo |
|----------|----------------|-------------------|----------|
| **Cloudflare R2** | $0.30 | **$0.00** ✅ | **$0.30** |
| AWS S3 | $0.46 | $9.00 | $9.46 |
| Backblaze B2 | $0.10 | $1.00 | $1.10 |
| MinIO (self-hosted) | Server costs | $0.00 | Variable |

**Raccomandazione**: Cloudflare R2 (zero egress fees = 95% savings)

---

## Migration

### From Local to S3

```powershell
# 1. Backup locale (safety)
Copy-Item -Recurse pdf_uploads pdf_uploads_backup

# 2. Dry run
.\tools\migrate-local-to-s3.ps1 -DryRun

# 3. Migrate
.\tools\migrate-local-to-s3.ps1

# 4. Verify
.\tools\migrate-local-to-s3.ps1 -Verify

# 5. Delete local (optional)
.\tools\migrate-local-to-s3.ps1 -DeleteLocal -Confirm
```

### From S3 to Local

```powershell
# 1. Download from S3
aws s3 sync s3://meepleai-uploads/pdf_uploads/ ./pdf_uploads/ --endpoint-url $S3_ENDPOINT

# 2. Update config
# storage.secret: STORAGE_PROVIDER=local

# 3. Restart API
docker compose restart api
```

---

## Security Best Practices

### 1. Credentials Rotation (90 giorni)

```powershell
# Cloudflare R2
# 1. Dashboard → R2 → API Tokens → Regenerate token
# 2. Update storage.secret (S3_ACCESS_KEY, S3_SECRET_KEY)
# 3. docker compose restart api

# AWS S3
# 1. IAM → Users → Security credentials → Create access key
# 2. Deactivate old key after migration
# 3. Update storage.secret + restart
```

### 2. Bucket Encryption

**Cloudflare R2**: Automatic (always on)

**AWS S3**:
```powershell
aws s3api put-bucket-encryption `
  --bucket meepleai-uploads `
  --server-side-encryption-configuration '{
    "Rules": [{
      "ApplyServerSideEncryptionByDefault": {
        "SSEAlgorithm": "AES256"
      }
    }]
  }'
```

### 3. Access Policies

**Cloudflare R2**: Token permissions (Object Read & Write only)

**AWS S3**: IAM policy
```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Action": [
      "s3:PutObject",
      "s3:GetObject",
      "s3:DeleteObject",
      "s3:ListBucket"
    ],
    "Resource": [
      "arn:aws:s3:::meepleai-uploads",
      "arn:aws:s3:::meepleai-uploads/*"
    ]
  }]
}
```

---

## Performance

### Metrics

**Operation latencies** (Cloudflare R2, EU):
- Upload (1MB): ~200-500ms
- Download (1MB): ~150-300ms
- Delete: ~100-200ms
- Health check: ~50-100ms

**Optimization**:
- Pre-signed URLs: Direct download (bypass API)
- CDN integration: Cloudflare automatic caching
- Connection pooling: Singleton IAmazonS3 client

### Monitoring Queries

```powershell
# Application logs
docker compose logs api | Select-String "Stored file to S3" | Measure-Object  # Upload count
docker compose logs api | Select-String "S3 error" | Measure-Object  # Error count

# Bucket size
aws s3 ls s3://meepleai-uploads/pdf_uploads/ --recursive --summarize --endpoint-url $S3_ENDPOINT
```

---

## Backup & Disaster Recovery

### Automated Backups (n8n)

**Workflow** (to implement in n8n):
1. Schedule: Daily 2 AM
2. Export PostgreSQL dump
3. Upload to `S3_BACKUP_BUCKET_NAME`
4. Retention: Delete backups >30 days

**Configuration** (`storage.secret`):
```bash
S3_BACKUP_BUCKET_NAME=meepleai-backups
S3_BACKUP_RETENTION_DAYS=30
```

### Manual Backup

```powershell
# Full bucket backup
aws s3 sync s3://meepleai-uploads/ ./s3-backup/ --endpoint-url $S3_ENDPOINT

# Incremental backup (only changes)
aws s3 sync s3://meepleai-uploads/ ./s3-backup/ --endpoint-url $S3_ENDPOINT --size-only
```

### Restore

```powershell
# Full restore
aws s3 sync ./s3-backup/ s3://meepleai-uploads/ --endpoint-url $S3_ENDPOINT

# Specific game restore
aws s3 sync ./s3-backup/pdf_uploads/<game-id>/ s3://meepleai-uploads/pdf_uploads/<game-id>/ --endpoint-url $S3_ENDPOINT
```

---

## API Reference

### IBlobStorageService Interface

```csharp
public interface IBlobStorageService
{
    // Store file, returns BlobStorageResult with FileId
    Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct = default);

    // Retrieve file stream (caller MUST dispose)
    Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct = default);

    // Delete file, returns true if successful
    Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct = default);

    // Get storage path/key for file
    string GetStoragePath(string fileId, string gameId, string fileName);

    // Check if file exists (sync method)
    bool Exists(string fileId, string gameId);
}
```

### S3BlobStorageService Additional Methods

```csharp
// Generate pre-signed download URL (S3-specific)
public async Task<string?> GetPresignedDownloadUrlAsync(
    string fileId,
    string gameId,
    int? expirySeconds = null)
```

---

## Provider Comparison

| Feature | R2 | AWS S3 | B2 | MinIO |
|---------|----|----|----|----|
| **Egress fees** | ✅ $0 | ❌ $0.09/GB | ⚠️ $0.01/GB | ✅ $0 |
| **Storage cost** | $0.015/GB | $0.023/GB | $0.005/GB | Self-hosted |
| **EU jurisdiction** | ✅ Yes | ⚠️ Depends | ⚠️ US | ✅ Self-hosted |
| **CDN integration** | ✅ Cloudflare | ⚠️ CloudFront | ❌ No | ❌ No |
| **Setup complexity** | ⭐⭐ Easy | ⭐⭐⭐ Medium | ⭐⭐ Easy | ⭐⭐⭐⭐ Complex |
| **API compatibility** | ✅ Full | ✅ Native | ✅ Full | ✅ Full |
| **Free tier** | ❌ No | ⚠️ 5GB | ✅ 10GB | N/A |

**Best for**:
- **Production**: Cloudflare R2 (zero egress, EU, CDN)
- **Budget**: Backblaze B2 (lowest storage cost)
- **Testing**: MinIO (local, no cloud account)
- **AWS ecosystem**: AWS S3 (native integration)

---

## References

- **Quick Start**: `docs/04-infrastructure/s3-quickstart.md`
- **Operations Runbook**: `docs/04-infrastructure/s3-storage-operations-runbook.md`
- **Migration Script**: `tools/migrate-local-to-s3.ps1`
- **Developer Guide**: `CLAUDE.md` (section "S3 Storage Configuration")
- **Issue**: #2703
- **PR**: #3683

**Implementation**:
- `Api/Services/Pdf/S3BlobStorageService.cs` (332 lines)
- `Api/Services/Pdf/BlobStorageServiceFactory.cs` (79 lines)
- `Api/Services/Pdf/S3StorageOptions.cs` (48 lines)
- `Api/Infrastructure/Health/Checks/S3StorageHealthCheck.cs` (70 lines)

---

**Last Updated**: 2026-02-05
**Version**: 1.0 (Initial implementation)
