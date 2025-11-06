# Admin Bootstrap - Quick Start

Get your MeepleAI instance up and running with an admin user in 5 minutes.

## Quick Setup (Docker Compose)

### 1. Create Environment File

```bash
cd infra
cp env/api.env.dev.example env/api.env.dev
```

### 2. Edit Admin Credentials

Open `infra/env/api.env.dev` and set:

```bash
# At the end of the file:
INITIAL_ADMIN_EMAIL=admin@yourdomain.com
INITIAL_ADMIN_PASSWORD=YourSecureP@ssw0rd123
INITIAL_ADMIN_DISPLAY_NAME=John Doe
```

### 3. Start Services

```bash
docker compose up -d
```

### 4. Verify Admin Creation

Check the logs:

```bash
docker compose logs api | grep "Initial admin"
```

Expected output:
```
✅ Initial admin user created successfully: admin@yourdomain.com (ID: ...)
```

### 5. Login

1. Open http://localhost:3000
2. Login with your credentials:
   - Email: `admin@yourdomain.com`
   - Password: `YourSecureP@ssw0rd123`

## Quick Setup (Local Development)

### 1. Set User Secrets

```bash
cd apps/api/src/Api

dotnet user-secrets set "INITIAL_ADMIN_EMAIL" "admin@localhost"
dotnet user-secrets set "INITIAL_ADMIN_PASSWORD" "DevAdmin123!"
dotnet user-secrets set "INITIAL_ADMIN_DISPLAY_NAME" "Local Admin"
```

### 2. Start Database

```bash
cd infra
docker compose up -d postgres qdrant redis
```

### 3. Run API

```bash
cd apps/api/src/Api
dotnet run
```

### 4. Verify

Look for log message:
```
✅ Initial admin user created successfully: admin@localhost (ID: ...)
```

## What Happens?

### First Startup
- Application checks if any admin users exist
- If none found and env vars are set → Creates admin user
- Logs success message with user ID
- Creates audit log entry

### Subsequent Startups
- Application finds existing admin
- Logs: `Admin user already exists, skipping bootstrap`
- No duplicate users created

## Password Requirements

✅ **Valid passwords:**
- `Admin123!` (8+ chars, uppercase, digit)
- `MySecureP@ss1` (special chars allowed)
- `Bootstrap2024` (simple but valid)

❌ **Invalid passwords:**
- `admin123` (no uppercase)
- `ADMIN` (too short, no digit)
- `password` (too short, no uppercase, no digit)

## Security Tips

1. **Change default password immediately** after first login
2. **Enable 2FA** for the admin user
3. **Delete environment variables** after first startup (optional)
4. **Use secrets manager** in production (Kubernetes, Azure Key Vault)

## Troubleshooting

### No admin user created?

**Check environment variables:**
```bash
docker compose exec api printenv | grep INITIAL_ADMIN
```

**Check logs for errors:**
```bash
docker compose logs api | grep -i "admin\|error"
```

### "Admin user already exists"?

This is **normal** if you've already run the application once. The bootstrap is idempotent.

To reset (for testing):
```bash
# Stop services
docker compose down -v

# Start fresh
docker compose up -d
```

## Next Steps

- ✅ Login to admin panel: http://localhost:3000/admin
- ✅ Create additional users
- ✅ Enable 2FA for admin account
- ✅ Explore documentation: [Admin Bootstrap Guide](./admin-bootstrap-guide.md)

## Need Help?

- Full documentation: [docs/guide/admin-bootstrap-guide.md](./admin-bootstrap-guide.md)
- Security best practices: [docs/SECURITY.md](../SECURITY.md)
- Environment setup: [docs/guide/secrets-management.md](./secrets-management.md)
