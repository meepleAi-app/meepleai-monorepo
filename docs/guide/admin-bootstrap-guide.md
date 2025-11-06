# Admin User Bootstrap Guide

This guide explains how to configure the automatic creation of the initial admin user when the application starts for the first time.

## Overview

The MeepleAI API automatically creates an initial admin user on startup if:
1. No admin users exist in the database
2. The environment variables `INITIAL_ADMIN_EMAIL` and `INITIAL_ADMIN_PASSWORD` are set

This feature is useful for:
- First-time deployment in development, staging, or production
- Automated deployments via Docker, Kubernetes, or CI/CD pipelines
- Ensuring there's always a way to access the system initially

## Configuration

### Environment Variables

| Variable | Required | Description | Example |
|----------|----------|-------------|---------|
| `INITIAL_ADMIN_EMAIL` | Yes | Email address for the admin user | `admin@yourdomain.com` |
| `INITIAL_ADMIN_PASSWORD` | Yes | Password for the admin user | `SecureP@ssw0rd123` |
| `INITIAL_ADMIN_DISPLAY_NAME` | No | Display name for the admin (default: "System Admin") | `John Doe` |

### Password Requirements

The password must meet these criteria:
- **Minimum length**: 8 characters
- **At least one uppercase letter** (A-Z)
- **At least one digit** (0-9)

Examples of valid passwords:
- ✅ `Admin123!`
- ✅ `MySecureP@ss1`
- ✅ `Bootstrap2024`

Examples of invalid passwords:
- ❌ `admin123` (no uppercase)
- ❌ `ADMIN` (too short, no digit)
- ❌ `password` (too short, no uppercase, no digit)

## Setup Instructions

### 1. Docker Compose (Development)

**Step 1**: Copy the example environment file:
```bash
cp infra/env/api.env.dev.example infra/env/api.env.dev
```

**Step 2**: Edit `infra/env/api.env.dev` and set your admin credentials:
```bash
# Bootstrap: Initial admin user configuration
INITIAL_ADMIN_EMAIL=admin@meepleai.dev
INITIAL_ADMIN_PASSWORD=YourSecureP@ssw0rd123
INITIAL_ADMIN_DISPLAY_NAME=System Administrator
```

**Step 3**: Start the services:
```bash
cd infra
docker compose up -d
```

**Step 4**: Check the logs to verify admin creation:
```bash
docker compose logs api | grep "Initial admin user"
```

You should see:
```
✅ Initial admin user created successfully: admin@meepleai.dev (ID: ...)
```

### 2. Kubernetes (Production)

**Step 1**: Create a Kubernetes secret with admin credentials:
```bash
kubectl create secret generic admin-bootstrap \
  --from-literal=INITIAL_ADMIN_EMAIL=admin@prod.com \
  --from-literal=INITIAL_ADMIN_PASSWORD='YourS3cur3P@ssw0rd!' \
  --from-literal=INITIAL_ADMIN_DISPLAY_NAME='Production Admin'
```

**Step 2**: Reference the secret in your deployment:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: meepleai-api
spec:
  template:
    spec:
      containers:
      - name: api
        image: meepleai/api:latest
        env:
        - name: INITIAL_ADMIN_EMAIL
          valueFrom:
            secretKeyRef:
              name: admin-bootstrap
              key: INITIAL_ADMIN_EMAIL
        - name: INITIAL_ADMIN_PASSWORD
          valueFrom:
            secretKeyRef:
              name: admin-bootstrap
              key: INITIAL_ADMIN_PASSWORD
        - name: INITIAL_ADMIN_DISPLAY_NAME
          valueFrom:
            secretKeyRef:
              name: admin-bootstrap
              key: INITIAL_ADMIN_DISPLAY_NAME
```

### 3. Azure App Service

**Step 1**: Navigate to Configuration → Application settings

**Step 2**: Add the following settings:
- `INITIAL_ADMIN_EMAIL`: `admin@yourdomain.com`
- `INITIAL_ADMIN_PASSWORD`: `YourSecureP@ssw0rd123`
- `INITIAL_ADMIN_DISPLAY_NAME`: `System Administrator`

**Step 3**: Save and restart the app service

### 4. Local Development (.NET)

**Step 1**: Use user secrets for local development:
```bash
cd apps/api/src/Api
dotnet user-secrets set "INITIAL_ADMIN_EMAIL" "admin@localhost"
dotnet user-secrets set "INITIAL_ADMIN_PASSWORD" "DevP@ssw0rd123"
dotnet user-secrets set "INITIAL_ADMIN_DISPLAY_NAME" "Local Admin"
```

**Step 2**: Run the application:
```bash
dotnet run
```

### 5. GitHub Actions CI/CD

**Step 1**: Add secrets to your GitHub repository:
- Go to Settings → Secrets and variables → Actions
- Add these secrets:
  - `INITIAL_ADMIN_EMAIL`
  - `INITIAL_ADMIN_PASSWORD`
  - `INITIAL_ADMIN_DISPLAY_NAME`

**Step 2**: Use secrets in your workflow:
```yaml
- name: Deploy API
  env:
    INITIAL_ADMIN_EMAIL: ${{ secrets.INITIAL_ADMIN_EMAIL }}
    INITIAL_ADMIN_PASSWORD: ${{ secrets.INITIAL_ADMIN_PASSWORD }}
    INITIAL_ADMIN_DISPLAY_NAME: ${{ secrets.INITIAL_ADMIN_DISPLAY_NAME }}
  run: |
    # Your deployment commands here
```

## Behavior

### First Startup (No Admin Exists)

When the application starts and no admin user exists:

1. **Environment variables are set**: Admin user is created automatically
   ```
   ✅ Initial admin user created successfully: admin@example.com (ID: abc123...)
   ```

2. **Environment variables are NOT set**: Warning message is logged
   ```
   ⚠️  No admin user found and INITIAL_ADMIN_EMAIL/INITIAL_ADMIN_PASSWORD not set.
   Please create an admin user manually or set environment variables.
   ```

3. **Invalid credentials**: Error message is logged and no user is created
   ```
   Invalid INITIAL_ADMIN_EMAIL format: invalid-email
   INITIAL_ADMIN_PASSWORD must be at least 8 characters with uppercase and digit
   ```

### Subsequent Startups (Admin Already Exists)

If an admin user already exists:
```
Admin user already exists, skipping bootstrap
```

The system will **not** create a duplicate admin user, even if environment variables are set.

## Security Best Practices

### ✅ DO

1. **Use strong, unique passwords** for each environment
   ```bash
   # Generate a secure password
   openssl rand -base64 24
   ```

2. **Store credentials in secure secret managers**:
   - Kubernetes Secrets
   - Azure Key Vault
   - AWS Secrets Manager
   - HashiCorp Vault
   - GitHub Secrets (for CI/CD)

3. **Rotate credentials regularly** (every 90 days recommended)

4. **Use different credentials for each environment** (dev, staging, production)

5. **Delete or unset environment variables after first startup** (optional, for extra security)

6. **Enable 2FA (Two-Factor Authentication)** for the admin user after first login

### ❌ DON'T

1. **Never commit credentials to source control**
   - Ensure `.env` files are in `.gitignore`
   - Never use hardcoded values in `docker-compose.yml`

2. **Never use weak passwords** like `admin123`, `password`, etc.

3. **Never share production credentials** via email, chat, or documentation

4. **Never log passwords** in application logs

## Audit Trail

Every admin user creation is logged in the `audit_logs` table:

```sql
SELECT * FROM audit_logs
WHERE action = 'BOOTSTRAP_ADMIN_CREATED';
```

Example audit log:
```json
{
  "id": "abc123...",
  "userId": null,
  "action": "BOOTSTRAP_ADMIN_CREATED",
  "resource": "User",
  "resourceId": "def456...",
  "result": "Success",
  "details": "Initial admin user created: admin@example.com",
  "ipAddress": "system",
  "userAgent": "bootstrap",
  "createdAt": "2025-01-15T10:30:00Z"
}
```

## Troubleshooting

### Problem: No admin user is created

**Solution 1**: Check environment variables are set correctly
```bash
# Docker Compose
docker compose exec api printenv | grep INITIAL_ADMIN

# Kubernetes
kubectl exec -it <pod-name> -- env | grep INITIAL_ADMIN
```

**Solution 2**: Check application logs for error messages
```bash
docker compose logs api | grep -i "admin"
```

### Problem: "Admin user already exists" message

This is **expected behavior** if an admin user was created in a previous startup. The bootstrap process is **idempotent** and will not create duplicate admins.

To create a new admin user, either:
1. Delete the existing admin user via SQL
2. Use the admin panel to create additional users

### Problem: "Invalid INITIAL_ADMIN_EMAIL format"

Ensure the email contains an `@` symbol:
```bash
# ❌ Invalid
INITIAL_ADMIN_EMAIL=admin

# ✅ Valid
INITIAL_ADMIN_EMAIL=admin@example.com
```

### Problem: "INITIAL_ADMIN_PASSWORD must be at least 8 characters"

Ensure the password meets all requirements:
```bash
# ❌ Invalid (too short, no uppercase, no digit)
INITIAL_ADMIN_PASSWORD=admin

# ✅ Valid
INITIAL_ADMIN_PASSWORD=Admin123!
```

## Manual Admin Creation (Alternative)

If you prefer not to use environment variables, you can create the admin user manually:

### Option 1: SQL Script
```sql
-- Generate password hash using a tool or script first
INSERT INTO users (id, email, display_name, password_hash, role, created_at)
VALUES (
  gen_random_uuid()::text,
  'admin@example.com',
  'Manual Admin',
  'v1.210000.{salt}.{hash}', -- Replace with actual hash
  'Admin',
  NOW()
);
```

### Option 2: Use the API (requires existing admin/editor)
```bash
curl -X POST http://localhost:8080/api/v1/admin/users \
  -H "Content-Type: application/json" \
  -H "Cookie: session_token=..." \
  -d '{
    "email": "newadmin@example.com",
    "password": "NewAdmin123!",
    "displayName": "New Admin",
    "role": "Admin"
  }'
```

## References

- [SECURITY.md](../SECURITY.md) - Security policies and best practices
- [Secrets Management Guide](./secrets-management.md) - Detailed secrets management
- [Docker Compose Configuration](../../infra/docker-compose.yml)
- [Environment Variables Reference](../../infra/env/api.env.dev.example)

## Support

If you encounter issues with admin bootstrap:

1. Check this guide first
2. Review application logs: `docker compose logs api`
3. Check database: `SELECT * FROM users WHERE role = 'Admin';`
4. Open an issue on GitHub with logs (redact sensitive information)
