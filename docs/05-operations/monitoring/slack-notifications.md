# Slack Notifications for CI/CD

**Automated Slack notifications for GitHub Actions failures** - Configure real-time alerts for K6 performance test failures and other critical CI/CD events.

---

## Overview

The MeepleAI CI/CD pipeline includes automated Slack notifications for critical workflow failures. When enabled, the system sends rich, formatted notifications to your Slack workspace, providing immediate visibility into performance issues and test failures.

**Currently Supported Workflows**:
- K6 Performance Tests (nightly runs)

**Planned Support**:
- Security scan failures
- Deployment failures
- Critical test failures

---

## Quick Setup (5 minutes)

### Prerequisites
- Slack workspace with admin access
- GitHub repository with admin access
- K6 performance workflow enabled (`.github/workflows/k6-performance.yml`)

### Steps

1. **Create Slack App** (2 min)
2. **Enable Incoming Webhooks** (1 min)
3. **Add Webhook to Channel** (1 min)
4. **Configure GitHub Secret** (1 min)

---

## Step 1: Create Slack App

### Option A: Quick Setup (Recommended)

1. Navigate to https://api.slack.com/apps
2. Click **"Create New App"**
3. Select **"From scratch"**
4. Configure:
   - **App Name**: `MeepleAI CI/CD`
   - **Workspace**: Select your workspace
5. Click **"Create App"**

### Option B: App Manifest (Advanced)

Use this manifest for pre-configured settings:

```json
{
  "display_information": {
    "name": "MeepleAI CI/CD",
    "description": "Automated notifications for CI/CD pipeline failures",
    "background_color": "#2c2d30"
  },
  "features": {
    "bot_user": {
      "display_name": "MeepleAI CI/CD",
      "always_online": true
    }
  },
  "oauth_config": {
    "scopes": {
      "bot": [
        "incoming-webhook"
      ]
    }
  },
  "settings": {
    "org_deploy_enabled": false,
    "socket_mode_enabled": false,
    "token_rotation_enabled": false
  }
}
```

---

## Step 2: Enable Incoming Webhooks

1. In your Slack App settings, navigate to **"Incoming Webhooks"**
2. Toggle **"Activate Incoming Webhooks"** to **ON**
3. Scroll down and click **"Add New Webhook to Workspace"**
4. Select the channel for notifications (e.g., `#ci-cd-alerts` or `#engineering`)
5. Click **"Allow"**

**Recommended Channels**:
- `#ci-cd-alerts` - Dedicated channel for CI/CD notifications
- `#engineering` - General engineering channel
- `#on-call` - On-call rotation channel
- `#performance` - Performance-specific alerts

---

## Step 3: Copy Webhook URL

After authorizing, you'll see a webhook URL like:

```
https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX
```

**⚠️ Important**: Treat this URL like a password - never commit it to Git!

---

## Step 4: Configure GitHub Secret

### Via GitHub UI (Recommended)

1. Navigate to your repository on GitHub
2. Go to **Settings** → **Secrets and variables** → **Actions**
3. Click **"New repository secret"**
4. Configure:
   - **Name**: `SLACK_WEBHOOK_URL`
   - **Value**: (paste your webhook URL)
5. Click **"Add secret"**

### Via GitHub CLI

```bash
gh secret set SLACK_WEBHOOK_URL --body "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX"
```

### For Multiple Repositories

If you have multiple repositories in the same organization:

```bash
gh secret set SLACK_WEBHOOK_URL --org --body "https://hooks.slack.com/services/..."
```

---

## Notification Format

### K6 Performance Test Failure

When K6 performance tests fail (nightly runs), you'll receive:

**Header**:
```
🚨 K6 Performance Tests Failed
```

**Details**:
- **Repository**: `DegrassiAaron/meepleai-monorepo`
- **Branch**: `main`
- **Trigger**: Scheduled (nightly)
- **Run ID**: `12345678`

**Action Button**:
- `View Workflow Run` - Direct link to GitHub Actions run

**Example Notification**:

```
🚨 K6 Performance Tests Failed

Repository: DegrassiAaron/meepleai-monorepo
Branch: main
Trigger: Scheduled (nightly)
Run ID: 12345678

[View Workflow Run]
```

---

## Testing Notifications

### Manual Test (Recommended)

Test your webhook without triggering a real failure:

```bash
curl -X POST -H 'Content-type: application/json' \
  --data '{
    "text": "🧪 Test Notification",
    "blocks": [
      {
        "type": "header",
        "text": {
          "type": "plain_text",
          "text": "🧪 Test Notification",
          "emoji": true
        }
      },
      {
        "type": "section",
        "text": {
          "type": "mrkdwn",
          "text": "This is a test notification from MeepleAI CI/CD setup."
        }
      }
    ]
  }' \
  YOUR_WEBHOOK_URL
```

### Trigger Workflow Manually

Force a test failure to verify end-to-end:

1. Navigate to **Actions** → **K6 Performance Tests**
2. Click **"Run workflow"**
3. Intentionally break a test threshold
4. Wait for failure notification

---

## Customization

### Changing Notification Format

The notification format is defined in `.github/workflows/k6-performance.yml` (lines 329-376):

```javascript
const payload = {
  text: '🚨 K6 Performance Tests Failed',
  blocks: [
    {
      type: 'header',
      text: {
        type: 'plain_text',
        text: '🚨 K6 Performance Tests Failed',
        emoji: true
      }
    },
    // ... additional blocks
  ]
};
```

**Customization Options**:
- Change emoji (🚨 → ⚠️, 🔴, ❌)
- Add additional fields (commit SHA, author, etc.)
- Modify button text
- Add multiple action buttons

**Slack Block Kit Builder**: https://app.slack.com/block-kit-builder

### Adding Mentions

Mention specific users or teams on failure:

```javascript
{
  type: 'section',
  text: {
    type: 'mrkdwn',
    text: '<!channel> Performance tests failed! <@U12345678> please investigate.'
  }
}
```

**User/Channel Mentions**:
- `<!channel>` - @channel (all online members)
- `<!here>` - @here (all online members)
- `<@U12345678>` - Specific user (find ID via Slack API)
- `<!subteam^S12345678>` - User group (e.g., @on-call)

### Changing Notification Channel

To send notifications to a different channel:

1. Go to Slack App settings → **Incoming Webhooks**
2. Click **"Add New Webhook to Workspace"**
3. Select new channel
4. Update `SLACK_WEBHOOK_URL` secret in GitHub

---

## Troubleshooting

### Notifications Not Received

**Check 1: Secret Configured**
```bash
# Verify secret exists (will show obfuscated value)
gh secret list | grep SLACK_WEBHOOK_URL
```

**Check 2: Webhook Valid**
```bash
# Test webhook directly
curl -X POST -H 'Content-type: application/json' \
  --data '{"text":"Test"}' \
  YOUR_WEBHOOK_URL

# Expected response: "ok"
```

**Check 3: Workflow Triggered**
- Notifications only send on **scheduled** K6 failures
- Manual workflow runs won't trigger notifications (by design)
- Check workflow logs for "Send Slack notification" step

**Check 4: Conditional Logic**
- Notification step has condition: `if: ${{ secrets.SLACK_WEBHOOK_URL != '' }}`
- If secret is not set, step is skipped (not an error)

### Invalid Webhook Error

**Symptom**: Workflow logs show `Failed to send Slack notification: 404`

**Cause**: Webhook URL is invalid or has been revoked

**Fix**:
1. Verify webhook still exists in Slack App settings
2. Regenerate webhook if needed
3. Update GitHub secret

### Webhook Revoked

**Symptom**: Notifications stopped working after Slack app changes

**Cause**: Slack app was deleted or webhook was revoked

**Fix**:
1. Recreate Slack app (follow Step 1)
2. Create new webhook (follow Step 2)
3. Update GitHub secret (follow Step 4)

### Formatting Issues

**Symptom**: Notification appears as plain text without formatting

**Cause**: Block Kit JSON is invalid

**Fix**:
1. Validate payload using Slack Block Kit Builder: https://app.slack.com/block-kit-builder
2. Check for syntax errors in workflow YAML
3. Ensure proper JSON escaping

---

## Security Best Practices

### ✅ DO
- Store webhook URL in GitHub Secrets (encrypted)
- Use organization-level secrets for multiple repos
- Rotate webhook URLs periodically (every 90 days)
- Limit webhook to specific channels
- Monitor webhook usage in Slack audit logs

### ❌ DON'T
- Commit webhook URLs to Git
- Share webhook URLs in public channels
- Use same webhook for multiple environments (dev/staging/prod)
- Log webhook URLs in workflow output
- Store webhooks in plaintext files

### Webhook Rotation

**Recommended Frequency**: Every 90 days

**Process**:
1. Create new webhook in Slack
2. Update GitHub secret with new URL
3. Test new webhook
4. Delete old webhook in Slack
5. Verify old webhook no longer works

---

## GitHub Issue Integration

In addition to Slack notifications, the K6 workflow also creates GitHub issues on failure.

**Features**:
- **Duplicate Detection**: Checks for existing open issues before creating new ones
- **Timestamped Comments**: Adds timestamped entries to existing issues
- **Troubleshooting Checklist**: Includes actionable investigation steps
- **Auto-Labeling**: Tags with `performance`, `automated`, `bug`, `priority: high`

**Example Issue**:
```markdown
## 🔴 Performance Test Failure - 2025-11-21T02:00:00.000Z

**Run:** [#12345678](https://github.com/...)
**Branch:** main
**Trigger:** Scheduled (nightly)

### Action Required
The nightly K6 performance tests have failed. Please investigate the workflow run for details.

### Common Issues to Check
- [ ] API response times exceeding thresholds
- [ ] Increased error rates
- [ ] Database connection issues
- [ ] Memory or resource constraints
- [ ] Breaking changes in recent commits

### Next Steps
1. Review the workflow run logs
2. Check the performance reports artifact
3. Investigate and fix the root cause
4. Re-run the workflow to verify the fix
5. Close this issue once resolved
```

**Why Both Slack + GitHub Issues?**
- **Slack**: Immediate awareness (push notification)
- **GitHub Issues**: Permanent tracking, investigation history, action items

---

## Advanced Configuration

### Multiple Webhooks

Send notifications to multiple channels:

**Option 1: Multiple Secrets**
```yaml
- name: Send to Engineering
  uses: actions/github-script@v8
  env:
    SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK_ENGINEERING }}
  # ... notification logic

- name: Send to On-Call
  uses: actions/github-script@v8
  env:
    SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK_ONCALL }}
  # ... notification logic
```

**Option 2: Multiple POST Requests**
```javascript
const webhooks = [
  process.env.SLACK_WEBHOOK_ENGINEERING,
  process.env.SLACK_WEBHOOK_ONCALL
];

for (const webhook of webhooks) {
  await fetch(webhook, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
}
```

### Rate Limiting

Slack webhooks have rate limits:
- **1 message per second** per webhook
- **Burst**: up to 30 messages per minute

**Best Practice**: Add delays between rapid notifications:
```javascript
await new Promise(resolve => setTimeout(resolve, 1000)); // 1s delay
```

### Notification Throttling

Prevent alert fatigue:

```javascript
// Check last notification time
const lastNotified = await getLastNotificationTime(); // From cache or DB
const now = Date.now();
const minInterval = 30 * 60 * 1000; // 30 minutes

if (now - lastNotified < minInterval) {
  console.log('Skipping notification (too soon)');
  return;
}

// Send notification
await sendSlackNotification(payload);

// Update last notification time
await setLastNotificationTime(now);
```

---

## Monitoring Notification Delivery

### Slack Audit Logs

Track webhook usage:

1. Navigate to **Slack Workspace Settings** → **Audit Logs**
2. Filter by **"Webhook"** events
3. Review successful/failed deliveries

**Metrics to Track**:
- Delivery success rate (target: >99%)
- Average delivery time (target: <2s)
- Failed deliveries (investigate if >1%)

### GitHub Actions Logs

Verify notification step execution:

1. Navigate to **Actions** → **K6 Performance Tests** → Failed run
2. Expand **"Send Slack notification"** step
3. Look for log output:
   - ✅ `Slack notification sent successfully`
   - ❌ `Failed to send Slack notification: [error]`

---

## Migration from Other Notification Systems

### From Email to Slack

**Advantages**:
- ✅ Real-time delivery (push notification)
- ✅ Rich formatting (buttons, links, mentions)
- ✅ Centralized in team communication tool
- ✅ Easy acknowledgment/threading

**Disadvantages**:
- ❌ Requires Slack workspace access
- ❌ Notifications can be missed if Slack is closed

**Recommendation**: Use both (Slack for immediate alerts, email for backup)

### From PagerDuty to Slack

**When to Use PagerDuty**:
- SEV-1/SEV-2 incidents (critical/high severity)
- On-call rotation enforcement
- Escalation policies needed
- Acknowledgment tracking required

**When to Use Slack**:
- SEV-3/SEV-4 incidents (low/medium severity)
- Informational alerts
- CI/CD notifications
- Team awareness

**Best Practice**: Route SEV-1/SEV-2 to PagerDuty, SEV-3/SEV-4 to Slack

---

## Related Documentation

- [K6 Performance Testing](../../02-development/testing/k6-performance-testing.md) - K6 test setup
- [GitHub Actions Workflows](.github/workflows/k6-performance.yml) - Workflow configuration
- [Monitoring & Observability](./logging-and-audit.md) - Full observability stack
- [Alerting Configuration](./alerting.md) - Prometheus Alertmanager setup

---

## FAQ

### Q: Can I use Microsoft Teams instead of Slack?
**A**: Yes, but requires custom webhook configuration. Teams webhooks use different JSON format. See [Teams Webhook Documentation](https://docs.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook).

### Q: Can I route different failures to different channels?
**A**: Yes, create multiple webhooks and configure conditional logic in workflows based on failure type.

### Q: What if my Slack workspace is on free plan?
**A**: Incoming webhooks work on free plan. No limitations.

### Q: Can I send notifications to private channels?
**A**: Yes, when adding webhook to workspace, select private channel.

### Q: How do I silence notifications temporarily?
**A**: Option 1: Mute channel. Option 2: Remove/comment out `SLACK_WEBHOOK_URL` secret.

### Q: Can I add custom data to notifications?
**A**: Yes, modify payload in workflow YAML. Any data accessible via GitHub context can be included.

### Q: What's the maximum message size?
**A**: Slack Block Kit supports up to 50 blocks per message, 3000 characters per text block.

---

## Appendix: Webhook URL Format

Slack webhook URLs follow this format:

```
https://hooks.slack.com/services/{WORKSPACE_ID}/{APP_ID}/{TOKEN}
```

**Components**:
- `WORKSPACE_ID`: Your Slack workspace identifier (T followed by 8-11 alphanumeric characters)
- `APP_ID`: Your Slack app identifier (B followed by 8-11 alphanumeric characters)
- `TOKEN`: Webhook-specific token (24 alphanumeric characters)

**Example**:
```
https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX
```

**Security Note**: Never extract or store these components separately - always use the full URL.

---

**Last Updated**: 2025-11-21
**Maintainer**: DevOps Team
**Related Issue**: [#1457 - GitHub Actions Phase 3 Optimizations](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1457)
**Workflow**: `.github/workflows/k6-performance.yml`
