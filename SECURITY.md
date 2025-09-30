# Security Policy

## Reporting Security Vulnerabilities

**Please do not report security vulnerabilities through public GitHub issues.**

If you discover a security vulnerability in MeepleAI, please report it by emailing the maintainers directly. Include:

- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if available)

We will respond within 48 hours and work with you to understand and address the issue.

## Secrets Management

### Environment Files

This repository uses environment files to manage configuration and secrets:

- **`.env.example`** / **`.env.ci.example`**: Template files checked into git (no real secrets)
- **`.env.dev`** / **`.env.local`**: Local development files (NOT checked into git)
- **`.env.prod`**: Production files (NOT checked into git)

### Setup for Development

1. Copy template files:
   ```bash
   # Run from repository root
   .\scripts\dev-up.ps1
   ```
   This automatically creates `.env.dev` files from `.env.*.example` templates.

2. Update secrets in your local `.env.dev` files:
   - `infra/env/api.env.dev` - Add your OpenRouter API key
   - `infra/env/n8n.env.dev` - Update database credentials if needed

3. **Never commit `.env.dev` files** - they are excluded by `.gitignore`

### Secrets in CI/CD

For CI/CD pipelines, use environment-specific templates:
- Use `.env.ci.example` files as templates
- Inject real secrets via CI/CD environment variables or secret management systems
- Never hardcode secrets in pipeline configuration files

### Pre-Commit Hooks

This repository uses pre-commit hooks to detect secrets before they are committed:

```bash
# Install pre-commit (requires Python 3.9+)
pip install -r requirements-dev.txt

# Install git hooks
pre-commit install

# Run manually on all files
pre-commit run --all-files
```

The hooks will:
- ✓ Detect common secret patterns (API keys, tokens, private keys)
- ✓ Block commits containing secrets
- ✓ Check for large files, merge conflicts, and trailing whitespace
- ✓ Validate YAML syntax

### What Gets Blocked

The pre-commit hooks will prevent commits containing:
- Private keys (RSA, SSH, PGP)
- AWS keys
- API tokens and secrets
- High-entropy strings (potential passwords/keys)
- Database connection strings with credentials
- Files larger than 1MB

### Rotating Secrets

#### OpenRouter API Keys

1. **Generate new key**: Visit [OpenRouter Keys](https://openrouter.ai/keys)
2. **Update locally**: Edit `infra/env/api.env.dev`
3. **Update CI/CD**: Update secret in your CI/CD platform
4. **Revoke old key**: Delete old key from OpenRouter dashboard

#### GitHub Personal Access Tokens (PAT)

1. **Generate new token**: GitHub Settings → Developer Settings → Personal Access Tokens
2. **Update CI/CD**: Update `GITHUB_TOKEN` in your CI/CD secrets
3. **Update local tools**: If used locally (e.g., `gh` CLI), update via `gh auth login`
4. **Revoke old token**: Delete from GitHub settings

#### Database Credentials

For local development:
1. Update password in `infra/env/api.env.dev` and `infra/env/n8n.env.dev`
2. Restart services: `.\scripts\dev-down.ps1 && .\scripts\dev-up.ps1`

For production:
1. Change password in database management system
2. Update credentials in secret management system (e.g., AWS Secrets Manager, Azure Key Vault)
3. Restart application services

### Best Practices

1. **Use Secret Management Systems** in production (AWS Secrets Manager, Azure Key Vault, HashiCorp Vault)
2. **Rotate secrets regularly** (at least every 90 days)
3. **Use least-privilege access** (minimal permissions for each key/token)
4. **Audit secret usage** (monitor API key usage, review access logs)
5. **Never log secrets** (sanitize logs before writing)
6. **Use different secrets** for dev/staging/production environments

## Authentication & Authorization

### Session Security

- **HTTP-only cookies**: Session tokens stored in secure, HTTP-only cookies
- **Session lifetime**: 7 days (configurable in `AuthService.cs`)
- **Secure flag**: Enabled in production (HTTPS only)
- **SameSite**: Strict mode to prevent CSRF attacks

### Password Security

- **Hashing**: PBKDF2 with SHA-256 (210,000 iterations)
- **Salt**: Unique 16-byte random salt per password
- **Minimum length**: 8 characters enforced
- **Storage**: Never stored in plain text

### Role-Based Access Control (RBAC)

Three roles supported:
- **Admin**: Full access to all endpoints including `/admin/*`
- **Editor**: Can ingest content (`/ingest/*`) and use QA agents
- **User**: Can only use QA agents (`/agents/qa`)

See `apps/api/src/Api/Program.cs` for endpoint-level authorization.

## Known Security Considerations

### Development Environment

- Default database credentials in `.env.dev.example` are weak (for convenience)
- Change these for any environment accessible outside localhost
- Use strong passwords in CI/CD and production

### CORS Configuration

- Development: Allows `http://localhost:3000` by default
- Production: Configure `ALLOW_ORIGIN` environment variable to your frontend domain
- Never use `*` (allow all origins) in production

### API Rate Limiting

Currently **not implemented**. Consider adding:
- Rate limiting middleware (e.g., AspNetCoreRateLimit)
- Request throttling per user/IP
- DDoS protection at infrastructure level

## Dependency Security

### Automated Scanning

Dependencies are scanned using:
- **GitHub Dependabot**: Automatic security updates for vulnerable dependencies
- **Pre-commit hooks**: Checks for known vulnerabilities before commit

### Manual Review

Run security audits periodically:

```bash
# .NET backend
cd apps/api
dotnet list package --vulnerable

# Node.js frontend
cd apps/web
npm audit
```

## Incident Response

If a security incident occurs:

1. **Contain**: Immediately revoke compromised credentials
2. **Assess**: Determine scope and impact of the breach
3. **Notify**: Inform affected users and stakeholders
4. **Remediate**: Fix vulnerabilities and rotate all potentially affected secrets
5. **Document**: Create incident report and lessons learned
6. **Improve**: Update security policies and practices

## Security Checklist for Production

Before deploying to production:

- [ ] All secrets rotated and stored in secret management system
- [ ] HTTPS enabled with valid SSL/TLS certificates
- [ ] CORS configured to specific allowed origins (no wildcards)
- [ ] Strong database passwords (16+ characters, random)
- [ ] API rate limiting configured
- [ ] Logging and monitoring enabled (without logging secrets)
- [ ] Security headers configured (CSP, HSTS, X-Frame-Options)
- [ ] Database backups automated and tested
- [ ] Incident response plan documented
- [ ] Security contact information published

## Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [OWASP API Security Top 10](https://owasp.org/www-project-api-security/)
- [ASP.NET Core Security Best Practices](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [Next.js Security](https://nextjs.org/docs/advanced-features/security-headers)

---

**Last Updated**: 2025-09-30