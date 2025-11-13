# 📚 Knowledge Base - MeepleAI Production Patterns

**Purpose**: Curated collection of production-ready patterns, best practices, and architectural decisions
**Audience**: Development team (current + future)
**Status**: Living documentation (updated as patterns evolve)

---

## 📋 Knowledge Base Index

### 1. [React 19 + Next.js 16 Best Practices](react19-nextjs16-best-practices.md)
**What**: Migration patterns, new features (Actions, use() hook, async APIs), performance optimization
**When to Use**: All future frontend development
**Key Topics**:
- React 19 Compiler (automatic memoization)
- Next.js 16 async request APIs (cookies, headers, params)
- `useActionState` for form handling
- `use()` hook for promises/context
- Turbopack configuration
- Testing patterns for React 19

**Source**: Issue #823 (React 19 + Next.js 16 migration, Completed 2025-11-10)

---

### 2. [E2E Testing Patterns](e2e-testing-patterns.md)
**What**: Playwright testing patterns, fixtures, common failures, debugging techniques
**When to Use**: Writing new E2E tests, debugging failing tests
**Key Topics**:
- Fixture patterns (authentication, test data)
- API mocking with Playwright
- OAuth button testing (mock external flows)
- File upload testing
- SSE/streaming testing
- CI/CD integration
- Flaky test debugging (95% of issues: selectors + race conditions)

**Source**: Issue #795, #797 (228 E2E test fixes, Completed 2025-11-07)

---

### 3. [Security Patterns](security-patterns.md)
**What**: Path traversal prevention, PII masking, IDisposable best practices, input validation
**When to Use**: All features handling user input, file uploads, sensitive data
**Key Topics**:
- Path traversal defense (multi-layer: pattern detection, sanitization, validation)
- PII masking in logs (GDPR compliance: email, IP, credit cards)
- IDisposable enforcement (HttpContent, IServiceScope, IHttpClientFactory)
- Input validation (Email VO, password strength, SQL injection prevention)
- Rate limiting per role
- CSRF protection (SameSite cookies)
- XSS prevention (output encoding, input sanitization)
- Roslyn analyzers configuration (CA2000, CA3001-3012 enforced)

**Source**: Issue #798 (CODE-02), #814 (Integration test fixes, Completed 2025-11-07/09)

---

### 4. [Dependency Management](dependency-management.md)
**What**: Security audits, update strategies, breaking changes management, version locking
**When to Use**: Weekly audits, monthly updates, major version migrations
**Key Topics**:
- Weekly security audits (pnpm audit, dotnet list package --vulnerable)
- Update strategy (patch: auto, minor: review, major: plan)
- Breaking changes process (research → plan → implement → validate → document)
- Package.json best practices (caret ranges, no wildcards)
- Central Package Management (.NET Directory.Packages.props)
- Dependabot configuration (weekly PRs, auto-merge low-risk)
- Version range guide (^, ~, exact)

**Source**: Issue #815 (Dependency audit, Completed 2025-11-09)

---

### 5. [Codebase Maintenance](codebase-maintenance.md)
**What**: Cache cleanup, TODO/FIXME management, git hygiene, documentation lifecycle
**When to Use**: Monthly/quarterly maintenance, pre-release checklist
**Key Topics**:
- Monthly cache cleanup (tools/cleanup-caches.sh, saves ~800 MB)
- TODO/FIXME policy (link to issues OR remove)
- Git branch cleanup (delete merged, prune orphans)
- Commit message quality (Conventional Commits)
- Documentation hygiene (archive obsolete, extract To-Be to kb/)
- Code quality audits (SonarQube, ESLint, Roslyn)
- Dead code elimination (ts-prune, manual review)
- Test maintenance (coverage tracking, flaky test elimination)
- Performance monitoring (build time, test time)

**Source**: Issue #816 (Cleanup scripts), #813 (TODO review, Completed 2025-11-09)

---

## 🎯 How to Use This Knowledge Base

### For New Developers

1. **Read all KB files** (onboarding, ~2 hours)
2. **Reference during development** (quick lookup)
3. **Update when discovering new patterns** (contribute back)

### For Experienced Developers

1. **Reference for edge cases** (security, E2E flakiness, etc.)
2. **Review before major changes** (migrations, refactors)
3. **Update with lessons learned** (post-mortems, retrospectives)

### For Code Reviews

1. **Check PR against KB patterns**:
   - React 19 patterns followed?
   - Security checklist complete?
   - E2E tests use fixtures?
   - Dependencies properly managed?

2. **Suggest KB updates** (if new patterns discovered)

---

## 📝 Contributing to KB

### When to Add New KB File

**Add new file when**:
- Pattern used 3+ times across codebase
- Security-critical knowledge (always document)
- Complex solution to common problem
- Framework migration (major version upgrades)
- Architectural decision with broad impact

**Don't add**:
- One-off solutions (put in code comments instead)
- Obvious patterns (already in framework docs)
- Issue-specific details (belongs in issue tracker)

### KB File Template

```markdown
# [Topic] - [One-Line Description]

**Source**: [Issue numbers, dates completed]
**Framework/Tool**: [React, ASP.NET, etc.]
**Status**: Production-ready OR Experimental

---

## Pattern 1: [Name]

**What**: [Problem solved]
**When to Use**: [Scenarios]

**Implementation**:
```[language]
[Code example]
```

**Best Practices**:
- [Guideline 1]
- [Guideline 2]

---

## Checklist

- [ ] [Action 1]
- [ ] [Action 2]

---

**Knowledge extracted from**: [Source files]
**Status**: [Production-ready OR Draft OR Deprecated]
```

### Update Process

1. **Discover Pattern**: During development OR code review
2. **Document**: Create/update KB file
3. **Review**: Tech lead approval (ensure quality)
4. **Commit**: `docs(kb): Add [pattern-name] to knowledge base`
5. **Communicate**: Announce in team channel

---

## 🔄 KB Maintenance

### Quarterly Review (Every 3 Months)

**Process**:
1. **Review all KB files** (are they still relevant?)
2. **Update deprecated patterns** (mark as deprecated OR remove)
3. **Check for new patterns** (scan closed issues for lessons learned)
4. **Consolidate duplicates** (merge similar patterns)
5. **Update index** (this README)

**Owners**: Tech Lead + Senior Developers

---

### Deprecation Policy

**When Pattern Becomes Obsolete**:
1. **Mark as deprecated** (add warning at top)
   ```markdown
   # ⚠️ DEPRECATED - Use [new-pattern.md] instead

   **Deprecated**: 2025-11-11
   **Reason**: Framework update made this pattern obsolete
   **Migration**: See [new-pattern.md] for updated approach
   ```

2. **Keep for 6 months** (give team time to migrate)

3. **Move to archive** (after 6 months)
   ```bash
   mv kb/deprecated-pattern.md kb/archive/deprecated-pattern.md
   ```

4. **Delete** (after 1 year in archive)

---

## 📊 KB Metrics

**Current Status** (2025-11-11):
- **Total KB Files**: 5 (README + 4 pattern files)
- **Total Patterns**: ~30+ documented
- **Source Issues**: 10+ closed issues (knowledge extracted)
- **Coverage**: Frontend, Backend, Security, Testing, Maintenance

**Growth Target**: 8-12 KB files by end of 2025

---

## 🎯 KB Quality Standards

### Every KB File Should Have

- [ ] **Clear title** (describes pattern/topic)
- [ ] **Source attribution** (issue numbers, dates)
- [ ] **Production status** (ready, experimental, deprecated)
- [ ] **Code examples** (copy-pasteable, working)
- [ ] **When to use** (scenarios, triggers)
- [ ] **Best practices** (do's and don'ts)
- [ ] **Checklist** (actionable steps)
- [ ] **Testing** (how to validate pattern works)

### Quality Levels

**⭐⭐⭐ Excellent**:
- Comprehensive (covers 90%+ of use cases)
- Production-tested (used in real features)
- Well-tested (examples have tests)
- Clear (junior dev can follow)

**⭐⭐ Good**:
- Covers common cases (70%+)
- Production-ready (not yet used)
- Examples present (may lack tests)
- Understandable (requires some experience)

**⭐ Draft**:
- Basic coverage (50%+)
- Experimental (not production-tested)
- Minimal examples
- Needs revision

**Aim for**: ⭐⭐⭐ Excellent (all KB files)

---

## 🚀 Quick Reference

### Common Lookups

**React/Next.js Development**: → `react19-nextjs16-best-practices.md`
**Writing E2E Tests**: → `e2e-testing-patterns.md` (Section: "Common Patterns")
**Security Validation**: → `security-patterns.md` (Section: "Security Checklist")
**Dependency Updates**: → `dependency-management.md` (Section: "Update Strategy")
**Monthly Maintenance**: → `codebase-maintenance.md` (Section: "Monthly Maintenance")

---

## 📞 KB Ownership

**Primary Owner**: Tech Lead (curate quality)
**Contributors**: All developers (submit patterns)
**Reviewers**: Senior developers (approve changes)

**Communication**:
- New KB file: Announce in team channel
- Major update: Include in sprint review
- Quarterly review: Dedicated meeting (1 hour)

---

## 🏁 Summary

**Knowledge Base Purpose**: Preserve To-Be patterns and best practices from completed work

**Benefits**:
- ✅ Faster onboarding (new devs read KB)
- ✅ Consistent patterns (reference during dev)
- ✅ Avoid repeated mistakes (lessons learned documented)
- ✅ Knowledge retention (survives team changes)
- ✅ Quality assurance (KB patterns in code reviews)

**Next Steps**:
1. Read all KB files (2 hours for full team)
2. Reference during development
3. Contribute new patterns as discovered
4. Quarterly review and updates

---

**KB established**: 2025-11-11
**Current files**: 5 (1 README + 4 patterns)
**Status**: Production-ready, actively maintained
