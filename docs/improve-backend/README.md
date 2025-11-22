# 🚀 API Improvements Initiative - Quick Start Guide

**Status**: 🟡 In Progress (0/12 completed)
**DDD Progress**: 99% → Target: 100%
**Estimated Effort**: 78-110 hours

---

## 📍 START HERE

👉 **Prima volta qui?** Leggi **[INDEX.md](./INDEX.md)** per orientarti nella documentazione!

---

## 📚 Documentation

| Document | Description | Location |
|----------|-------------|----------|
| **INDEX** | 📍 Indice generale e guida alla documentazione | [INDEX.md](./INDEX.md) |
| **Issue Tracker** | ⭐ Tracking operativo di tutte le 12 issues | [ISSUE_TRACKER.md](./ISSUE_TRACKER.md) |
| **Issue Templates** | 📋 Template dettagliati per GitHub issues | [issues-templates.md](./issues-templates.md) |
| **Executive Summary** | 📊 Report esecutivo per stakeholder | [executive-summary.md](./executive-summary.md) |
| **Creation Script** | 🤖 Script automatico (richiede gh CLI) | `../../tools/create-api-improvement-issues.sh` |

---

## 🎯 Quick Start

### Step 1: Review Documentation
```bash
# Leggi l'indice generale (consigliato!)
cat docs/improve-backend/INDEX.md

# Leggi il report esecutivo
cat docs/improve-backend/executive-summary.md

# Apri il tracker operativo
code docs/improve-backend/ISSUE_TRACKER.md

# Vedi i template delle issue
cat docs/improve-backend/issues-templates.md
```

### Step 2: Create GitHub Issues

**Option A: Manual Creation** (Recommended - gh CLI not available)
1. Open `docs/improve-backend/issues-templates.md`
2. Copy each issue template
3. Create new GitHub issue
4. Paste template as description
5. Add labels and milestone
6. Update `docs/improve-backend/ISSUE_TRACKER.md` with issue number

**Option B: Automated Script** (If gh CLI becomes available)
```bash
bash tools/create-api-improvement-issues.sh
```

### Step 3: Start Working

1. **Priority Order**: P0 → P1 → P2 → P3
2. **First Task**: Issue #1 (Fix Deadlock Risk - CRITICAL)
3. **Update Tracker**: Mark status in `docs/ISSUE_TRACKER.md`
4. **Commit Progress**: Regular commits with tracker updates

---

## 🔴 CRITICAL - Start Here

### Issue #1: Fix Deadlock Risk in RateLimitService

**⚠️ THIS MUST BE FIXED BEFORE PRODUCTION DEPLOYMENT**

**File**: `apps/api/src/Api/Services/RateLimitService.cs:160-161`

**Problem**:
```csharp
// DEADLOCK RISK!
var maxTokens = GetRateLimitValueAsync<int>("MaxTokens", normalizedRole).Result;
var refillRate = GetRateLimitValueAsync<double>("RefillRate", normalizedRole).Result;
```

**Impact**: All rate-limited endpoints (auth, 2FA, OAuth) can deadlock under load

**Time**: 2-3 hours

**Quick Fix**:
```csharp
// Make method async
public async Task<RateLimitConfig> GetConfigForRoleAsync(string? role, CancellationToken ct = default)
{
    // ...
    var maxTokens = await GetRateLimitValueAsync<int>("MaxTokens", normalizedRole, ct);
    var refillRate = await GetRateLimitValueAsync<double>("RefillRate", normalizedRole, ct);
    return new RateLimitConfig(maxTokens, refillRate);
}
```

---

## 📋 Issue Summary

### 🔴 Phase 1 - CRITICAL (1 issue, 2-3h)
- **#1**: Fix Deadlock Risk in RateLimitService

### 🟠 Phase 2 - HIGH (4 issues, 34-48h)
- **#2**: Migrate ChatService to CQRS (12-16h)
- **#3**: Migrate RuleSpecService to CQRS (10-14h)
- **#4**: Implement Streaming Query Handlers (8-12h)
- **#5**: Replace Hardcoded Configuration (4-6h)

### 🟡 Phase 3 - MEDIUM (4 issues, 30-42h)
- **#6**: Migrate Agent Services to CQRS (10-14h)
- **#7**: Migrate Comment/Diff Services (6-8h)
- **#8**: Implement 42 Domain Events (8-12h)
- **#9**: Complete OAuth CQRS Migration (6-8h)

### 🟢 Phase 4 - LOW (3 issues, 12-17h)
- **#10**: Add AsNoTracking for Performance (2-3h)
- **#11**: Session Authorization Improvements (4-6h)
- **#12**: Centralize Error Handling (6-8h)

---

## 📊 Progress Tracking

Update `docs/ISSUE_TRACKER.md` as you work:

```markdown
### ✅ Issue #1: [P0] Fix Deadlock Risk in RateLimitService

**Status**: 🟡 In Progress  ← Update this
**Assignee**: John Doe       ← Add name
**GitHub Issue**: #1234      ← Add issue number
**PR**: #1235                ← Add PR number
**Started**: 2025-11-15      ← Add date
**Actual**: 2.5h             ← Track time

**Notes**:
- Fixed blocking async calls
- Updated all callers
- Load tested successfully
```

---

## 🎓 Learning Resources

### CQRS Pattern
- **MediatR Docs**: https://github.com/jbogard/MediatR
- **Internal Guide**: `docs/02-development/cqrs-guidelines.md`
- **DDD Patterns**: `docs/01-architecture/ddd-patterns.md`

### Streaming with MediatR
```csharp
// IStreamRequestHandler pattern
public class StreamExplainQueryHandler : IStreamRequestHandler<StreamExplainQuery, string>
{
    public async IAsyncEnumerable<string> Handle(
        StreamExplainQuery request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var chunk in _ragService.StreamAsync(request, ct))
        {
            yield return chunk;
        }
    }
}
```

### Domain Events
```csharp
// Publishing events from aggregates
public static ChatThread Create(...)
{
    var thread = new ChatThread(...);
    thread.AddDomainEvent(new ChatThreadCreatedEvent(thread.Id));
    return thread;
}
```

---

## ✅ Checklist for Each Issue

Before starting:
- [ ] Read full issue template in `ISSUES_API_IMPROVEMENTS.md`
- [ ] Understand acceptance criteria
- [ ] Review related code files
- [ ] Create GitHub issue (if not automated)
- [ ] Update tracker with issue number

During work:
- [ ] Update tracker status to "In Progress"
- [ ] Follow task checklist in tracker
- [ ] Write tests (maintain 90%+ coverage)
- [ ] Update documentation
- [ ] Commit frequently with clear messages

Before completing:
- [ ] All tasks checked off
- [ ] Tests pass
- [ ] Code review completed
- [ ] Update tracker with actual hours
- [ ] Mark status as "Completed"
- [ ] Update progress overview

---

## 🔄 Workflow

```
1. Pick Next Issue (Priority Order)
   ↓
2. Create GitHub Issue (if not exists)
   ↓
3. Update Tracker (Status → In Progress, Assignee, Issue #)
   ↓
4. Create Branch (e.g., fix/issue-1-deadlock)
   ↓
5. Work on Tasks (Check off in tracker)
   ↓
6. Write Tests (90%+ coverage)
   ↓
7. Create PR (Reference issue)
   ↓
8. Code Review
   ↓
9. Merge PR
   ↓
10. Update Tracker (Status → Completed, Actual Hours)
    ↓
11. Next Issue
```

---

## 🎯 Success Metrics

Track these in `docs/ISSUE_TRACKER.md`:

| Metric | Baseline | Target | Current |
|--------|----------|--------|---------|
| DDD Completion | 99% | 100% | 99% |
| Legacy Code | 2,500 lines | 0 lines | 2,500 lines |
| CQRS Endpoints | 85% | 100% | 85% |
| Domain Events | 0 published | 42 published | 0 |
| Blocking Async | 2 locations | 0 locations | 2 |
| Test Coverage | 90% | 90%+ | 90% |

---

## 📞 Support

**Questions?**
- See `docs/architecture/api-improvements-summary.md` for detailed context
- Review `CLAUDE.md` for project architecture
- Check `docs/02-development/` for development guides

**Issues?**
- Update tracker with blockers
- Document decisions in issue notes
- Commit tracker frequently

---

## 🚦 Status Legend

- ⬜ **Not Started**: Issue not yet begun
- 🟡 **In Progress**: Actively being worked on
- ✅ **Completed**: All tasks done, merged to main
- 🔴 **Blocked**: Waiting on dependency/decision
- ⏸️ **Paused**: Temporarily stopped

---

**Created**: 2025-11-15
**Last Updated**: 2025-11-15
**Owner**: Engineering Team
