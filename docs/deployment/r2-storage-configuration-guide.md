# MeepleAI R2 Storage Configuration Guide

**Document Version**: 1.0
**Last Updated**: 2026-02-01
**Target Audience**: DevOps Engineers, Backend Developers, System Administrators
**Related Issue**: [#2703 - S3-Compatible Object Storage Implementation](https://github.com/meepleAi-app/meepleai-monorepo/issues/2703)

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Decision Rationale](#decision-rationale)
3. [Prerequisites](#prerequisites)
4. [Cloudflare R2 Account Setup](#cloudflare-r2-account-setup)
5. [Bucket Configuration](#bucket-configuration)
6. [Secrets Configuration](#secrets-configuration)
7. [Application Integration](#application-integration)
8. [Security & Access Control](#security--access-control)
9. [Data Migration](#data-migration)
10. [n8n Backup Integration](#n8n-backup-integration)
11. [Monitoring & Health Checks](#monitoring--health-checks)
12. [Troubleshooting](#troubleshooting)
13. [Cost Optimization](#cost-optimization)
14. [Appendix](#appendix)

---

## Executive Summary

MeepleAI uses **Cloudflare R2** as its S3-compatible object storage solution for:

| Use Case | Description | Estimated Volume |
|----------|-------------|------------------|
| **PDF Rulebooks** | User-uploaded board game manuals | ~500 files (~2-5GB) |
| **Database Backups** | PostgreSQL dumps via n8n | ~100MB/backup |
| **Temporary Files** | Document extraction pipeline | Transient |

### Key Benefits

- **Zero Egress Fees**: Users download PDFs without bandwidth costs
- **EU Data Residency**: GDPR-compliant with EU jurisdiction option
- **S3-Compatible API**: Drop-in replacement for existing `IBlobStorageService`
- **Free Tier**: 10GB storage, 10M Class A ops, 1M Class B ops/month

### Cost Projection (MVP Phase)

| Period | Storage | Egress | **Total** |
|--------|---------|--------|-----------|
| Months 1-12 | Free tier | $0 (always free) | **$0/month** |
| Post-free tier | ~$0.08/month | $0 | **~$1/year** |

---

## Decision Rationale

### Provider Comparison (Issue #2703 Research)

| Provider | Storage/GB | Egress/GB | EU Region | Free Tier | **MVP Cost** |
|----------|------------|-----------|-----------|-----------|--------------|
| **Cloudflare R2** | $0.015 | **$0** | Yes | 10GB | **$0** |
| Backblaze B2 | $0.006 | $0.01 | Yes | 10GB | ~$0.50 |
| AWS S3 | $0.023 | $0.09 | Yes | 5GB/12mo | ~$12 |
| MinIO (self) | Infra | $0 | Depends | N/A | ~$60+ |

### Selection Criteria

1. **Budget**: Lowest cost for MVP → R2 wins (free tier + zero egress)
2. **GDPR Compliance**: EU data residency required → R2 supports EU jurisdiction
3. **Existing Account**: Cloudflare already in use → No new vendor onboarding
4. **S3 Compatibility**: Existing `IBlobStorageService` works unchanged

---

## Prerequisites

Before starting, ensure you have:

- [ ] Cloudflare account (free tier sufficient)
- [ ] Access to Cloudflare dashboard
- [ ] MeepleAI repository cloned locally
- [ ] `infra/secrets/` directory accessible

---

## Cloudflare R2 Account Setup

### Step 1: Enable R2 in Cloudflare Dashboard

1. Log in to [Cloudflare Dashboard](https://dash.cloudflare.com)
2. Navigate to **R2** in the left sidebar
3. If first time, click **Get Started** and accept terms
4. Note your **Account ID** (visible in dashboard URL or Overview page)

### Step 2: Create API Token

1. Go to **R2** → **Manage R2 API Tokens**
2. Click **Create API Token**
3. Configure token:
   - **Token name**: `meepleai-storage`
   - **Permissions**: `Object Read & Write`
   - **Bucket scope**: Specific buckets (create bucket first) or All buckets
   - **TTL**: No expiration (or set 1 year for security)
4. Click **Create API Token**
5. **IMPORTANT**: Copy both values immediately:
   - `Access Key ID`: `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
   - `Secret Access Key`: `yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy`

> **Security Note**: Secret Access Key is shown only once. Store securely.

---

## Bucket Configuration

### Step 3: Create Storage Bucket

1. In R2 dashboard, click **Create bucket**
2. Configure:
   - **Bucket name**: `meepleai-uploads` (or your preferred name)
   - **Location hint**: `EEUR` (Western Europe) for GDPR compliance
3. Click **Create bucket**

### Step 4: Configure EU Data Jurisdiction (GDPR)

1. Select your bucket → **Settings**
2. Under **Location**, verify **European Union** jurisdiction
3. This ensures all data stored in EU data centers

### Step 5: Create Backup Bucket (Optional)

For database backups, create a separate bucket:

1. Create bucket named `meepleai-backups`
2. Same EU jurisdiction settings
3. Consider enabling **Object Lifecycle Rules** for automatic cleanup:
   - Delete objects older than 30 days

---

## Secrets Configuration

### Step 6: Configure MeepleAI Secrets

Update `infra/secrets/storage.secret` with your R2 credentials:

```bash
# Copy example file if not exists
cd infra/secrets
cp storage.secret.example storage.secret

# Edit with your values
notepad storage.secret  # Windows
# or: nano storage.secret  # Linux/Mac
```

**Required values**:

```ini
# Cloudflare R2 Configuration
# Get credentials from: https://dash.cloudflare.com/?to=/:account/r2/api-tokens

# Your Cloudflare Account ID (from dashboard URL)
S3_ACCOUNT_ID=your_cloudflare_account_id

# R2 API Token credentials
S3_ACCESS_KEY=your_r2_access_key_id
S3_SECRET_KEY=your_r2_secret_access_key

# Bucket configuration
S3_BUCKET_NAME=meepleai-uploads
S3_REGION=auto
S3_ENDPOINT=https://{account_id}.r2.cloudflarestorage.com

# Backup bucket (for n8n PostgreSQL backups)
S3_BACKUP_BUCKET_NAME=meepleai-backups
```

**Example with placeholder account ID**:

```ini
S3_ACCOUNT_ID=a1b2c3d4e5f6g7h8i9j0
S3_ACCESS_KEY=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
S3_SECRET_KEY=yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
S3_BUCKET_NAME=meepleai-uploads
S3_REGION=auto
S3_ENDPOINT=https://a1b2c3d4e5f6g7h8i9j0.r2.cloudflarestorage.com
S3_BACKUP_BUCKET_NAME=meepleai-backups
```

### Validation

Restart API to load new secrets:

```bash
cd infra
docker compose restart api
docker compose logs api | grep -i storage
```

Expected log output:
```
[INFO] OPTIONAL secrets missing (some features disabled): (none with storage)
# or
[INFO] Applied X secret values as environment variables
```

---

## Application Integration

### Existing Interface

MeepleAI uses `IBlobStorageService` abstraction located at:
- `Api/Services/Pdf/IBlobStorageService.cs`

### S3BlobStorageService Implementation

The S3 implementation should use AWS SDK with R2 endpoint:

```csharp
// Configuration pattern
services.AddSingleton<IAmazonS3>(sp =>
{
    var config = new AmazonS3Config
    {
        ServiceURL = Environment.GetEnvironmentVariable("S3_ENDPOINT"),
        ForcePathStyle = true  // Required for R2
    };

    return new AmazonS3Client(
        Environment.GetEnvironmentVariable("S3_ACCESS_KEY"),
        Environment.GetEnvironmentVariable("S3_SECRET_KEY"),
        config
    );
});
```

### File Path Convention

Maintain existing naming pattern:
```
pdf_uploads/{gameId}/{fileId}_{filename}
```

Example:
```
pdf_uploads/550e8400-e29b-41d4-a716-446655440000/abc123_catan-rules.pdf
```

---

## Security & Access Control

### Bucket Policy

R2 buckets are **private by default**. Configure access:

1. **API Access Only** (Recommended for MVP):
   - No public access
   - All access via pre-signed URLs

2. **Pre-Signed URLs**:
   - Generate time-limited URLs for downloads
   - Typical expiration: 1 hour for downloads

```csharp
// Generate pre-signed URL
var request = new GetPreSignedUrlRequest
{
    BucketName = bucketName,
    Key = objectKey,
    Expires = DateTime.UtcNow.AddHours(1)
};
var url = s3Client.GetPreSignedURL(request);
```

### CORS Configuration (If Direct Browser Upload)

If implementing direct browser uploads:

1. Go to bucket → **Settings** → **CORS Policy**
2. Add rule:

```json
[
  {
    "AllowedOrigins": ["https://yourdomain.com"],
    "AllowedMethods": ["GET", "PUT"],
    "AllowedHeaders": ["*"],
    "MaxAgeSeconds": 3600
  }
]
```

### Encryption

R2 automatically encrypts data at rest. No additional configuration needed.

---

## Data Migration

### Migrate Existing Local Files

If you have existing PDF files in local storage:

```powershell
# PowerShell migration script
$localPath = "path/to/local/pdf_uploads"
$bucketName = "meepleai-uploads"

Get-ChildItem -Path $localPath -Recurse -File | ForEach-Object {
    $key = $_.FullName.Replace($localPath, "pdf_uploads").Replace("\", "/")

    # Upload using AWS CLI (configured for R2)
    aws s3 cp $_.FullName "s3://$bucketName/$key" --endpoint-url $env:S3_ENDPOINT
}
```

### AWS CLI Configuration for R2

```bash
# Configure AWS CLI profile for R2
aws configure --profile r2
# AWS Access Key ID: your_r2_access_key
# AWS Secret Access Key: your_r2_secret_key
# Default region: auto
# Default output: json

# Usage
aws s3 ls s3://meepleai-uploads --endpoint-url https://ACCOUNT_ID.r2.cloudflarestorage.com --profile r2
```

---

## n8n Backup Integration

### Configure n8n for PostgreSQL Backups

1. Create n8n credentials for R2:
   - Type: AWS S3
   - Access Key ID: `S3_BACKUP_ACCESS_KEY`
   - Secret Access Key: `S3_BACKUP_SECRET_KEY`
   - Region: `auto`
   - Custom Endpoint: `https://ACCOUNT_ID.r2.cloudflarestorage.com`

2. Workflow nodes:
   - **Schedule Trigger**: Daily at 2:00 AM
   - **Execute Command**: `pg_dump` to create backup
   - **S3 Upload**: Upload to `meepleai-backups` bucket

### Backup Naming Convention

```
backups/postgres/{date}/meepleai_db_{timestamp}.sql.gz
```

Example:
```
backups/postgres/2026-02-01/meepleai_db_20260201_020000.sql.gz
```

### Retention Policy

Configure R2 lifecycle rules:
- Delete backups older than 30 days
- Keep at least last 7 backups

---

## Monitoring & Health Checks

### Health Check Endpoint

Add storage health check to existing health endpoints:

```csharp
// Health check implementation
public async Task<HealthCheckResult> CheckHealthAsync(...)
{
    try
    {
        await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = _bucketName,
            MaxKeys = 1
        });
        return HealthCheckResult.Healthy("R2 storage accessible");
    }
    catch (Exception ex)
    {
        return HealthCheckResult.Unhealthy("R2 storage error", ex);
    }
}
```

### Cloudflare R2 Metrics

Monitor in Cloudflare dashboard:
- **Storage Used**: Total bytes stored
- **Class A Operations**: PUT, POST, LIST operations
- **Class B Operations**: GET, HEAD operations
- **Egress**: Always $0 but track for capacity planning

---

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| `Access Denied` | Invalid credentials | Verify S3_ACCESS_KEY and S3_SECRET_KEY |
| `Bucket not found` | Wrong bucket name or region | Check S3_BUCKET_NAME matches dashboard |
| `Connection refused` | Wrong endpoint | Verify S3_ENDPOINT format |
| `SignatureDoesNotMatch` | Clock skew | Sync system time |

### Debug Logging

Enable AWS SDK logging:

```csharp
AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
AWSConfigs.LoggingConfig.LogResponses = ResponseLoggingOption.OnError;
```

### Test Connection

```bash
# Test with AWS CLI
aws s3 ls s3://meepleai-uploads \
  --endpoint-url https://ACCOUNT_ID.r2.cloudflarestorage.com \
  --profile r2

# Expected: Empty list or file listing (no error)
```

---

## Cost Optimization

### Free Tier Limits (Per Month)

| Resource | Free Allowance | Overage Cost |
|----------|----------------|--------------|
| Storage | 10 GB | $0.015/GB |
| Class A ops | 1,000,000 | $4.50/million |
| Class B ops | 10,000,000 | $0.36/million |
| Egress | **Unlimited** | $0 (always free) |

### Optimization Tips

1. **Compress PDFs**: Use PDF optimization before upload
2. **Lifecycle Rules**: Auto-delete temporary files after 24h
3. **Deduplicate**: Hash files to avoid duplicate storage
4. **Monitor Usage**: Set Cloudflare alerts at 80% of free tier

### Projected Growth

| Phase | Storage | Monthly Cost |
|-------|---------|--------------|
| MVP (current) | <10 GB | $0 |
| Beta (6 months) | ~20 GB | ~$0.15 |
| Production (1 year) | ~50 GB | ~$0.60 |

---

## Appendix

### Environment Variables Reference

| Variable | Description | Example |
|----------|-------------|---------|
| `S3_ACCOUNT_ID` | Cloudflare account ID | `a1b2c3d4e5f6` |
| `S3_ACCESS_KEY` | R2 API access key | `xxxxxxxxxx` |
| `S3_SECRET_KEY` | R2 API secret key | `yyyyyyyyyy` |
| `S3_BUCKET_NAME` | Primary bucket name | `meepleai-uploads` |
| `S3_REGION` | AWS region (use `auto` for R2) | `auto` |
| `S3_ENDPOINT` | R2 endpoint URL | `https://{id}.r2.cloudflarestorage.com` |
| `S3_BACKUP_BUCKET_NAME` | Backup bucket name | `meepleai-backups` |

### Related Documentation

- [Cloudflare R2 Documentation](https://developers.cloudflare.com/r2/)
- [R2 S3 API Compatibility](https://developers.cloudflare.com/r2/api/s3/api/)
- [AWS SDK for .NET](https://docs.aws.amazon.com/sdk-for-net/)
- [Issue #2703 - Original Requirements](https://github.com/meepleAi-app/meepleai-monorepo/issues/2703)

### Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-01 | Initial documentation |
