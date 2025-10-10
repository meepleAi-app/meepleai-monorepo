# Security Scanning

This document describes the security scanning tools and processes implemented for the MeepleAI monorepo.

## Overview

The project implements a multi-layered security scanning approach to identify vulnerabilities early in the development lifecycle:

1. **SAST (Static Application Security Testing)** - CodeQL for code analysis
2. **Dependency Scanning** - Automated vulnerability detection in dependencies
3. **Security Analyzers** - .NET-specific security code analysis
4. **Automated Updates** - Dependabot for dependency patches

## Implementation (Issue #307 - SEC-03)

**Priority**: P0
**Milestone**: MVP
**Acceptance Criteria**: Pipeline fails on HIGH severity vulnerabilities; reports available as CI artifacts

## Security Workflows

### 1. CodeQL Analysis (SAST)

**Workflow**: `.github/workflows/security-scan.yml`
**Languages**: C# (.NET 8.0), JavaScript/TypeScript
**Trigger**: Push to main, PRs, weekly schedule (Mondays), manual dispatch

**Features**:
- Extended security queries (`security-extended`, `security-and-quality`)
- Separate analysis for C# and JavaScript
- Results uploaded to GitHub Security tab
- Automatic vulnerability detection in code patterns

**Configuration**:
```yaml
uses: github/codeql-action/init@v3
with:
  languages: csharp | javascript
  queries: security-extended,security-and-quality
```

### 2. Dependency Vulnerability Scanning

**Backend (.NET)**:
```bash
dotnet list package --vulnerable --include-transitive
```

**Frontend (pnpm)**:
```bash
pnpm audit --audit-level=moderate
```

**Severity Thresholds**:
- ✅ **Low/Moderate**: Warning (does not fail pipeline)
- ❌ **High/Critical**: Pipeline fails immediately

**Reports**:
- `.NET`: `dotnet-vulnerability-report` artifact (30-day retention)
- `Frontend`: `frontend-vulnerability-report` artifact (30-day retention)

### 3. .NET Security Code Scan

**Analyzer**: SecurityCodeScan.VS2019 v5.6.7

Added to `apps/api/src/Api/Api.csproj`:
```xml
<PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
<PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

**Detects**:
- SQL injection vulnerabilities
- XSS (Cross-Site Scripting) risks
- Insecure deserialization
- Weak cryptography usage
- Path traversal vulnerabilities
- LDAP injection
- Cookie security issues
- And more...

### 4. Dependabot Configuration

**File**: `.github/dependabot.yml`

**Update Schedule**: Weekly on Mondays at 00:00 UTC

**Monitored Ecosystems**:
- **NuGet** (.NET dependencies in `/apps/api`)
- **npm** (Frontend dependencies in `/apps/web`)
- **GitHub Actions** (Workflow actions)
- **Docker** (Container images in `/infra`)

**Features**:
- Auto-grouped minor/patch updates
- Security-labeled PRs
- Reviewer assignment (@DegrassiAaron)
- Major version updates ignored for React/Next.js (manual review required)

## Running Security Scans Locally

### .NET Security Analysis

```bash
cd apps/api

# Restore with security analyzers
dotnet restore

# Build with security analysis (warnings visible)
dotnet build --configuration Release

# Check for vulnerable packages
dotnet list package --vulnerable --include-transitive
```

### Frontend Security Analysis

```bash
cd apps/web

# Install dependencies
pnpm install

# Run security audit
pnpm audit

# Check only high/critical severity
pnpm audit --audit-level=high

# Generate JSON report
pnpm audit --json > audit-report.json
```

### CodeQL Local Analysis

CodeQL is primarily designed for GitHub Actions, but you can run it locally:

```bash
# Install CodeQL CLI
# https://github.com/github/codeql-cli-binaries/releases

# Create database
codeql database create csharp-db --language=csharp --source-root=apps/api

# Run analysis
codeql database analyze csharp-db \
  --format=sarif-latest \
  --output=results.sarif \
  codeql/csharp-queries:security-extended.qls
```

## CI/CD Integration

### Automatic Triggers

1. **Pull Requests**: Security scan runs on every PR
2. **Push to Main**: Full scan on merge
3. **Weekly Schedule**: Complete scan every Monday
4. **Manual**: Workflow dispatch available

### Pipeline Failure Conditions

The pipeline **FAILS** if:
- HIGH or CRITICAL vulnerabilities found in .NET dependencies
- HIGH or CRITICAL vulnerabilities found in frontend dependencies
- CodeQL detects critical security issues

The pipeline **WARNS** if:
- Low/Moderate vulnerabilities detected
- Security code scan finds potential issues

### Security Reports

All security reports are stored as GitHub Actions artifacts:

| Report | Artifact Name | Retention |
|--------|--------------|-----------|
| .NET Vulnerabilities | `dotnet-vulnerability-report` | 30 days |
| Frontend Vulnerabilities | `frontend-vulnerability-report` | 30 days |
| .NET Security Scan | `dotnet-security-scan-report` | 30 days |
| CodeQL Results | GitHub Security tab | Indefinite |

**Accessing Reports**:
1. Go to GitHub Actions workflow run
2. Scroll to "Artifacts" section
3. Download relevant report
4. For CodeQL: Check "Security" tab in repository

## Security Best Practices

### For Developers

1. **Run local scans before pushing**:
   ```bash
   dotnet list package --vulnerable
   pnpm audit
   ```

2. **Review Dependabot PRs promptly**:
   - Security updates should be prioritized
   - Review changelogs for breaking changes
   - Test locally before merging

3. **Address security warnings**:
   - SecurityCodeScan warnings should be investigated
   - Use `#pragma warning disable` only with justification
   - Document any suppressed warnings

4. **Keep dependencies updated**:
   - Regularly merge Dependabot PRs
   - Monitor security advisories
   - Update major versions during sprint planning

### For Reviewers

1. **Check security scan results** before approving PRs
2. **Verify vulnerability fixes** in Dependabot PRs
3. **Question any disabled security warnings**
4. **Ensure new dependencies** are from trusted sources

## Troubleshooting

### "High severity vulnerability" pipeline failure

1. Review the artifact report to identify affected package
2. Check for available patch version
3. Update dependency:
   ```bash
   # .NET
   dotnet add package <PackageName> --version <SafeVersion>

   # Frontend
   pnpm update <package-name>
   ```
4. If no patch available:
   - Document the risk
   - Consider alternative package
   - Add to security exceptions (with approval)

### CodeQL false positives

1. Review the security alert in GitHub Security tab
2. If confirmed false positive:
   - Add CodeQL suppression comment
   - Document reason in code
   - Dismiss alert in Security tab with justification

### Dependabot PR conflicts

1. Update your branch with latest main:
   ```bash
   git pull origin main
   ```
2. Let Dependabot auto-rebase
3. If conflicts persist, close PR and manually update

### Security scan timeout

1. Check if Qdrant/services are running
2. Verify network connectivity in CI
3. Increase timeout in workflow if needed:
   ```yaml
   timeout-minutes: 30
   ```

## Security Alert Response Process

### Critical/High Severity

1. **Immediate Action** (within 24 hours):
   - Assess impact on production
   - Check if vulnerability is exploitable in our context
   - Create hotfix branch if needed

2. **Remediation**:
   - Update affected dependency
   - Run full test suite
   - Deploy patch ASAP

3. **Communication**:
   - Notify team in Slack/Discord
   - Update issue tracker
   - Document in decision log

### Medium/Low Severity

1. **Schedule Fix** (within sprint):
   - Add to backlog
   - Prioritize in next sprint planning
   - Group with related updates

2. **Monitor**:
   - Watch for severity escalation
   - Check for exploit proof-of-concepts

## Compliance & Reporting

### Security Metrics

Track these metrics in sprint retrospectives:
- Number of vulnerabilities detected
- Time to remediation (TTR)
- Dependabot PR merge rate
- Security scan failure rate

### Audit Trail

Security scan results provide audit trail for:
- Dependency security posture
- Code security compliance
- Vulnerability response time
- Security tool effectiveness

## Related Documentation

- [Code Coverage](./code-coverage.md) - Test coverage measurement
- [CI/CD Workflows](../.github/workflows/ci.yml) - Main CI pipeline
- [Project README](../README.md) - Development setup

## Security Tool Versions

| Tool | Version | Purpose |
|------|---------|---------|
| CodeQL | Latest (GitHub-managed) | SAST for C#/JS |
| SecurityCodeScan.VS2019 | 5.6.7 | .NET security analyzer |
| Microsoft.CodeAnalysis.NetAnalyzers | 9.0.0 | .NET code quality |
| pnpm audit | Built-in (pnpm 9) | npm vulnerability scan |
| dotnet list package | Built-in (.NET 8) | NuGet vulnerability scan |
| Dependabot | GitHub-managed | Dependency updates |

## Support

For security concerns or questions:
- **Security Issues**: Create private security advisory in GitHub
- **Tool Questions**: Tag @DegrassiAaron in issues
- **False Positives**: Document in issue #307 comments
