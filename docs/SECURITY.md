# Security Policy

This document outlines security practices, secret management, and key rotation procedures for the MeepleAI project.

## Table of Contents

- [Secrets Management](#secrets-management)
- [Pre-commit Hooks](#pre-commit-hooks)
- [API Key Rotation](#api-key-rotation)
- [GitHub Personal Access Token (PAT) Rotation](#github-personal-access-token-pat-rotation)
- [Security Checklist](#security-checklist)
- [Reporting Security Issues](#reporting-security-issues)

## Secrets Management

### Environment Files

The project uses separate environment files for different contexts:

- `*.env.dev.example` - Template files (committed to git)
- `*.env.ci.example` - CI/CD template files (committed to git)
- `*.env.dev` - Local development files with real secrets (**NOT committed**)
- `*.env.local` - Local overrides (**NOT committed**)
- `*.env.prod` - Production secrets (**NOT committed**)

### .gitignore Protection

The `.gitignore` file explicitly excludes secret-containing files:

```gitignore
.env
.env.*
*.env
*.env.*
!*.env.example
!*.env.ci.example
*.env.dev
*.env.local
*.env.prod
*.env.production

# Service-specific secrets (SEC-700)
infra/env/n8n-service-session.env
infra/env/alertmanager.env
```

### Never Commit Secrets

❌ **Never commit**:
- API keys (OpenRouter, third-party services)
- Database passwords
- Gmail App Passwords (Alertmanager)
- Slack webhook URLs
- GitHub Personal Access Tokens
- Session secrets
- Private keys or certificates

✅ **Always use**:
- Environment variables
- GitHub Secrets (for CI/CD)
- Secret management services (for production)

## Pre-commit Hooks

The project uses `pre-commit` framework with secret detection to prevent accidental commits of sensitive data.

### Installation

```bash
# Install pre-commit framework
pip install pre-commit

# Install git hooks
pre-commit install

# Run manually on all files
pre-commit run --all-files
```

### Configured Hooks

1. **detect-secrets** - Yelp's secret scanner
   - Scans for high-entropy strings
   - Detects common secret patterns
   - Uses baseline file (`.secrets.baseline`)

2. **detect-private-key** - Detects private SSH/PGP keys

3. **check-added-large-files** - Prevents large file commits (max 1MB)

### If a Hook Fails

If the pre-commit hook detects a potential secret:

1. **Review the flagged content**
2. **Remove the secret** from the commit
3. **Rotate the compromised secret** immediately (see rotation procedures below)
4. **Update `.secrets.baseline`** if it's a false positive:
   ```bash
   detect-secrets scan > .secrets.baseline
   ```

## API Key Rotation

### OpenRouter API Key

OpenRouter is used for LLM and embedding services.

#### Rotation Schedule

- **Recommended**: Every 90 days
- **Required**: Immediately if compromised

#### Rotation Procedure

1. **Generate new key**:
   - Log in to [OpenRouter dashboard](https://openrouter.ai/)
   - Navigate to API Keys section
   - Create new API key
   - Copy the new key (shown only once)

2. **Update local development** (`infra/env/api.env.dev`):
   ```bash
   OPENROUTER_API_KEY=sk-or-v1-new-key-here
   ```

3. **Update CI/CD secrets**:
   - Go to repository Settings → Secrets and variables → Actions
   - Update `OPENROUTER_API_KEY` secret
   - Or use GitHub CLI:
     ```bash
     gh secret set OPENROUTER_API_KEY --body "sk-or-v1-new-key-here"
     ```

4. **Update production environment**:
   - Update secret in deployment platform (e.g., Vercel, Railway, AWS Secrets Manager)
   - Restart services to pick up new key

5. **Revoke old key**:
   - Return to OpenRouter dashboard
   - Delete/revoke the old API key
   - Verify new key works before revoking

6. **Verify**:
   ```bash
   # Test locally
   cd apps/api
   dotnet test --filter "FullyQualifiedName~LlmServiceTests"

   # Test in CI - trigger workflow or push commit
   git commit --allow-empty -m "test: verify OpenRouter key rotation"
   git push
   ```

### Gmail App Password (Alertmanager)

Used for email notifications from Alertmanager.

#### Rotation Schedule

- **Recommended**: Annually or when prompted by Gmail
- **Required**: Immediately if compromised

#### Rotation Procedure

1. **Generate new App Password**:
   - Go to https://myaccount.google.com/security
   - Ensure 2-Step Verification is enabled
   - Visit https://myaccount.google.com/apppasswords
   - Create new App Password for "Mail" → "MeepleAI Alertmanager"
   - Copy 16-character password (format: xxxx xxxx xxxx xxxx)

2. **Update local development** (`infra/env/alertmanager.env`):
   ```bash
   GMAIL_APP_PASSWORD=abcdabcdabcdabcd  # Remove spaces
   ```

3. **Restart Alertmanager**:
   ```bash
   cd infra
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

5. **Revoke old App Password**:
   - Return to https://myaccount.google.com/apppasswords
   - Delete the old "MeepleAI Alertmanager" entry

6. **Update production environment** (if applicable):
   - Update secret in deployment platform
   - Restart Alertmanager service

See `docs/guide/secrets-management.md` for detailed Gmail App Password setup.

### Other API Keys

Follow similar procedure for any third-party API keys:
- Qdrant cloud keys
- Database connection strings with embedded passwords
- Third-party service tokens
- Slack webhook URLs

## GitHub Personal Access Token (PAT) Rotation

PATs are used for:
- CI/CD workflows (`GITHUB_TOKEN` is auto-generated, no rotation needed)
- Custom automation scripts
- GitHub API access

#### Rotation Schedule

- **Recommended**: Every 90 days
- **Required**: Immediately if compromised or when a team member with access leaves

#### Rotation Procedure

1. **Generate new PAT**:
   - Go to GitHub Settings → Developer settings → Personal access tokens → Fine-grained tokens
   - Click "Generate new token"
   - Set appropriate scopes (minimal required permissions)
   - Set expiration (max 90 days recommended)
   - Generate and copy token

2. **Update repository secrets**:
   ```bash
   # If PAT is stored as GitHub Secret
   gh secret set CUSTOM_PAT --body "ghp_new-token-here"
   ```

3. **Update local environment** (if used locally):
   ```bash
   # Add to ~/.bashrc or ~/.zshrc
   export GITHUB_PAT=ghp_new-token-here
   ```

4. **Update automation scripts**:
   - Check for hardcoded tokens in scripts (should use env vars)
   - Verify scripts use `$GITHUB_PAT` or similar

5. **Revoke old PAT**:
   - Return to GitHub PAT settings
   - Delete old token
   - Verify automation still works

6. **Verify**:
   ```bash
   # Test GitHub API access
   gh api user

   # Test CI workflows
   git push  # Trigger workflow
   ```

## Security Checklist

### For New Developers

- [ ] Install pre-commit hooks: `pre-commit install`
- [ ] Copy `.env.example` files to `.env.dev`
- [ ] Request API keys from team lead (never use production keys locally)
- [ ] Review this SECURITY.md document
- [ ] Never commit `.env.dev` or similar files

### Before Every Commit

- [ ] Pre-commit hooks pass without warnings
- [ ] No secrets or API keys in code
- [ ] No hardcoded connection strings
- [ ] No sensitive data in test fixtures

### Quarterly (Every 90 Days)

- [ ] Rotate OpenRouter API key
- [ ] Rotate GitHub PAT (if applicable)
- [ ] Review access to repository secrets
- [ ] Audit team member access (remove former team members)
- [ ] Review `.secrets.baseline` for false positives

### Annually

- [ ] Rotate Gmail App Password (Alertmanager)
- [ ] Review all service-specific secrets
- [ ] Update secrets management documentation

### After a Suspected Breach

- [ ] Immediately rotate all potentially compromised secrets
- [ ] Review recent commits for exposed secrets
- [ ] Check CI/CD logs for secret leakage
- [ ] Notify team lead/security contact
- [ ] Update passwords for affected services
- [ ] Review access logs for unauthorized access

## Reporting Security Issues

If you discover a security vulnerability:

1. **Do NOT** open a public issue
2. **Do NOT** commit or push the vulnerability
3. **DO** email the repository maintainer directly
4. **DO** provide details:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if known)

## Additional Resources

- **Internal Documentation**:
  - [Secrets Management Guide](guide/secrets-management.md) - Comprehensive guide with examples
  - [Alertmanager Setup](../infra/OPS-05-SETUP.md) - Email notification configuration
- **External Resources**:
  - [GitHub Secret Scanning](https://docs.github.com/en/code-security/secret-scanning/about-secret-scanning)
  - [OpenRouter API Documentation](https://openrouter.ai/docs)
  - [Gmail App Passwords](https://support.google.com/accounts/answer/185833)
  - [pre-commit Documentation](https://pre-commit.com/)
  - [OWASP Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)

---

**Last Updated**: 2024-11-04 (SEC-700: Added Gmail App Password rotation)
**Version**: 1.1
