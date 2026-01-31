# Issue Analysis: Infinite Dependency Loop in useEffect

## Issue Location
**File**: `apps/web/src/app/(authenticated)/toolkit/[sessionId]/page.tsx:62`

**Code**:
```typescript
useEffect(() => {
  const load = async () => {
    try {
      await loadSession(sessionId);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to load session');
      router.push('/toolkit');
    }
  };

  void load();
}, [sessionId, loadSession, toast, router]);
```

---

## Dependency Analysis

### 1. **sessionId** ✅ STABLE
- **Type**: `string` (destructured from `params.sessionId`)
- **Stability**: Primitive value - changes only when route changes
- **Impact**: Should be included - legitimate dependency

### 2. **loadSession** ❌ NOT STABLE (PRIMARY CULPRIT)
- **Source**: Zustand store (`useSessionStore()`)
- **Implementation**: 
  ```typescript
  loadSession: async (sessionId: string) => {
    set({ isLoading: true, error: null }, false, 'loadSession/start');
    // ... fetch implementation
  }
  ```
- **Problem**: 
  - Zustand by default creates a NEW function reference on every render
  - No `useCallback` or `useShallow` optimization in the store
  - **WITHOUT selective extraction**, hook returns entire store object
  - Every store update → all selectors re-run → new function refs
  
- **Stability Level**: ⚠️ **RECREATED ON EVERY RENDER** (unless using `useShallow`)

### 3. **toast** ❌ APPEARS STABLE BUT MISUSED
- **Source**: `import { toast } from 'sonner'`
- **What is it**: Object with methods like `toast.error()`, `toast.success()`
- **Stability**: `toast` itself is a stable singleton reference
- **Why it's problematic**:
  - ✅ The `toast` object IS stable (imported constant)
  - ❌ BUT: Including it in dependency array is **redundant and anti-pattern**
  - **Rule**: External library references should NOT be in dependencies
  - Similar to never including `console` or `Math` in dependencies

### 4. **router** ❌ NOT STABLE
- **Source**: `useRouter()` from `next/navigation`
- **Behavior**: `useRouter()` returns new ref on re-renders (by design in Next.js)
- **Stability Level**: ⚠️ **RECREATED ON EVERY RENDER**

---

## Root Cause Analysis

### Infinite Loop Mechanism:

1. **Component renders** → destructures `loadSession`, `toast`, `router`
2. **Zustand hook** (`useSessionStore()`) returns entire store
3. **No selective extraction** → `loadSession` is new function reference
4. **Dependencies check**: Previous `loadSession` !== current `loadSession`
5. **useEffect triggers** → calls `loadSession(sessionId)`
6. **loadSession calls `set()`** → Zustand state updates
7. **State update** → all consumers re-render → back to step 1
8. **Loop continues** ♻️

---

## Evidence from Code

### Zustand Store (sessionStore.ts)
```typescript
export const useSessionStore = create<SessionStore>()(
  devtools(
    (set, get) => ({
      // ... all actions defined here
      loadSession: async (sessionId: string) => {
        // ... implementation
      },
      // ... other actions
    }),
    { name: 'SessionStore' }
  )
);
```

**Key Issue**: 
- No `useCallback` wrapping actions
- No `useShallow` for shallow comparison
- Entire store object returned without selective extraction

---

## Why Dependencies Are Problematic

| Dependency | Status | Why |
|-----------|--------|-----|
| `sessionId` | ✅ Safe | Primitive, changes only with route |
| `loadSession` | ❌ Loop | Zustand action recreated every store update |
| `toast` | ❌ Anti-pattern | External lib, always stable, shouldn't be included |
| `router` | ❌ Loop | Next.js returns new ref each render |

---

## Impact Assessment

### Symptom Pattern:
1. Page loads → useEffect runs → loadSession called
2. loadSession updates store state
3. Component re-renders → loadSession ref changes
4. useEffect detects dependency change → runs again
5. Infinite fetch requests to `/api/v1/sessions/{id}/details`
6. Network tab shows repeated identical requests
7. Loading spinner may flash repeatedly
8. Potential race condition with SSE connection

---

## Severity Scoring Factors

### HIGH SEVERITY INDICATORS:
- ✅ Network spam (repeated API calls)
- ✅ Potential infinite loops
- ✅ Performance degradation
- ✅ User-visible issues (flashing, delays)
- ✅ Clear root cause (unstable dependencies)

### CONTEXT CLUES:
- Zustand store WITHOUT selective extraction
- Direct use of store methods in dependencies
- Multiple unstable dependencies
- SSE connection competing with effect (timing issues)

---

## Recommended Fixes (Ordered by Quality)

### ❌ DO NOT DO THIS:
```typescript
// Removing dependencies entirely - WRONG
}, []);  // Ignores sessionId changes in route

// Adding more deps - WRONG
}, [sessionId, loadSession, toast, router, activeSession]);  // Adds noise
```

### ✅ OPTION 1: Selective Store Extraction (BEST)
```typescript
const { loadSession } = useSessionStore(
  useShallow(state => ({ loadSession: state.loadSession }))
);

useEffect(() => {
  // ...
}, [sessionId, loadSession]);  // loadSession now stable via useShallow
```

### ✅ OPTION 2: SessionId Only (SIMPLEST)
```typescript
useEffect(() => {
  // ...
}, [sessionId]);  // Only parameter that legitimately changes
```

### ✅ OPTION 3: useCallback Wrapper in Store (ARCHITECTURE)
```typescript
// In sessionStore.ts
loadSession: useCallback(async (sessionId: string) => {
  // ...
}, [])
```

---

## Additional Issues Found

1. **SSE Race Condition**: 
   - useSessionSync hook also listening for `onFinalized`
   - Both useEffect and SSE can call `router.push('/toolkit')`
   - Race condition possible

2. **Async in useEffect**:
   - ✅ Correctly wrapped in async IIFE
   - ✅ Void operator prevents warning
   - Pattern is correct

3. **Error Handling**:
   - ✅ Catches errors properly
   - ✅ Shows toast + redirects
   - No issues here

---

## Score Justification

**SCORE: 72/100**

### Why Not Higher (85+):
- Not immediately crashing the app
- Dependency array is technically valid React code
- Performance impact depends on store size/update frequency
- Issue is architectural, not syntactic

### Why Not Lower (50-):
- Network spam from repeated requests
- Infinite loop potential is real
- User-visible performance problems
- Clear best practice violation
- Multiple unstable dependencies compounding issue

### Factors Contributing to 72:
- **High severity mechanism** (infinite loop) → +35
- **Multiple unstable deps** (loadSession, router) → +15
- **Redundant deps** (toast) → +10
- **Demonstrable negative impact** → +15
- **Clear fix exists** → -5 (slightly lowers for implementability)
- **Not critical/crasher** → -2
- **Context-dependent impact** (depends on store update frequency) → -2

---

## Final Verdict

**CONFIRMED ISSUE** - Real infinite dependency loop exists

**Root Cause**: Zustand `loadSession` function recreated on every render due to no selective extraction, causing dependency array to detect change even though function logic is identical.

**Recommended Priority**: HIGH (affects performance, network traffic, user experience)
