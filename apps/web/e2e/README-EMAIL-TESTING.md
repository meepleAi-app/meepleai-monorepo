# E2E Email Testing - Quick Start Guide

**Issue**: #922 - E2E Report Generation + Email Validation
**Last Updated**: 2025-12-12

## Prerequisites

1. **Mailpit Service Running**
   ```bash
   cd infra
   docker compose --profile dev up -d mailpit

   # Verify
   curl http://localhost:8025/api/v1/messages
   # Expected: {"total":0,"unread":0,"count":0,...}
   ```

2. **API Service Running** (with Mailpit configuration)
   ```bash
   # Option 1: Full Docker stack
   cd infra
   docker compose --profile dev up -d

   # Option 2: API locally (requires Postgres, Redis, Qdrant running)
   cd apps/api/src/Api
   dotnet run
   ```

   **Important**: API must have Email config pointing to Mailpit:
   ```bash
   Email__SmtpHost=mailpit
   Email__SmtpPort=1025
   Email__EnableSsl=false
   ```

3. **Web Service Running**
   ```bash
   cd apps/web
   pnpm dev
   ```

## Running Email Tests

### Run All Email Tests

```bash
cd apps/web
pnpm test:e2e admin-reports-email
```

### Run Single Test

```bash
# Complete flow test
pnpm test:e2e admin-reports-email -g "complete full report generation"

# HTML rendering test
pnpm test:e2e admin-reports-email -g "render email HTML"

# Attachment validation
pnpm test:e2e admin-reports-email -g "include valid PDF attachment"
```

### Debug Mode

```bash
# Run with UI (see browser actions)
pnpm test:e2e admin-reports-email --headed

# Debug mode (step through test)
pnpm test:e2e admin-reports-email --debug
```

## Verifying Email Delivery

### Via Mailpit Web UI (Manual Verification)

1. Open browser: http://localhost:8025
2. You'll see all captured emails
3. Click on email to view:
   - HTML preview
   - Plain text
   - Attachments
   - Headers
   - Raw source

### Via Mailpit API (Automated Verification)

```bash
# List all messages
curl http://localhost:8025/api/v1/messages | jq .

# Get specific message detail
curl http://localhost:8025/api/v1/message/{MESSAGE_ID} | jq .

# Search messages
curl "http://localhost:8025/api/v1/search?query=Report%20Ready" | jq .

# Clear all messages
curl -X DELETE http://localhost:8025/api/v1/messages
```

## Test Scenarios Covered

| Scenario | Description | File Location |
|----------|-------------|---------------|
| **Complete Flow** | Full report generation → scheduling → email delivery | Line 125 |
| **HTML Rendering** | Verify email HTML structure and Mailpit preview | Line 179 |
| **Attachment Validation** | PDF attachment name, size, content-type | Line 226 |
| **Multiple Recipients** | Send to 3+ recipients | Line 269 |
| **Email Failures** | Graceful SMTP error handling | Line 303 |
| **Visual Regression** | Screenshot email rendering | Line 327 |
| **Mailpit Health** | Service availability checks | Line 396 |

## Expected Test Results

```
✓ should complete full report generation and email delivery flow (15s)
✓ should render email HTML with correct structure and styling (12s)
✓ should include valid PDF attachment in report email (10s)
✓ should send email to multiple recipients (11s)
✓ should handle email delivery failures gracefully (5s)
✓ should render email with consistent visual appearance (13s)
✓ should have Mailpit service running and accessible (1s)
✓ should be able to clear messages via API (1s)

Test Suites: 1 passed, 1 total
Tests:       8 passed, 8 total
Time:        ~68s
```

## Troubleshooting

### Test Fails: "Email not received"

**Cause**: Email not arriving in Mailpit

**Solutions**:
```bash
# 1. Check Mailpit is running
docker compose ps mailpit
# Should show: Up X seconds (healthy)

# 2. Check API email configuration
docker compose exec api sh -c 'env | grep Email'
# Should show: Email__SmtpHost=mailpit

# 3. Check API logs for email sending
docker compose logs api | grep -i "SendReportEmailAsync"

# 4. Manually test SMTP connection
curl -X POST http://localhost:8025/api/v1/send-test-email
```

### Test Fails: "iframe not visible"

**Cause**: Mailpit Web UI not loading or iframe blocked

**Solutions**:
```bash
# 1. Verify Mailpit Web UI accessible
curl http://localhost:8025/

# 2. Check browser console for errors
# Open DevTools in test browser

# 3. Try without iframe (use API only)
# Verify HTML via API instead of Web UI
```

### Port Conflicts

**Symptom**: `Error: Port 1025 or 8025 already in use`

**Solutions**:
```bash
# Windows: Find and kill process
netstat -ano | findstr "1025"
taskkill /PID {PID} /F

# Linux/Mac
lsof -i :1025
kill -9 {PID}

# Or change Mailpit ports in docker-compose.yml
ports:
  - "127.0.0.1:11025:1025"  # Different host port
  - "127.0.0.1:18025:8025"
```

## Visual Regression Artifacts

After running tests, check screenshots in:

```
apps/web/test-results/
├── email-html-rendering-verification.png    # Full Mailpit UI + email
├── email-visual-regression-full.png         # Complete email view
└── email-visual-regression-body.png         # Email body only
```

## Next Steps

1. **Run Tests**: Follow steps above
2. **Review Screenshots**: Check visual regression artifacts
3. **Verify Coverage**: All 6 scenarios from Issue #922 should pass
4. **Documentation**: See `docs/02-development/testing/mailpit-email-testing-guide.md`

## Related Files

- **E2E Test**: `apps/web/tests/e2e/admin-reports-email.spec.ts`
- **Docker Config**: `infra/docker-compose.yml` (line 90-122)
- **Email Service**: `apps/api/src/Api/Services/EmailService.cs`
- **Full Guide**: `docs/02-development/testing/mailpit-email-testing-guide.md`
