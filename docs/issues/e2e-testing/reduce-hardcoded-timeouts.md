# Reduce Hardcoded Timeouts

**Issue ID**: E2E-007 | **Priorità**: 🟡 MEDIA | **Effort**: 8-10 ore

---

## 📋 Problem

**67 occorrenze** di `waitForTimeout()` con valori hardcoded (100ms-5000ms). Anti-pattern che rende test lenti e fragili.

```bash
$ grep -r "waitForTimeout" apps/web/e2e/*.spec.ts | wc -l
67
```

```typescript
// ❌ ANTI-PATTERN
await page.waitForTimeout(2000); // Why 2000? What are we waiting for?
await page.waitForTimeout(500);  // Arbitrary delay
```

**Problemi**:
- Test più lenti del necessario
- Fragili (fail su CI lento, pass su developer machine veloce)
- Non semantici (non chiaro cosa aspettiamo)

---

## 🎯 Impact

- **Speed**: Test suite ~2-3 minuti più lento
- **Stability**: ~10% flakiness causato da timeout insufficienti
- **Maintainability**: Difficile capire cosa test aspetta

---

## ✅ Solution

### Replace Patterns

```typescript
// ❌ BEFORE: Hardcoded timeout
await page.waitForTimeout(2000);

// ✅ AFTER: Semantic wait
await page.waitForLoadState('networkidle');

// OR wait for specific element
await page.getByTestId('loading-indicator').waitFor({ state: 'hidden' });

// OR wait for API call
await page.waitForResponse(resp => resp.url().includes('/api/chat'));

// OR custom condition
await page.waitForFunction(() => document.querySelector('[data-loaded="true"]'));
```

### Common Replacements

| Hardcoded | Better Alternative |
|-----------|-------------------|
| `waitForTimeout(2000)` after navigation | `waitForLoadState('networkidle')` |
| `waitForTimeout(500)` after click | `waitFor({ state: 'visible' })` on expected element |
| `waitForTimeout(1000)` for animation | Disable animations globally |
| `waitForTimeout(100)` for debounce | `waitForFunction()` for final state |

---

## 📝 Checklist

- [ ] Audit all 67 occurrences, categorize
- [ ] Replace navigation timeouts (20 occurrences)
- [ ] Replace animation timeouts (15 occurrences)
- [ ] Replace debounce timeouts (10 occurrences)
- [ ] Replace API wait timeouts (12 occurrences)
- [ ] Replace misc timeouts (10 occurrences)
- [ ] Target: <10 remaining (only where truly necessary)

---

**Target**: 67 → <10 occurrences
