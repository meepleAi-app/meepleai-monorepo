// hooks/devWarn.ts

/**
 * Shared dev-only warning helper with per-message deduplication.
 *
 * Granularity: dedups by message string (module-global Set). Appropriate for
 * warnings where the message itself is specific enough to identify the issue —
 * e.g. W2 (onClick without href, indexed), W3 (onPlusClick dropped, indexed),
 * W4 (dual source detected in useConnectionSource).
 *
 * NOT appropriate for W1 (deprecation of navItems/manaPips): spec §R1.6.4
 * requires per-MeepleCard-instance dedup, so the dispatcher in Task 5 uses a
 * separate `WeakSet<object>` keyed by the props object. Do NOT unify them.
 *
 * Silent in production.
 */
const seenMessages = new Set<string>();

/** Test-only helper to reset dedup state between tests. */
export function __resetDevWarnDedup(): void {
  seenMessages.clear();
}

export function devWarnOnce(msg: string): void {
  if (process.env.NODE_ENV === 'production') return;
  if (seenMessages.has(msg)) return;
  seenMessages.add(msg);
  console.warn(msg);
}
