# S3 Storage Operations Runbook

**Quick Reference**: Common operations for managing S3-compatible object storage in MeepleAI

## Prerequisites

- AWS CLI installed: `aws --version`
- S3 credentials configured in `infra/secrets/storage.secret`
- Environment variable `STORAGE_PROVIDER=s3` set

## Configuration

### Cloudflare R2 (Recommended)

```bash
# infra/secrets/storage.secret
STORAGE_PROVIDER=s3
S3_ENDPOINT=https://<account-id>.r2.cloudflarestorage.com
S3_ACCESS_KEY=<r2-access-key>
S3_SECRET_KEY=<r2-secret-key>
S3_BUCKET_NAME=meepleai-uploads
S3_REGION=auto
S3_PRESIGNED_URL_EXPIRY=3600
S3_FORCE_PATH_STYLE=false
```

### AWS S3

```bash
S3_ENDPOINT=https://s3.amazonaws.com
S3_REGION=eu-west-1  # Or your region
# ... same keys and bucket
```

### MinIO (Self-Hosted)

```bash
S3_ENDPOINT=http://localhost:9000
S3_FORCE_PATH_STYLE=true  # Required for MinIO
# ... same keys and bucket
```

## Common Operations

### 1. Verify S3 Connectivity

```bash
# Check health endpoint
curl http://localhost:8080/health | jq '.checks.s3storage'

# Expected output (healthy):
# {
#   "status": "Healthy",
#   "description": "S3 storage accessible (endpoint: ..., bucket: ...)"
# }
```

### 2. List Files in Bucket

```bash
# Set credentials from storage.secret
source <(grep -v '^#' infra/secrets/storage.secret | sed 's/^/export /')

# List all files
aws s3 ls s3://$S3_BUCKET_NAME/pdf_uploads/ --recursive --endpoint-url $S3_ENDPOINT

# List files for specific game
aws s3 ls s3://$S3_BUCKET_NAME/pdf_uploads/<game-id>/ --endpoint-url $S3_ENDPOINT
```

### 3. Migrate Existing Files

```bash
# Dry run (preview)
pwsh tools/migrate-local-to-s3.ps1 -DryRun

# Execute migration
pwsh tools/migrate-local-to-s3.ps1

# Migrate and delete local files after verification
pwsh tools/migrate-local-to-s3.ps1 -DeleteLocal -Confirm
```

### 4. Download File from S3

```bash
# Download specific file
aws s3 cp s3://$S3_BUCKET_NAME/pdf_uploads/<game-id>/<file-id>_<filename> \
    ./downloaded.pdf \
    --endpoint-url $S3_ENDPOINT

# Download all files for a game
aws s3 sync s3://$S3_BUCKET_NAME/pdf_uploads/<game-id>/ \
    ./game-pdfs/ \
    --endpoint-url $S3_ENDPOINT
```

### 5. Delete Files

```bash
# Delete specific file
aws s3 rm s3://$S3_BUCKET_NAME/pdf_uploads/<game-id>/<file-id>_<filename> \
    --endpoint-url $S3_ENDPOINT

# Delete all files for a game
aws s3 rm s3://$S3_BUCKET_NAME/pdf_uploads/<game-id>/ \
    --recursive \
    --endpoint-url $S3_ENDPOINT
```

### 6. Bucket Operations

```bash
# Create bucket (Cloudflare R2 via dashboard, AWS via CLI)
aws s3 mb s3://$S3_BUCKET_NAME --endpoint-url $S3_ENDPOINT

# List buckets
aws s3 ls --endpoint-url $S3_ENDPOINT

# Check bucket size
aws s3 ls s3://$S3_BUCKET_NAME/pdf_uploads/ --recursive --summarize --endpoint-url $S3_ENDPOINT
```

## Troubleshooting

### Health Check Failures

**Symptom**: `/health` endpoint shows `s3storage: Unhealthy`

**Possible Causes**:

1. **Configuration missing**:
   ```bash
   # Check if storage.secret exists
   cat infra/secrets/storage.secret

   # Verify environment loading
   docker compose config | grep S3_
   ```

2. **Invalid credentials**:
   ```bash
   # Test credentials manually
   aws s3 ls s3://$S3_BUCKET_NAME --endpoint-url $S3_ENDPOINT

   # Expected: list of files or empty (success)
   # Error: 403 Forbidden = invalid credentials
   ```

3. **Bucket doesn't exist**:
   ```bash
   # Create bucket
   aws s3 mb s3://$S3_BUCKET_NAME --endpoint-url $S3_ENDPOINT
   ```

4. **Network connectivity**:
   ```bash
   # Test endpoint reachability
   curl -I $S3_ENDPOINT

   # For Cloudflare R2, ensure account ID is correct
   ```

### Upload Failures

**Symptom**: PDF uploads fail with `BlobStorageResult.Success = false`

**Debugging**:

1. **Check application logs**:
   ```bash
   docker compose logs api | grep "S3 error"

   # Common errors:
   # - NoSuchBucket: Bucket doesn't exist
   # - InvalidAccessKeyId: Credentials invalid
   # - SignatureDoesNotMatch: Secret key mismatch
   ```

2. **Test with AWS CLI**:
   ```bash
   # Upload test file
   echo "test" > test.pdf
   aws s3 cp test.pdf s3://$S3_BUCKET_NAME/test/ --endpoint-url $S3_ENDPOINT

   # If this fails, issue is with S3 config, not application
   ```

3. **Verify bucket policy** (AWS S3 only):
   ```bash
   aws s3api get-bucket-policy --bucket $S3_BUCKET_NAME

   # Ensure policy allows PutObject, GetObject, DeleteObject
   ```

### Migration Issues

**Symptom**: Migration script fails or shows errors

**Solutions**:

1. **AWS CLI not found**:
   ```bash
   # Install AWS CLI
   # Windows: choco install awscli
   # Linux: apt install awscli
   # macOS: brew install awscli
   ```

2. **Permission denied**:
   ```bash
   # Check storage.secret permissions
   chmod 600 infra/secrets/storage.secret
   ```

3. **Partial migration** (some files failed):
   ```bash
   # Retry migration (skips existing files)
   pwsh tools/migrate-local-to-s3.ps1

   # Or manually upload failed files
   aws s3 sync pdf_uploads/ s3://$S3_BUCKET_NAME/pdf_uploads/ --endpoint-url $S3_ENDPOINT
   ```

## Monitoring

### Storage Metrics

```bash
# Check bucket size and file count
aws s3 ls s3://$S3_BUCKET_NAME/pdf_uploads/ --recursive --summarize --human-readable --endpoint-url $S3_ENDPOINT

# Output:
# Total Objects: 1234
# Total Size: 4.5 GB
```

### Cost Monitoring (Cloudflare R2)

```bash
# R2 Dashboard: https://dash.cloudflare.com/
# Navigate to: R2 -> <bucket> -> Metrics
# Monitor: Storage (GB), Requests (Class A/B), Egress (free)
```

### Performance Metrics

Application logs track S3 operation latency:

```bash
# Search logs for S3 performance
docker compose logs api | grep "Stored file to S3"

# Example output:
# [INF] Stored file to S3: pdf_uploads/game123/abc_rulebook.pdf (size: 2048000 bytes, ETag: "...")
```

## Backup & Restore

### Backup S3 Bucket

```bash
# Full bucket backup to local directory
aws s3 sync s3://$S3_BUCKET_NAME/ ./s3-backup/ --endpoint-url $S3_ENDPOINT

# Incremental backup (only new/changed files)
aws s3 sync s3://$S3_BUCKET_NAME/ ./s3-backup/ --endpoint-url $S3_ENDPOINT --size-only
```

### Restore from Backup

```bash
# Restore full bucket
aws s3 sync ./s3-backup/ s3://$S3_BUCKET_NAME/ --endpoint-url $S3_ENDPOINT

# Restore specific game
aws s3 sync ./s3-backup/pdf_uploads/<game-id>/ s3://$S3_BUCKET_NAME/pdf_uploads/<game-id>/ --endpoint-url $S3_ENDPOINT
```

## Security Best Practices

1. **Credentials Rotation** (every 90 days):
   ```bash
   # Cloudflare R2: Dashboard -> API Tokens -> Regenerate
   # AWS S3: IAM -> Users -> Security credentials -> Create access key
   # Update storage.secret and restart API
   docker compose restart api
   ```

2. **Bucket Encryption** (AWS S3):
   ```bash
   aws s3api put-bucket-encryption \
       --bucket $S3_BUCKET_NAME \
       --server-side-encryption-configuration '{
         "Rules": [{
           "ApplyServerSideEncryptionByDefault": {
             "SSEAlgorithm": "AES256"
           }
         }]
       }'
   ```

3. **Access Logging** (AWS S3):
   ```bash
   aws s3api put-bucket-logging \
       --bucket $S3_BUCKET_NAME \
       --bucket-logging-status '{
         "LoggingEnabled": {
           "TargetBucket": "meepleai-logs",
           "TargetPrefix": "s3-access-logs/"
         }
       }'
   ```

## Provider-Specific Notes

### Cloudflare R2

- **Zero egress fees**: No charges for downloads
- **EU jurisdiction**: GDPR-compliant storage
- **Custom domains**: Configure via R2 Dashboard -> Public Buckets
- **CDN integration**: Automatic Cloudflare CDN caching

### AWS S3

- **Egress fees**: $0.09/GB after 100GB free tier
- **Glacier storage**: Use lifecycle policies for archival
- **CloudFront**: Configure CDN for better performance

### MinIO

- **Self-hosted**: Full control over infrastructure
- **S3-compatible**: Drop-in replacement for AWS S3
- **High performance**: Optimized for throughput
- **Path-style URLs**: Set `S3_FORCE_PATH_STYLE=true`

### Backblaze B2

- **Low cost**: $0.005/GB storage, $0.01/GB egress
- **S3-compatible API**: Use S3 endpoint URLs
- **Free tier**: 10GB storage + 1GB egress daily

## References

- Cloudflare R2: https://developers.cloudflare.com/r2/
- AWS S3: https://docs.aws.amazon.com/s3/
- MinIO: https://min.io/docs/
- Backblaze B2: https://www.backblaze.com/b2/docs/
- AWS CLI: https://docs.aws.amazon.com/cli/

---

**Last Updated**: 2026-02-05
**Related**: Issue #2703, `storage.secret.example`, `migrate-local-to-s3.ps1`
