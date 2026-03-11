# Email Testing in Development

## Quick Start

```bash
# 1. Start Mailpit (modern MailHog alternative)
cd infra && docker compose --profile dev up -d mailpit

# 2. Verify
curl -s http://localhost:8025/api/v1/messages | jq '.total'
# Should return 0

# 3. Open Web UI
# http://localhost:8025
```

## How It Works

The API is pre-configured for Mailpit in development:

| Setting | Value | File |
|---------|-------|------|
| SMTP Host | `localhost` | `appsettings.Development.json` |
| SMTP Port | `1025` | `appsettings.Development.json` |
| SSL | `false` | `appsettings.Development.json` |
| From | `noreply@meepleai.local` | `appsettings.Development.json` |
| Auth | Not required | Mailpit accepts any credentials |

No configuration needed — just start Mailpit and run the API.

## Triggering Test Emails

### Via Admin Endpoint
```bash
curl -X POST http://localhost:8080/api/v1/admin/emails/test \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <admin-token>" \
  -d '{"to": "test@example.com"}'
```

### Via Application Events
- Upload a PDF → triggers `pdf_upload_completed` email
- Create a share request → triggers notification emails
- Complete gamification action → triggers badge email

### Via Mailpit API (Automation)
```bash
# List all messages
curl http://localhost:8025/api/v1/messages

# Search messages
curl "http://localhost:8025/api/v1/search?query=subject:PDF"

# Delete all messages
curl -X DELETE http://localhost:8025/api/v1/messages

# Get specific message HTML
curl http://localhost:8025/api/v1/message/{id}/part/1
```

## Mailpit Web UI

Open `http://localhost:8025` to:
- View all captured emails
- Inspect HTML rendering
- Check headers (List-Unsubscribe, etc.)
- View raw MIME content
- Search by subject, sender, recipient
- Mobile responsive preview

## Docker Configuration

Mailpit runs in `dev` and `full` profiles:

```yaml
# infra/docker-compose.yml
mailpit:
  image: axllent/mailpit:v1.22
  ports:
    - "127.0.0.1:1025:1025"  # SMTP
    - "127.0.0.1:8025:8025"  # Web UI
  profiles: [dev, observability, full]  # Also available in observability profile
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Mailpit UI not loading | `docker compose --profile dev up -d mailpit` |
| Emails not appearing | Check API logs for SMTP errors |
| Port 1025 occupied | `netstat -ano \| findstr :1025` → kill process |
| Port 8025 occupied | `netstat -ano \| findstr :8025` → kill process |
| Connection refused | Verify Mailpit container is healthy: `docker inspect mailpit --format='{{.State.Health.Status}}'` |
