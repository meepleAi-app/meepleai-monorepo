# Code Scanning Issue Templates

This directory contains detailed GitHub issue templates for remediating **2,323 CodeQL security and code quality alerts**.

## 📋 Available Templates

| File | Priority | Count | Effort | Description |
|------|----------|-------|--------|-------------|
| `meta-tracker.md` | META | 2,323 | - | Master tracking issue |
| `p0-log-forging.md` | 🔴 P0 | 426 | 5 days | Log injection vulnerabilities |
| `p0-sensitive-info.md` | 🔴 P0 | 9 | 1 day | Sensitive data in logs |
| `p0-path-injection.md` | 🔴 P0 | 4 | 1 day | Path traversal vulnerabilities |
| `p1-missing-dispose.md` | 🟠 P1 | 601 | 10 days | Resource leaks (IDisposable) |
| `p2-null-reference.md` | 🟡 P2 | 56 | 5 days | Null reference exceptions |
| `p2-generic-catch.md` | 🟡 P2 | 220 | 8 days | Generic exception handling |
| `p3-code-smells.md` | ⚪ P3 | 382+ | 15 days | Code quality issues |

**Total**: 8 issues covering 1,692 open alerts

---

## 🚀 Quick Start

### Option 1: Automated (Recommended)

If you have `gh` CLI installed and authenticated:

```bash
# From repository root
cd /path/to/meepleai-monorepo

# Run the automation script
./create-code-scanning-issues.sh

# This creates all 8 issues automatically
```

### Option 2: Manual Creation

For each template file:

1. Open the file (e.g., `meta-tracker.md`)
2. Copy the entire content
3. Go to: https://github.com/DegrassiAaron/meepleai-monorepo/issues/new
4. Paste the content
5. The title and labels are in YAML frontmatter at the top
6. Create the issue

#### Manual Creation Steps

**Step 1**: Create the META tracker issue first
- File: `meta-tracker.md`
- This will be issue #[tracker-number]

**Step 2**: Create P0 (Critical) issues
- `p0-log-forging.md`
- `p0-sensitive-info.md`
- `p0-path-injection.md`

**Step 3**: Create P1 (High) issues
- `p1-missing-dispose.md`

**Step 4**: Create P2 (Medium) issues
- `p2-null-reference.md`
- `p2-generic-catch.md`

**Step 5**: Create P3 (Low) issues
- `p3-code-smells.md`

**Step 6**: Link child issues to META tracker
- Edit the META tracker issue
- Replace `#[issue-number]` placeholders with actual issue numbers

---

## 📝 Template Structure

Each template includes:

### 1. Frontmatter (YAML)
```yaml
---
title: "[CATEGORY] Issue Title"
labels: ["label1", "label2", "priority-level"]
---
```

### 2. Summary
- Alert count
- Severity level
- CWE references
- Risk assessment

### 3. Problem Description
- Vulnerable code examples
- Security implications
- Impact analysis

### 4. Solutions
- Secure code examples
- Best practices
- Implementation patterns

### 5. Remediation Plan
- Phased approach
- Task breakdown
- Effort estimates

### 6. Testing
- Unit test templates
- Integration test examples
- Verification steps

### 7. Prevention Strategy
- Roslyn analyzers configuration
- Pre-commit hooks
- CI/CD quality gates
- Code review checklists

### 8. Acceptance Criteria
- Completion checklist
- Quality metrics
- Verification steps

---

## 🔗 Cross-References

After creating issues, update these cross-references:

### In META Tracker (`meta-tracker.md`)
Replace:
- `#[log-forging]` → Actual issue number
- `#[sensitive-info]` → Actual issue number
- `#[path-injection]` → Actual issue number
- `#[missing-dispose]` → Actual issue number
- `#[null-reference]` → Actual issue number
- `#[generic-catch]` → Actual issue number
- `#[code-smells]` → Actual issue number

### In Child Issues
Replace:
- `#[code-scanning-tracker]` → META tracker issue number

---

## 📊 Priority Guide

### 🔴 P0 - CRITICAL (1 week)
**Fix immediately**. Security vulnerabilities that could lead to:
- Data breaches
- Unauthorized access
- Log injection attacks
- Privilege escalation

### 🟠 P1 - HIGH (2 weeks)
**Fix soon**. Resource management issues causing:
- Memory leaks
- Connection pool exhaustion
- Service degradation
- Performance problems

### 🟡 P2 - MEDIUM (3 weeks)
**Fix when possible**. Code quality issues causing:
- Runtime crashes
- Hidden bugs
- Difficult debugging
- Maintenance overhead

### ⚪ P3 - LOW (4 weeks)
**Incremental cleanup**. Technical debt:
- Unused code
- Code duplication
- Naming issues
- Complexity

---

## 🛠️ GitHub CLI Commands

### Authenticate
```bash
gh auth login
```

### Create Issue from Template
```bash
# Create issue from file
gh issue create \
  --title "[SECURITY] Fix Log Forging Vulnerabilities" \
  --body-file p0-log-forging.md \
  --label security,priority-critical,P0,code-scanning,log-injection
```

### List Created Issues
```bash
# View all code scanning issues
gh issue list --label code-scanning

# View by priority
gh issue list --label priority-critical
gh issue list --label priority-high
gh issue list --label priority-medium
```

### Update Issue
```bash
# Add comment
gh issue comment <number> --body "Progress update..."

# Close issue
gh issue close <number> --comment "Fixed in PR #123"
```

---

## 📈 Progress Tracking

### Update META Tracker

After completing work on a child issue:

1. Update progress bars in META tracker
2. Check off completion criteria
3. Add weekly update comment
4. Update milestone status

### Weekly Updates

Post progress updates in META tracker:

```markdown
### Week of [Date]

**Completed**:
- ✅ Fixed 50 log forging instances in AuthService
- ✅ Added structured logging utility

**In Progress**:
- 🟡 Fixing log forging in GameService (30/50 done)

**Blockers**:
- Need clarification on logging format for audit logs

**Next Week**:
- Complete GameService log forging fixes
- Start RuleSpecService remediation
```

---

## 🔍 Verification

### After Creating Issues

Verify all issues were created:

```bash
# Should show 8 issues
gh issue list --label code-scanning --state open | wc -l

# Check each priority level
gh issue list --label priority-critical  # Should show 3 (P0)
gh issue list --label priority-high      # Should show 1 (P1)
gh issue list --label priority-medium    # Should show 2 (P2)
gh issue list --label priority-low       # Should show 1 (P3)
gh issue list --label meta               # Should show 1 (META)
```

### Verify Labels

Ensure each issue has correct labels:
- Category: `security` or `code-quality`
- Priority: `priority-critical`, `priority-high`, `priority-medium`, `priority-low`
- Level: `P0`, `P1`, `P2`, `P3`
- Type: `code-scanning`
- Specific: `log-injection`, `resource-leak`, `null-safety`, etc.

---

## 📚 Additional Resources

### Documentation
- [CODE_SCANNING_SUMMARY.md](../../../CODE_SCANNING_SUMMARY.md) - Executive summary
- [CODE_SCANNING_ISSUES.md](../../../CODE_SCANNING_ISSUES.md) - All templates in one file
- [create-code-scanning-issues.sh](../../../create-code-scanning-issues.sh) - Automation script

### External References
- [GitHub Code Scanning Docs](https://docs.github.com/en/code-security/code-scanning)
- [CodeQL Documentation](https://codeql.github.com/docs/)
- [CWE Database](https://cwe.mitre.org/)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)

---

## 💡 Tips

1. **Start with META tracker** - Creates context for all child issues
2. **Fix P0 first** - Security issues take absolute priority
3. **Batch similar fixes** - Fix all log forging in one service at once
4. **Test incrementally** - Don't wait until end to test
5. **Update progress weekly** - Keep META tracker current
6. **Link PRs to issues** - Use "Fixes #123" in PR description
7. **Review prevention measures** - Implement analyzers/hooks after fixing

---

## 🤝 Contributing

When working on these issues:

1. **Assign yourself** to the issue
2. **Update status** in META tracker
3. **Follow the remediation plan** in the issue template
4. **Add tests** for your fixes
5. **Update documentation** if needed
6. **Request code review** before merging
7. **Close issue** after verification

---

## ❓ FAQ

**Q: Do I need to create all 8 issues?**
A: Yes, to track progress properly. Start with META + P0 issues.

**Q: Can I modify the templates?**
A: Yes! Templates are starting points. Adjust as needed.

**Q: What if I find more issues?**
A: Create new issues and link them to the META tracker.

**Q: Should I fix all instances in one PR?**
A: No, break into smaller PRs per service or logical group.

**Q: How do I verify fixes?**
A: Each template includes testing sections and acceptance criteria.

---

**Last Updated**: 2025-01-03
**Status**: Ready for issue creation
**Total Alerts**: 2,323 (1,692 open, 631 fixed)
