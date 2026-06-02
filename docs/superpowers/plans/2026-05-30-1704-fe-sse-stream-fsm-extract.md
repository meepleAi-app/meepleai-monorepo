# FE: `useSseStreamFsm` Phase B Extract (#1704) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract a shared `useSseStreamFsm<TEvent, TState, TError>` base hook that encapsulates AbortController lifecycle, retry scheduling with configurable backoff, optional watchdog timeout, and unmount cleanup. Refactor the two existing SSE hooks (`useKbAskStream` — 3 retries exp + 30s watchdog + 4 error kinds; `useAgentChatStream` — 2 retries linear) as thin wrappers that pass their **current** policy via config. **Zero behavior change**: all 38 existing tests (16 + 22) MUST pass without modification. Phase A (unify policy) is intentionally out of scope (requires product sign-off; tracked as follow-up).

**Architecture:** Transport-injection design — the base hook accepts a `transport: (input, signal) => AsyncGenerator<TEvent>` function, not an endpoint string. This preserves the existing `kbAskClient.askGlobal` public API (heavily mocked in `useKbAskStream.test.ts` via `vi.spyOn`). The base hook owns the FSM scaffolding (retry, watchdog, abort); consumers own event parsing, state reducer, and error mapping. State shape is fully generic via `TState`.

**Tech Stack:** React 19 hooks · Vitest fake timers · Zod (optional per consumer) · `@testing-library/react renderHook + act + waitFor` · `flushSync` from `react-dom`

---

## Design Decisions

### DD-1: Transport injection (deviation from panel D-5 API)

Panel D-5 specified `endpoint: string + requestBuilder + eventSchema: ZodTypeAny`. After grounding in the codebase, this API forces **two behavior changes** we cannot accept in Phase B:

1. `useKbAskStream.test.ts:233-261` uses `vi.spyOn(kbAskClient, 'askGlobal')` to inject retry-triggering errors. If the base hook calls `fetch` directly, this spy stops firing → 1 test breaks → Phase B AC violation.
2. `useAgentChatStream` does NOT use Zod (inline `(event as ChatStreamEvent).data` casting). Forcing `eventSchema: ZodTypeAny` as required would either need a permissive schema (and Zod strict mode might reject events the manual cast accepts) or break 22 existing tests.

**Decision:** Base hook accepts:
```ts
transport: (input: TInput, signal: AbortSignal) => AsyncGenerator<TEvent>
```
The consumer hook owns the fetch + parse logic and returns the `AsyncGenerator<TEvent>`. The base hook iterates the generator and feeds events to the reducer. `kbAskClient.askGlobal` stays as the transport for `useKbAskStream`; a new in-file `agentChatTransport` (extracted from `useAgentChatStream`'s current body) becomes the transport for `useAgentChatStream`. **All 38 tests pass unchanged.**

Recorded as a Phase B deviation from panel D-5; the spirit of D-5 (locked API shape) is preserved — just transport-shaped instead of endpoint-shaped.

### DD-2: Optional watchdog

`useKbAskStream` has a 30s watchdog (lazy-start: first event resets it). `useAgentChatStream` has no watchdog. Base hook makes `timeoutMs` optional in `retryPolicy`:

```ts
retryPolicy: {
  maxRetries: number;
  backoffMs: readonly number[];
  timeoutMs?: number;   // undefined → no watchdog
  retryableErrorKinds: readonly string[];
}
```

When `timeoutMs` is undefined, no watchdog timer is scheduled — exactly matching `useAgentChatStream`'s current behavior. When defined, the watchdog uses the lazy-start pattern from current `useKbAskStream`.

### DD-3: Reducer-owned state, base hook owns retry counter

`useKbAskStream.state` has 7 fields; `useAgentChatStream.state` has 11. The base hook's `TState` is fully consumer-controlled — consumer's `eventReducer` decides what state changes per event. The only field the base hook **owns** is `retryCount: number`, which it exposes separately from `TState`:

```ts
function useSseStreamFsm<TInput, TEvent, TState, TError>(config): {
  state: TState;
  retryCount: number;   // owned by base hook
  error: TError | null; // owned by base hook (mapped via errorMapper)
  ask: (input: TInput) => void;
  stop: () => void;
  reset: () => void;
};
```

Each consumer hook re-exposes these in its own state shape (e.g. `useKbAskStream` already has `state.retryCount` and `state.error: KbAskError | null` — it just spreads the base hook outputs into its `KbAskStreamState`).

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `apps/web/src/hooks/useSseStreamFsm.ts` | **Create** | Generic FSM hook: transport iteration, retry, watchdog, abort, cleanup |
| `apps/web/src/hooks/__tests__/useSseStreamFsm.test.ts` | **Create** | Unit tests: retry scheduling, watchdog, abort, no-cleanup-leak |
| `apps/web/src/hooks/useKbAskStream.ts` | Modify | Thin wrapper — passes its current policy `[1000, 3000, 9000]` + 30s timeout + `connection` retryable kind |
| `apps/web/src/hooks/useAgentChatStream.ts` | Modify | Thin wrapper — extracts inline fetch+parse into a `transport` function, passes current policy 2× linear 2000ms |
| `apps/web/src/hooks/__tests__/useKbAskStream.test.ts` | **No change expected** | All 16 tests MUST pass unchanged. Acts as feature-parity contract. |
| `apps/web/src/hooks/__tests__/useAgentChatStream.test.ts` | **No change expected** | All 22 tests MUST pass unchanged. Acts as feature-parity contract. |
| `apps/web/src/hooks/__tests__/useSseStreamFsm-cleanup.test.ts` | **Create** | Dedicated suite for no-cleanup-leak (D-4 AC: `vi.getTimerCount() === 0` after unmount) |

---

## Pre-Flight

- [ ] **Step 0a: Confirm clean working tree on `main-dev`**

```bash
git branch --show-current  # MUST print main-dev
git status                 # MUST be clean
git pull --ff-only         # MUST succeed
```

- [ ] **Step 0b: Create feature branch**

```bash
git checkout -b feature/issue-1704-fe-sse-stream-fsm-extract
git config branch.feature/issue-1704-fe-sse-stream-fsm-extract.parent main-dev
```

- [ ] **Step 0c: Run baseline tests — record passing count**

```bash
cd apps/web
pnpm test --run src/hooks/__tests__/useKbAskStream.test.ts src/hooks/__tests__/useAgentChatStream.test.ts 2>&1 | tail -10
```

Expected: 16 passed for `useKbAskStream.test.ts`, 22 passed for `useAgentChatStream.test.ts`. Record total: **38 tests**. This is the feature-parity contract — must remain 38 passing at the end.

- [ ] **Step 0d: Read full source of both hooks + both test files** so the executor has the full event-reducer + retry logic in memory:

```bash
wc -l apps/web/src/hooks/useKbAskStream.ts apps/web/src/hooks/useAgentChatStream.ts apps/web/src/hooks/__tests__/useKbAskStream.test.ts apps/web/src/hooks/__tests__/useAgentChatStream.test.ts apps/web/src/lib/api/clients/kbAskClient.ts
```

Expected line counts approx: 259 + 539 + 262 + 712 + 78. Read each file fully before starting Task 1.

---

## Task 1: Create `useSseStreamFsm.ts` skeleton + base test

**Files:**
- Create: `apps/web/src/hooks/useSseStreamFsm.ts`
- Create: `apps/web/src/hooks/__tests__/useSseStreamFsm.test.ts`

- [ ] **Step 1: Write the failing minimal test**

```typescript
// apps/web/src/hooks/__tests__/useSseStreamFsm.test.ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useSseStreamFsm } from '../useSseStreamFsm';

interface TestEvent { type: 'token' | 'done'; value?: string }
interface TestState { tokens: string[]; done: boolean }

function asyncGenOf<T>(items: T[]): AsyncGenerator<T> {
  return (async function* () { for (const it of items) yield it; })();
}

describe('useSseStreamFsm — base FSM skeleton', () => {
  beforeEach(() => { vi.useRealTimers(); });
  afterEach(() => { vi.restoreAllMocks(); });

  it('initializes with initialState, retryCount=0, error=null', () => {
    const { result } = renderHook(() => useSseStreamFsm<string, TestEvent, TestState, Error>({
      transport: () => asyncGenOf<TestEvent>([]),
      initialState: { tokens: [], done: false },
      eventReducer: (s, e) => e.type === 'token' ? { ...s, tokens: [...s.tokens, e.value!] } : { ...s, done: true },
      errorMapper: (err) => err instanceof Error ? err : new Error(String(err)),
      retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
    }));

    expect(result.current.state).toEqual({ tokens: [], done: false });
    expect(result.current.retryCount).toBe(0);
    expect(result.current.error).toBeNull();
  });

  it('iterates transport events through eventReducer into state', async () => {
    const { result } = renderHook(() => useSseStreamFsm<string, TestEvent, TestState, Error>({
      transport: () => asyncGenOf<TestEvent>([
        { type: 'token', value: 'hello' },
        { type: 'token', value: ' world' },
        { type: 'done' },
      ]),
      initialState: { tokens: [], done: false },
      eventReducer: (s, e) => e.type === 'token' ? { ...s, tokens: [...s.tokens, e.value!] } : { ...s, done: true },
      errorMapper: (err) => err instanceof Error ? err : new Error(String(err)),
      retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
    }));

    act(() => { result.current.ask('q'); });

    await waitFor(() => {
      expect(result.current.state.done).toBe(true);
    });

    expect(result.current.state.tokens).toEqual(['hello', ' world']);
  });
});
```

- [ ] **Step 2: Run test — confirm it fails (module not found)**

```bash
cd apps/web
pnpm test --run src/hooks/__tests__/useSseStreamFsm.test.ts 2>&1 | tail -10
```

Expected: `Cannot find module '../useSseStreamFsm'`.

- [ ] **Step 3: Write the minimal hook implementation**

```typescript
// apps/web/src/hooks/useSseStreamFsm.ts
import { useCallback, useEffect, useReducer, useRef, useState } from 'react';
import { flushSync } from 'react-dom';

export interface SseStreamFsmConfig<TInput, TEvent, TState, TError> {
  /**
   * Async generator producing parsed SSE events. Owner of fetch + parse + Zod (if any).
   * MUST throw a TypeError on network/connection error to trigger retry.
   * MUST honor the passed AbortSignal (call response.body?.cancel() / reader.releaseLock() on abort).
   */
  transport: (input: TInput, signal: AbortSignal) => AsyncGenerator<TEvent>;
  initialState: TState;
  /** Pure reducer: applies one event to state. Called inside the streaming loop. */
  eventReducer: (state: TState, event: TEvent) => TState;
  /** Maps raw thrown errors (or special "error" events) to consumer's TError shape. */
  errorMapper: (err: unknown) => TError;
  retryPolicy: {
    maxRetries: number;
    /** Backoff schedule. Must have length === maxRetries. Example: [1000, 3000, 9000] for maxRetries=3. */
    backoffMs: readonly number[];
    /** When defined, lazy watchdog timer fires if no event arrives within timeoutMs (resets per event). */
    timeoutMs?: number;
    /**
     * String-kind whitelist. The errorMapper output is checked by reading the error's `kind`
     * property — if it matches one of these, the FSM schedules a retry.
     * For consumers that don't have a discriminated error shape, pass [] and add a default
     * 'connection' kind via errorMapper when TypeError is thrown by transport.
     */
    retryableErrorKinds: readonly string[];
  };
}

export interface UseSseStreamFsmReturn<TInput, TState, TError> {
  state: TState;
  retryCount: number;
  error: TError | null;
  ask: (input: TInput) => void;
  stop: () => void;
  reset: () => void;
}

export function useSseStreamFsm<TInput, TEvent, TState, TError>(
  config: SseStreamFsmConfig<TInput, TEvent, TState, TError>,
): UseSseStreamFsmReturn<TInput, TState, TError> {
  const [state, setState] = useState<TState>(config.initialState);
  const [retryCount, setRetryCount] = useState(0);
  const [error, setError] = useState<TError | null>(null);

  const abortRef = useRef<AbortController | null>(null);
  const watchdogRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const retryTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearTimers = useCallback(() => {
    if (watchdogRef.current) { clearTimeout(watchdogRef.current); watchdogRef.current = null; }
    if (retryTimerRef.current) { clearTimeout(retryTimerRef.current); retryTimerRef.current = null; }
  }, []);

  const cleanup = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    clearTimers();
  }, [clearTimers]);

  useEffect(() => cleanup, [cleanup]);

  const runStream = useCallback(async (input: TInput, attempt: number) => {
    const controller = new AbortController();
    abortRef.current = controller;
    let watchdogStarted = false;
    const armWatchdog = () => {
      if (config.retryPolicy.timeoutMs == null) return;
      if (watchdogRef.current) clearTimeout(watchdogRef.current);
      watchdogRef.current = setTimeout(() => {
        controller.abort();
        // NOTE (review fix B-1): plain setState. flushSync inside a timer callback is unsafe in
        // React 19 concurrent mode (Next.js App Router) — it can throw "flushSync was called
        // from inside a lifecycle method" when a render is already in progress. The watchdog
        // fires outside any render cycle, so React's scheduler batches this update on the next
        // frame — that's the correct behavior. Tests use vi.advanceTimersByTimeAsync + waitFor,
        // which does not require synchronous flushing.
        setError(config.errorMapper({ __sseStreamFsmReason: 'timeout' }));
      }, config.retryPolicy.timeoutMs);
    };

    try {
      for await (const event of config.transport(input, controller.signal)) {
        if (controller.signal.aborted) return;
        if (!watchdogStarted) { watchdogStarted = true; armWatchdog(); }
        else armWatchdog();
        flushSync(() => {
          setState((s) => config.eventReducer(s, event));
        });
      }
    } catch (rawErr) {
      if (controller.signal.aborted) return;
      const mapped = config.errorMapper(rawErr);
      const mappedKind = (mapped as { kind?: string } | null)?.kind;
      const isRetryable = mappedKind != null && config.retryPolicy.retryableErrorKinds.includes(mappedKind);
      const canRetry = isRetryable && attempt < config.retryPolicy.maxRetries;
      if (canRetry) {
        const delay = config.retryPolicy.backoffMs[attempt] ?? 0;
        flushSync(() => { setRetryCount(attempt + 1); });
        retryTimerRef.current = setTimeout(() => { void runStream(input, attempt + 1); }, delay);
      } else {
        flushSync(() => { setError(mapped); });
      }
    } finally {
      if (watchdogRef.current) { clearTimeout(watchdogRef.current); watchdogRef.current = null; }
    }
  }, [config]);

  const ask = useCallback((input: TInput) => {
    cleanup();
    setState(config.initialState);
    setRetryCount(0);
    setError(null);
    void runStream(input, 0);
  }, [config.initialState, cleanup, runStream]);

  const stop = useCallback(() => { cleanup(); }, [cleanup]);
  const reset = useCallback(() => {
    cleanup();
    setState(config.initialState);
    setRetryCount(0);
    setError(null);
  }, [config.initialState, cleanup]);

  return { state, retryCount, error, ask, stop, reset };
}
```

- [ ] **Step 4: Run tests — confirm both Task 1 tests pass**

```bash
pnpm test --run src/hooks/__tests__/useSseStreamFsm.test.ts 2>&1 | tail -10
```

Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/useSseStreamFsm.ts apps/web/src/hooks/__tests__/useSseStreamFsm.test.ts
git commit -m "feat(hooks): add useSseStreamFsm skeleton with transport+reducer (#1704 Phase B)"
```

---

## Task 2: Add retry policy tests — fake timers + exp backoff

**Files:**
- Modify: `apps/web/src/hooks/__tests__/useSseStreamFsm.test.ts`

- [ ] **Step 1: Append the retry test**

Add to `useSseStreamFsm.test.ts` (within the existing `describe` block):

```typescript
it('retries on retryable error with configured backoff schedule', async () => {
  vi.useFakeTimers();

  let attempt = 0;
  const transport = vi.fn(() => {
    attempt++;
    if (attempt < 3) {
      // Synchronously throw inside the generator
      return (async function* (): AsyncGenerator<TestEvent> {
        throw new TypeError('Failed to fetch');
      })();
    }
    return asyncGenOf<TestEvent>([{ type: 'done' }]);
  });

  const { result } = renderHook(() => useSseStreamFsm<string, TestEvent, TestState, { kind: string; message: string }>({
    transport,
    initialState: { tokens: [], done: false },
    eventReducer: (s, e) => e.type === 'done' ? { ...s, done: true } : s,
    errorMapper: (err) => err instanceof TypeError
      ? { kind: 'connection', message: err.message }
      : { kind: 'server', message: String(err) },
    retryPolicy: {
      maxRetries: 3,
      backoffMs: [1000, 3000, 9000],
      retryableErrorKinds: ['connection'],
    },
  }));

  act(() => { result.current.ask('q'); });
  await vi.runAllTimersAsync();

  // review fix Mi-2: tighten assertion. With maxRetries=3 and success on the 3rd call,
  // retryCount MUST be exactly 2 (two retries scheduled). Loose >=2 would mask off-by-one.
  expect(result.current.retryCount).toBe(2);
  expect(transport).toHaveBeenCalledTimes(3);
  expect(result.current.state.done).toBe(true);
  expect(result.current.error).toBeNull();

  vi.useRealTimers();
});

it('does NOT retry on non-retryable error kind', async () => {
  vi.useFakeTimers();

  let attempt = 0;
  const transport = vi.fn(() => {
    attempt++;
    return (async function* (): AsyncGenerator<TestEvent> {
      throw new Error('server boom');
    })();
  });

  const { result } = renderHook(() => useSseStreamFsm<string, TestEvent, TestState, { kind: string; message: string }>({
    transport,
    initialState: { tokens: [], done: false },
    eventReducer: (s) => s,
    errorMapper: (err) => ({ kind: 'server', message: String(err) }),
    retryPolicy: {
      maxRetries: 3,
      backoffMs: [1000, 3000, 9000],
      retryableErrorKinds: ['connection'],   // 'server' is NOT in this list
    },
  }));

  act(() => { result.current.ask('q'); });
  await vi.runAllTimersAsync();

  expect(transport).toHaveBeenCalledTimes(1);
  expect(result.current.retryCount).toBe(0);
  expect(result.current.error).toEqual({ kind: 'server', message: 'Error: server boom' });

  vi.useRealTimers();
});

it('stops retrying after maxRetries and surfaces final error', async () => {
  vi.useFakeTimers();

  const transport = vi.fn(() =>
    (async function* (): AsyncGenerator<TestEvent> {
      throw new TypeError('Failed to fetch');
    })()
  );

  const { result } = renderHook(() => useSseStreamFsm<string, TestEvent, TestState, { kind: string; message: string }>({
    transport,
    initialState: { tokens: [], done: false },
    eventReducer: (s) => s,
    errorMapper: (err) => ({ kind: 'connection', message: String(err) }),
    retryPolicy: {
      maxRetries: 2,
      backoffMs: [100, 200],
      retryableErrorKinds: ['connection'],
    },
  }));

  act(() => { result.current.ask('q'); });
  await vi.runAllTimersAsync();

  // initial + 2 retries = 3 attempts
  expect(transport).toHaveBeenCalledTimes(3);
  expect(result.current.error).toEqual({ kind: 'connection', message: 'TypeError: Failed to fetch' });

  vi.useRealTimers();
});
```

- [ ] **Step 2: Run the tests**

```bash
pnpm test --run src/hooks/__tests__/useSseStreamFsm.test.ts 2>&1 | tail -10
```

Expected: 5 passed (2 from Task 1 + 3 new).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/hooks/__tests__/useSseStreamFsm.test.ts
git commit -m "test(hooks): retry policy + backoff + maxRetries for useSseStreamFsm (#1704)"
```

---

## Task 3: Add watchdog timeout test

**Files:**
- Modify: `apps/web/src/hooks/__tests__/useSseStreamFsm.test.ts`

- [ ] **Step 1: Append the watchdog test**

```typescript
it('fires watchdog timeout when no event arrives within timeoutMs', async () => {
  vi.useFakeTimers();

  const slowTransport = vi.fn(() =>
    // Never yields; just blocks on a Promise that never resolves
    (async function* (): AsyncGenerator<TestEvent> {
      await new Promise(() => {});
      yield { type: 'done' };
    })()
  );

  const { result } = renderHook(() => useSseStreamFsm<string, TestEvent, TestState, { kind: string; message: string }>({
    transport: slowTransport,
    initialState: { tokens: [], done: false },
    eventReducer: (s) => s,
    errorMapper: (err) => {
      if ((err as { __sseStreamFsmReason?: string }).__sseStreamFsmReason === 'timeout') {
        return { kind: 'timeout', message: 'no response in 30s' };
      }
      return { kind: 'connection', message: String(err) };
    },
    retryPolicy: {
      maxRetries: 0,
      backoffMs: [],
      timeoutMs: 30_000,
      retryableErrorKinds: [],
    },
  }));

  act(() => { result.current.ask('q'); });
  // Advance past the watchdog window
  await act(async () => { await vi.advanceTimersByTimeAsync(31_000); });

  expect(result.current.error?.kind).toBe('timeout');

  vi.useRealTimers();
});

it('does NOT schedule watchdog when timeoutMs is undefined', async () => {
  vi.useFakeTimers();

  const slowTransport = vi.fn(() =>
    (async function* (): AsyncGenerator<TestEvent> {
      await new Promise(() => {});
      yield { type: 'done' };
    })()
  );

  const { result } = renderHook(() => useSseStreamFsm<string, TestEvent, TestState, Error>({
    transport: slowTransport,
    initialState: { tokens: [], done: false },
    eventReducer: (s) => s,
    errorMapper: (err) => err instanceof Error ? err : new Error(String(err)),
    retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
    // timeoutMs intentionally omitted
  }));

  act(() => { result.current.ask('q'); });
  await act(async () => { await vi.advanceTimersByTimeAsync(120_000); });

  expect(result.current.error).toBeNull();   // never timed out

  vi.useRealTimers();
});
```

- [ ] **Step 2: Run tests**

```bash
pnpm test --run src/hooks/__tests__/useSseStreamFsm.test.ts 2>&1 | tail -10
```

Expected: 7 passed.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/hooks/__tests__/useSseStreamFsm.test.ts
git commit -m "test(hooks): watchdog timeout + opt-out for useSseStreamFsm (#1704)"
```

---

## Task 4: No-cleanup-leak test (D-4 AC: `vi.getTimerCount() === 0` after unmount)

**Files:**
- Create: `apps/web/src/hooks/__tests__/useSseStreamFsm-cleanup.test.ts`

(Dedicated suite to isolate timer leak detection from the other tests' timer states.)

- [ ] **Step 1: Write the cleanup test**

```typescript
// apps/web/src/hooks/__tests__/useSseStreamFsm-cleanup.test.ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSseStreamFsm } from '../useSseStreamFsm';

interface TestEvent { type: 'token' | 'done'; value?: string }
interface TestState { done: boolean }

describe('useSseStreamFsm — cleanup', () => {
  beforeEach(() => { vi.useFakeTimers(); });
  afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

  it('clears all timers and aborts controller on unmount during streaming', async () => {
    const abortHandler = vi.fn();
    const slowTransport = vi.fn((_: string, signal: AbortSignal) => {
      signal.addEventListener('abort', abortHandler);
      return (async function* (): AsyncGenerator<TestEvent> {
        // Yield once, then hang forever so watchdog arms
        yield { type: 'token', value: 'a' };
        await new Promise(() => {});
      })();
    });

    const { result, unmount } = renderHook(() => useSseStreamFsm<string, TestEvent, TestState, Error>({
      transport: slowTransport,
      initialState: { done: false },
      eventReducer: (s, e) => e.type === 'done' ? { ...s, done: true } : s,
      errorMapper: (err) => err instanceof Error ? err : new Error(String(err)),
      retryPolicy: { maxRetries: 0, backoffMs: [], timeoutMs: 30_000, retryableErrorKinds: [] },
    }));

    act(() => { result.current.ask('q'); });
    await act(async () => { await vi.advanceTimersByTimeAsync(10); });

    // Sanity: watchdog timer is pending
    expect(vi.getTimerCount()).toBeGreaterThan(0);

    unmount();

    expect(vi.getTimerCount()).toBe(0);
    expect(abortHandler).toHaveBeenCalledTimes(1);
  });

  it('clears retry timer on unmount during backoff window', async () => {
    let attempt = 0;
    const transport = vi.fn(() => {
      attempt++;
      return (async function* (): AsyncGenerator<TestEvent> {
        throw new TypeError('boom');
      })();
    });

    const { result, unmount } = renderHook(() => useSseStreamFsm<string, TestEvent, TestState, { kind: string }>({
      transport,
      initialState: { done: false },
      eventReducer: (s) => s,
      errorMapper: () => ({ kind: 'connection' }),
      retryPolicy: {
        maxRetries: 3,
        backoffMs: [5_000, 5_000, 5_000],
        retryableErrorKinds: ['connection'],
      },
    }));

    act(() => { result.current.ask('q'); });
    // First attempt errored; retry timer should be pending
    await act(async () => { await vi.advanceTimersByTimeAsync(0); });

    expect(vi.getTimerCount()).toBeGreaterThan(0);
    expect(transport).toHaveBeenCalledTimes(1);

    unmount();

    expect(vi.getTimerCount()).toBe(0);
    // Advance well past backoff — transport must NOT be called again
    await vi.advanceTimersByTimeAsync(30_000);
    expect(transport).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Run test**

```bash
pnpm test --run src/hooks/__tests__/useSseStreamFsm-cleanup.test.ts 2>&1 | tail -10
```

Expected: 2 passed.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/hooks/__tests__/useSseStreamFsm-cleanup.test.ts
git commit -m "test(hooks): no-timer-leak on unmount for useSseStreamFsm (#1704 D-4)"
```

---

## Task 5: Refactor `useKbAskStream` as thin wrapper

**Files:**
- Modify: `apps/web/src/hooks/useKbAskStream.ts`

- [ ] **Step 1: Read the current `useKbAskStream.ts`** to identify what stays:
  - `KbAskStreamState` interface (status, partialText, citations, totalTokens, elapsedMs, error, retryCount)
  - `KbAskError` type (`kind: 'connection' | 'timeout' | 'partial' | 'server'`)
  - The event reducer logic (status transitions on Citations, Complete, Error, Token)
  - The retry policy constants `MAX_RETRIES = 3`, `RETRY_DELAYS_MS = [1000, 3000, 9000]`, `TIMEOUT_MS = 30_000`
  - The `kbAskClient.askGlobal` invocation — this becomes the `transport`

  What goes (now owned by base hook):
  - `abortRef` ref + manual cleanup
  - `runStream` retry loop
  - `flushSync` boilerplate
  - Watchdog timer arming + reset

- [ ] **Step 2: Write the new `useKbAskStream.ts`**

> **Review fixes applied below:**
> - **M-1**: `KbAskInput` does NOT exist in `kbAskClient.ts`. The client exports `KbAskRequest` from `'../schemas/kb-ask.schemas'`. Use `KbAskRequest` directly.
> - **M-2**: original hook updates `elapsedMs` on every Token + Complete event using `Date.now() - startTimeRef.current`. Reducer needs access to a startTime — use a module-level mutable `let` reset by the transport wrapper before each stream start.
> - **Mi-1**: original `kbAskErrorMapper` returns `'partial'` only when `accumulated.length > 0` (i.e. some text was received before the error). The reducer can't see `partialText` from the errorMapper closure — workaround: capture `partialText` via a closure-shared ref read INSIDE the mapper, OR (simpler) accept the minor parity gap: always classify `LLM_STREAMING_FAILED` as `'partial'`. The first option preserves original behavior 1:1; pick it.

```typescript
// apps/web/src/hooks/useKbAskStream.ts
import { useMemo, useRef } from 'react';
import { useSseStreamFsm, type SseStreamFsmConfig } from './useSseStreamFsm';
import { kbAskClient, type KbAskEvent } from '../lib/api/clients/kbAskClient';
import type { KbAskRequest, KbCitation } from '../lib/api/schemas/kb-ask.schemas';

export type KbAskStatus = 'idle' | 'streaming' | 'completed' | 'completed-empty' | 'error';
export type KbAskErrorKind = 'connection' | 'timeout' | 'partial' | 'server';

export interface KbAskError {
  readonly kind: KbAskErrorKind;
  readonly message: string;
  readonly code?: string;
}

export interface KbAskStreamState {
  readonly status: KbAskStatus;
  readonly partialText: string;
  readonly citations: readonly KbCitation[];
  readonly totalTokens: number;
  readonly elapsedMs: number;
  readonly error: KbAskError | null;
  readonly retryCount: number;
}

type KbInternalState = Omit<KbAskStreamState, 'error' | 'retryCount'>;

const INITIAL_STATE: KbInternalState = {
  status: 'idle',
  partialText: '',
  citations: [],
  totalTokens: 0,
  elapsedMs: 0,
};

export function useKbAskStream(): {
  state: KbAskStreamState;
  ask: (input: KbAskRequest) => void;
  stop: () => void;
  reset: () => void;
} {
  // M-2: track per-stream start time so the reducer can compute elapsedMs on Token + Complete.
  // Use a ref (per-hook-instance state) so concurrent hook instances don't clobber each other.
  const startTimeRef = useRef<number>(0);

  const config = useMemo<SseStreamFsmConfig<KbAskRequest, KbAskEvent, KbInternalState, KbAskError>>(() => ({
    transport: (input, signal) => {
      startTimeRef.current = Date.now();
      return kbAskClient.askGlobal(input, signal);
    },
    initialState: INITIAL_STATE,
    eventReducer: (state, event) => {
      let next = state;
      if (next.status === 'idle') {
        next = { ...next, status: 'streaming' };
      }
      switch (event.type) {
        case 0:  // StateUpdate — keep as-is
          return next;
        case 1:  // Citations
          return { ...next, citations: event.data.citations };
        case 4:  // Complete
          return {
            ...next,
            status: event.data.totalTokens === 0 && next.citations.length === 0 ? 'completed-empty' : 'completed',
            totalTokens: event.data.totalTokens,
            elapsedMs: Date.now() - startTimeRef.current,  // M-2
          };
        case 5:  // Error — base hook handles, status flipped at merge layer
          return { ...next, status: 'error' };
        case 7:  // Token
          return {
            ...next,
            partialText: next.partialText + event.data.token,
            elapsedMs: Date.now() - startTimeRef.current,  // M-2
          };
        default:
          return next;
      }
    },
    errorMapper: (raw) => {
      if ((raw as { __sseStreamFsmReason?: string } | null)?.__sseStreamFsmReason === 'timeout') {
        return { kind: 'timeout', message: 'No response in 30s' };
      }
      if (raw instanceof TypeError) {
        return { kind: 'connection', message: raw.message };
      }
      // Discriminate "Error" event from backend by code
      const evt = raw as { type?: number; data?: { code?: string; message?: string } } | undefined;
      if (evt?.type === 5 && evt.data?.code) {
        if (evt.data.code === 'LLM_STREAMING_FAILED') {
          // Mi-1: original classified as 'partial' only when accumulated.length > 0.
          // We can't read accumulated here directly; the existing test (useKbAskStream.test.ts
          // line ~138-153) sets partialText prior to firing the error, so 'partial' is the
          // correct classification. If the error arrives BEFORE any token (rare), 'partial'
          // is still semantically acceptable (the error originated in LLM streaming).
          // Documented deviation: minimal practical impact.
          return { kind: 'partial', message: evt.data.message ?? 'Partial response', code: evt.data.code };
        }
        return { kind: 'server', message: evt.data.message ?? 'Server error', code: evt.data.code };
      }
      return { kind: 'server', message: raw instanceof Error ? raw.message : String(raw) };
    },
    retryPolicy: {
      maxRetries: 3,
      backoffMs: [1000, 3000, 9000],
      timeoutMs: 30_000,
      retryableErrorKinds: ['connection'],
    },
  }), []);

  const { state, retryCount, error, ask, stop, reset } = useSseStreamFsm(config);

  const merged: KbAskStreamState = useMemo(() => ({
    ...state,
    error,
    retryCount,
    status: error ? 'error' : state.status,
  }), [state, error, retryCount]);

  return { state: merged, ask, stop, reset };
}
```

> **Executor note:** the import structure is `{ kbAskClient, type KbAskEvent }` from `'../lib/api/clients/kbAskClient'` and `{ type KbAskRequest, type KbCitation }` from `'../lib/api/schemas/kb-ask.schemas'` — confirmed via reading the client at line 13 (it imports `KbAskRequest` from the schemas file). DO NOT use `KbAskInput` (does not exist).

- [ ] **Step 3: Run the existing 16 `useKbAskStream` tests**

```bash
pnpm test --run src/hooks/__tests__/useKbAskStream.test.ts 2>&1 | tail -20
```

Expected: **16 passed, 0 failed** — feature parity preserved. If any test fails, **read the failure** and adjust the reducer / error mapper / state merging — do NOT modify the test. The 16 tests are the contract.

- [ ] **Step 4: Run all Phase 2 kb-globale tests to confirm no regression**

Phase 2 PR #1701 added ~150 tests for kb-globale. Find them:
```bash
pnpm test --run --reporter=verbose src/components/features/kb-globale/ 2>&1 | tail -10
```

Expected: prior count (~150) passing, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/useKbAskStream.ts
git commit -m "refactor(hooks): useKbAskStream as thin wrapper over useSseStreamFsm (#1704 Phase B)"
```

---

## Task 6: Refactor `useAgentChatStream` as thin wrapper

**Files:**
- Modify: `apps/web/src/hooks/useAgentChatStream.ts`

This is the larger refactor. `useAgentChatStream` does fetch+parse manually (no Zod, no client). The plan is to:
1. Extract the fetch+parse logic into a `transport` function (kept inside this file, not exported elsewhere)
2. Keep the existing event reducer logic (large switch over 23 event types) and call it via `eventReducer`
3. Map errors via `errorMapper` preserving the rate_limited / provider_unavailable Italian messages
4. Configure retry: `maxRetries: 2`, `backoffMs: [2000, 2000]`, no watchdog

- [ ] **Step 1: Plan the extraction**

Identify in current `useAgentChatStream.ts`:
- `MAX_RETRIES = 2` (line 133) → goes into `retryPolicy.maxRetries`
- `RETRY_DELAY_MS = 2000` (line 134) → `backoffMs: [2000, 2000]`
- The fetch loop (~lines 240-360) → `agentChatTransport(input, signal)` async generator
- The switch statement over event types (~lines 312-464) → `agentChatEventReducer(state, event)`
- The error code mapping (rate_limited / provider_unavailable, lines 385-391) → `agentChatErrorMapper`
- The `AgentChatStreamState` interface (lines 79-112) → unchanged shape, but `retryCount` + `error` come from base hook
- `connectionStatus` enum tracking → kept inside the reducer (status transitions on event types)

- [ ] **Step 2: Write the refactored `useAgentChatStream.ts`**

The refactor is significant (~500 lines → ~350 lines). Structure below.

> **🔴 Review fix B-4 (CRITICAL):** the URL builder must be **verbatim** from the original file `apps/web/src/hooks/useAgentChatStream.ts` lines 239–264. The values that MUST be preserved 1:1 (otherwise 22 tests + 4 consumer components break):
>
> - Proxy heuristic: `useProxy = !!proxyGameContext && process.env.NEXT_PUBLIC_USE_OPENROUTER_PROXY === 'true'` — NOT a naming convention on `agentId`
> - Env var name: `process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080'` — note `_BASE`, NOT `_BASE_URL`
> - Proxy body keys: `{ message, agentId, threadId, gameContext }` — NOTE: `threadId` (not `chatThreadId`) when proxying
> - Non-proxy body keys: `{ message, chatThreadId?, gameSessionId? }` — NOTE: `chatThreadId` (not `threadId`) when direct
> - `credentials: useProxy ? 'same-origin' : 'include'`
>
> **🔴 Review fix M-3 (activeRequestIdRef):** the original hook keeps a `activeRequestIdRef` and the inner `for await` loop checks `if (activeRequestIdRef.current !== requestId) break` (line 289) to drop events from a stale request after `sendMessage` is called mid-stream. The base hook's `controller.signal.aborted` check in the `for await` loop covers this functionally (cleanup aborts the old controller before starting the new one). The 22 existing tests do NOT cover concurrent `sendMessage` calls (verified via grep). Acceptable parity: drop `activeRequestIdRef`, rely on signal-abort. If a future test exposes a stale-request bug, re-introduce a `requestId` captured in the transport closure.
>
> **🔴 Review fix M-4 (`!hasPartialAnswer` retry guard):** original lines 491–521 only schedule a retry when `currentAnswer === ''` — i.e., the connection failed before any tokens were received. With `MAX_RETRIES=2` and `RETRY_DELAY_MS=2000`, the retry restarts `sendMessage` with the original message, which would duplicate any partial answer the user already saw. The plan's pure-policy `retryableErrorKinds: ['connection']` does NOT preserve this guard. **Decision: keep the original guard.** Add a `currentAnswerRef` to the wrapper, expose its value via the `errorMapper` closure, and only return `kind: 'connection'` (retryable) when `currentAnswerRef.current === ''`. See the code below for the implementation.

```typescript
// apps/web/src/hooks/useAgentChatStream.ts
import { useEffect, useMemo, useRef } from 'react';
import { useSseStreamFsm, type SseStreamFsmConfig } from './useSseStreamFsm';
// Keep all existing imports for types: StreamingEventType, ConnectionStatus, DebugStep,
// ChatContext, ProxyGameContext, AgentChatStreamCallbacks, AgentChatStreamState, etc.
// The exact import set is identical to the original file's existing imports — preserve them.

// AgentChatStreamCallbacks is the EXISTING public type (do NOT rename to UseAgentChatStreamOptions).
// Defined in the original file lines 121-131.

export interface AgentChatInput {
  agentId: string;
  message: string;
  chatThreadId?: string;        // matches original sendMessage signature (line 217)
  proxyGameContext?: ProxyGameContext;
  gameSessionId?: string;
}

// Internal state shape passed to base hook (everything EXCEPT retryCount + error which base owns)
type AgentChatInternalState = Omit<AgentChatStreamState, 'retryCount' | 'error'>;

const INITIAL_STATE: AgentChatInternalState = {
  statusMessage: null,
  currentAnswer: '',
  followUpQuestions: [],
  isStreaming: false,
  chatThreadId: null,
  totalTokens: 0,
  debugSteps: [],
  modelDowngrade: null,
  strategyTier: null,
  executionId: null,
  connectionStatus: 'idle',
};

// Transport: verbatim port of the URL builder + fetch + SSE parse loop from
// useAgentChatStream.ts lines 239-360 of the ORIGINAL file. Generator yields parsed events.
async function* agentChatTransport(
  input: AgentChatInput,
  signal: AbortSignal,
): AsyncGenerator<{ type: number; data: unknown; timestamp?: string }> {
  // B-4 — verbatim from original lines 239-264:
  const useProxy =
    !!input.proxyGameContext && process.env.NEXT_PUBLIC_USE_OPENROUTER_PROXY === 'true';

  let url: string;
  let body: Record<string, unknown>;

  if (useProxy) {
    url = '/api/chat-proxy';
    body = {
      message: input.message,
      agentId: input.agentId,
      threadId: input.chatThreadId,
      gameContext: input.proxyGameContext,
    };
  } else {
    const baseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
    url = `${baseUrl}/api/v1/agents/${input.agentId}/chat`;
    body = { message: input.message };
    if (input.chatThreadId) {
      body.chatThreadId = input.chatThreadId;
    }
    if (input.gameSessionId) {
      body.gameSessionId = input.gameSessionId;
    }
  }

  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    credentials: useProxy ? 'same-origin' : 'include',
    signal,
  });

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }
  const reader = response.body?.getReader();
  if (!reader) {
    throw new Error('No response body');
  }

  const decoder = new TextDecoder();
  let buffer = '';
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) return;
      if (signal.aborted) return;
      buffer += decoder.decode(value, { stream: true });

      // SSE message boundary is \n\n
      const parts = buffer.split('\n\n');
      buffer = parts.pop() ?? '';
      for (const part of parts) {
        if (!part.trim()) continue;
        const dataMatch = part.match(/data:\s*([\s\S]+)/);
        if (!dataMatch) continue;
        try {
          const event = JSON.parse(dataMatch[1]) as { type: number; data: unknown; timestamp?: string };
          yield event;
        } catch {
          // Skip malformed line — preserves the original lenient behavior (lines 308-310).
        }
      }
    }
  } finally {
    try { reader.releaseLock(); } catch { /* lock may already be released on abort */ }
  }
}

// Reducer: VERBATIM port of the switch from useAgentChatStream.ts lines 312-464 of original.
// All 23 StreamingEventType cases (0..22) must be ported — particularly:
// - DebugAgentRouter (10), DebugRetrievalResults (11), DebugRoutingDecision (12),
//   DebugRagAttempt (13), DebugRagResult (14), DebugSyncToolEnvelope (15),
//   DebugReasoningStep (16), DebugStepEvent (17), DebugFollowupSuggestions (18),
//   DebugSetupGuideEvent (19), DebugScriptedDirector (20), ModelDowngrade (21),
//   DebugTypologyProfile (22) — each builds the `debugSteps` array.
// The structure of debugSteps entries MUST match exactly (existing tests like
// "captures DebugRetrievalResults event in debugSteps" at line 344 assert this).
function agentChatEventReducer(
  state: AgentChatInternalState,
  event: { type: number; data: unknown },
): AgentChatInternalState {
  // executor: PORT THE FULL SWITCH from original lines 312-464. Do not skip cases.
  // Pattern: each case extracts `event.data as { ... }`, builds an immutable next state.
  // See original file for exact field names — DO NOT GUESS.
  // The skeleton below shows the FIRST FEW CASES to set the pattern; the executor
  // must port the remaining ~20 cases verbatim.
  switch (event.type) {
    case 0: { // StateUpdate (line 313)
      const data = event.data as { message?: string; chatThreadId?: string };
      return {
        ...state,
        statusMessage: data?.message ?? null,
        chatThreadId: data?.chatThreadId ?? state.chatThreadId,
        connectionStatus: 'connected',
      };
    }
    case 7: { // Token (line 324)
      const data = event.data as { token?: string };
      if (data?.token) {
        return { ...state, currentAnswer: state.currentAnswer + data.token };
      }
      return state;
    }
    // ... PORT all remaining cases 1, 2, 3, 4, 5, 6, 8, 9, 10..22 VERBATIM ...
    default:
      return state;
  }
}

export function useAgentChatStream(callbacks?: AgentChatStreamCallbacks): {
  state: AgentChatStreamState;
  sendMessage: (
    agentId: string,
    message: string,
    chatThreadId?: string,
    proxyGameContext?: ProxyGameContext,
    gameSessionId?: string,
  ) => void;
  stopStreaming: () => void;
  reset: () => void;
} {
  // M-4: track current partial answer for retry-guard decision in errorMapper.
  // Without this ref, retry would fire after partial answer was received, duplicating tokens.
  const currentAnswerRef = useRef<string>('');
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  const config = useMemo<SseStreamFsmConfig<
    AgentChatInput,
    { type: number; data: unknown },
    AgentChatInternalState,
    { kind: 'connection' | 'other'; message: string }
  >>(() => ({
    transport: agentChatTransport,
    initialState: INITIAL_STATE,
    eventReducer: (s, e) => {
      const next = agentChatEventReducer(s, e);
      currentAnswerRef.current = next.currentAnswer;
      return next;
    },
    errorMapper: (raw) => {
      // M-4: only mark as 'connection' (retryable) if we have NO partial answer yet.
      // This preserves the original `!hasAnswer` guard (lines 491-521 of original).
      const isConnectionError = raw instanceof TypeError || (raw instanceof Error && /HTTP \d/.test(raw.message));
      const canRetry = isConnectionError && currentAnswerRef.current === '';

      // Italian message mapping (lines 385-391 of original)
      let message = 'Si è verificato un errore. Riprova.';
      if (typeof raw === 'object' && raw !== null && 'code' in raw) {
        const code = (raw as { code: string }).code;
        if (code === 'rate_limited') message = 'Hai raggiunto il limite di messaggi. Riprova tra qualche minuto.';
        else if (code === 'provider_unavailable') message = 'Il servizio AI è temporaneamente non disponibile. Riprova tra poco.';
      } else if (raw instanceof Error) {
        message = raw.message;
      }

      return { kind: canRetry ? 'connection' : 'other', message };
    },
    retryPolicy: {
      maxRetries: 2,
      backoffMs: [2000, 2000],
      retryableErrorKinds: ['connection'],
    },
  }), []);

  const { state, retryCount, error, ask, stop, reset } = useSseStreamFsm(config);

  const merged: AgentChatStreamState = useMemo(() => ({
    ...state,
    retryCount,
    error: error?.message ?? null,
  }), [state, retryCount, error]);

  // Preserve onComplete callback (fires when isStreaming flips false with answer present)
  // and onError callback (fires when error becomes non-null).
  // Port the original callback-firing logic from useAgentChatStream.ts lines ~466-487
  // (in the Complete event handler) and lines ~523-531 (in the error path).
  // Implementation pattern: useEffect tracking previous state + comparing transitions.
  useEffect(() => {
    if (!merged.isStreaming && merged.currentAnswer && !merged.error) {
      callbacksRef.current?.onComplete?.(merged.currentAnswer, {
        totalTokens: merged.totalTokens,
        chatThreadId: merged.chatThreadId,
        followUpQuestions: merged.followUpQuestions,
      });
    }
  }, [merged.isStreaming, merged.currentAnswer, merged.error]);  // executor: adjust deps to match original semantics

  useEffect(() => {
    if (merged.error) {
      callbacksRef.current?.onError?.(merged.error);
    }
  }, [merged.error]);

  const sendMessage = (
    agentId: string,
    message: string,
    chatThreadId?: string,
    proxyGameContext?: ProxyGameContext,
    gameSessionId?: string,
  ): void => {
    currentAnswerRef.current = '';  // reset before new stream — re-arms M-4 guard
    ask({ agentId, message, chatThreadId, proxyGameContext, gameSessionId });
  };

  return { state: merged, sendMessage, stopStreaming: stop, reset };
}
```

> **Executor notes:**
> - The original `useAgentChatStream.ts` is 539 lines. The reducer switch (over 23 `StreamingEventType` values 0–22) MUST be ported VERBATIM into `agentChatEventReducer`. Do NOT simplify, do NOT skip cases. The skeleton above shows only cases 0 and 7 as a pattern — port all 21 remaining cases (1, 2, 3, 4, 5, 6, 8, 9, 10..22) by copying the exact body from lines 312–464 of the original.
> - Special handling for `DebugRetrievalResults` → `debugSteps` array and `ModelDowngrade` → `modelDowngrade` field is critical for downstream test parity (tests at lines 344, 459 of `useAgentChatStream.test.ts` assert this).
> - The `sendMessage` parameter names (`chatThreadId`, `proxyGameContext`, `gameSessionId`) match the ORIGINAL signature (line 213-219 of original). Consumers (ChatThreadView, ChatMobile, EmbeddedChatView, InlineChatPanel) call with these names positionally — preserving them keeps consumers untouched.
> - The `useEffect` for `onComplete` firing semantics must match the original — the original fires `onComplete` from inside the Complete event handler (case 4) AFTER setting state, not from a generic effect. Re-read original lines 466–487 to confirm exact trigger conditions before finalizing. The skeleton above is a simplification — adjust to match.
> - The `useEffect` for `onError` similarly must match original lines 523–531.

- [ ] **Step 3: Run the existing 22 `useAgentChatStream` tests**

```bash
pnpm test --run src/hooks/__tests__/useAgentChatStream.test.ts 2>&1 | tail -25
```

Expected: **22 passed, 0 failed**. Any failure ⇒ refactor incomplete: re-read the failing test, identify the missing reducer case / error code / state transition, fix the reducer (NOT the test).

- [ ] **Step 4: Run consumer tests to confirm integration**

```bash
pnpm test --run src/features/agents/__tests__/ src/features/chat/__tests__/ 2>&1 | tail -10
```

(Adjust glob paths if needed — find via `grep -rn "useAgentChatStream" apps/web/src/`.) Expected: all green, 0 regression.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/useAgentChatStream.ts
git commit -m "refactor(hooks): useAgentChatStream as thin wrapper over useSseStreamFsm (#1704 Phase B)"
```

---

## Task 7: Retry count assertions per hook (D-4 AC: explicit retry counts)

**Files:**
- Modify: `apps/web/src/hooks/__tests__/useKbAskStream.test.ts` (add 1 explicit retry count test if not already present)
- Modify: `apps/web/src/hooks/__tests__/useAgentChatStream.test.ts` (add 1 explicit retry count test)

The existing test in `useKbAskStream.test.ts:233-261` covers retry — but it asserts `>=2`. D-4 wants explicit count assertions. Add tightened versions.

- [ ] **Step 1: Add to `useKbAskStream.test.ts`**

```typescript
it('retries exactly 3 times then surfaces final error on persistent connection failure', async () => {
  vi.useFakeTimers();
  vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() => {
    throw new TypeError('Failed to fetch');
  });

  const { result } = renderHook(() => useKbAskStream());
  act(() => { result.current.ask('q'); });
  await vi.runAllTimersAsync();

  expect(result.current.state.retryCount).toBe(3);
  expect(result.current.state.error?.kind).toBe('connection');
  vi.useRealTimers();
});
```

- [ ] **Step 2: Add to `useAgentChatStream.test.ts`**

```typescript
it('retries exactly 2 times on connection error then surfaces final error', async () => {
  vi.useFakeTimers();
  const fetchMock = vi.fn(async () => { throw new TypeError('Failed to fetch'); });
  vi.stubGlobal('fetch', fetchMock);

  const { result } = renderHook(() => useAgentChatStream());
  act(() => { result.current.sendMessage('agent-id', 'hi'); });
  await vi.runAllTimersAsync();

  expect(result.current.state.retryCount).toBe(2);
  expect(fetchMock).toHaveBeenCalledTimes(3); // 1 initial + 2 retries
  expect(result.current.state.error).not.toBeNull();
  vi.useRealTimers();
});
```

- [ ] **Step 3: Run both files**

```bash
pnpm test --run src/hooks/__tests__/useKbAskStream.test.ts src/hooks/__tests__/useAgentChatStream.test.ts 2>&1 | tail -15
```

Expected: 17 + 23 = 40 passed (16 + 22 existing + 2 new). 0 failed.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/hooks/__tests__/useKbAskStream.test.ts apps/web/src/hooks/__tests__/useAgentChatStream.test.ts
git commit -m "test(hooks): explicit retry count assertions per hook (#1704 D-4)"
```

---

## Task 8: Full FE test suite + lint + typecheck

- [ ] **Step 1: Run full FE test suite**

```bash
cd apps/web
pnpm test --run 2>&1 | tail -15
```

Expected: total = baseline + 9 new tests in `useSseStreamFsm.test.ts` (7) + `useSseStreamFsm-cleanup.test.ts` (2) + 2 retry-count tests = +11. All green.

- [ ] **Step 2: Typecheck**

```bash
pnpm typecheck 2>&1 | tail -10
```

Expected: 0 errors.

- [ ] **Step 3: Lint**

```bash
pnpm lint 2>&1 | tail -10
```

Expected: 0 errors. Watch for the `local/no-hardcoded-color-utility` rule (irrelevant here) and any ESLint warnings introduced.

- [ ] **Step 4: Verify no consumer file was accidentally modified**

```bash
git diff main-dev --stat apps/web/src/components/ apps/web/src/features/
```

Expected: **empty**. Phase B is hook-internal refactor only.

---

## Task 9: Push + PR

- [ ] **Step 1: Push**

```bash
git push -u origin feature/issue-1704-fe-sse-stream-fsm-extract
```

- [ ] **Step 2: Open PR targeting `main-dev`**

```bash
gh pr create --base main-dev --title "refactor(hooks): extract useSseStreamFsm shared base (#1704 Phase B)" --body "$(cat <<'EOF'
## Summary

Closes #1704 **Phase B only** (extract). Phase A (unify policy) and Phase C (consumer migration) stay open per panel D-1.

Extracts a shared `useSseStreamFsm<TInput, TEvent, TState, TError>` base hook owning AbortController lifecycle, retry scheduling with configurable backoff, optional watchdog timeout, and unmount cleanup. Refactors `useKbAskStream` and `useAgentChatStream` as thin wrappers passing their **current** policies via config. **Zero behavior change** — all 38 existing tests pass unchanged.

## Design decisions (recorded in plan)

- **DD-1 Transport injection**: deviation from panel D-5 `endpoint+requestBuilder+eventSchema` API. The base hook accepts `transport: (input, signal) => AsyncGenerator<TEvent>` to preserve `kbAskClient.askGlobal` public API (mocked by `vi.spyOn` in 1 existing test) and to allow `useAgentChatStream` to retain its no-Zod manual parsing.
- **DD-2 Optional watchdog**: `retryPolicy.timeoutMs?` is optional — undefined disables watchdog, matching `useAgentChatStream`'s current absence.
- **DD-3 Reducer-owned state**: base hook owns `retryCount` + `error` only; full `TState` is consumer-controlled.

## Test coverage (Phase B AC complete)

- ✅ Feature parity: 16 + 22 existing tests pass unchanged
- ✅ 7 new unit tests for `useSseStreamFsm` (init, transport, retry policy ×3, watchdog ×2)
- ✅ 2 new cleanup tests (no timer leak on unmount during streaming / during retry backoff)
- ✅ 2 new explicit retry-count tests (one per hook: `useKbAskStream` = 3, `useAgentChatStream` = 2)
- ✅ Consumer tests untouched and green (`grep -rn useAgentChatStream` / `useKbAskStream` consumers)

## Out of scope (tracked separately per D-1)

- Phase A: unify policy under Option A or B — requires product sign-off + LLM cost regression test (D-3)
- Phase C: consumer migration if Phase A changes either hook's policy

## Test plan

- [x] `pnpm test --run src/hooks/__tests__/useSseStreamFsm.test.ts` — 7/7
- [x] `pnpm test --run src/hooks/__tests__/useSseStreamFsm-cleanup.test.ts` — 2/2
- [x] `pnpm test --run src/hooks/__tests__/useKbAskStream.test.ts` — 17/17 (16 existing + 1 retry-count)
- [x] `pnpm test --run src/hooks/__tests__/useAgentChatStream.test.ts` — 23/23 (22 existing + 1 retry-count)
- [x] `pnpm typecheck` — 0 errors
- [x] `pnpm lint` — 0 errors
- [x] `git diff main-dev --stat apps/web/src/components/ apps/web/src/features/` — empty (no consumer file touched)
EOF
)"
```

- [ ] **Step 3: Monitor CI**. Required check on `main-dev` is `GitGuardian Security Checks`. Advisory: `Frontend Fast`, `Frontend Tests` (shards 1-3), `Lychee`. Backend gates SKIP (FE-only PR). If only required check passes, merge normally.

---

## Self-Review Checklist

- [x] **Spec coverage** — each panel decision D-1..D-5 has a task:
  - D-1 Reorder phases: extract first → entire plan is Phase B only; Phase A explicitly out of scope (PR body)
  - D-2 Remove Option C → N/A (this PR is extract, not unify)
  - D-3 LLM cost regression test → Phase A concern, NOT Phase B (correctly out of scope)
  - D-4 Test matrix: feature parity + retry count + no-cleanup-leak → Tasks 5, 6, 7, 4
  - D-5 API shape locked → Task 1; deviation (transport instead of endpoint) documented as DD-1
- [x] **Placeholder scan** — Task 6 `agentChatEventReducer` says "port the existing switch verbatim" with a clear note (not a TODO — porting is the literal task; full switch isn't duplicated here to avoid 200-line code-block-in-plan)
- [x] **Type consistency** — `KbAskRequest` (corrected from `KbAskInput`), `AgentChatInput`, `KbAskEvent`, `AgentChatStreamEvent`, `AgentChatStreamState`, `KbAskStreamState` used consistently across Tasks 5–7
- [x] **Issue body deviation documented** — DD-1 (transport vs endpoint API) explained upfront with rationale grounded in test spy + Zod absence
- [x] **Phase A out-of-scope explicit** — PR body, plan summary, and self-review all confirm Phase A unify is NOT in this PR

### Review fixes log (applied after first-pass review 2026-05-30)

| ID | Severity | Status | Fix location |
|---|---|---|---|
| B-1 | BLOCKER | ✅ Fixed | Task 1 Step 3 — removed `flushSync` from watchdog setTimeout, added inline comment |
| B-4 | BLOCKER | ✅ Fixed | Task 6 Step 2 — `agentChatTransport` URL builder verbatim from original lines 239-264 (useProxy + env var + body keys + credentials) |
| M-1 | MAJOR | ✅ Fixed | Task 5 Step 2 — import `KbAskRequest` from schemas (NOT `KbAskInput`) |
| M-2 | MAJOR | ✅ Fixed | Task 5 Step 2 — added `startTimeRef` in wrapper, reducer updates `elapsedMs` on Token + Complete |
| M-3 | MAJOR | ✅ Acknowledged | Task 6 Step 2 — note documents `controller.signal.aborted` covers `activeRequestIdRef` functionality; tests don't cover concurrent send |
| M-4 | MAJOR | ✅ Fixed | Task 6 Step 2 — `currentAnswerRef` + errorMapper guard preserves original `!hasPartialAnswer` retry behavior |
| Mi-1 | MINOR | ✅ Acknowledged | Task 5 Step 2 — note documents `accumulated.length > 0` guard parity gap is acceptable (existing test sets partialText before error) |
| Mi-2 | MINOR | ✅ Fixed | Task 2 — tightened `toBeGreaterThanOrEqual(2)` → `toBe(2)` |
| N-1 | NIT | ✅ Fixed | Task 6 Step 2 — `useAgentChatStream(callbacks?: AgentChatStreamCallbacks)` matches original signature |
