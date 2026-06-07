/**
 * Issue #1929 Task C (DEC-C-2) — Hybrid assertion taxonomy helpers.
 *
 * **Strict literal assertions** for discrete state:
 *   - Drawer stack depth
 *   - URL (exact string or regex)
 *   - DB cleanup row counts
 *   - Element counts
 *
 * **Functional assertions** for continuous state:
 *   - Focus management (matching selector, not literal equality)
 *   - Scroll position threshold
 *   - Animation completion flag
 *
 * **Banditi pattern tolerant fallback** (DEC-C-2 explicit ban):
 *   - ❌ `Promise.race([sidebar, loginForm])`
 *   - ❌ Conditional URL branching with divergent expectations
 *   - ❌ Optional chaining `page.locator(...)?.click()`
 *
 * Spec ref: `docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md` DEC-C-2.
 */

// ============================================================================
// Strict literal assertions
// ============================================================================

export function assertExactStackDepth(actual: number, expected: number): void {
  if (actual !== expected) {
    throw new Error(
      `[strict] drawer stack depth assertion failed: expected ${expected}, got ${actual}`
    );
  }
}

export function assertExactUrl(actual: string, expected: string | RegExp): void {
  if (typeof expected === 'string') {
    if (actual !== expected) {
      throw new Error(`[strict] url assertion failed: expected "${expected}", got "${actual}"`);
    }
    return;
  }
  if (!expected.test(actual)) {
    throw new Error(
      `[strict] url assertion failed: expected match ${expected.source}, got "${actual}"`
    );
  }
}

export function assertExactCount(actual: number, expected: number, label: string): void {
  if (actual !== expected) {
    throw new Error(
      `[strict] count assertion failed for "${label}": expected ${expected}, got ${actual}`
    );
  }
}

// ============================================================================
// Functional assertions
// ============================================================================

/**
 * Returns true when the focused element matches the CSS selector.
 *
 * Functional (NOT strict): we do not assert identity (`focused === literalEl`)
 * because drawer push/pop can mount a new focus-trap element that is
 * conceptually "the same" but DOM-different. Selector match is the right
 * granularity for the cascade flow.
 */
export function assertFunctionalFocus(focused: Element | null, selector: string): boolean {
  if (focused == null) return false;
  // Re-check via matches() — selector might use `[data-...]` attributes
  // that JSDOM-style Element shims may not preserve. We swallow the
  // Element typing here because the runtime call is what matters.
  try {
    const el = focused as HTMLElement & { matches?: (s: string) => boolean };
    if (typeof el.matches === 'function') {
      return el.matches(selector) === true;
    }
    // Fallback for plain-object shims (test environments without a DOM):
    // parse simple `[data-testid="value"]` selectors via dataset lookup.
    const dataAttrMatch = /^\[data-([a-z-]+)="([^"]+)"\]$/.exec(selector);
    if (dataAttrMatch) {
      const [, attrName, attrValue] = dataAttrMatch;
      const camelKey = attrName.replace(/-([a-z])/g, (_, c: string) => c.toUpperCase());
      return (
        (el as unknown as { dataset?: Record<string, string> }).dataset?.[camelKey] === attrValue
      );
    }
    return false;
  } catch {
    return false;
  }
}

/**
 * Returns true when scrollY is greater than the threshold (no literal pixel).
 */
export function assertFunctionalScroll(scrollY: number, thresholdPx: number): boolean {
  return scrollY > thresholdPx;
}
