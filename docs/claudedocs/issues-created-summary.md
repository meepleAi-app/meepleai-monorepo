# Issues Create da Test Analysis - Quick Reference

**Data**: 2026-01-16
**Source**: `test-execution-report-2026-01-16.md`
**Total Issues**: 6

---

## 🔴 HIGH Priority (Fix Oggi - 3 issues)

### Backend (2 issues)

**[#2536](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2536)** - LINQ Translation Error
- File: `CreateRuleCommentCommandHandler.cs:98`
- Fix: `ToLowerInvariant()` → `EF.Functions.ILike()`
- Time: 30min

**[#2537](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2537)** - MediatR DI Registration
- File: `CreateGameWithBggIntegrationTests.cs:56`
- Fix: Add `services.AddMediatR()` in test setup
- Time: 20min

### Frontend (1 issue)

**[#2540](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2540)** - Language Mismatch in Tests
- File: `infrastructure-client-basic.test.tsx`
- Fix: "Monitoraggio Infrastruttura" → "Infrastructure Monitoring"
- Time: 15min

**Total Time**: ~1 hour

---

## ⚡ CRITICAL Performance (Fix Questa Settimana - 1 issue)

**[#2541](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2541)** - Test Performance Optimization
- Component: Backend Testcontainers
- Current: >20 minutes (crashing)
- Target: <3 minutes
- Fix: `IClassFixture<PostgresContainerFixture>` + parallelization
- Time: 4-6 hours
- Impact: 60-73% faster

---

## 🟡 MEDIUM Priority (Fix Questa Settimana - 2 issues)

**[#2538](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2538)** - DocumentVersion Format
- File: `SharedGameDocumentRepositoryIntegrationTests.cs:238`
- Fix: Use "1.0" format for version
- Time: 30min

**[#2542](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2542)** - E2E Test Suite Execution
- Component: Playwright E2E (20+ specs)
- Action: Execute full suite + establish baselines
- Time: 4 hours

---

## 🟢 LOW Priority (Fix Questo Sprint - 1 issue)

**[#2539](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2539)** - ShareLink FK Assertion
- File: `ShareLinkForeignKeyTests.cs:127`
- Fix: `DbUpdateException` → `InvalidOperationException`
- Time: 15min

---

## 📊 Summary Matrix

| Priority | Count | Total Time | Status |
|----------|-------|------------|--------|
| 🔴 HIGH | 3 | ~1 hour | Fix oggi |
| ⚡ CRITICAL | 1 | 4-6 hours | Fix questa settimana |
| 🟡 MEDIUM | 2 | 4.5 hours | Fix questa settimana |
| 🟢 LOW | 1 | 15 min | Fix questo sprint |
| **TOTAL** | **6** | **~10 hours** | |

---

## 🎯 Recommended Execution Order

### Today (2 hours)
1. #2536 - LINQ Translation (30min)
2. #2537 - DI Registration (20min)
3. #2540 - Language Mismatch (15min)
4. **Validate**: Re-run tests → Verify 9 → 6 failures
5. **Goal**: Quality gates passing

### Tomorrow (5 hours)
6. #2541 - Performance Optimization (4-6 hours)
7. **Validate**: Test execution <3 minutes
8. #2538 - DocumentVersion (30min)

### End of Week (4 hours)
9. #2539 - ShareLink FK (15min)
10. #2542 - E2E Suite (4 hours)
11. **Validate**: All quality gates green

---

## 📁 Related Documents

- **Detailed Report**: `test-execution-report-2026-01-16.md`
- **Quick Summary**: `test-summary-2026-01-16.txt`
- **Issue Tracking**: `test-issues-tracking-2026-01-16.md` (questo file)
- **Frontend Coverage**: `apps/web/coverage/index.html`

---

## 🔗 Quick Action Links

**View All Issues**:
```bash
gh issue list --repo DegrassiAaron/meepleai-monorepo --label test
```

**Filter by Priority**:
- HIGH: https://github.com/DegrassiAaron/meepleai-monorepo/issues?q=is%3Aissue+is%3Aopen+label%3Abug+label%3Atest
- CRITICAL: https://github.com/DegrassiAaron/meepleai-monorepo/issues/2541

**Assign to Self**:
```bash
gh issue edit 2536 --add-assignee @me
gh issue edit 2537 --add-assignee @me
gh issue edit 2540 --add-assignee @me
```

---

**Created**: 2026-01-16 @ 18:05
**Creator**: Claude Code Test Analysis
**Next Review**: After HIGH priority fixes (end of day)
