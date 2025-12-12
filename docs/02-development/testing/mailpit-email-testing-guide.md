# Mailpit Email Testing Guide

**Version**: 1.0
**Last Updated**: 2025-12-12
**Related**: Issue #922 - E2E Report Email Testing

## Overview

Mailpit is a modern, actively-maintained email testing tool that replaces the legacy Mailhog. It acts as a fake SMTP server for local development, capturing all outgoing emails without sending them to real recipients.

### Why Mailpit?

- **Modern**: Active development, latest features (2024+)
- **Drop-in Replacement**: Compatible with Mailhog ports (1025/8025)
- **REST API**: Complete API for automated testing
- **Web UI**: Beautiful interface for manual verification
- **Zero Dependencies**: Single Go binary, lightweight
- **Performance**: Low memory (<50MB), fast startup

## Quick Start

### 1. Start Mailpit

Mailpit is automatically started with the `dev`, `observability`, or `full` Docker profiles:

```bash
# Start full stack (includes Mailpit)
cd infra
docker compose --profile dev up -d

# Or start with explicit profile
docker compose --profile full up -d mailpit
```

### 2. Verify Mailpit is Running

```bash
# Check service health
curl http://localhost:8025/api/v1/messages

# Expected response: {"total":0,"unread":0,"count":0,"messages":[]}
```

### 3. Access Web UI

Open your browser: **http://localhost:8025**

You'll see:
- All captured emails
- HTML/Text preview
- Attachments
- Headers
- Source view

## Configuration

### API Configuration

The API is automatically configured to use Mailpit when running in development:

**File**: `infra/env/api.env.dev` (or `.env.development`)

```bash
Email__SmtpHost=mailpit
Email__SmtpPort=1025
Email__EnableSsl=false
Email__SmtpUsername=
Email__SmtpPassword=
Email__FromAddress=noreply@meepleai.dev
Email__FromName=MeepleAI Dev
```

### Mailpit Environment Variables

**File**: `infra/docker-compose.yml`

```yaml
environment:
  MP_MAX_MESSAGES: 500           # Keep last 500 messages
  MP_SMTP_AUTH_ACCEPT_ANY: 1     # Accept any SMTP credentials
  MP_SMTP_AUTH_ALLOW_INSECURE: 1 # Allow insecure connections
  MP_DATABASE: /data/mailpit.db  # Persistent storage
  MP_DATA_DIR: /data
```

## E2E Testing with Mailpit

### Test Pattern

```typescript
import { test, expect } from '@playwright/test';

const MAILPIT_API = 'http://localhost:8025/api/v1';

test('should send email via Mailpit', async ({ page, request }) => {
  // 1. Clear Mailpit before test
  await request.delete(`${MAILPIT_API}/messages`);

  // 2. Trigger email action in your app
  await page.goto('/admin/reports');
  await page.getByRole('button', { name: /send report/i }).click();

  // 3. Wait for email arrival (with retry)
  const email = await waitForEmail(request, /Report Ready/i);

  // 4. Verify email properties
  expect(email.Subject).toContain('Report Ready');
  expect(email.To[0].Address).toBe('admin@test.com');

  // 5. Get email details
  const detail = await request.get(`${MAILPIT_API}/message/${email.ID}`);
  const emailDetail = await detail.json();

  // 6. Verify HTML content
  expect(emailDetail.HTML).toContain('MeepleAI');

  // 7. Verify attachments
  expect(emailDetail.Attachments).toHaveLength(1);
  expect(emailDetail.Attachments[0].FileName).toMatch(/\.pdf$/);
});

// Helper function for waiting
async function waitForEmail(
  request: APIRequestContext,
  subjectPattern: RegExp,
  timeoutMs: number = 10000
): Promise<any> {
  const startTime = Date.now();

  while (Date.now() - startTime < timeoutMs) {
    const response = await request.get(`${MAILPIT_API}/messages`);
    const data = await response.json();
    const message = data.messages?.find((m: any) =>
      subjectPattern.test(m.Subject)
    );

    if (message) return message;

    await new Promise(resolve => setTimeout(resolve, 500));
  }

  throw new Error(`Email not received within ${timeoutMs}ms`);
}
```

### Complete Test Example

See: `apps/web/tests/e2e/admin-reports-email.spec.ts`

This file contains complete examples for:
- ✅ Full report workflow with email delivery
- ✅ HTML rendering verification
- ✅ Attachment validation
- ✅ Multiple recipients
- ✅ Visual regression testing

## Mailpit API Reference

### Common Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/messages` | List all messages |
| GET | `/api/v1/message/{id}` | Get message details |
| DELETE | `/api/v1/messages` | Delete all messages |
| DELETE | `/api/v1/message/{id}` | Delete specific message |
| GET | `/api/v1/search?query={text}` | Search messages |

### Response Formats

**List Messages Response**:
```json
{
  "total": 5,
  "unread": 2,
  "count": 5,
  "start": 0,
  "messages": [
    {
      "ID": "abc123",
      "From": {
        "Address": "noreply@meepleai.dev",
        "Name": "MeepleAI"
      },
      "To": [
        {
          "Address": "admin@test.com",
          "Name": ""
        }
      ],
      "Subject": "Report Ready: Daily System Health",
      "Created": "2025-12-12T10:30:00Z",
      "Size": 15234
    }
  ]
}
```

**Message Detail Response**:
```json
{
  "ID": "abc123",
  "Subject": "Report Ready",
  "HTML": "<html>...</html>",
  "Text": "Plain text version",
  "Attachments": [
    {
      "FileName": "report.pdf",
      "ContentType": "application/pdf",
      "Size": 12345
    }
  ],
  "Headers": {
    "Content-Type": ["text/html; charset=utf-8"],
    "Date": ["Thu, 12 Dec 2025 10:30:00 +0000"]
  }
}
```

## HTML Rendering Verification

### Using Mailpit Web UI

1. Navigate to http://localhost:8025
2. Click on the email
3. View HTML preview in iframe
4. Take screenshot for visual regression

### Using Playwright

```typescript
test('verify email HTML rendering', async ({ page }) => {
  await page.goto('http://localhost:8025');
  await page.getByText(/Report Ready/i).click();

  // Switch to HTML preview iframe
  const htmlFrame = page.frameLocator('iframe[title="HTML preview"]');

  // Verify content
  await expect(htmlFrame.locator('h1')).toContainText('MeepleAI');
  await expect(htmlFrame.locator('h2')).toContainText('Report Ready');

  // Screenshot for visual regression
  await page.screenshot({
    path: 'test-results/email-rendering.png',
    fullPage: true
  });

  // Verify colors/styling
  const headerBg = await htmlFrame.locator('h1').evaluate(
    el => window.getComputedStyle(el).backgroundColor
  );
  expect(headerBg).toBe('rgb(248, 249, 250)'); // #f8f9fa
});
```

## Troubleshooting

### Mailpit Not Starting

**Symptom**: `curl http://localhost:8025` fails

**Solutions**:
```bash
# Check service status
docker compose ps mailpit

# Check logs
docker compose logs mailpit

# Restart service
docker compose restart mailpit

# Verify health
docker compose exec mailpit wget -qO- http://localhost:8025/api/v1/messages
```

### Emails Not Arriving

**Symptom**: Test fails with "Email not received"

**Checklist**:
1. ✅ Mailpit is running: `docker compose ps mailpit`
2. ✅ SMTP config correct: Check `api.env.dev` → `Email__SmtpHost=mailpit`
3. ✅ API restarted: `docker compose restart api`
4. ✅ Email actually sent: Check API logs: `docker compose logs api | grep -i email`
5. ✅ Mailpit receiving: Check Mailpit logs: `docker compose logs mailpit`

**Debug Commands**:
```bash
# Check Mailpit messages
curl http://localhost:8025/api/v1/messages | jq .

# Test SMTP connection manually
docker compose exec api sh -c "echo 'Test' | mailx -v -S smtp=mailpit:1025 test@example.com"

# Check API logs for email sending
docker compose logs api | grep "SendReportEmailAsync"
```

### Port Conflicts

**Symptom**: `Port 1025 or 8025 already in use`

**Solutions**:
```bash
# Find process using port
# Windows
netstat -ano | findstr "1025"
netstat -ano | findstr "8025"

# Linux/Mac
lsof -i :1025
lsof -i :8025

# Kill conflicting process or change Mailpit ports in docker-compose.yml
```

### HTML Not Rendering

**Symptom**: Email HTML preview is blank

**Solutions**:
1. Check email actually has HTML content
2. Verify iframe loads: Check browser console for errors
3. Try different browser (some block nested iframes)
4. Check email HTML validity: Use Mailpit "Source" tab

## Best Practices

### Test Isolation

✅ **DO**: Clear Mailpit before each test
```typescript
test.beforeEach(async ({ request }) => {
  await request.delete('http://localhost:8025/api/v1/messages');
});
```

❌ **DON'T**: Rely on specific message counts
```typescript
// BAD: Flaky if other tests ran
expect(messages).toHaveLength(1);

// GOOD: Search for specific subject
const myEmail = messages.find(m => m.Subject.includes('My Report'));
expect(myEmail).toBeDefined();
```

### Waiting for Emails

✅ **DO**: Implement retry logic
```typescript
async function waitForEmail(pattern: RegExp, timeoutMs = 10000) {
  const startTime = Date.now();
  while (Date.now() - startTime < timeoutMs) {
    const messages = await getMessages();
    const match = messages.find(m => pattern.test(m.Subject));
    if (match) return match;
    await sleep(500);
  }
  throw new Error('Email timeout');
}
```

❌ **DON'T**: Use fixed delays
```typescript
// BAD: Wastes time or fails randomly
await page.waitForTimeout(5000);
```

### Verification Depth

✅ **DO**: Verify key properties
```typescript
expect(email.Subject).toContain('Report Ready');
expect(email.To[0].Address).toBe('admin@test.com');
expect(email.Attachments[0].Size).toBeGreaterThan(1024);
```

❌ **DON'T**: Over-verify unstable properties
```typescript
// BAD: Timestamps change every run
expect(email.Created).toBe('2025-12-12T10:30:00Z');

// BAD: File size may vary slightly
expect(email.Size).toBe(15234);
```

## CI/CD Integration

### GitHub Actions (Local Testing Only)

**Important**: Mailpit is configured for **local development only**. E2E email tests are **not run in GitHub Actions CI**.

```yaml
# .github/workflows/ci.yml (example - not currently used)
jobs:
  e2e-email-tests:
    runs-on: ubuntu-latest
    services:
      mailpit:
        image: axllent/mailpit:v1.22
        ports:
          - 1025:1025
          - 8025:8025

    steps:
      - name: Run E2E Email Tests
        run: pnpm test:e2e admin-reports-email
```

**Current Status**: Email tests run **locally only** during development.

## Migration from Mailhog

If you were previously using Mailhog, migration to Mailpit is seamless:

### What Stays the Same
- SMTP Port: `1025`
- Web UI Port: `8025`
- API Endpoint: `/api/v1/messages`

### What Changes
- Docker Image: `mailhog/mailhog` → `axllent/mailpit:v1.22`
- API Response Format: Slight differences (check API docs)
- Web UI: Improved interface, more features

### Migration Steps
1. Update `docker-compose.yml`: Change image to `axllent/mailpit:v1.22`
2. Restart services: `docker compose restart`
3. Update E2E tests: Adjust for new API response format if needed

No code changes required in EmailService!

## Additional Resources

- **Mailpit Official Docs**: https://mailpit.axllent.org/
- **Mailpit API Docs**: https://mailpit.axllent.org/docs/api/
- **GitHub Repository**: https://github.com/axllent/mailpit
- **Docker Hub**: https://hub.docker.com/r/axllent/mailpit
- **Issue #922**: E2E Report Email Testing Implementation

## Support

For issues or questions:
1. Check Mailpit logs: `docker compose logs mailpit`
2. Verify configuration in `docker-compose.yml` and `.env.development`
3. Review test examples in `apps/web/tests/e2e/admin-reports-email.spec.ts`
4. Consult Mailpit docs: https://mailpit.axllent.org/docs/
