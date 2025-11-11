# Secrets Management Guide

This guide covers best practices for managing secrets and sensitive configuration in the MeepleAI project.

## Overview

MeepleAI follows security-first principles for secrets management:
- ✅ **Never commit secrets** to version control
- ✅ **Use environment variables** for all sensitive data
- ✅ **Provide `.example` templates** for configuration
- ✅ **Gitignore protection** for local secret files
- ✅ **Automated secret detection** via pre-commit hooks

## Environment File Strategy

### File Naming Convention

| File Pattern | Purpose | Committed to Git? | Use Case |
|--------------|---------|-------------------|----------|
| `*.env.example` | Configuration template | ✅ Yes | Documentation and onboarding |
| `*.env.ci.example` | CI/CD template | ✅ Yes | GitHub Actions workflow |
| `*.env.dev` | Local development secrets | ❌ No | Developer workstation |
| `*.env.local` | Local overrides | ❌ No | Personal customization |
| `*.env.prod` | Production secrets | ❌ No | Production deployment |
| `*.env` | Generic secrets | ❌ No | Catch-all exclusion |

### Current Environment Files

#### API Configuration
- **Template**: `infra/env/api.env.dev.example`
- **Local**: `infra/env/api.env.dev` (create from template)
- **Secrets**: `OPENROUTER_API_KEY`, database passwords, JWT secrets

#### Alertmanager Configuration
- **Template**: `infra/env/alertmanager.env.example`
- **Local**: `infra/env/alertmanager.env` (create from template)
- **Secrets**: `GMAIL_APP_PASSWORD`, `SLACK_WEBHOOK_URL`

#### n8n Configuration
- **Template**: `infra/env/n8n.env.dev.example`
- **Local**: `infra/env/n8n.env.dev` (create from template)
- **Secrets**: `N8N_ENCRYPTION_KEY`, webhook URLs

#### Web Frontend Configuration
- **Template**: `infra/env/web.env.dev.example`
- **Local**: `infra/env/web.env.dev` (create from template)
- **Secrets**: `NEXT_PUBLIC_API_BASE`, analytics tokens

## Setup Instructions

### Initial Development Setup

1. **Copy example templates** to create local environment files:
   ```bash
   cd infra/env

   # Copy all templates
   cp api.env.dev.example api.env.dev
   cp alertmanager.env.example alertmanager.env
   cp n8n.env.dev.example n8n.env.dev
   cp web.env.dev.example web.env.dev
   ```

2. **Fill in secrets** from your secure source:
   - OpenRouter API key from https://openrouter.ai/
   - Gmail App Password (see [Gmail Setup](#gmail-app-password))
   - Database passwords (generate secure passwords)
   - JWT secrets (use `openssl rand -base64 32`)

3. **Verify gitignore protection**:
   ```bash
   # Should show: "infra/env/alertmanager.env" is ignored
   git check-ignore infra/env/alertmanager.env

   # Verify no secrets tracked
   git status --porcelain | grep '\.env'
   ```

4. **Start services**:
   ```bash
   cd ../
   docker compose up -d
   ```

### Gmail App Password

Alertmanager requires a Gmail App Password for email notifications:

1. **Enable 2-Step Verification**:
   - Go to https://myaccount.google.com/security
   - Enable "2-Step Verification" if not already active

2. **Generate App Password**:
   - Visit https://myaccount.google.com/apppasswords
   - Select app: **Mail**
   - Select device: **Other (Custom name)** → "MeepleAI Alertmanager"
   - Click **Generate**
   - Copy the 16-character password (format: `xxxx xxxx xxxx xxxx`)

3. **Configure Alertmanager**:
   ```bash
   # Edit infra/env/alertmanager.env
   GMAIL_APP_PASSWORD=abcdabcdabcdabcd  # Remove spaces from generated password
   ```

4. **Restart Alertmanager**:
   ```bash
   docker compose restart alertmanager
   ```

5. **Verify**:
   ```bash
   docker compose logs alertmanager | grep "Completed loading of configuration"
   ```

## Security Best Practices

### Never Commit These

❌ **Forbidden in Git**:
- API keys (OpenRouter, third-party services)
- Database passwords or connection strings with embedded credentials
- Gmail App Passwords
- Slack webhook URLs
- GitHub Personal Access Tokens (PATs)
- JWT signing secrets
- Session secrets
- Private keys or certificates
- Any file matching `*.env.dev`, `*.env.local`, `*.env.prod`

✅ **Always Allowed**:
- Template files: `*.env.example`, `*.env.ci.example`
- Documentation references to secret names (not values)
- Example placeholder values (e.g., `GMAIL_APP_PASSWORD=your-password-here`)

### Gitignore Configuration

The `.gitignore` file provides multi-layer protection:

```gitignore
# Generic environment files
.env
.env.*
*.env
*.env.*

# Allow only templates
!*.env.example
!*.env.ci.example

# Explicitly block development/production
*.env.dev
*.env.local
*.env.prod
*.env.production

# Specific service secrets
infra/env/n8n-service-session.env
infra/env/alertmanager.env  # Added in SEC-700
```

### Pre-commit Hooks

Automated secret detection prevents accidental commits:

```bash
# Install pre-commit framework
pip install pre-commit

# Install hooks
pre-commit install

# Run manually
pre-commit run --all-files
```

**Configured checks**:
- `detect-secrets`: Scans for high-entropy strings and secret patterns
- `detect-private-key`: Detects SSH/PGP private keys
- `check-added-large-files`: Prevents large binary files (max 1MB)

## Secret Rotation

### Strategy Overview

MeepleAI follows a **two-phase approach** to secrets management:

| Phase | Solution | Rotation | Versioning | Status |
|-------|----------|----------|------------|--------|
| **Phase 1 (MVP)** | Docker Secrets | ❌ Manual only | ❌ No | ✅ Current |
| **Phase 2 (Production)** | Infisical (self-hosted) | ✅ Automatic | ✅ Unlimited | 📋 Planned |

**Phase 2 Decision** (Issue #708):
- **Selected**: [Infisical](https://infisical.com/) (MIT license, self-hosted)
- **Why**: FREE automatic rotation + versioning, Docker Compose deployment, compatible stack (PostgreSQL + Redis)
- **POC**: Issue #936
- **Alternatives evaluated**: HashiCorp Vault (no free rotation), CyberArk Conjur (complex setup)

### When to Rotate

**Immediately rotate if**:
- Secret exposed in git commit (even if reverted)
- Secret exposed in logs or error messages
- Team member with access leaves
- Suspected security breach
- Service provider recommends rotation

**Scheduled rotation** (Phase 1 - Manual):
- API keys: Every 90 days
- Database passwords: Every 180 days
- JWT secrets: Every 365 days
- Gmail App Passwords: When Gmail prompts or annually

**Scheduled rotation** (Phase 2 - Automatic via Infisical):
- Database credentials: Every 30 days (auto-rotated)
- API keys: Every 30 days (auto-rotated)
- JWT secrets: Every 90 days (auto-rotated)
- Service account credentials: Every 30 days (auto-rotated)

### Gmail App Password Rotation

1. **Generate new password** (see [Gmail Setup](#gmail-app-password))
2. **Update local file**:
   ```bash
   # Edit infra/env/alertmanager.env
   GMAIL_APP_PASSWORD=new-password-here
   ```
3. **Restart Alertmanager**:
   ```bash
   docker compose restart alertmanager
   ```
4. **Verify email delivery**:
   ```bash
   # Trigger test alert
   curl -X POST http://localhost:9093/api/v1/alerts -d '[{
     "labels": {"alertname": "TestAlert", "severity": "critical"},
     "annotations": {"summary": "Testing email after rotation"}
   }]'
   ```
5. **Revoke old password**:
   - Return to https://myaccount.google.com/apppasswords
   - Delete the old "MeepleAI Alertmanager" entry

### OpenRouter API Key Rotation

See `docs/SECURITY.md` for detailed OpenRouter rotation procedure.

## Troubleshooting

### "Secret detected" in pre-commit

1. **Review the flagged content**
2. **Remove the secret** from staged files
3. **Rotate the compromised secret** immediately
4. **If false positive**, update baseline:
   ```bash
   detect-secrets scan > .secrets.baseline
   git add .secrets.baseline
   ```

### Environment file not loading

1. **Check file exists**:
   ```bash
   ls -la infra/env/alertmanager.env
   ```
2. **Verify gitignore**:
   ```bash
   git check-ignore -v infra/env/alertmanager.env
   ```
3. **Check Docker Compose**:
   ```bash
   docker compose config | grep -A 5 "env_file"
   ```
4. **Restart services**:
   ```bash
   docker compose restart
   ```

### Email notifications failing

1. **Verify App Password is correct** (no spaces):
   ```bash
   # Check length (should be 16 characters)
   grep GMAIL_APP_PASSWORD infra/env/alertmanager.env | wc -c
   ```
2. **Check 2-Step Verification** is enabled on Gmail
3. **Review Alertmanager logs**:
   ```bash
   docker compose logs alertmanager | grep -i error
   ```
4. **Test SMTP connectivity**:
   ```bash
   docker compose exec alertmanager wget --spider smtp.gmail.com:587
   ```

## Incident Response

### If a secret is committed

**Immediate actions** (within 15 minutes):
1. **DO NOT** push the commit if still local
2. **Rotate the exposed secret** immediately
3. **Remove the secret** from the commit:
   ```bash
   # If not pushed
   git reset HEAD~1
   # Edit file to remove secret
   git add .
   git commit -m "fix: remove exposed secret"

   # If pushed
   git revert <commit-hash>
   git push
   ```

**Follow-up actions** (within 1 hour):
4. **Scan commit history**:
   ```bash
   git log --all --full-history --source --oneline -- '*secret*' '*password*' '*.env'
   ```
5. **Notify team** via secure channel (Slack DM, email)
6. **Document incident** in security log
7. **Review and improve** pre-commit hooks

**Post-incident** (within 24 hours):
8. **Root cause analysis**: Why did hooks fail?
9. **Update documentation** with lessons learned
10. **Strengthen prevention**: Add patterns to `.secrets.baseline`

### If a service is compromised

1. **Rotate all secrets** for that service
2. **Review access logs** for unauthorized activity
3. **Update all environments** (dev, staging, prod)
4. **Notify stakeholders** per incident response plan
5. **Document timeline** and remediation steps

## References

- **Project Security Policy**: `docs/SECURITY.md`
- **Alertmanager Setup**: `infra/OPS-05-SETUP.md`
- **Pre-commit Framework**: https://pre-commit.com/
- **GitHub Secret Scanning**: https://docs.github.com/en/code-security/secret-scanning
- **OWASP Secrets Cheat Sheet**: https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html
- **Gmail App Passwords**: https://support.google.com/accounts/answer/185833

### Phase 2 (Infisical) Resources

- **Infisical Documentation**: https://infisical.com/docs
- **Docker Compose Setup**: https://infisical.com/docs/self-hosting/deployment-options/docker-compose
- **Secret Rotation Guide**: https://infisical.com/docs/documentation/platform/secret-rotation/overview
- **GitHub Repository**: https://github.com/Infisical/infisical
- **Issue #708**: Secrets management strategy (parent issue)
- **Issue #936**: POC Infisical rotation (spike task)

## Checklist

### New Developer Onboarding
- [ ] Copy all `.env.example` templates to local `.env.*` files
- [ ] Fill in secrets from secure source (never production keys!)
- [ ] Install pre-commit hooks: `pre-commit install`
- [ ] Verify gitignore: `git check-ignore infra/env/*.env`
- [ ] Test services: `docker compose up -d && docker compose ps`

### Before Every Commit
- [ ] Pre-commit hooks pass without warnings
- [ ] No secrets in `git diff --cached`
- [ ] No hardcoded credentials in code
- [ ] No sensitive data in test fixtures

### Quarterly Security Review
- [ ] Rotate OpenRouter API key
- [ ] Rotate Gmail App Password
- [ ] Review access to repository secrets
- [ ] Audit team member access (remove former members)
- [ ] Update `.secrets.baseline` for new patterns
- [ ] Review this document for accuracy

---

**Last Updated**: 2024-11-04
**Related Issues**: #700 (Gmail password hardcoded)
**Version**: 1.0
