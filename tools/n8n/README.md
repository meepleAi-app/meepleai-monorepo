# n8n Workflow Automation

Scripts for integrating with n8n workflow automation platform.

## Scripts

### 🔗 **register-n8n-webhook.ps1**
**Purpose:** Register webhooks with n8n for automated workflows

**What it does:**
- Configures n8n webhook endpoints
- Registers workflow triggers (GitHub events, API events)
- Tests webhook connectivity

**Usage:**
```powershell
# Register webhook
.\tools\n8n\register-n8n-webhook.ps1 -WebhookUrl "https://n8n.meepleai.dev/webhook/test"

# Test webhook
.\tools\n8n\register-n8n-webhook.ps1 -Test
```

**Who:** DevOps team setting up workflow automation
**When:** n8n workflow deployment
**Requirements:** n8n instance running, PowerShell 5.1+, access to n8n API

**Related:** See `apps/api/src/Api/BoundedContexts/WorkflowIntegration/` for n8n integration code

---

**Last Updated:** 2025-11-22
**Maintained by:** DevOps team
