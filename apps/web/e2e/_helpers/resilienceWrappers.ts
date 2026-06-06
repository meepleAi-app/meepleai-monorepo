/**
 * Issue #1929 Task C (DEC-C-6) — Resilience wrappers for E2E spec calls.
 *
 * **Pattern**: retry exactly **1 time** with **500ms default backoff** (override
 * via `backoffMs`), then **loud fail** with aggregate error message including
 * both first + second failure detail.
 *
 * **Applied to** (per spec):
 *   - `seedGameNight/Session/Player` (transient network)
 *   - Wizard step transitions (race condition mitigation)
 *   - Drawer cascade push (level N+1 settle wait)
 *
 * **NOT applied to**:
 *   - Login flow (`seedAuthSession` is sync via cookie addCookies)
 *   - Pure DOM assertions (no retry, fail fast — `expect(...).toBe(...)`)
 *
 * Spec ref: `docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md` DEC-C-6.
 */

export interface WithRetryOptions {
  /** Human-readable label for diagnostics in loud-fail error message. */
  readonly reason: string;
  /** Milliseconds between first failure and retry. Default: 500. */
  readonly backoffMs?: number;
}

const DEFAULT_BACKOFF_MS = 500;

export async function withRetry<T>(fn: () => Promise<T>, options: WithRetryOptions): Promise<T> {
  const backoff = options.backoffMs ?? DEFAULT_BACKOFF_MS;
  try {
    return await fn();
  } catch (firstError) {
    await new Promise<void>(r => setTimeout(r, backoff));
    try {
      return await fn();
    } catch (secondError) {
      const firstMsg = firstError instanceof Error ? firstError.message : String(firstError);
      const secondMsg = secondError instanceof Error ? secondError.message : String(secondError);
      throw new Error(
        `Test action failed twice (reason: ${options.reason}). ` +
          `First: ${firstMsg}. Second: ${secondMsg}.`
      );
    }
  }
}
