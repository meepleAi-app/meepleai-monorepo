# S3-Compatible Storage Options for MeepleAI

> **Last Updated**: 2026-01-20
> **Status**: Research Complete
> **Decision Required**: Choose between cloud-hosted or self-hosted solution

## Executive Summary

MeepleAI requires S3-compatible object storage for:
- **PDF uploads**: User-uploaded board game rulebooks (primary use case)
- **Database backups**: Automated PostgreSQL backups via n8n
- **Document processing**: Temporary storage during extraction pipeline

**Recommendation**: **Cloudflare R2** for cloud-hosted or **Garage** for self-hosted.

---

## Use Case Analysis for MeepleAI

### Estimated Storage Requirements

| Category | Estimate | Notes |
|----------|----------|-------|
| PDF uploads | 50-200 GB/year | ~500 rulebooks × 100-400 KB avg |
| Database backups | 10-50 GB | Daily backups, 30-day retention |
| Temp processing | 5-10 GB | Ephemeral, cleared after processing |
| **Total Year 1** | **~100 GB** | Conservative estimate |
| **Total Year 3** | **~500 GB** | With growth |

### Access Patterns

- **Writes**: Moderate (PDF uploads, daily backups)
- **Reads**: High (serving PDFs to users, RAG pipeline)
- **Egress**: Medium-High (PDF downloads, API responses)

---

## Cloud-Hosted Options

### 1. Cloudflare R2 (Recommended for Cloud)

| Aspect | Details |
|--------|---------|
| **Pricing** | $0.015/GB-month storage |
| **Egress** | **FREE** (zero egress fees) |
| **Free Tier** | 10 GB storage + 1M ops/month |
| **Operations** | Class A: $4.50/M, Class B: $0.36/M |

**Cost Estimate for MeepleAI:**

| Usage | Year 1 | Year 3 |
|-------|--------|--------|
| Storage (100GB → 500GB) | $1.50/mo | $7.50/mo |
| Operations (est. 1M/mo) | ~$1/mo | ~$2/mo |
| Egress | $0 | $0 |
| **Monthly Total** | **~$2.50** | **~$9.50** |
| **Annual Total** | **~$30** | **~$114** |

**Pros:**
- Zero egress fees (critical for serving PDFs)
- S3-compatible API
- Global CDN integration
- Free migration tool (Super Slurper)
- Workers integration for edge computing

**Cons:**
- Vendor lock-in to Cloudflare ecosystem
- Infrequent Access tier has retrieval fees
- Smaller ecosystem than AWS

**Configuration:**
```yaml
# infra/secrets/storage.secret
S3_ENDPOINT=https://<account-id>.r2.cloudflarestorage.com
S3_ACCESS_KEY=your_r2_access_key
S3_SECRET_KEY=your_r2_secret_key
S3_BUCKET_NAME=meepleai-uploads
S3_REGION=auto

S3_BACKUP_ENDPOINT=https://<account-id>.r2.cloudflarestorage.com
S3_BACKUP_ACCESS_KEY=your_r2_access_key
S3_BACKUP_SECRET_KEY=your_r2_secret_key
S3_BACKUP_BUCKET_NAME=meepleai-backups
```

---

### 2. Backblaze B2

| Aspect | Details |
|--------|---------|
| **Pricing** | $0.006/GB-month ($6/TB) |
| **Egress** | Free up to 3x storage, then $0.01/GB |
| **CDN Partners** | FREE egress via Cloudflare, Fastly, bunny.net |
| **Free Tier** | 10 GB storage |

**Cost Estimate for MeepleAI:**

| Usage | Year 1 | Year 3 |
|-------|--------|--------|
| Storage (100GB → 500GB) | $0.60/mo | $3/mo |
| Egress (via Cloudflare) | $0 | $0 |
| **Monthly Total** | **~$0.60** | **~$3** |
| **Annual Total** | **~$7** | **~$36** |

**Pros:**
- Cheapest storage pricing
- Free egress with CDN partners (Cloudflare, Fastly)
- 3x free egress even without CDN
- Strong compliance features

**Cons:**
- Need to set up CDN for free egress
- Slightly more complex architecture
- B2 Overdrive (high perf) costs more

**Configuration:**
```yaml
# With Cloudflare CDN in front for free egress
S3_ENDPOINT=s3.us-west-004.backblazeb2.com
S3_BUCKET_NAME=meepleai-uploads
S3_REGION=us-west-004
```

---

### 3. DigitalOcean Spaces

| Aspect | Details |
|--------|---------|
| **Pricing** | $5/mo for 250GB + 1TB transfer |
| **Overage** | $0.02/GB storage, $0.01/GB transfer |
| **CDN** | Built-in, included |

**Cost Estimate for MeepleAI:**

| Usage | Year 1 | Year 3 |
|-------|--------|--------|
| Base plan | $5/mo | $5/mo |
| Overage storage | $0 | ~$5/mo |
| **Monthly Total** | **$5** | **~$10** |
| **Annual Total** | **$60** | **~$120** |

**Pros:**
- Simple flat pricing
- Built-in CDN
- Good DigitalOcean ecosystem integration
- Easy setup

**Cons:**
- More expensive than R2/B2 for low storage
- Transfer fees after 1TB
- Less S3-compatible than others

---

### 4. AWS S3

| Aspect | Details |
|--------|---------|
| **Pricing** | $0.023/GB-month (Standard) |
| **Egress** | $0.09/GB (first 10TB) |
| **Free Tier** | 5GB for 12 months |

**Cost Estimate for MeepleAI:**

| Usage | Year 1 | Year 3 |
|-------|--------|--------|
| Storage (100GB → 500GB) | $2.30/mo | $11.50/mo |
| Egress (50GB/mo) | $4.50/mo | $9/mo |
| **Monthly Total** | **~$7** | **~$20** |
| **Annual Total** | **~$84** | **~$240** |

**Pros:**
- Industry standard
- Most complete feature set
- Best tooling and documentation
- Enterprise-grade reliability

**Cons:**
- Expensive egress fees
- Complex pricing model
- Overkill for small projects

---

## Self-Hosted Options

### 1. Garage (Recommended for Self-Hosted)

| Aspect | Details |
|--------|---------|
| **License** | AGPLv3 |
| **Language** | Rust |
| **Min Resources** | 256MB RAM, low CPU |
| **Best For** | Geo-distributed, edge deployments |

**Cost Estimate (VPS):**

| Setup | Monthly Cost |
|-------|--------------|
| Single node (Hetzner CX22) | ~€4/mo |
| 3-node cluster (resilient) | ~€12/mo |
| Storage (100GB SSD) | Included |

**Pros:**
- Lightweight, single binary
- Geo-distribution built-in
- No central coordinator (gossip protocol)
- Active development with EU funding
- Perfect for small-scale self-hosting

**Cons:**
- Limited S3 API coverage
- Lower throughput than MinIO (~5Gbps vs 20Gbps)
- Smaller community
- CLI-focused operations

**Docker Compose:**
```yaml
services:
  garage:
    image: dxflrs/garage:v1.0.0
    container_name: meepleai-garage
    restart: unless-stopped
    ports:
      - "3900:3900"  # S3 API
      - "3901:3901"  # Admin API
      - "3902:3902"  # Web (optional)
    volumes:
      - garage_data:/var/lib/garage/data
      - garage_meta:/var/lib/garage/meta
      - ./garage.toml:/etc/garage.toml:ro
    environment:
      GARAGE_CONFIG_FILE: /etc/garage.toml
    networks:
      - meepleai

volumes:
  garage_data:
  garage_meta:
```

**garage.toml:**
```toml
metadata_dir = "/var/lib/garage/meta"
data_dir = "/var/lib/garage/data"

replication_mode = "1"  # Single node, use "3" for production

[rpc]
bind_addr = "[::]:3901"
secret = "your-rpc-secret-here"

[s3_api]
s3_region = "garage"
api_bind_addr = "[::]:3900"

[s3_web]
bind_addr = "[::]:3902"
root_domain = ".web.garage.localhost"

[admin]
api_bind_addr = "[::]:3903"
```

---

### 2. MinIO (Maintenance Mode Warning)

| Aspect | Details |
|--------|---------|
| **License** | AGPLv3 |
| **Status** | ⚠️ Community edition in maintenance mode |
| **Min Resources** | 2GB RAM, 2 CPU |

**Important Update (Dec 2025):**
> MinIO community edition is now source-only distribution. No pre-compiled binaries. Must build from source with Go 1.24+.

**Pros:**
- Highest performance (~20-25 Gbps)
- Most complete S3 API
- Large community and documentation
- Kubernetes-native

**Cons:**
- ⚠️ Community edition entering maintenance
- Higher resource requirements
- Enterprise features require license
- Uncertain future for open-source version

**Not Recommended** due to maintenance mode status. Consider Garage or RustFS instead.

---

### 3. SeaweedFS

| Aspect | Details |
|--------|---------|
| **License** | Apache 2.0 |
| **Language** | Go |
| **Min Resources** | 512MB RAM |

**Pros:**
- Apache 2.0 license (more permissive)
- Good for large files
- Tiered storage support
- Active development

**Cons:**
- More complex architecture (master + volume servers)
- S3 gateway is secondary feature
- Less documentation

---

## Comparison Matrix

| Feature | Cloudflare R2 | Backblaze B2 | DigitalOcean | AWS S3 | Garage | MinIO |
|---------|---------------|--------------|--------------|--------|--------|-------|
| **Storage/GB** | $0.015 | $0.006 | $0.02 | $0.023 | VPS cost | VPS cost |
| **Egress** | FREE | Free (CDN) | $0.01 | $0.09 | FREE | FREE |
| **S3 Compat** | High | High | Medium | Native | Medium | High |
| **Setup** | Easy | Medium | Easy | Medium | Medium | Medium |
| **Free Tier** | 10GB | 10GB | N/A | 5GB/12mo | N/A | N/A |
| **Best For** | High egress | Budget | DO users | Enterprise | Self-host | Perf |

---

## Recommendations

### For MeepleAI Production (Cloud)

**Primary: Cloudflare R2**
- Zero egress fees critical for PDF serving
- Low cost at our scale (~$30/year)
- S3-compatible API works with existing code
- Easy CDN integration

### For MeepleAI Self-Hosted

**Primary: Garage**
- Lightweight, runs on minimal VPS
- Good enough S3 compatibility
- Active development with funding
- Geo-distribution if needed later

### Migration Path

1. **Start**: Cloudflare R2 (easiest, cheapest for our scale)
2. **Scale**: Backblaze B2 + Cloudflare CDN (if storage grows significantly)
3. **Self-host**: Garage on dedicated VPS (if cost optimization needed)

---

## Implementation Checklist

### For Cloudflare R2

- [ ] Create Cloudflare account
- [ ] Enable R2 in dashboard
- [ ] Create buckets: `meepleai-uploads`, `meepleai-backups`
- [ ] Generate API tokens with appropriate permissions
- [ ] Update `infra/secrets/storage.secret`
- [ ] Configure n8n backup workflow
- [ ] Test integration with API service

### For Garage (Self-Hosted)

- [ ] Provision VPS (Hetzner CX22 or similar)
- [ ] Deploy Garage via Docker Compose
- [ ] Initialize cluster and create buckets
- [ ] Generate access keys
- [ ] Update `infra/secrets/storage.secret`
- [ ] Configure backup workflow
- [ ] Set up monitoring

---

## Current Codebase Implementation

### Storage Service Architecture

MeepleAI currently uses **local file storage** via `IBlobStorageService`. The service is designed with a clean abstraction that makes S3 migration straightforward.

**Key Files:**
- `Api/Services/Pdf/IBlobStorageService.cs` - Storage interface
- `Api/Services/Pdf/BlobStorageService.cs` - Local filesystem implementation

### Interface Contract

```csharp
internal interface IBlobStorageService
{
    Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct);
    Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct);
    Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct);
    string GetStoragePath(string fileId, string gameId, string fileName);
    bool Exists(string fileId, string gameId);
}
```

### Storage Lifecycle

```
PDF Upload → Validation → Store → DB Record → Background Processing
                           ↓
                    BlobStorageService.StoreAsync()
                           ↓
                    Local: pdf_uploads/{gameId}/{fileId}_{filename}
                    S3:    s3://bucket/{gameId}/{fileId}_{filename}
```

### Usage in Handlers

| Handler | Operations | Notes |
|---------|-----------|-------|
| `UploadPdfCommandHandler` | `StoreAsync()` | Primary upload path |
| `DeletePdfCommandHandler` | `DeleteAsync()` | Cleanup on delete |
| `DownloadPdfQueryHandler` | `RetrieveAsync()` | Serve to users |
| `CompleteChunkedUploadCommandHandler` | `StoreAsync()` | Large file support |

### Migration Path to S3

**Option A: Drop-in S3 Implementation** (Recommended)

Create `S3BlobStorageService` implementing `IBlobStorageService`:

```csharp
internal class S3BlobStorageService : IBlobStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public async Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct)
    {
        var fileId = Guid.NewGuid().ToString("N");
        var key = $"{gameId}/{fileId}_{SanitizeFileName(fileName)}";

        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = "application/pdf"
        };

        await _s3Client.PutObjectAsync(putRequest, ct);

        return new BlobStorageResult(true, fileId, key, stream.Length);
    }

    // ... other methods
}
```

**Option B: Hybrid Approach** (Gradual Migration)

1. Keep local storage for processing
2. Sync to S3 after processing completes
3. Serve from S3 via CDN for downloads

### Configuration Pattern

**Current (Local):**
```yaml
# appsettings.json
PDF_STORAGE_PATH: "/data/pdf_uploads"
```

**Future (S3):**
```yaml
# Environment variables from secrets
S3_ENDPOINT: "https://account.r2.cloudflarestorage.com"
S3_ACCESS_KEY: "${S3_ACCESS_KEY}"
S3_SECRET_KEY: "${S3_SECRET_KEY}"
S3_BUCKET_NAME: "meepleai-uploads"
STORAGE_PROVIDER: "s3"  # or "local" for dev
```

### DI Registration

```csharp
// ApplicationServiceExtensions.cs
services.AddSingleton<IBlobStorageService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var storageProvider = config["STORAGE_PROVIDER"] ?? "local";

    return storageProvider switch
    {
        "s3" => new S3BlobStorageService(config, sp.GetRequiredService<ILogger<S3BlobStorageService>>()),
        _ => new BlobStorageService(config, sp.GetRequiredService<ILogger<BlobStorageService>>())
    };
});
```

### Backup Integration

The n8n backup workflow (`infra/n8n/templates/backup-automation.json`) is designed to:
1. Dump PostgreSQL database
2. Upload to S3 bucket
3. Clean up old backups based on retention policy

**Required Secrets:**
```yaml
# infra/secrets/storage.secret
S3_BACKUP_ACCESS_KEY=...
S3_BACKUP_SECRET_KEY=...
S3_BACKUP_BUCKET_NAME=meepleai-backups
S3_BACKUP_ENDPOINT=...
S3_BACKUP_RETENTION_DAYS=30
```

### Security Considerations

Current implementation includes:
- **Path Traversal Protection**: `PathSecurity.ValidateIdentifier()` and `ValidatePathIsInDirectory()`
- **Filename Sanitization**: `SanitizeFileName()` removes dangerous characters
- **Access Control**: Files organized by `gameId` for tenant isolation

S3 implementation should add:
- Pre-signed URLs for secure downloads
- Bucket policies restricting access
- Server-side encryption (SSE-S3 or SSE-KMS)
- CORS configuration for direct browser uploads

---

## References

### Cloud Providers
- [Cloudflare R2 Pricing](https://developers.cloudflare.com/r2/pricing/)
- [Cloudflare R2 vs AWS S3](https://www.cloudflare.com/pg-cloudflare-r2-vs-aws-s3/)
- [Backblaze B2 Pricing](https://www.backblaze.com/cloud-storage/pricing)
- [DigitalOcean S3 Alternatives](https://www.digitalocean.com/resources/articles/amazon-s3-alternatives)

### Self-Hosted
- [Garage Documentation](https://garagehq.deuxfleurs.fr/)
- [Garage GitHub](https://github.com/deuxfleurs-org/garage)
- [MinIO Maintenance Announcement](https://www.infoq.com/news/2025/12/minio-s3-api-alternatives/)
- [S3 Storage Benchmarks](https://www.repoflow.io/blog/benchmarking-self-hosted-s3-compatible-storage-a-practical-performance-comparison)

### Cost Calculators
- [Cloudflare R2 Calculator](https://r2-calculator.cloudflare.com/)
- [Cloud Storage Cost Comparison](https://transactional.blog/blog/2023-cloud-storage-costs)
