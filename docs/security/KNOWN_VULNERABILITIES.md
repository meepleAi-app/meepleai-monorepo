# Known Vulnerabilities - Accepted Risks

This document tracks security vulnerabilities that have been reviewed and accepted as low-risk for this project.

**Last Updated**: 2026-02-14

---

## Accepted Vulnerabilities

### 1. Elliptic Cryptographic Primitive (LOW)

**Status**: ✅ ACCEPTED - Dev dependency only, no production impact

| Property | Value |
|----------|-------|
| **Package** | `elliptic@6.6.1` |
| **Severity** | LOW |
| **Advisory** | [GHSA-848j-6mx2-7j84](https://github.com/advisories/GHSA-848j-6mx2-7j84) |
| **Patched Version** | None available (`<0.0.0`) |
| **Discovered** | 2026-02-14 |
| **Reviewed By** | Development Team |
| **Next Review** | 2026-05-14 (3 months) |

#### Description
The elliptic package uses a cryptographic primitive with a risky implementation. The package is deprecated and unmaintained, with no patch available.

#### Dependency Path
```
@storybook/nextjs
  └─ node-polyfill-webpack-plugin
      └─ crypto-browserify
          └─ browserify-sign
              └─ elliptic@6.6.1 (VULNERABLE)
```

#### Risk Assessment
**Risk Level**: MINIMAL

**Justification**:
1. **Dev-only dependency**: Only used by Storybook for development/testing
2. **Build-time only**: Not bundled or shipped to production
3. **Isolated environment**: Runs only in development environment
4. **No user exposure**: Not accessible to end users
5. **Low severity**: Requires specific attack conditions to exploit

#### Mitigation Strategy
- **Current**: Accept risk due to dev-only scope
- **Monitoring**: Check Storybook updates for dependency removal
- **Review Cycle**: Re-evaluate every 3 months
- **Fallback**: Remove Storybook if vulnerability escalates to MODERATE or higher

#### Alternative Considered
- **Remove Storybook**: Not viable - essential for component development
- **Webpack config**: No viable alternative to node-polyfill-webpack-plugin
- **Override elliptic**: Could break Storybook functionality

---

## Vulnerability Management Process

### Review Schedule
- **Critical/High**: Immediate action required
- **Moderate**: Fix within 7 days
- **Low**: Review and accept/fix within 30 days

### Accepted Risk Criteria
A vulnerability may be accepted if ALL of the following are true:
1. Severity is LOW
2. No patch available OR fix breaks critical functionality
3. Not in production dependencies
4. Risk is documented and approved
5. Monitoring plan is in place

### Review Process
1. **Discovery**: Vulnerability identified via `pnpm audit`
2. **Assessment**: Evaluate severity, impact, and fix availability
3. **Decision**: Fix immediately, schedule fix, or accept risk
4. **Documentation**: Update this file if accepting risk
5. **Monitoring**: Schedule quarterly reviews for accepted risks

---

## Security Audit History

### 2026-02-14: Comprehensive Security Audit
**Auditor**: Development Team
**Scope**: All frontend dependencies (production + dev)

**Findings**:
- Fixed 5 production vulnerabilities (1 HIGH, 3 MODERATE, 1 LOW)
- Fixed 4 dev dependency vulnerabilities (1 HIGH, 3 LOW)
- Accepted 1 dev dependency vulnerability (elliptic - LOW, no patch)

**Actions Taken**:
- Updated `pdfjs-dist` to 5.4.624 (HIGH severity fix)
- Updated `diff` to 8.0.3 (LOW severity fix)
- Added pnpm overrides for transitive dependencies:
  - `lodash@>=4.17.23`
  - `lodash-es@>=4.17.23`
  - `markdown-it@>=14.1.1`
  - `axios@>=1.13.5`
  - `webpack@>=5.104.1`
  - `qs@>=6.14.2`

**Result**: ✅ 0 production vulnerabilities, 1 accepted dev vulnerability

**Next Audit**: 2026-05-14

---

## Contact

For security concerns, contact:
- **Security Team**: [security@meepleai.com]
- **Development Lead**: [dev@meepleai.com]

For reporting new vulnerabilities, see [SECURITY.md](../SECURITY.md)
