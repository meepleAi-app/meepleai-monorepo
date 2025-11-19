# 📋 Code Review Issue Templates

Issue templates generati dalla code review delle interazioni Backend-Frontend (2025-01-19).

## 🎯 Overview

Questi template contengono raccomandazioni per migliorare sicurezza, validazione, developer experience e performance dell'applicazione MeepleAI.

## 📑 Issue Templates

| # | Title | Priority | Complexity | Time | Sprint |
|---|-------|----------|------------|------|--------|
| 01 | SecurityHeadersMiddleware | 🔴 Critical | Low | 4-6h | 1 |
| 02 | CORS Whitelist Headers | 🔴 Critical | Low | 2-3h | 1 |
| 03 | FluentValidation Authentication | 🟡 High | Medium | 5-6h | 2 |
| 04 | NSwag Code Generation | 🟡 High | Medium | 8-10h | 3 |
| 05 | Streaming Hooks Consolidation | 🟢 Medium | Medium | 6-8h | 4 |
| 06 | Rate Limiting UX | 🟢 Medium | Low | 4-6h | 4 |
| 07 | Retry Logic Exponential Backoff | 🟢 Medium | Medium | 6-8h | 5 |
| 08 | Request Deduplication | 🟢 Medium | Medium | 4-6h | 5 |

**Total Estimated Time**: 39-52 hours (8-10 sprints part-time)

## 🚀 How to Create Issues

### Option 1: Manual Creation (GitHub UI)

1. Go to https://github.com/DegrassiAaron/meepleai-monorepo/issues/new
2. Copy content from each `.md` file
3. Paste into issue body
4. Add labels as specified at bottom of each template
5. Create issue

### Option 2: GitHub CLI (Recommended)

Use the provided script to create all issues at once:

```bash
# Make script executable
chmod +x .github/ISSUES_TEMPLATES_CR/create-issues.sh

# Create all issues
./.github/ISSUES_TEMPLATES_CR/create-issues.sh

# Or create individual issues
gh issue create --title "🔐 [Security] Implement SecurityHeadersMiddleware" \
  --body-file .github/ISSUES_TEMPLATES_CR/01-security-headers-middleware.md \
  --label "priority: critical,type: security,area: backend,effort: small"
```

### Option 3: Bulk Import Script

PowerShell script for Windows:

```powershell
.\\.github\ISSUES_TEMPLATES_CR\create-issues.ps1
```

## 📊 Implementation Timeline

```
Sprint 1 (Week 1-2):   🔴 Security (Issues #1, #2)
Sprint 2 (Week 3-4):   🟡 Validation (Issue #3)
Sprint 3 (Week 5-6):   🟡 Code Gen (Issue #4)
Sprint 4 (Week 7-8):   🟢 UX (Issues #5, #6)
Sprint 5 (Week 9-10):  🟢 Resilience (Issues #7, #8)
```

## 🔗 Dependencies

```
Issue #1 → No dependencies
Issue #2 → No dependencies
Issue #3 → No dependencies
Issue #4 → Requires Swagger/OpenAPI (✅ already configured)
Issue #5 → No dependencies
Issue #6 → No dependencies
Issue #7 → No dependencies
Issue #8 → No dependencies
```

**All issues can be worked on in parallel within their sprint.**

## 🏷️ Label Mapping

Ensure these labels exist in your repository:

### Priority
- `priority: critical` (🔴 red)
- `priority: high` (🟡 yellow)
- `priority: medium` (🟢 green)

### Type
- `type: security` (🔒 dark red)
- `type: enhancement` (✨ light blue)
- `type: refactor` (🔧 purple)

### Area
- `area: backend` (🎯 blue)
- `area: frontend` (🎨 green)

### Effort
- `effort: small` (1-3 days)
- `effort: medium` (3-7 days)
- `effort: large` (1-2 weeks)

### Sprint
- `sprint: 1`
- `sprint: 2`
- `sprint: 3`
- `sprint: 4`
- `sprint: 5`

### Create Labels Script

```bash
# Run this to create all necessary labels
./.github/ISSUES_TEMPLATES_CR/create-labels.sh
```

## 📝 Template Format

Each template includes:

- **🎯 Objective**: What to accomplish
- **📋 Context**: Background and source
- **✅ Task Checklist**: Implementation tasks
- **📁 Files to Create/Modify**: File changes needed
- **🔗 References**: External documentation
- **📊 Acceptance Criteria**: Definition of done
- **🏷️ Labels**: GitHub labels to apply

## 🤝 Contributing

When working on these issues:

1. **Create feature branch**: `git checkout -b feature/issue-XXX-short-description`
2. **Follow checklist**: Mark tasks as completed in issue
3. **Update documentation**: Keep docs in sync
4. **Write tests**: Maintain >= 90% coverage
5. **Create PR**: Link to issue with `Closes #XXX`

## 📚 Related Documentation

- [Code Review Report](../../docs/code-reviews/2025-01-19-backend-frontend-interactions.md)
- [Security Documentation](../../docs/06-security/)
- [Development Guide](../../docs/02-development/)
- [API Documentation](../../docs/03-api/)

## 🔄 Status Tracking

Track progress in GitHub Project:
https://github.com/DegrassiAaron/meepleai-monorepo/projects/new

Suggested columns:
- 📋 Backlog
- 🏗️ In Progress
- 👀 In Review
- ✅ Done

---

**Generated**: 2025-01-19
**Source**: Code Review - Backend-Frontend Interactions
**Reviewer**: Claude Code Assistant
**Total Issues**: 8
**Estimated Total Effort**: 39-52 hours
