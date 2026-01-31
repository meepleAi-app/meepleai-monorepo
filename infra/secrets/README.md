# MeepleAI Secrets Management

**3-Level Validation System**: CRITICAL → IMPORTANT → OPTIONAL

---

## Quick Reference - All Secret Files

| File | Priority | Variables | Default Values | Path |
|------|----------|-----------|----------------|------|
| **admin.secret** | 🔴 CRITICAL | `ADMIN_EMAIL`<br>`ADMIN_PASSWORD`<br>`ADMIN_DISPLAY_NAME` | `admin@meepleai.local`<br>`change_me_min_8_chars...`<br>`System Administrator` | `infra/secrets/admin.secret` |
| **database.secret** | 🔴 CRITICAL | `POSTGRES_USER`<br>`POSTGRES_PASSWORD`<br>`POSTGRES_DB` | `meepleai`<br>`change_me_strong_password...`<br>`meepleai_db` | `infra/secrets/database.secret` |
| **embedding-service.secret** | 🔴 CRITICAL | `EMBEDDING_SERVICE_API_KEY` | `change_me_embedding_service...` | `infra/secrets/embedding-service.secret` |
| **jwt.secret** | 🔴 CRITICAL | `JWT_SECRET_KEY`<br>`JWT_ISSUER`<br>`JWT_AUDIENCE` | `change_me_use_openssl...`<br>`meepleai-api`<br>`meepleai-web` | `infra/secrets/jwt.secret` |
| **qdrant.secret** | 🔴 CRITICAL | `QDRANT_API_KEY` | `change_me_qdrant_api_key...` | `infra/secrets/qdrant.secret` |
| **redis.secret** | 🔴 CRITICAL | `REDIS_PASSWORD` | `change_me_strong_redis...` | `infra/secrets/redis.secret` |
| **bgg.secret** | 🟡 IMPORTANT | `BGG_USERNAME`<br>`BGG_PASSWORD` | `your_bgg_username`<br>`your_bgg_password` | `infra/secrets/bgg.secret` |
| **openrouter.secret** | 🟡 IMPORTANT | `OPENROUTER_API_KEY`<br>`OPENROUTER_DEFAULT_MODEL` | `sk-or-v1-change_me...`<br>`meta-llama/llama-3.3-70b-instruct:free` | `infra/secrets/openrouter.secret` |
| **unstructured-service.secret** | 🟡 IMPORTANT | `UNSTRUCTURED_API_KEY` | `change_me_unstructured...` | `infra/secrets/unstructured-service.secret` |
| **email.secret** | 🟢 OPTIONAL | `SMTP_HOST`<br>`SMTP_PORT`<br>`SMTP_USER`<br>`SMTP_PASSWORD`<br>`SMTP_FROM_EMAIL` | `smtp.gmail.com`<br>`587`<br>`your_email@gmail.com`<br>`your_app_password...`<br>`noreply@meepleai.local` | `infra/secrets/email.secret` |
| **monitoring.secret** | 🟢 OPTIONAL | `GRAFANA_ADMIN_PASSWORD`<br>`PROMETHEUS_PASSWORD` | `change_me_grafana...`<br>`change_me_prometheus...` | `infra/secrets/monitoring.secret` |
| **oauth.secret** | 🟢 OPTIONAL | `GOOGLE_CLIENT_ID`<br>`GOOGLE_CLIENT_SECRET`<br>`GITHUB_CLIENT_ID`<br>`GITHUB_CLIENT_SECRET`<br>`DISCORD_CLIENT_ID`<br>`DISCORD_CLIENT_SECRET` | `your_google_client_id...`<br>`your_google_client_secret`<br>`your_github_client_id`<br>`your_github_client_secret`<br>`your_discord_client_id`<br>`your_discord_client_secret` | `infra/secrets/oauth.secret` |
| **reranker-service.secret** | 🟢 OPTIONAL | `RERANKER_API_KEY` | `change_me_reranker...` | `infra/secrets/reranker-service.secret` |
| **smoldocling-service.secret** | 🟢 OPTIONAL | `SMOLDOCLING_API_KEY` | `change_me_smoldocling...` | `infra/secrets/smoldocling-service.secret` |
| **storage.secret** | 🟢 OPTIONAL | `S3_ACCESS_KEY`<br>`S3_SECRET_KEY`<br>`S3_BUCKET_NAME`<br>`S3_REGION` | `your_s3_access_key`<br>`your_s3_secret_key`<br>`meepleai-uploads`<br>`us-east-1` | `infra/secrets/storage.secret` |
| **traefik.secret** | 🟢 OPTIONAL | `TRAEFIK_DASHBOARD_USER`<br>`TRAEFIK_DASHBOARD_PASSWORD` | `admin`<br>`change_me_traefik...` | `infra/secrets/traefik.secret` |

---

## Setup Instructions

### Option 1: Quick Setup (Copy All Examples)

#### PowerShell (Windows) - RECOMMENDED ✅

**Using Helper Script** (easiest - WITH AUTO-GENERATION 🎉):
```powershell
cd infra/secrets
.\setup-secrets.ps1

# Output example:
# =======================================
#    MeepleAI Secrets Auto-Setup
# =======================================
#
# [STEP 1] Generating secure values...
#   [OK] JWT_SECRET_KEY:           OwBQ90/g***
#   [OK] POSTGRES_PASSWORD:        zNo%vL27***
#   [OK] REDIS_PASSWORD:           [d<9mynL***
#   [OK] EMBEDDING_SERVICE_API_KEY: C5CirwRq***
#   ... (8 more values)
#
# [STEP 2] Creating and populating secret files...
#   [OK] Created: admin.secret (1 auto-generated)
#   [OK] Created: embedding-service.secret (1 auto-generated)
#   ... (14 more files)
#
# [SUCCESS] All CRITICAL secrets configured!
#           Application can start successfully.
```

**With Backup** (recommended for production):
```powershell
cd infra/secrets
.\setup-secrets.ps1 -SaveGenerated

# Saves all generated values to .generated-values-TIMESTAMP.txt
# Example: .generated-values-20260116-191704.txt
#
# Contents:
# ADMIN_PASSWORD=rgy&U,*0!%Ymfbhx
# EMBEDDING_SERVICE_API_KEY=C5CirwRqXcZJF3E8l+BqeLTqNRm2MwEMMpDsYRpUY5g=
# JWT_SECRET_KEY=OwBQ90/gsq09hyKSJ6+2Hgl5GpTchLe3wz8E44D3wiz...
# ... (all 12 generated values)
#
# ⚠️ Copy to password manager, then DELETE the file!
```

**Benefits**:
- ✅ 11 secrets auto-generated with cryptographic strength
- ✅ Setup time: <1 minute (vs 15-30 minutes manually)
- ✅ Zero weak passwords (256-512 bits entropy)
- ✅ Ready to start immediately (all CRITICAL secrets configured)

**Manual Command**:
```powershell
cd infra/secrets
Get-ChildItem -Filter "*.secret.example" | ForEach-Object {
    Copy-Item $_.FullName -Destination ($_.FullName -replace '\.example$','')
}
```

⚠️ **IMPORTANT**: The bash command `for file in *.secret.example; do ...` **DOES NOT WORK** in PowerShell!

#### Bash (Linux/macOS/Git Bash)

```bash
cd infra/secrets
for file in *.secret.example; do cp "$file" "${file%.example}"; done
```

#### CMD (Windows)

```cmd
cd infra\secrets
for %f in (*.secret.example) do copy "%f" "%~nf"
```

### Option 2: Copy Individual Files

**Bash**:
```bash
cd infra/secrets
cp admin.secret.example admin.secret
cp database.secret.example database.secret
cp jwt.secret.example jwt.secret
# ... repeat for other files
```

**PowerShell**:
```powershell
cd infra/secrets
Copy-Item admin.secret.example admin.secret
Copy-Item database.secret.example database.secret
Copy-Item jwt.secret.example jwt.secret
# ... repeat for other files
```

---

## Updating Configurations

### When to Update Secrets

**Common Scenarios**:
- 🔑 **Token Expiration**: API keys expired or rotated
- 🔄 **Service Migration**: Changing service URLs or endpoints
- 🔐 **Security Incident**: Credentials compromised
- 📈 **Scaling**: Moving from free to paid tiers
- 🛠️ **Troubleshooting**: Testing different configurations

### Step-by-Step Update Process

#### 1. Backup Current Configuration

**Before making any changes**, always create a backup:

```powershell
# PowerShell
cd infra/secrets
Copy-Item jwt.secret jwt.secret.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')
```

```bash
# Bash
cd infra/secrets
cp jwt.secret jwt.secret.backup.$(date +%Y%m%d-%H%M%S)
```

#### 2. Edit the Secret File

**Option A: Using Text Editor**:
```powershell
# PowerShell
notepad infra/secrets/openrouter.secret

# Or VS Code
code infra/secrets/openrouter.secret
```

```bash
# Bash
nano infra/secrets/openrouter.secret
# Or vim, VS Code, etc.
```

**Option B: Using sed (Linux/macOS/Git Bash)**:
```bash
# Update specific variable
sed -i 's/^OPENROUTER_API_KEY=.*/OPENROUTER_API_KEY=sk-or-v1-new-key-here/' infra/secrets/openrouter.secret
```

**Option C: Using PowerShell Scripting**:
```powershell
# Update specific variable
(Get-Content infra/secrets/openrouter.secret) -replace '^OPENROUTER_API_KEY=.*','OPENROUTER_API_KEY=sk-or-v1-new-key-here' | Set-Content infra/secrets/openrouter.secret
```

#### 3. Verify Changes

**Check file content**:
```bash
# Show file (mask sensitive values)
cat infra/secrets/openrouter.secret | sed 's/\(API_KEY=\).*/\1***REDACTED***/'
```

```powershell
# PowerShell
Get-Content infra/secrets/openrouter.secret | ForEach-Object { $_ -replace '(API_KEY=).*','$1***REDACTED***' }
```

#### 4. Apply Changes to Services

**Development (Local Docker)**:
```bash
cd ../../  # Back to repo root

# Option 1: Restart affected service only
docker compose restart api

# Option 2: Restart all services
docker compose restart

# Option 3: Full rebuild if env vars changed
docker compose down
docker compose up -d
```

**Production (depends on deployment)**:
```bash
# Example: Kubernetes
kubectl rollout restart deployment/meepleai-api

# Example: Docker Swarm
docker service update --force meepleai_api

# Example: Systemd
sudo systemctl restart meepleai-api
```

#### 5. Validate Changes

**Check logs for errors**:
```bash
# Docker Compose
docker compose logs -f api | grep -i "error\|warning\|secret"

# Check specific service
docker compose logs api --tail=50
```

**Test functionality**:
```bash
# Health check
curl http://localhost:8080/health

# Test specific feature (e.g., OpenRouter)
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "test"}'
```

---

### Common Update Scenarios

#### Scenario 1: Update OpenRouter API Key

**Context**: Token expired or switching to paid tier

**Steps**:
```bash
# 1. Backup
cp infra/secrets/openrouter.secret infra/secrets/openrouter.secret.backup

# 2. Edit file
nano infra/secrets/openrouter.secret

# Change:
# OPENROUTER_API_KEY=sk-or-v1-old-key
# To:
# OPENROUTER_API_KEY=sk-or-v1-new-key-from-openrouter-ai

# 3. Optional: Update model
# OPENROUTER_DEFAULT_MODEL=meta-llama/llama-3.3-70b-instruct:free
# To:
# OPENROUTER_DEFAULT_MODEL=anthropic/claude-3.5-sonnet

# 4. Restart API service
docker compose restart api

# 5. Test
docker compose logs api | tail -20
# Look for: "OpenRouter configuration loaded successfully"
```

**Validation**:
```bash
# Send test chat message
curl -X POST http://localhost:8080/api/v1/chat/send \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello, test OpenRouter connection"}'
```

#### Scenario 2: Change Database Password

**Context**: Security incident or routine rotation

⚠️ **CRITICAL**: Requires coordinated update of PostgreSQL and application

**Steps**:
```bash
# 1. Backup current config
cp infra/secrets/database.secret infra/secrets/database.secret.backup

# 2. Stop application (prevents connection errors during password change)
docker compose stop api

# 3. Update PostgreSQL password
docker compose exec postgres psql -U meepleai -c "ALTER USER meepleai WITH PASSWORD 'new_strong_password_here';"

# 4. Update secret file
nano infra/secrets/database.secret
# Change:
# POSTGRES_PASSWORD=old_password
# To:
# POSTGRES_PASSWORD=new_strong_password_here

# 5. Restart all services
docker compose up -d

# 6. Verify connection
docker compose logs api | grep -i "database\|postgres"
# Look for: "Database connection established successfully"
```

**Rollback if needed**:
```bash
# If new password fails
cp infra/secrets/database.secret.backup infra/secrets/database.secret
docker compose exec postgres psql -U meepleai -c "ALTER USER meepleai WITH PASSWORD 'old_password';"
docker compose restart
```

#### Scenario 3: Migrate to New Embedding Service URL

**Context**: Moving from local to cloud-hosted embedding service

**Steps**:
```bash
# 1. Backup
cp infra/secrets/embedding-service.secret infra/secrets/embedding-service.secret.backup

# 2. Update secret file with new endpoint
nano infra/secrets/embedding-service.secret

# Add new variable (check service docs for required format):
# EMBEDDING_SERVICE_URL=https://embedding.yourdomain.com
# EMBEDDING_SERVICE_API_KEY=new-api-key-from-provider

# 3. Update docker-compose.yml or app config to use new URL
# (This might require code changes if URL was hardcoded)

# 4. Restart with rebuild
docker compose down
docker compose up -d --build

# 5. Test embedding generation
curl -X POST http://localhost:8080/api/v1/documents/process \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -F "file=@test.pdf"
```

#### Scenario 4: Rotate JWT Secret Key

**Context**: Security best practice (every 90 days) or compromise

⚠️ **CRITICAL**: This will invalidate ALL existing user sessions

**Steps**:
```bash
# 1. Notify users (if production)
# Send notification: "Maintenance in 5 minutes, please save work"

# 2. Backup
cp infra/secrets/jwt.secret infra/secrets/jwt.secret.backup

# 3. Generate new secret
openssl rand -base64 64

# 4. Update secret file
nano infra/secrets/jwt.secret
# JWT_SECRET_KEY=<paste new key from step 3>

# 5. Restart API (this invalidates all tokens)
docker compose restart api

# 6. Verify new token generation
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@meepleai.local","password":"your_password"}'

# Response should contain new JWT token
```

**Post-Rotation**:
- All users need to log in again
- Mobile apps need to re-authenticate
- Update any service-to-service tokens

#### Scenario 5: Enable Previously Optional Service

**Context**: Enabling email notifications after initial setup

**Steps**:
```bash
# 1. Create email secret from example
cp infra/secrets/email.secret.example infra/secrets/email.secret

# 2. Configure SMTP settings
nano infra/secrets/email.secret

# Fill in:
# SMTP_HOST=smtp.gmail.com
# SMTP_PORT=587
# SMTP_USER=your-app@gmail.com
# SMTP_PASSWORD=your-app-password  # From Google App Passwords
# SMTP_FROM_EMAIL=noreply@meepleai.local

# 3. Restart API
docker compose restart api

# 4. Test email sending
curl -X POST http://localhost:8080/api/v1/notifications/test-email \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"recipient":"your-email@example.com"}'

# 5. Check logs
docker compose logs api | grep -i "email\|smtp"
# Look for: "Email service initialized successfully"
```

---

### Troubleshooting Configuration Changes

#### Issue: Service Won't Start After Update

**Symptoms**:
```
ERROR: CRITICAL secret validation failed: jwt.secret
JWT_SECRET_KEY must be at least 32 characters
```

**Solution**:
```bash
# 1. Check secret file format
cat infra/secrets/jwt.secret

# 2. Verify no extra whitespace or newlines
# File should be:
# JWT_SECRET_KEY=your_base64_key_here
# JWT_ISSUER=meepleai-api
# JWT_AUDIENCE=meepleai-web

# 3. Regenerate if needed
openssl rand -base64 64 | tr -d '\n' > temp_key.txt
# Copy key and update jwt.secret

# 4. Restart
docker compose restart api
```

#### Issue: Changes Not Applied

**Symptoms**: Old configuration still active after restart

**Solution**:
```bash
# 1. Verify file was saved
cat infra/secrets/openrouter.secret | grep API_KEY

# 2. Check if Docker cached old env vars
docker compose down -v  # Remove volumes
docker compose up -d

# 3. Verify environment in container
docker compose exec api printenv | grep OPENROUTER

# 4. Check if app needs rebuild
docker compose up -d --build
```

#### Issue: Database Connection Failed After Password Change

**Symptoms**:
```
ERROR: Failed to connect to PostgreSQL
FATAL: password authentication failed for user "meepleai"
```

**Solution**:
```bash
# 1. Verify password in secret file
cat infra/secrets/database.secret | grep PASSWORD

# 2. Verify PostgreSQL password was actually changed
docker compose exec postgres psql -U postgres -c "\du"

# 3. If passwords don't match, sync them:

# Option A: Update PostgreSQL to match secret file
docker compose exec postgres psql -U postgres -c "ALTER USER meepleai WITH PASSWORD 'password_from_secret_file';"

# Option B: Update secret file to match PostgreSQL
# (Get current password from backup or reset it)

# 4. Restart
docker compose restart
```

#### Issue: API Key Validation Failed

**Symptoms**:
```
ERROR: Invalid API key format for OpenRouter
Expected: sk-or-v1-*
```

**Solution**:
```bash
# 1. Check API key format
cat infra/secrets/openrouter.secret | grep API_KEY
# Should start with: sk-or-v1-

# 2. Verify key from provider dashboard
# OpenRouter: https://openrouter.ai/keys
# Check key is active and not expired

# 3. Copy exact key (no extra spaces)
# Use:
echo "OPENROUTER_API_KEY=sk-or-v1-exact-key-here" > temp_line.txt
# Copy line from temp_line.txt to openrouter.secret

# 4. Restart
docker compose restart api
```

---

### Best Practices for Configuration Updates

#### ✅ DO

1. **Always backup before changes**:
   ```bash
   cp secret.file secret.file.backup.$(date +%Y%m%d-%H%M%S)
   ```

2. **Test in development first**:
   - Never test new credentials directly in production
   - Use staging environment for validation

3. **Use version control for templates**:
   - Keep `.example` files up to date
   - Document changes in git commits
   - Example: `git commit -m "docs(secrets): add SMTP_TLS_ENABLED variable to email.secret.example"`

4. **Validate before applying**:
   ```bash
   # Check syntax
   grep -E '^[A-Z_]+=.*$' infra/secrets/jwt.secret

   # Check for common mistakes
   grep -v '^#' infra/secrets/jwt.secret | grep -v '^$' | grep -v '='
   ```

5. **Monitor after changes**:
   ```bash
   # Watch logs for 1 minute after restart
   docker compose logs -f api &
   PID=$!
   sleep 60
   kill $PID
   ```

6. **Document changes**:
   ```bash
   # Keep a changelog
   echo "$(date): Updated OpenRouter API key (token rotation)" >> infra/secrets/CHANGELOG.md
   ```

#### ❌ DON'T

1. **Don't skip backups** - Even for "small" changes
2. **Don't edit secrets in production without testing**
3. **Don't commit `.secret` files to git** (only `.secret.example`)
4. **Don't share secrets via insecure channels** (Slack, email, etc.)
5. **Don't leave backup files in secrets directory**:
   ```bash
   # Clean old backups (keep last 3)
   ls -t *.backup.* | tail -n +4 | xargs rm -f
   ```

---

### Configuration Change Checklist

Use this checklist for critical configuration updates:

- [ ] **Pre-Change**
  - [ ] Backup current secret file with timestamp
  - [ ] Document reason for change (ticket, incident, rotation)
  - [ ] Test new credentials/values in staging
  - [ ] Notify team (if production)
  - [ ] Schedule maintenance window (if needed)

- [ ] **During Change**
  - [ ] Edit secret file with new values
  - [ ] Verify file syntax (no extra spaces, newlines)
  - [ ] Apply changes to external services first (if applicable, e.g., database password)
  - [ ] Restart affected services
  - [ ] Monitor logs for errors

- [ ] **Post-Change**
  - [ ] Test functionality (health checks, feature tests)
  - [ ] Verify logs show successful initialization
  - [ ] Check dependent services still work
  - [ ] Document change in changelog
  - [ ] Archive backup file securely
  - [ ] Update team documentation (if procedures changed)

---

### Emergency Rollback Procedure

If configuration change causes production issues:

```bash
# 1. IMMEDIATE: Restore backup
cp infra/secrets/jwt.secret.backup.20260116-143000 infra/secrets/jwt.secret

# 2. Restart services
docker compose restart

# 3. Verify rollback successful
docker compose logs api | tail -50

# 4. Investigate issue
# - Check what went wrong
# - Fix underlying problem
# - Test in staging before re-attempting

# 5. Document incident
echo "$(date): Rolled back JWT secret change due to validation errors" >> infra/secrets/INCIDENTS.md
```

---

## Auto-Generated Values

The `setup-secrets.ps1` script **automatically generates** secure values for the following:

### 🔐 Cryptographically Secure (RNG-based)

| Variable | File | Type | Strength |
|----------|------|------|----------|
| `JWT_SECRET_KEY` | jwt.secret | Base64 (64 bytes) | 512 bits |
| `QDRANT_API_KEY` | qdrant.secret | Base64 (32 bytes) | 256 bits |
| `EMBEDDING_SERVICE_API_KEY` | embedding-service.secret | Base64 (32 bytes) | 256 bits |
| `RERANKER_API_KEY` | reranker-service.secret | Base64 (32 bytes) | 256 bits |
| `SMOLDOCLING_API_KEY` | smoldocling-service.secret | Base64 (32 bytes) | 256 bits |
| `UNSTRUCTURED_API_KEY` | unstructured-service.secret | Base64 (32 bytes) | 256 bits |

### 🔑 Secure Passwords (Alphanumeric + Symbols)

| Variable | File | Length | Requirements |
|----------|------|--------|--------------|
| `POSTGRES_PASSWORD` | database.secret | 20 chars | Upper + digit + symbol |
| `REDIS_PASSWORD` | redis.secret | 20 chars | Upper + digit + symbol |
| `ADMIN_PASSWORD` | admin.secret | 16 chars | Upper + digit + symbol |
| `GRAFANA_ADMIN_PASSWORD` | monitoring.secret | 16 chars | Upper + digit + symbol |
| `PROMETHEUS_PASSWORD` | monitoring.secret | 16 chars | Upper + digit + symbol |
| `TRAEFIK_DASHBOARD_PASSWORD` | traefik.secret | 16 chars | Upper + digit + symbol |

### 📋 Manual Configuration Still Required

These values **cannot be auto-generated** (require external accounts):

- **bgg.secret**: BoardGameGeek username/password (from https://boardgamegeek.com)
- **openrouter.secret**: OpenRouter API key (from https://openrouter.ai/keys)
- **email.secret**: SMTP credentials (from your email provider)
- **oauth.secret**: Google/GitHub OAuth client IDs (from provider consoles)
- **storage.secret**: S3 credentials (from AWS/cloud provider)

**After running the script**, edit these files manually with your actual credentials.

---

## Validation Levels

### 🔴 CRITICAL - Startup Blocked
**Files**: `admin.secret`, `database.secret`, `embedding-service.secret`, `jwt.secret`, `qdrant.secret`, `redis.secret`

**Behavior**:
- Application **WILL NOT START** if any critical secret is missing
- Error logged with clear message: `"CRITICAL secret missing: {filename}"`
- Exit code: `1` (failure)

**Example Error**:
```
ERROR: CRITICAL secret missing: infra/secrets/database.secret
Application startup blocked. Please copy database.secret.example to database.secret and configure values.
```

### 🟡 IMPORTANT - Warning Logged
**Files**: `bgg.secret`, `openrouter.secret`, `unstructured-service.secret`

**Behavior**:
- Application **STARTS** with reduced functionality
- Warning logged: `"IMPORTANT secret missing: {filename}. Feature {feature_name} disabled."`
- Exit code: `0` (success, but degraded)

**Example Warning**:
```
WARN: IMPORTANT secret missing: infra/secrets/bgg.secret
BoardGameGeek integration disabled. Game catalog limited to manual entries.
```

### 🟢 OPTIONAL - Info Logged
**Files**: `email.secret`, `monitoring.secret`, `oauth.secret`, `reranker-service.secret`, `smoldocling-service.secret`, `storage.secret`, `traefik.secret`

**Behavior**:
- Application **STARTS** normally with fallback defaults
- Info logged: `"OPTIONAL secret missing: {filename}. Using default configuration."`
- Exit code: `0` (success)

**Example Info**:
```
INFO: OPTIONAL secret missing: infra/secrets/email.secret
Email notifications disabled. Using in-app notifications only.
```

---

## Detailed Configuration

### 🔴 CRITICAL Secrets

#### admin.secret
**Purpose**: Initial administrator account setup

**Variables**:
```bash
ADMIN_EMAIL=admin@meepleai.local          # Must be valid email format
ADMIN_PASSWORD=SecureP@ssw0rd123          # Min 8 chars, uppercase + digit
ADMIN_DISPLAY_NAME=System Administrator   # Human-readable name
```

**Validation**:
- Email: RFC 5322 format
- Password: ≥8 chars, ≥1 uppercase, ≥1 digit
- Display name: 1-100 chars

#### database.secret
**Purpose**: PostgreSQL connection credentials

**Variables**:
```bash
POSTGRES_USER=meepleai                    # Database username
POSTGRES_PASSWORD=your_strong_password    # Min 12 chars recommended
POSTGRES_DB=meepleai_db                   # Database name
```

**Validation**:
- User: 1-63 chars, alphanumeric + underscore
- Password: ≥8 chars (≥12 recommended)
- Database: 1-63 chars, alphanumeric + underscore

#### embedding-service.secret
**Purpose**: AI embedding service authentication

**Variables**:
```bash
EMBEDDING_SERVICE_API_KEY=your_api_key_here  # API key from embedding provider
```

**Validation**:
- API key: Non-empty string, format depends on provider

#### jwt.secret
**Purpose**: JWT token signing and validation

**Variables**:
```bash
JWT_SECRET_KEY=your_base64_secret_here    # Generate: openssl rand -base64 64
JWT_ISSUER=meepleai-api                   # Token issuer identifier
JWT_AUDIENCE=meepleai-web                 # Token audience identifier
```

**Validation**:
- Secret key: ≥32 chars (64+ recommended), base64 encoded
- Issuer: Non-empty string
- Audience: Non-empty string

**Generate Strong Key**:
```bash
# Linux/macOS/Git Bash
openssl rand -base64 64

# PowerShell (Windows)
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

#### qdrant.secret
**Purpose**: Vector database authentication

**Variables**:
```bash
QDRANT_API_KEY=your_qdrant_api_key_here   # Qdrant API key
```

**Validation**:
- API key: Non-empty string

#### redis.secret
**Purpose**: Redis cache authentication

**Variables**:
```bash
REDIS_PASSWORD=your_strong_redis_password  # Min 12 chars recommended
```

**Validation**:
- Password: ≥8 chars (≥12 recommended)

---

### 🟡 IMPORTANT Secrets

#### bgg.secret
**Purpose**: BoardGameGeek API integration

**Variables**:
```bash
BGG_USERNAME=your_bgg_username            # BGG account username
BGG_PASSWORD=your_bgg_password            # BGG account password
```

**Validation**:
- Username: Non-empty string
- Password: Non-empty string

**Get Credentials**: Create account at https://boardgamegeek.com/register

#### openrouter.secret
**Purpose**: LLM API routing and fallback

**Variables**:
```bash
OPENROUTER_API_KEY=sk-or-v1-your_key_here           # OpenRouter API key
OPENROUTER_DEFAULT_MODEL=meta-llama/llama-3.3-70b-instruct:free  # Default model
```

**Validation**:
- API key: Must start with `sk-or-v1-`
- Model: Non-empty string (model identifier)

**Get API Key**: https://openrouter.ai/keys

#### unstructured-service.secret
**Purpose**: PDF processing and document extraction

**Variables**:
```bash
UNSTRUCTURED_API_KEY=your_unstructured_key_here  # Unstructured API key
```

**Validation**:
- API key: Non-empty string

---

### 🟢 OPTIONAL Secrets

#### email.secret
**Purpose**: SMTP email notifications

**Variables**:
```bash
SMTP_HOST=smtp.gmail.com                  # SMTP server hostname
SMTP_PORT=587                             # SMTP port (587 for TLS, 465 for SSL)
SMTP_USER=your_email@gmail.com            # SMTP username (usually email)
SMTP_PASSWORD=your_app_password_here      # SMTP password (app password for Gmail)
SMTP_FROM_EMAIL=noreply@meepleai.local    # From address for sent emails
```

**Validation**:
- Host: Valid hostname or IP
- Port: 1-65535 (common: 25, 465, 587)
- User: Valid email format
- Password: Non-empty string
- From email: Valid email format

**Gmail Setup**: Use App Passwords (https://support.google.com/accounts/answer/185833)

#### monitoring.secret
**Purpose**: Monitoring tools authentication

**Variables**:
```bash
GRAFANA_ADMIN_PASSWORD=your_grafana_password     # Grafana admin password
PROMETHEUS_PASSWORD=your_prometheus_password     # Prometheus password
```

**Validation**:
- Passwords: ≥8 chars each

#### oauth.secret
**Purpose**: OAuth social login (Google, GitHub, Discord)

**Variables**:
```bash
# Google OAuth
GOOGLE_CLIENT_ID=your_google_client_id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-your_google_secret

# GitHub OAuth
GITHUB_CLIENT_ID=your_github_client_id
GITHUB_CLIENT_SECRET=your_github_client_secret

# Discord OAuth
DISCORD_CLIENT_ID=your_discord_client_id
DISCORD_CLIENT_SECRET=your_discord_client_secret
```

**Validation**:
- Client IDs: Non-empty strings
- Client secrets: Non-empty strings

**Get Credentials**:
- Google: https://console.cloud.google.com/apis/credentials
- GitHub: https://github.com/settings/developers
- Discord: https://discord.com/developers/applications

#### reranker-service.secret
**Purpose**: AI reranking service (optional optimization)

**Variables**:
```bash
RERANKER_API_KEY=your_reranker_key_here   # Reranker API key
```

**Validation**:
- API key: Non-empty string

#### smoldocling-service.secret
**Purpose**: Document intelligence extraction

**Variables**:
```bash
SMOLDOCLING_API_KEY=your_smoldocling_key_here  # SmolDocling API key
```

**Validation**:
- API key: Non-empty string

#### storage.secret
**Purpose**: S3-compatible object storage (AWS S3, MinIO, etc.)

**Variables**:
```bash
S3_ACCESS_KEY=your_s3_access_key          # S3 access key ID
S3_SECRET_KEY=your_s3_secret_key          # S3 secret access key
S3_BUCKET_NAME=meepleai-uploads           # S3 bucket name
S3_REGION=us-east-1                       # AWS region
```

**Validation**:
- Access key: Non-empty string
- Secret key: Non-empty string
- Bucket name: Valid S3 bucket name format
- Region: Valid AWS region code

**Fallback**: If missing, uses local file storage in `uploads/` directory

#### traefik.secret
**Purpose**: Traefik reverse proxy dashboard access

**Variables**:
```bash
TRAEFIK_DASHBOARD_USER=admin              # Dashboard username
TRAEFIK_DASHBOARD_PASSWORD=your_password  # Dashboard password
```

**Validation**:
- Username: Non-empty string
- Password: ≥8 chars

---

## Security Best Practices

### ✅ DO

1. **Use strong passwords**:
   - Min 12 characters
   - Mix uppercase, lowercase, digits, symbols
   - Use password manager (1Password, Bitwarden, etc.)

2. **Generate cryptographic keys properly**:
   ```bash
   # JWT secret (64 bytes base64)
   openssl rand -base64 64

   # General purpose secret (32 bytes hex)
   openssl rand -hex 32
   ```

3. **Rotate secrets regularly**:
   - JWT secrets: Every 90 days
   - Database passwords: Every 180 days
   - API keys: When compromised or every 365 days

4. **Use separate secrets per environment**:
   - Development: `*.secret` (local only)
   - Staging: Vault/environment variables
   - Production: Vault/environment variables

5. **Store secrets securely**:
   - Development: Local files (git-ignored)
   - CI/CD: GitHub Secrets, Azure Key Vault, AWS Secrets Manager
   - Production: HashiCorp Vault, AWS Secrets Manager, Azure Key Vault

### ❌ DON'T

1. **Never commit secrets to git**:
   - ✅ `.gitignore` includes `*.secret` (not `*.secret.example`)
   - ❌ Don't remove `*.secret` from `.gitignore`
   - ❌ Don't commit `.env` files with real secrets

2. **Never use default/placeholder values in production**:
   - ❌ `change_me_*` passwords
   - ❌ `your_*_here` API keys
   - ❌ `admin@meepleai.local` emails

3. **Never share secrets via insecure channels**:
   - ❌ Email
   - ❌ Slack/Teams messages
   - ❌ SMS
   - ✅ Use encrypted vaults (1Password, Bitwarden, etc.)

4. **Never log secrets**:
   - ❌ `console.log(apiKey)`
   - ❌ `logger.Info($"Password: {password}")`
   - ✅ Use `[Redacted]` or `****` in logs

---

## Troubleshooting

### Application Won't Start

**Error**: `"CRITICAL secret missing: infra/secrets/database.secret"`

**Solution**:
```bash
# 1. Check if file exists
ls infra/secrets/*.secret

# 2. If missing, copy from example
cd infra/secrets
cp database.secret.example database.secret

# 3. Edit file with real credentials
# Use your preferred editor (nano, vim, VS Code, etc.)
nano database.secret

# 4. Verify file is not empty and contains valid values
cat database.secret
```

### Secret Validation Fails

**Error**: `"ADMIN_PASSWORD validation failed: Must be at least 8 characters"`

**Solution**:
```bash
# 1. Check password requirements in admin.secret.example comments
cat infra/secrets/admin.secret.example

# 2. Update password in admin.secret
# Ensure: ≥8 chars, ≥1 uppercase, ≥1 digit
echo "ADMIN_PASSWORD=SecureP@ssw0rd123" >> infra/secrets/admin.secret

# 3. Restart application
```

### Feature Disabled (IMPORTANT Secret Missing)

**Warning**: `"IMPORTANT secret missing: infra/secrets/bgg.secret. BGG integration disabled."`

**Solution**:
```bash
# 1. Decide if you need the feature
# - If yes: Get credentials and configure secret
# - If no: Ignore warning (application works without it)

# 2. To enable BGG integration:
cd infra/secrets
cp bgg.secret.example bgg.secret
nano bgg.secret  # Add your BGG username/password

# 3. Restart application
```

### Secret File Permissions (Linux/macOS)

**Error**: `"Permission denied reading infra/secrets/database.secret"`

**Solution**:
```bash
# 1. Fix file permissions (read-only for owner)
chmod 600 infra/secrets/*.secret

# 2. Verify permissions
ls -la infra/secrets/*.secret
# Should show: -rw------- (600)

# 3. If running in Docker, ensure correct user ownership
chown 1000:1000 infra/secrets/*.secret
```

---

## Development vs Production

### Development (Local)

**Storage**: Git-ignored files in `infra/secrets/`

**Setup**:
```bash
cd infra/secrets
for file in *.secret.example; do cp "$file" "${file%.example}"; done
# Edit each .secret file with development credentials
```

**Security**: Lower standards acceptable (weak passwords, shared credentials)

### Production (Cloud)

**Storage**:
- **AWS**: AWS Secrets Manager, Systems Manager Parameter Store
- **Azure**: Azure Key Vault
- **GCP**: Google Secret Manager
- **Self-hosted**: HashiCorp Vault, Docker Secrets

**Setup**:
```bash
# Example: AWS Secrets Manager
aws secretsmanager create-secret \
  --name meepleai/production/database \
  --secret-string '{"POSTGRES_USER":"meepleai","POSTGRES_PASSWORD":"strong-random-password"}'

# Application loads from environment variables
export POSTGRES_USER=$(aws secretsmanager get-secret-value --secret-id meepleai/production/database --query SecretString --output text | jq -r .POSTGRES_USER)
```

**Security**: High standards required (strong passwords, rotation, auditing)

---

## CI/CD Integration

### GitHub Actions (Recommended)

**Setup Secrets**:
1. Go to: Repository → Settings → Secrets and variables → Actions
2. Add each secret as separate variable:
   - `POSTGRES_PASSWORD`
   - `JWT_SECRET_KEY`
   - `OPENROUTER_API_KEY`
   - etc.

**Workflow Example**:
```yaml
# .github/workflows/deploy.yml
name: Deploy
on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Deploy with secrets
        env:
          POSTGRES_PASSWORD: ${{ secrets.POSTGRES_PASSWORD }}
          JWT_SECRET_KEY: ${{ secrets.JWT_SECRET_KEY }}
        run: |
          # Secrets available as environment variables
          ./scripts/deploy.sh
```

### Azure DevOps

**Setup**:
1. Pipelines → Library → Variable groups
2. Create group: `meepleai-secrets`
3. Add variables, mark as "secret"

**Pipeline Example**:
```yaml
# azure-pipelines.yml
variables:
  - group: meepleai-secrets

steps:
  - script: |
      echo "Deploying with secrets..."
      # Access via $(POSTGRES_PASSWORD)
    displayName: 'Deploy'
```

---

## Quick Commands Reference

### Copy All Examples (First-time Setup)

#### PowerShell (Windows) ✅

**Using Script** (easiest):
```powershell
cd infra/secrets
.\setup-secrets.ps1
```

**Manual**:
```powershell
cd infra/secrets
Get-ChildItem -Filter "*.secret.example" | ForEach-Object {
    Copy-Item $_.FullName -Destination ($_.FullName -replace '\.example$','')
}
```

#### Bash (Linux/macOS/Git Bash) ✅

```bash
cd infra/secrets
for file in *.secret.example; do cp "$file" "${file%.example}"; done
```

❌ **WARNING**: Don't use bash commands in PowerShell - they won't work!

### Verify All Secrets Exist

**PowerShell**:
```powershell
cd infra/secrets
$required = @('admin','database','jwt','qdrant','redis','embedding-service')
$required | ForEach-Object {
    if (!(Test-Path "$_.secret")) {
        Write-Host "❌ Missing: $_.secret" -ForegroundColor Red
    } else {
        Write-Host "✅ Found: $_.secret" -ForegroundColor Green
    }
}
```

**Bash**:
```bash
cd infra/secrets
for secret in admin database jwt qdrant redis embedding-service; do
    if [ ! -f "${secret}.secret" ]; then
        echo "❌ Missing: ${secret}.secret"
    else
        echo "✅ Found: ${secret}.secret"
    fi
done
```

### Generate Strong Secrets

**PowerShell**:
```powershell
# JWT Secret (64 bytes base64)
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))

# General Secret (32 bytes hex)
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object {[char]$_})
```

**Bash**:
```bash
# JWT Secret (64 bytes base64)
openssl rand -base64 64

# General Secret (32 bytes hex)
openssl rand -hex 32
```

---

## Related Documentation

- **Environment Variables**: [docs/04-deployment/environment-variables.md](../../docs/04-deployment/environment-variables.md)
- **Security Guide**: [docs/04-deployment/security.md](../../docs/04-deployment/security.md)
- **Deployment Guide**: [docs/04-deployment/README.md](../../docs/04-deployment/README.md)
- **Development Setup**: [docs/02-development/setup.md](../../docs/02-development/setup.md)

---

**Last Updated**: 2026-01-16
**Maintainer**: MeepleAI Infrastructure Team
