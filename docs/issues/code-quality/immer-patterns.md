# Issue: Consistent Immer Patterns in Zustand

**ID**: QA-001
**Category**: Code Quality
**Priority**: 🟢 **MEDIUM**
**Status**: 🔴 Open
**Created**: 2025-11-19

---

## 📋 Summary

Standardizzare i pattern di mutation in Zustand slices. Attualmente il codice usa sia spread operators che push diretti con Immer, creando inconsistenza.

---

## 🎯 Problem Statement

### Current Inconsistency
```typescript
// ❌ Pattern 1: Spread operator (ridondante con Immer)
set((state) => {
  state.messagesByChat[threadId] = [...currentMessages, userMessage];
});

// ❌ Pattern 2: Push diretto
set((state) => {
  state.messagesByChat[threadId].push(userMessage);
});
```

**Issues**:
- ⚠️ **Inconsistent patterns** nel codebase
- ⚠️ **Spread non necessario** con Immer

---

## 🔧 Solution: Standardize on Push Pattern

### Recommended Pattern

```typescript
// ✅ AFTER: Consistent push pattern with Immer
set((state) => {
  // Initialize if needed
  if (!state.messagesByChat[threadId]) {
    state.messagesByChat[threadId] = [];
  }
  // Push directly (Immer handles immutability)
  state.messagesByChat[threadId].push(userMessage);
});
```

### Alternative: Consistent Spread Pattern

```typescript
// ✅ Alternative: Consistent spread (if preferred)
set((state) => {
  state.messagesByChat[threadId] = [
    ...(state.messagesByChat[threadId] ?? []),
    userMessage,
  ];
});
```

---

## 📝 Implementation Checklist

- [ ] Review all Zustand slices (5 files)
- [ ] Choose pattern (push vs spread)
- [ ] Update all mutations consistently
- [ ] Document pattern in CONTRIBUTING.md
- [ ] Add ESLint rule if possible

---

## ✅ Acceptance Criteria

- [ ] Single consistent pattern across all slices
- [ ] No mixing of push/spread in same file
- [ ] Tests pass
- [ ] Documentation updated

---

## 📊 Effort: 3 hours

---

**Last Updated**: 2025-11-19
**Status**: 🔴 Open
