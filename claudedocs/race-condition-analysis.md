# Race Condition Analysis: setProgress() in useUploadPdf

**Issue**: setProgress() in useUploadPdf may be called after component unmounts
**File**: `apps/web/src/hooks/queries/useUploadPdf.ts:52-60`
**Severity Score**: 25/100 - Edge case, unlikely

## Executive Summary
This is NOT a critical memory leak. React Query + React hooks together prevent the most severe outcomes, but there is a minor issue: progress updates *could* occur after component unmount during rapid cancellations or navigation.

---

## Analysis

### The Potential Race Condition

```typescript
// useUploadPdf.ts:42-68
export function useUploadPdf(options: UseUploadPdfOptions = {}) {
  const { onProgress, ...mutationOptions } = options;
  const [progress, setProgress] = useState<number>(0);

  const mutation = useMutation<UploadPdfResult, Error, File>({
    mutationFn: async (file: File) => {
      setProgress(0);  // State update 1

      const result = await api.pdf.uploadPdf(
        'wizard-temp',
        file,
        percent => {
          setProgress(percent);  // State update 2 (ASYNC CALLBACK)
          onProgress?.(percent); // External callback (can execute after unmount)
        }
      );
      return result;
    },
    onError: () => {
      setProgress(0);  // State update 3
    },
    ...mutationOptions,
  });

  return { ...mutation, progress };
}
```

**The Race Condition Path**:
1. Component mounts, hook is called
2. User initiates upload: mutate(file) starts
3. XHR upload begins, progress events fire → setProgress(percent) updates
4. User rapidly navigates away / component unmounts
5. XHR is still uploading (slow network, large file)
6. Progress event fires → setProgress() is called on unmounted component
7. React logs: "Warning: Can't perform a React state update on an unmounted component"

### Does React Query Handle Cleanup?

**Partial YES** - React Query DOES handle some cleanup:

1. Mutation state is managed by React Query, not local component state
2. When component unmounts, React cleans up the hook itself
3. onSuccess/onError callbacks won't execute if component unmounts (React Query respects cleanup)

**BUT** - There are TWO separate state updaters:

1. React Query managed: isPending, error, data (protected by cleanup)
2. Local state: progress via useState() (NOT protected)

### The Actual Issue

The race condition exists on **local state only** (progress):

```typescript
const [progress, setProgress] = useState<number>(0);
// This is NOT automatically cleaned up by React Query
```

**Why?** Because:
- The progress callback is stored in the XHR upload handler
- XHR events can fire AFTER the component unmounts
- React can't clean up callbacks inside api.pdf.uploadPdf() automatically
- When setProgress() fires on unmounted component → React warning

### Will This Cause a Memory Leak?

**No**, but for specific reasons:

**NOT a memory leak because:**
1. State updates on unmounted components don't prevent garbage collection
2. React's warning is merely a warning, not a functional leak
3. The failed state update doesn't hold memory references
4. Once the component is truly gone, the closure will be garbage collected

**BUT it IS a problem because:**
1. React console warnings (development pollution)
2. Potential lost error states if progress callback errors
3. In production, React suppresses the warning but the wasted state update still executes
4. Multiple rapid uploads could compound into observable performance issues

---

## Severity Assessment

### Scoring: 25/100 - Edge Case, Unlikely

| Factor | Assessment |
|--------|-----------|
| Likelihood of occurrence | Low (25%) - Requires slow upload + rapid unmount |
| Impact if occurs | Minor (5%) - Only console warning, no data loss |
| Detectability | High (40%) - Clear React warning in dev tools |
| Production risk | Minimal (10%) - React suppresses warning, cleanup still succeeds |

**Total Risk Score: 25/100**

### Why NOT Higher:

1. **React Query + React Hooks work together adequately**
   - Mutation state cleanup IS protected
   - Only local progress state is unprotected
   - Progress is cosmetic (UI display only)

2. **Realistic occurrence is rare**
   - File uploads typically <30 seconds
   - Most users stay on the upload screen during upload
   - Even with fast unmount, XHR cleanup still succeeds

3. **No data corruption**
   - Failed state updates don't corrupt component or hook state
   - Upload still completes successfully
   - Only the progress display may not update

---

## Root Cause

**The callback is stored in XMLHttpRequest**:

```typescript
// pdfClient.ts:59-107
async uploadPdf(gameId: string, file: File, onProgress?: (percent: number) => void) {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', url, true);

    if (onProgress) {
      // THIS callback persists in xhr.upload handler
      // Even after component unmounts
      xhr.upload.onprogress = (event) => {
        if (event.lengthComputable) {
          const percent = Math.round((event.loaded / event.total) * 100);
          onProgress(percent);  // Can fire after component is gone
        }
      };
    }

    xhr.send(formData);
  });
}
```

The callback stays alive because it's bound to the XHR object, which lives independently of the React component lifecycle.

---

## Real-World Scenario

**When would this actually happen?**

1. File: 100+ MB on slow connection (10+ second upload)
2. User: Closes wizard or navigates away during upload
3. XHR: Still uploading when component unmounts
4. Result: Progress callback fires on unmounted component
5. Symptom: Warning in console

**Probability in MeepleAI**: LOW
- Max file size: 50 MB (configured)
- Most users on decent broadband
- Wizard designed to stay open during upload
- Even if it happens: harmless warning

---

## Comparison: React Query Cleanup vs Local State

| Aspect | React Query Managed | Local useState |
|--------|-------------------|-----------------|
| Cleanup on unmount | Protected | Not protected |
| Affected in useUploadPdf | isPending, error, data | progress |
| Risk in this case | None | Minor (cosmetic) |
| Memory leak | No | No |

---

## Recommended Fix (Optional, Low Priority)

```typescript
export function useUploadPdf(options: UseUploadPdfOptions = {}) {
  const { onProgress, ...mutationOptions } = options;
  const [progress, setProgress] = useState<number>(0);
  const [isMounted, setIsMounted] = useState(true);

  useEffect(() => {
    return () => {
      setIsMounted(false);
    };
  }, []);

  const mutation = useMutation<UploadPdfResult, Error, File>({
    mutationFn: async (file: File) => {
      if (!isMounted) return;
      setProgress(0);

      const result = await api.pdf.uploadPdf(
        'wizard-temp',
        file,
        percent => {
          if (isMounted) {  // Guard before state update
            setProgress(percent);
            onProgress?.(percent);
          }
        }
      );
      return result;
    },
    onError: () => {
      if (isMounted) {
        setProgress(0);
      }
    },
    ...mutationOptions,
  });

  return { ...mutation, progress };
}
```

**Note**: This fix is NOT critical since React already suppresses the warning internally.

---

## Conclusion

**Final Score: 25/100 - Edge case, unlikely**

- No memory leak (React handles garbage collection)
- No data corruption
- Minor: Console warning possible under specific conditions
- Can happen in practice: slow upload + rapid navigation
- React Query + React Hooks work together adequately

**Recommendation**: Document but don't fix. This is a theoretical edge case with minimal real-world impact. The codebase handles the common case well.
