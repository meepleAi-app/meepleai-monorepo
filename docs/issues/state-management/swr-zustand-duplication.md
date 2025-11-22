# Issue: Fix SWR + Zustand State Duplication

**ID**: STATE-001
**Category**: State Management
**Priority**: 🟢 **MEDIUM**
**Status**: 🔴 Open
**Created**: 2025-11-19

---

## 📋 Summary

Eliminare duplicazione di state tra SWR e Zustand nel hook `useChatOptimistic`. Attualmente i messaggi sono gestiti da entrambi, creando potenziali sync issues.

---

## 🎯 Problem Statement

### Current Code
```typescript
// ❌ PROBLEMA: Due fonti di verità
const contextMessages = useActiveMessages(); // Zustand
const { data: swrMessages } = useSWR(swrKey, null, {
  fallbackData: contextMessages, // Duplicazione
});
const messages = swrMessages ?? contextMessages; // Quale è "truth"?
```

**Issues**:
- ⚠️ **State duplication** - Stesso state in due posti
- ⚠️ **Sync issues** - Possibile desync
- ⚠️ **Complexity** - Hard to debug

---

## 🔧 Solution: Remove SWR (Use Only Zustand)

### Simplified Implementation

**File**: `apps/web/src/hooks/useChatOptimistic.ts`
```typescript
// ✅ AFTER: Single source of truth
export function useChatOptimistic(): UseChatOptimisticResult {
  const sendMessage = useChatStore((state) => state.sendMessage);
  const messages = useActiveMessages(); // Zustand only
  const [optimisticId, setOptimisticId] = useState<string | null>(null);

  const sendMessageOptimistic = useCallback(
    async (content: string) => {
      setOptimisticId(`temp-${Date.now()}`);
      try {
        await sendMessage(content); // Zustand handles everything
        setOptimisticId(null);
      } catch (err) {
        setOptimisticId(null);
        throw err;
      }
    },
    [sendMessage]
  );

  return {
    sendMessageOptimistic,
    isOptimisticUpdate: optimisticId !== null,
    messages, // From Zustand only
  };
}
```

---

## 📝 Implementation Checklist

- [ ] Remove SWR imports from useChatOptimistic
- [ ] Simplify hook to use only Zustand
- [ ] Update tests (remove SWR mocks)
- [ ] Verify optimistic updates still work
- [ ] Test in dev and staging

---

## ✅ Acceptance Criteria

- [ ] Single source of truth (Zustand)
- [ ] No SWR usage for messages
- [ ] Optimistic updates work
- [ ] Tests pass
- [ ] Bundle size reduced

---

## 📊 Effort: 6 hours

---

**Last Updated**: 2025-11-19
**Status**: 🔴 Open
