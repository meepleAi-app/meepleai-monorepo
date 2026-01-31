# Secrets Management Guide

> **Last Updated**: 2026-01-17
> **Related ADR**: [ADR-021 - Auto-Configuration System](../01-architecture/adr/adr-021-auto-configuration-system.md)
> **Detailed Reference**: [infra/secrets/README.md](../../infra/secrets/README.md)

## Overview

This guide covers **production-grade secret management** for MeepleAI deployment, including rotation strategies, cloud integration, backup/recovery, and security compliance.

For **development setup**, see [Auto-Configuration Guide](./auto-configuration-guide.md).
For **complete secret file reference**, see [infra/secrets/README.md](../../infra/secrets/README.md).

**Scope**:
- 🏢 Production deployment patterns
- 🔄 Secret rotation automation
- ☁️ Cloud secrets manager integration (AWS/Azure/GCP)
- 🔐 Encryption at rest & in transit
- 📋 Compliance requirements (SOC2, GDPR, PCI DSS)
- 🚨 Incident response procedures

---

## Secret Organization

### Directory Structure

```
infra/secrets/
├── .gitignore                    # Excludes *.secret and backups
├── README.md                     # Complete reference guide
├── setup-secrets.ps1             # Auto-configuration script
│
├── *.secret.example              # Template files (committed)
│
├── *.secret                      # Active secrets (git-ignored)
│
├── staging/                      # Staging environment
│   └── *.secret
│
├── prod/                         # Production environment
│   └── *.secret.encrypted        # Encrypted with GPG/SOPS
│
└── .generated-values-*.txt       # Backup files (git-ignored, delete after use)
```

### Secret Categories

| Category | Files (Count) | Priority | Deployment Strategy |
|----------|---------------|----------|---------------------|
| **Core Infrastructure** | 6 | 🔴 CRITICAL | Cloud secrets manager, auto-rotate |
| **AI Services** | 5 | 🟡 IMPORTANT | Vault + fallback config |
| **External APIs** | 3 | 🟢 OPTIONAL | Environment variables |
| **Monitoring** | 3 | 🟢 OPTIONAL | Local files (encrypted) |

---

## Encryption at Rest

### GPG Encryption (Recommended for Git-Tracked Secrets)

**Use Case**: Store production secrets in repository with encryption.

#### Setup GPG Key

```bash
# Generate GPG key pair
gpg --full-generate-key
# Select: (1) RSA and RSA
# Key size: 4096 bits
# Expiration: 1 year
# Name: MeepleAI Production Secrets
# Email: devops@meepleai.com

# Export public key
gpg --export -a "MeepleAI Production Secrets" > production-key.pub

# List keys
gpg --list-keys
# Note the key ID (e.g., 1A2B3C4D5E6F7G8H)
```

#### Encrypt Secrets

```bash
# Encrypt single file
gpg --encrypt --recipient "MeepleAI Production Secrets" \
    -o infra/secrets/prod/database.secret.encrypted \
    infra/secrets/database.secret

# Encrypt all production secrets
cd infra/secrets/prod
for file in ../*.secret; do
    gpg --encrypt --recipient "MeepleAI Production Secrets" \
        -o "$(basename $file).encrypted" "$file"
done

# Commit encrypted files
git add *.encrypted
git commit -m "chore(secrets): encrypt production secrets"
```

#### Decrypt Secrets (Deployment)

```bash
# Decrypt single file
gpg --decrypt infra/secrets/prod/database.secret.encrypted > infra/secrets/database.secret

# Decrypt all for deployment
cd infra/secrets/prod
for file in *.encrypted; do
    gpg --decrypt "$file" > "../${file%.encrypted}"
done
```

### SOPS (Mozilla Secrets OPerationS) - Alternative

**Benefits**: YAML-aware, partial encryption, multi-cloud KMS integration.

#### Install SOPS

```bash
# macOS
brew install sops

# Linux
wget https://github.com/mozilla/sops/releases/download/v3.8.1/sops-v3.8.1.linux.amd64
chmod +x sops-v3.8.1.linux.amd64
sudo mv sops-v3.8.1.linux.amd64 /usr/local/bin/sops
```

#### Configure SOPS with AWS KMS

```yaml
# .sops.yaml
creation_rules:
  - path_regex: infra/secrets/prod/.*\.secret$
    kms: 'arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012'
    encrypted_regex: '^(.*_PASSWORD|.*_SECRET_KEY|.*_API_KEY)$'
```

#### Encrypt with SOPS

```bash
# Encrypt file
sops -e infra/secrets/prod/database.secret > infra/secrets/prod/database.secret.enc

# Edit encrypted file (auto-decrypt in editor)
sops infra/secrets/prod/database.secret.enc

# Decrypt for deployment
sops -d infra/secrets/prod/database.secret.enc > infra/secrets/database.secret
```

---

## Cloud Secrets Manager Integration

### AWS Secrets Manager

#### Store Secrets

```bash
# Create secret
aws secretsmanager create-secret \
    --name meepleai/prod/database \
    --secret-string '{"POSTGRES_USER":"meepleai","POSTGRES_PASSWORD":"strong_password_here","POSTGRES_DB":"meepleai_db"}'

# Update secret
aws secretsmanager update-secret \
    --secret-id meepleai/prod/database \
    --secret-string '{"POSTGRES_USER":"meepleai","POSTGRES_PASSWORD":"new_password","POSTGRES_DB":"meepleai_db"}'

# List secrets
aws secretsmanager list-secrets --filters Key=name,Values=meepleai/
```

#### Retrieve Secrets (Deployment)

```bash
# Load into environment
export POSTGRES_USER=$(aws secretsmanager get-secret-value \
    --secret-id meepleai/prod/database \
    --query SecretString --output text | jq -r .POSTGRES_USER)

export POSTGRES_PASSWORD=$(aws secretsmanager get-secret-value \
    --secret-id meepleai/prod/database \
    --query SecretString --output text | jq -r .POSTGRES_PASSWORD)

export POSTGRES_DB=$(aws secretsmanager get-secret-value \
    --secret-id meepleai/prod/database \
    --query SecretString --output text | jq -r .POSTGRES_DB)

# Start application (loads from environment)
dotnet run
```

#### Auto-Rotation Lambda

```python
# lambda_rotation.py
import boto3
import os

def lambda_handler(event, context):
    secret_id = event['SecretId']
    token = event['ClientRequestToken']
    step = event['Step']

    sm = boto3.client('secretsmanager')

    if step == 'createSecret':
        # Generate new password
        new_password = generate_strong_password()
        sm.put_secret_value(
            SecretId=secret_id,
            ClientRequestToken=token,
            SecretString=new_password,
            VersionStages=['AWSPENDING']
        )

    elif step == 'setSecret':
        # Update database with new password
        update_database_password(new_password)

    elif step == 'testSecret':
        # Verify new password works
        test_connection(new_password)

    elif step == 'finishSecret':
        # Finalize rotation
        sm.update_secret_version_stage(
            SecretId=secret_id,
            VersionStage='AWSCURRENT',
            MoveToVersionId=token
        )
```

**Schedule Rotation**:
```bash
aws secretsmanager rotate-secret \
    --secret-id meepleai/prod/database \
    --rotation-lambda-arn arn:aws:lambda:us-east-1:123456789012:function:rotate-db-secret \
    --rotation-rules AutomaticallyAfterDays=90
```

### Azure Key Vault

#### Store Secrets

```bash
# Create Key Vault
az keyvault create \
    --name meepleai-vault \
    --resource-group meepleai-prod \
    --location eastus

# Add secret
az keyvault secret set \
    --vault-name meepleai-vault \
    --name postgres-password \
    --value "strong_password_here"

# List secrets
az keyvault secret list --vault-name meepleai-vault
```

#### Retrieve Secrets

```bash
# Load into environment
export POSTGRES_PASSWORD=$(az keyvault secret show \
    --vault-name meepleai-vault \
    --name postgres-password \
    --query value --output tsv)

# Start application
dotnet run
```

### Google Cloud Secret Manager

#### Store Secrets

```bash
# Create secret
echo -n "strong_password_here" | gcloud secrets create postgres-password \
    --data-file=- \
    --replication-policy="automatic"

# Update secret
echo -n "new_password_here" | gcloud secrets versions add postgres-password \
    --data-file=-
```

#### Retrieve Secrets

```bash
# Load into environment
export POSTGRES_PASSWORD=$(gcloud secrets versions access latest \
    --secret="postgres-password")

# Start application
dotnet run
```

---

## Secret Rotation

### Rotation Frequencies (Best Practices)

| Secret Type | Rotation Frequency | Automation | Rationale |
|-------------|-------------------|------------|-----------|
| JWT Secret Key | 90 days | Manual | Invalidates all tokens, requires coordination |
| Database Password | 180 days | Automated (zero-downtime) | High-value target, automated rotation safer |
| API Keys (external) | On compromise | Manual | Provider-controlled, reactive only |
| Admin Password | 90 days | Manual | Human account, enforce via policy |
| Redis Password | 180 days | Automated | Cache invalidation acceptable |
| Service-to-Service | 365 days | Automated | Low exposure, less frequent OK |

### Zero-Downtime Database Rotation

```bash
#!/bin/bash
# rotate-db-password.sh - Zero-downtime PostgreSQL password rotation

set -e

OLD_PASSWORD="$(cat infra/secrets/database.secret | grep POSTGRES_PASSWORD | cut -d= -f2)"
NEW_PASSWORD="$(openssl rand -base64 32)"

echo "Step 1: Create temporary user with new password..."
docker compose exec postgres psql -U postgres -c \
    "CREATE USER meepleai_temp WITH PASSWORD '$NEW_PASSWORD';"

echo "Step 2: Grant same privileges..."
docker compose exec postgres psql -U postgres -c \
    "GRANT ALL PRIVILEGES ON DATABASE meepleai_db TO meepleai_temp;"

echo "Step 3: Update connection string to use new user..."
sed -i "s/POSTGRES_USER=meepleai/POSTGRES_USER=meepleai_temp/" infra/secrets/database.secret
sed -i "s/POSTGRES_PASSWORD=$OLD_PASSWORD/POSTGRES_PASSWORD=$NEW_PASSWORD/" infra/secrets/database.secret

echo "Step 4: Rolling restart API instances (one at a time)..."
docker compose restart api

echo "Step 5: Wait for health check..."
sleep 10
curl -f http://localhost:8080/api/v1/health

echo "Step 6: Drop old user..."
docker compose exec postgres psql -U postgres -c \
    "DROP USER meepleai;"

echo "Step 7: Rename temp user to original..."
docker compose exec postgres psql -U postgres -c \
    "ALTER USER meepleai_temp RENAME TO meepleai;"

echo "Step 8: Update connection string back to original user..."
sed -i "s/POSTGRES_USER=meepleai_temp/POSTGRES_USER=meepleai/" infra/secrets/database.secret

echo "✅ Password rotation completed successfully!"
```

### JWT Secret Rotation (Coordinated Downtime)

```bash
#!/bin/bash
# rotate-jwt-secret.sh - JWT secret rotation with user notification

set -e

echo "⚠️  WARNING: This will invalidate ALL user sessions!"
read -p "Notify users before continuing? (yes/no): " confirm
if [[ "$confirm" != "yes" ]]; then
    echo "Rotation cancelled."
    exit 1
fi

echo "Step 1: Backup current secret..."
cp infra/secrets/jwt.secret infra/secrets/jwt.secret.backup.$(date +%Y%m%d-%H%M%S)

echo "Step 2: Generate new secret..."
NEW_SECRET=$(openssl rand -base64 64)

echo "Step 3: Update jwt.secret..."
sed -i "s/^JWT_SECRET_KEY=.*/JWT_SECRET_KEY=$NEW_SECRET/" infra/secrets/jwt.secret

echo "Step 4: Restart API..."
docker compose restart api

echo "Step 5: Verify health check..."
sleep 5
curl -f http://localhost:8080/api/v1/health

echo "✅ JWT secret rotated successfully!"
echo "📧 ACTION REQUIRED: Notify users that they need to log in again."
```

---

## Backup and Recovery

### Backup Strategy

**Frequency**:
- **Before rotation**: Always backup before changing secrets
- **Weekly**: Automated encrypted backups to secure storage
- **After major changes**: Manual backup with git tag

#### Encrypted Backup

```bash
#!/bin/bash
# backup-secrets.sh - Create encrypted backup of all secrets

BACKUP_DATE=$(date +%Y%m%d-%H%M%S)
BACKUP_DIR="infra/secrets/backups"
BACKUP_FILE="secrets-backup-$BACKUP_DATE.tar.gz.gpg"

mkdir -p "$BACKUP_DIR"

echo "Creating backup..."
tar czf - infra/secrets/*.secret | \
    gpg --encrypt --recipient "MeepleAI Backups" \
    -o "$BACKUP_DIR/$BACKUP_FILE"

echo "✅ Backup created: $BACKUP_DIR/$BACKUP_FILE"
echo "   Size: $(du -h "$BACKUP_DIR/$BACKUP_FILE" | cut -f1)"

# Upload to S3
aws s3 cp "$BACKUP_DIR/$BACKUP_FILE" \
    s3://meepleai-backups/secrets/ \
    --sse AES256

echo "✅ Backup uploaded to S3"
```

#### Recovery Procedure

```bash
#!/bin/bash
# restore-secrets.sh - Restore secrets from encrypted backup

BACKUP_FILE="$1"

if [[ -z "$BACKUP_FILE" ]]; then
    echo "Usage: ./restore-secrets.sh <backup-file>"
    exit 1
fi

echo "⚠️  WARNING: This will overwrite current secrets!"
read -p "Continue? (yes/no): " confirm
if [[ "$confirm" != "yes" ]]; then
    echo "Restore cancelled."
    exit 1
fi

echo "Downloading backup from S3..."
aws s3 cp "s3://meepleai-backups/secrets/$BACKUP_FILE" .

echo "Decrypting and extracting..."
gpg --decrypt "$BACKUP_FILE" | tar xzf -

echo "Verifying restored files..."
ls -la infra/secrets/*.secret

echo "✅ Secrets restored successfully!"
echo "🔄 Restart application to apply changes."
```

---

## Security Compliance

### SOC 2 Compliance

**Requirements**:
- ✅ Secrets stored encrypted at rest
- ✅ Access logs for secret retrieval
- ✅ Secrets rotated every 90-180 days
- ✅ Secrets not committed to git
- ✅ Least-privilege access control

**Audit Trail**:
```bash
# AWS Secrets Manager audit trail
aws cloudtrail lookup-events \
    --lookup-attributes AttributeKey=ResourceName,AttributeValue=meepleai/prod/database \
    --max-items 50

# Who accessed which secret when?
aws cloudtrail lookup-events \
    --lookup-attributes AttributeKey=EventName,AttributeValue=GetSecretValue \
    --max-items 100 \
    --query 'Events[].{Time:EventTime,User:Username,Secret:Resources[0].ResourceName}'
```

### GDPR Compliance

**Requirements**:
- ✅ User passwords hashed with bcrypt (not stored in secrets)
- ✅ Secrets containing PII encrypted at rest
- ✅ Right to erasure: Delete user secrets on account deletion
- ✅ Data portability: Export user data (excluding secrets)

### PCI DSS Compliance (if handling payments)

**Requirements**:
- ✅ Secrets encrypted with AES-256
- ✅ Quarterly rotation for critical secrets
- ✅ Multi-factor authentication for secret access
- ✅ Secrets never logged or displayed in plaintext

---

## Incident Response

### Secret Compromise Procedure

**Immediate Actions** (Within 1 hour):

```bash
# 1. REVOKE compromised secret
aws secretsmanager update-secret \
    --secret-id meepleai/prod/database \
    --secret-string '{"POSTGRES_PASSWORD":"REVOKED_ROTATE_IMMEDIATELY"}'

# 2. ROTATE to new secret
./rotate-db-password.sh

# 3. AUDIT access logs
aws cloudtrail lookup-events \
    --lookup-attributes AttributeKey=ResourceName,AttributeValue=meepleai/prod/database \
    --start-time $(date -u -d '7 days ago' +%Y-%m-%dT%H:%M:%S) \
    --end-time $(date -u +%Y-%m-%dT%H:%M:%S)

# 4. NOTIFY security team
echo "SECURITY INCIDENT: Database secret compromised" | \
    mail -s "URGENT: Secret Compromise" security@meepleai.com
```

**Follow-Up Actions** (Within 24 hours):
1. Root cause analysis: How was secret exposed?
2. Git history scan: Search for accidentally committed secrets
3. Log analysis: Check for unauthorized access attempts
4. Rotate related secrets: If database compromised, rotate API keys too
5. Update runbooks: Document lessons learned

### Git History Cleanup (Leaked Secrets)

```bash
# Scan for accidentally committed secrets
git log -S "POSTGRES_PASSWORD=" --all --oneline

# Remove secret from history (DANGER: rewrites history)
git filter-branch --force --index-filter \
    "git rm --cached --ignore-unmatch infra/secrets/database.secret" \
    --prune-empty --tag-name-filter cat -- --all

# Force push (coordinate with team first!)
git push origin --force --all
git push origin --force --tags

# Invalidate compromised secret
./rotate-db-password.sh
```

---

## Best Practices Checklist

### Development

- [ ] Use `setup-secrets.ps1` for auto-generation
- [ ] Save generated values with `-SaveGenerated`
- [ ] Store backup in password manager (1Password, Bitwarden)
- [ ] Delete `.generated-values-*.txt` after storing
- [ ] Never commit `*.secret` files to git
- [ ] Use weak passwords for development (acceptable)

### Staging

- [ ] Use separate secrets from production
- [ ] Encrypt secrets with GPG or SOPS
- [ ] Store encrypted files in git
- [ ] Test rotation procedures monthly
- [ ] Validate health check integration

### Production

- [ ] Use cloud secrets manager (AWS/Azure/GCP)
- [ ] Enable auto-rotation for critical secrets
- [ ] Set up audit logging (CloudTrail/Activity Log)
- [ ] Configure alerts for secret access
- [ ] Document rotation procedures
- [ ] Test disaster recovery quarterly

---

## Related Documentation

- **Architecture Decision**: [ADR-021 - Auto-Configuration System](../01-architecture/adr/adr-021-auto-configuration-system.md)
- **Deployment Guide**: [Auto-Configuration Guide](./auto-configuration-guide.md)
- **Complete Reference**: [infra/secrets/README.md](../../infra/secrets/README.md)
- **Health Check System**: [Health Checks](./health-checks.md)

---

**Maintained by**: MeepleAI Security Team
**Questions**: security@meepleai.com
