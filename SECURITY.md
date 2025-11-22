# Security Policy

## Reporting a Vulnerability

**We take security seriously.** If you discover a security vulnerability in MeepleAI, please report it responsibly.

### How to Report

**DO**:
- Report via GitHub Security Advisories: [Create Security Advisory](https://github.com/DegrassiAaron/meepleai-monorepo/security/advisories/new)
- Include a clear description of the vulnerability
- Provide steps to reproduce the issue
- Describe the potential impact
- Suggest a fix if you have one

**DO NOT**:
- Open a public GitHub issue
- Disclose the vulnerability publicly before it's been addressed
- Exploit the vulnerability beyond what's necessary to demonstrate it

### What Happens Next

1. **Acknowledgment**: We'll acknowledge receipt within 48 hours
2. **Assessment**: We'll assess the severity and impact
3. **Fix**: We'll work on a fix and coordinate disclosure timing with you
4. **Credit**: We'll credit you in the security advisory (unless you prefer to remain anonymous)
5. **Disclosure**: After the fix is deployed, we'll publicly disclose the issue

### Response Time

- **Critical vulnerabilities**: Fix within 7 days
- **High severity**: Fix within 30 days
- **Medium/Low severity**: Fix in next release cycle

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| main    | ✅ Yes (latest)    |
| < 1.0   | ❌ No              |

We recommend always using the latest version from the `main` branch.

## Security Best Practices for Contributors

If you're contributing to MeepleAI, please follow these security practices:

- **Never commit secrets** (API keys, passwords, tokens)
- **Use `.env` files** for local secrets (never commit `.env.dev` or `.env.local`)
- **Install pre-commit hooks**: `pre-commit install` (detects secrets before commit)
- **Rotate keys regularly**: See [Key Rotation Procedures](./docs/SECURITY.md#api-key-rotation)
- **Review dependencies**: Check for known vulnerabilities with `dotnet list package --vulnerable` and `pnpm audit`

### Pre-commit Secret Detection

We use `pre-commit` hooks to prevent accidental secret commits:

```bash
# Install pre-commit
pip install pre-commit

# Install hooks
pre-commit install

# Run manually
pre-commit run --all-files
```

## Detailed Security Documentation

For comprehensive security information, including:

- Secrets management guidelines
- API key rotation procedures
- GitHub PAT rotation
- Pre-commit hook setup
- Security checklist for developers
- Quarterly security maintenance

**See**: [docs/SECURITY.md](./docs/SECURITY.md)

## Security Features in MeepleAI

### Authentication & Authorization

- **Session-based authentication** with secure cookies
- **API key authentication** with PBKDF2 hashing (210k iterations)
- **Role-based access control** (Admin, Editor, User)
- **Session management** with automatic revocation of inactive sessions (30 days)
- **Password hashing** with PBKDF2-SHA256

### Infrastructure Security

- **HTTPS-only** in production
- **CORS protection** with allowlist origins
- **CSRF protection** via SameSite cookies
- **SQL injection prevention** via parameterized queries (EF Core)
- **Rate limiting** on API endpoints
- **Secrets management** via environment variables

### Dependency Security

- **Automated security scanning** via GitHub CodeQL
- **Dependabot** for automatic dependency updates
- **Vulnerability scanning** in CI/CD pipeline
- **.NET Security Analyzers** enabled (SecurityCodeScan, NetAnalyzers)

### Monitoring & Auditing

- **Audit logging** for sensitive operations
- **Structured logging** with Serilog + Seq
- **Distributed tracing** with OpenTelemetry
- **Health checks** for all critical services
- **Error monitoring** with alerts

## Security Scanning in CI

Our CI pipeline includes:

1. **CodeQL SAST**: Static analysis for C# and TypeScript
2. **Dependency Scanning**: `dotnet list package --vulnerable` and `pnpm audit`
3. **.NET Security Analyzers**: SecurityCodeScan, NetAnalyzers

Reports are available in [GitHub Security tab](https://github.com/DegrassiAaron/meepleai-monorepo/security).

## Third-Party Security Policies

We rely on the following third-party services:

- **OpenRouter**: [Security Policy](https://openrouter.ai/security)
- **PostgreSQL**: [Security Documentation](https://www.postgresql.org/support/security/)
- **Redis**: [Security Documentation](https://redis.io/docs/management/security/)
- **Qdrant**: [Security Best Practices](https://qdrant.tech/documentation/guides/security/)

## Compliance & Standards

- **OWASP Top 10**: We follow OWASP security best practices
- **CWE Top 25**: Automated scanning for common weakness enumerations
- **Secrets Management**: Following [OWASP Secrets Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)

## Security Disclosure History

No security vulnerabilities have been publicly disclosed yet. We'll maintain a list here once any are resolved.

---

## Additional Resources

- [CONTRIBUTING.md](./CONTRIBUTING.md) - Contribution guidelines
- [docs/SECURITY.md](./docs/SECURITY.md) - Detailed security procedures
- [docs/observability.md](./docs/observability.md) - Monitoring and logging
- [GitHub Security Advisories](https://github.com/DegrassiAaron/meepleai-monorepo/security/advisories)

---

**Last Updated**: 2025-01-16
**Version**: 1.0

Thank you for helping keep MeepleAI secure! 🔒
