# Security Analyzers Configuration

> **Issue**: #2181 - Abilitare e risolvere i warning di sicurezza e le regole CA3xxx/Sxxxx
> **Status**: Compliant - All security rules enabled as errors
> **Last Audit**: 2025-12-14

## Overview

MeepleAI API uses three security analyzer packages configured in `.editorconfig` with 90+ security rules set to `error` severity. This ensures build failures on any security violation.

## Analyzer Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.CodeAnalysis.NetAnalyzers | 10.0.100 | Official .NET security analyzers (CA rules) |
| SonarAnalyzer.CSharp | 10.16.1 | Comprehensive security and code quality (S rules) |
| Meziantou.Analyzer | 2.0.257 | Additional best practices and security checks (MA rules) |

## Security Rule Categories

### 1. Injection Vulnerabilities (CA3xxx)
| Rule | Description | Severity |
|------|-------------|----------|
| CA3001 | SQL injection | error |
| CA3002 | XSS vulnerabilities | error |
| CA3003 | File path injection | error |
| CA3004 | Information disclosure | error |
| CA3005 | LDAP injection | error |
| CA3006 | Process command injection | error |
| CA3007 | Open redirect | error |
| CA3008 | XPath injection | error |
| CA3009 | XML injection | error |
| CA3010 | XAML injection | error |
| CA3011 | DLL injection | error |
| CA3012 | Regex injection | error |

### 2. Cryptographic Security (CA53xx)
| Rule | Description | Severity |
|------|-------------|----------|
| CA5350 | Weak cryptographic algorithms | error |
| CA5351 | Broken cryptographic algorithms | error |
| CA5359 | Certificate validation disabled | error |
| CA5373 | Obsolete key derivation function | error |
| CA5379 | Weak key derivation function | error |
| CA5384 | DSA usage | error |
| CA5385 | Insufficient RSA key size | error |
| CA5387 | Weak key derivation iteration count | error |
| CA5390 | Hard-coded encryption key | error |

### 3. Deserialization Security (CA23xx, CA53xx)
| Rule | Description | Severity |
|------|-------------|----------|
| CA2300 | BinaryFormatter (insecure) | error |
| CA2326 | TypeNameHandling (JSON) | error |
| CA2327 | Insecure JsonSerializerSettings | error |
| CA5362 | Reference cycle in deserialization | error |
| CA5369 | XmlSerializer.Deserialize without XmlReader | error |

### 4. SonarSource Security Rules (Sxxxx)
| Rule | Description | Severity |
|------|-------------|----------|
| S2068 | Hard-coded credentials | error |
| S2245 | Insecure PRNG | error |
| S3330 | Cookies without HttpOnly | error |
| S3649 | SQL injection | error |
| S4426 | Weak cryptographic keys | error |
| S4790 | Weak hashing algorithms | error |
| S5122 | Permissive CORS | error |
| S5131 | Reflected XSS | error |
| S5144 | SSRF validation | error |
| S5145 | Log injection | error |
| S6377 | XXE attacks | error |

## Current Compliance Status

### Production Code (src/)
- **Violations**: 0
- **Suppressions**: 0

### Test Code (tests/)
| Rule | File | Justification |
|------|------|---------------|
| CA5394 | RagServicePerformanceTests.cs | Random.Shared for latency simulation (non-cryptographic) |
| CA5399 | FirstAccuracyBaselineTest.cs | HttpClient for localhost test (cert revocation not needed) |

## Configuration Files

### apps/api/.editorconfig
Contains all 90+ security rules configured as `error` for production code.
Test code has relaxed settings for specific scenarios.

### apps/api/src/Api/Api.csproj
```xml
<PropertyGroup>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisLevel>latest</AnalysisLevel>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
</PropertyGroup>
```

## Adding New Suppressions

When suppressing a security rule, follow this process:

1. **Document the justification** in the pragma comment
2. **Add to warnings_tracking.csv** with full details
3. **Consider alternatives** before suppressing
4. **Test coverage** - ensure mitigations are tested

Example:
```csharp
#pragma warning disable CA5394 // Justification: Random.Shared used for test latency simulation only, not cryptographic purposes
var delay = Random.Shared.Next(10, 50);
#pragma warning restore CA5394
```

## CI/CD Integration

Security analyzers are enforced in CI through:
1. `dotnet build` with `EnforceCodeStyleInBuild=true`
2. Any security rule violation fails the build
3. CodeQL provides additional security scanning

## Monitoring and Auditing

- **warnings_tracking.csv**: Complete inventory of rules and suppressions
- **Quarterly audits**: Review suppressions and update documentation
- **Dependabot**: Weekly updates for analyzer packages

## References

- [Microsoft Security Rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/)
- [SonarSource Rules](https://rules.sonarsource.com/csharp)
- [Meziantou Analyzer](https://github.com/meziantou/Meziantou.Analyzer/tree/main/docs)
