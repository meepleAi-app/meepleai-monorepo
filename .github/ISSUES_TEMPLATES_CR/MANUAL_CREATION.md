# Manual GitHub Issue Creation Guide

GitHub CLI (`gh`) is not available in this environment. Follow these step-by-step instructions to create all 8 issues manually.

## 🔗 Quick Links

- **Issues Page**: https://github.com/DegrassiAaron/meepleai-monorepo/issues
- **New Issue**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/new

---

## 📋 Issue #1: SecurityHeadersMiddleware

### Title
```
🔐 [Security] Implement SecurityHeadersMiddleware
```

### Body
Copy the entire content from:
```
.github/ISSUES_TEMPLATES_CR/01-security-headers-middleware.md
```

Or use this link to read it:
```bash
cat .github/ISSUES_TEMPLATES_CR/01-security-headers-middleware.md
```

### Labels
```
priority: critical
type: security
area: backend
effort: small
sprint: 1
```

### Assignee
- Assign to yourself

---

## 📋 Issue #2: CORS Whitelist Headers

### Title
```
🔒 [Security] CORS Whitelist Headers (Remove AllowAnyHeader)
```

### Body
Copy from:
```
.github/ISSUES_TEMPLATES_CR/02-cors-whitelist-headers.md
```

### Labels
```
priority: critical
type: security
area: backend
effort: small
sprint: 1
```

---

## 📋 Issue #3: FluentValidation Authentication

### Title
```
✅ [Validation] FluentValidation for Authentication Context
```

### Body
Copy from:
```
.github/ISSUES_TEMPLATES_CR/03-fluentvalidation-authentication.md
```

### Labels
```
priority: high
type: enhancement
area: backend
effort: medium
sprint: 2
```

---

## 📋 Issue #4: NSwag Code Generation

### Title
```
🔧 [DX] NSwag TypeScript Code Generation from OpenAPI
```

### Body
Copy from:
```
.github/ISSUES_TEMPLATES_CR/04-nswag-code-generation.md
```

### Labels
```
priority: high
type: enhancement
area: frontend
area: backend
effort: large
sprint: 3
```

---

## 📋 Issue #5: Streaming Hooks Consolidation

### Title
```
🔄 [Refactor] Consolidate Streaming Hooks
```

### Body
Copy from:
```
.github/ISSUES_TEMPLATES_CR/05-streaming-hooks-consolidation.md
```

### Labels
```
priority: medium
type: refactor
area: frontend
effort: medium
sprint: 4
```

---

## 📋 Issue #6: Rate Limiting UX

### Title
```
⏱️ [UX] Rate Limiting User Experience with Retry-After
```

### Body
Copy from:
```
.github/ISSUES_TEMPLATES_CR/06-rate-limiting-ux.md
```

### Labels
```
priority: medium
type: enhancement
area: frontend
effort: small
ux
sprint: 4
```

---

## 📋 Issue #7: Retry Logic Exponential Backoff

### Title
```
🔄 [Resilience] Retry Logic with Exponential Backoff
```

### Body
Copy from:
```
.github/ISSUES_TEMPLATES_CR/07-retry-logic-exponential-backoff.md
```

### Labels
```
priority: medium
type: enhancement
area: frontend
effort: medium
resilience
sprint: 5
```

---

## 📋 Issue #8: Request Deduplication

### Title
```
🔀 [Performance] Request Deduplication Cache
```

### Body
Copy from:
```
.github/ISSUES_TEMPLATES_CR/08-request-deduplication.md
```

### Labels
```
priority: medium
type: enhancement
area: frontend
effort: medium
performance
sprint: 5
```

---

## 🏷️ Required Labels

Before creating issues, ensure these labels exist in your repository:

### Priority Labels
```bash
priority: critical     # Color: #d73a4a (red)
priority: high         # Color: #fbca04 (yellow)
priority: medium       # Color: #0e8a16 (green)
priority: low          # Color: #0075ca (blue)
```

### Type Labels
```bash
type: security         # Color: #d73a4a (red)
type: enhancement      # Color: #84b6eb (light blue)
type: refactor         # Color: #5319e7 (purple)
```

### Area Labels
```bash
area: backend          # Color: #1d76db (blue)
area: frontend         # Color: #0e8a16 (green)
```

### Effort Labels
```bash
effort: small          # Color: #c2e0c6 (light green)
effort: medium         # Color: #fef2c0 (light yellow)
effort: large          # Color: #f9d0c4 (light orange)
```

### Sprint Labels
```bash
sprint: 1              # Color: #fbca04
sprint: 2              # Color: #fbca04
sprint: 3              # Color: #fbca04
sprint: 4              # Color: #fbca04
sprint: 5              # Color: #fbca04
```

### Additional Labels
```bash
ux                     # Color: #e99695
resilience             # Color: #c5def5
performance            # Color: #d4c5f9
```

---

## 🚀 Creation Workflow

### For Each Issue:

1. **Go to**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/new

2. **Copy Title**: Use exact title from sections above

3. **Copy Body**:
   ```bash
   # Read template file
   cat .github/ISSUES_TEMPLATES_CR/01-security-headers-middleware.md

   # Select all content (Ctrl+A or Cmd+A)
   # Copy (Ctrl+C or Cmd+C)
   # Paste in GitHub issue body
   ```

4. **Add Labels**: Click on labels dropdown, select all required labels

5. **Assign**: Assign to yourself (optional)

6. **Create Issue**: Click "Submit new issue"

7. **Repeat**: For all 8 issues

---

## 📊 Progress Tracking

After creating all issues, you should have:

```
✅ Issue #N: SecurityHeadersMiddleware
✅ Issue #N+1: CORS Whitelist Headers
✅ Issue #N+2: FluentValidation Authentication
✅ Issue #N+3: NSwag Code Generation
✅ Issue #N+4: Streaming Hooks Consolidation
✅ Issue #N+5: Rate Limiting UX
✅ Issue #N+6: Retry Logic Exponential Backoff
✅ Issue #N+7: Request Deduplication
```

Total: 8 issues created

---

## 🔗 Alternative: Use GitHub API

If you have a GitHub token, you can use the API:

```bash
# Set your GitHub token
export GITHUB_TOKEN="your_token_here"

# Create issue #1
curl -X POST \
  -H "Authorization: token $GITHUB_TOKEN" \
  -H "Accept: application/vnd.github.v3+json" \
  https://api.github.com/repos/DegrassiAaron/meepleai-monorepo/issues \
  -d '{
    "title": "🔐 [Security] Implement SecurityHeadersMiddleware",
    "body": "..."  # Paste full template content here
    "labels": ["priority: critical", "type: security", "area: backend", "effort: small", "sprint: 1"]
  }'
```

Repeat for all 8 issues.

---

## 📝 Post-Creation Checklist

After creating all issues:

- [ ] All 8 issues created
- [ ] Correct labels applied
- [ ] Issues assigned (if applicable)
- [ ] Create GitHub Project (see `create-project.md`)
- [ ] Add issues to project
- [ ] Start Sprint 1 implementation

---

**Created**: 2025-01-19
**Total Issues**: 8
**Estimated Time**: 15-20 minutes for manual creation
