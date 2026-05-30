/**
 * Unit tests for a11y-summarize — issue #1698.
 *
 * Validates:
 *   - Scenarios A/B/C/D (all-green / axe-only / flake-only / mixed)
 *   - exit codes per AC: 0 (none) / 1 (flake only) / 2 (any axe)
 *   - Determinism (AC #6): identical input → byte-identical output
 *   - Sort order: route → rule → selector for axe; spec → line → type for flakes
 *   - Workflow annotation format (::error:: for axe, ::warning:: for flake)
 *   - Dominant class rule: axe wins (AC #5) when both classes present
 */

import { describe, expect, it } from 'vitest';

import { classify, summarize } from '../a11y-summarize';

const stableStats = (extras: Record<string, number> = {}): { stats: Record<string, number> } => ({
  stats: { duration: 252000, expected: 96, unexpected: 0, skipped: 0, flaky: 0, ...extras },
});

const allGreenReport = {
  ...stableStats(),
  suites: [
    {
      title: 'a11y/library.spec.ts',
      file: 'e2e/a11y/library.spec.ts',
      specs: [
        {
          title: '/library renders without axe violations',
          ok: true,
          line: 21,
          tests: [{ results: [{ status: 'passed', duration: 1200 }] }],
        },
      ],
    },
  ],
};

const axeErrorMessage = `expect(received).toEqual(expected) // deep equality

- Expected  - 0
+ Received  + 1

  Array [
+   Object {
+     "id": "color-contrast",
+     "impact": "serious",
+     "description": "Elements must have sufficient color contrast",
+     "nodes": Array [
+       Object {
+         "target": Array [
+           ".GamesFiltersInline button[aria-pressed=true]",
+         ],
+         "html": "<button>...</button>",
+       },
+     ],
+   },
  ]`;

const axeOnlyReport = {
  ...stableStats({ unexpected: 1 }),
  suites: [
    {
      title: 'a11y/games-library.spec.ts',
      file: 'e2e/a11y/games-library.spec.ts',
      specs: [
        {
          title: '/library (games tab) — axe WCAG 2.1 AA',
          ok: false,
          line: 65,
          file: 'e2e/a11y/games-library.spec.ts',
          tests: [
            {
              results: [
                {
                  status: 'failed',
                  duration: 4500,
                  errors: [
                    { message: axeErrorMessage, location: { file: 'e2e/a11y/games-library.spec.ts', line: 88 } },
                  ],
                },
              ],
            },
          ],
        },
      ],
    },
  ],
};

const flakeErrorMessage = `TimeoutError: locator.click: Timeout 30000ms exceeded.
Call log:
  - waiting for locator('[role="tab"][data-tab-key="games"]')`;

const flakeOnlyReport = {
  ...stableStats({ unexpected: 1 }),
  suites: [
    {
      title: 'a11y/sessions-index.spec.ts',
      file: 'e2e/a11y/sessions-index.spec.ts',
      specs: [
        {
          title: '/sessions list renders without axe violations',
          ok: false,
          line: 42,
          file: 'e2e/a11y/sessions-index.spec.ts',
          tests: [
            {
              results: [
                {
                  status: 'failed',
                  duration: 30000,
                  errors: [
                    { message: flakeErrorMessage, location: { file: 'e2e/a11y/sessions-index.spec.ts', line: 42 } },
                  ],
                },
              ],
            },
          ],
        },
      ],
    },
  ],
};

const mixedReport = {
  ...stableStats({ unexpected: 2 }),
  suites: [
    axeOnlyReport.suites[0],
    flakeOnlyReport.suites[0],
  ],
};

describe('classify', () => {
  it('returns "axe" when message contains axe impact field', () => {
    expect(classify(axeErrorMessage)).toBe('axe');
  });

  it('returns "flake" when message is a Playwright timeout', () => {
    expect(classify(flakeErrorMessage)).toBe('flake');
  });

  it('returns "flake" for arbitrary assertion errors without axe impact', () => {
    expect(classify('AssertionError: expected value to be truthy')).toBe('flake');
  });

  it('returns "axe" for all axe impact severities', () => {
    for (const impact of ['minor', 'moderate', 'serious', 'critical']) {
      expect(classify(`{"impact":"${impact}"}`)).toBe('axe');
    }
  });
});

describe('summarize — Scenario A: all green', () => {
  const result = summarize(allGreenReport);

  it('exit code is 0', () => {
    expect(result.exitCode).toBe(0);
  });

  it('dominant class is none', () => {
    expect(result.dominantClass).toBe('none');
  });

  it('summary leads with the green emoji', () => {
    expect(result.summary.split('\n')[0]).toMatch(/^## 🟢 Frontend A11y E2E/);
  });

  it('emits no workflow annotations', () => {
    expect(result.annotations).toEqual([]);
  });

  it('reports zero failures', () => {
    expect(result.axeViolations).toHaveLength(0);
    expect(result.flakes).toHaveLength(0);
  });
});

describe('summarize — Scenario B: axe-only failure', () => {
  const result = summarize(axeOnlyReport);

  it('exit code is 2 (axe wins)', () => {
    expect(result.exitCode).toBe(2);
  });

  it('dominant class is axe', () => {
    expect(result.dominantClass).toBe('axe');
  });

  it('summary leads with the red emoji and axe label', () => {
    expect(result.summary.split('\n')[0]).toBe('## 🔴 Frontend A11y E2E — axe AA violations detected');
  });

  it('extracts route, rule, selector, and impact from the error message', () => {
    expect(result.axeViolations).toHaveLength(1);
    const v = result.axeViolations[0];
    expect(v.rule).toBe('color-contrast');
    expect(v.impact).toBe('serious');
    expect(v.selector).toBe('.GamesFiltersInline button[aria-pressed=true]');
  });

  it('emits one ::error:: annotation per violation', () => {
    expect(result.annotations).toHaveLength(1);
    expect(result.annotations[0]).toMatch(/^::error /);
    expect(result.annotations[0]).toContain('rule=color-contrast');
    expect(result.annotations[0]).toContain('impact=serious');
  });
});

describe('summarize — Scenario C: flake-only failure', () => {
  const result = summarize(flakeOnlyReport);

  it('exit code is 1 (flake only)', () => {
    expect(result.exitCode).toBe(1);
  });

  it('dominant class is flake', () => {
    expect(result.dominantClass).toBe('flake');
  });

  it('summary leads with the warning emoji', () => {
    expect(result.summary.split('\n')[0]).toBe('## ⚠️ Frontend A11y E2E — infrastructure failure (no axe violations)');
  });

  it('classifies as Playwright timeout', () => {
    expect(result.flakes).toHaveLength(1);
    expect(result.flakes[0].failureType).toBe('Playwright timeout');
  });

  it('emits ::warning:: annotation with file:line', () => {
    expect(result.annotations).toHaveLength(1);
    expect(result.annotations[0]).toMatch(/^::warning /);
    expect(result.annotations[0]).toContain('file=e2e/a11y/sessions-index.spec.ts');
    expect(result.annotations[0]).toContain('line=42');
  });
});

describe('summarize — Scenario D: mixed (axe + flake)', () => {
  const result = summarize(mixedReport);

  it('exit code is 2 (axe dominates over flake)', () => {
    expect(result.exitCode).toBe(2);
  });

  it('dominant class is axe', () => {
    expect(result.dominantClass).toBe('axe');
  });

  it('summary headline mentions both axe count and flake suffix', () => {
    const headline = result.summary.split('\n')[0];
    expect(headline).toContain('🔴');
    expect(headline).toContain('axe AA violations detected');
    expect(headline).toContain('(+ 1 flake)');
  });

  it('summary surfaces both axe and flake sections', () => {
    expect(result.summary).toContain('### axe violations');
    expect(result.summary).toContain('### Infrastructure failures');
  });

  it('emits ::error:: + ::warning:: annotations', () => {
    expect(result.annotations).toHaveLength(2);
    expect(result.annotations[0]).toMatch(/^::error /);
    expect(result.annotations[1]).toMatch(/^::warning /);
  });
});

describe('summarize — determinism (AC #6)', () => {
  it('produces byte-identical output across two invocations on the same input', () => {
    const a = summarize(mixedReport);
    const b = summarize(mixedReport);
    expect(a.summary).toBe(b.summary);
    expect(a.annotations).toEqual(b.annotations);
    expect(a.exitCode).toBe(b.exitCode);
  });

  it('produces byte-identical output when input violations arrive in different order', () => {
    const shuffled = {
      ...mixedReport,
      suites: [...mixedReport.suites].reverse(),
    };
    const ordered = summarize(mixedReport);
    const reversed = summarize(shuffled);
    expect(reversed.summary).toBe(ordered.summary);
    expect(reversed.annotations).toEqual(ordered.annotations);
  });
});

describe('summarize — input validation', () => {
  it('throws TypeError on non-object input', () => {
    expect(() => summarize('not a report')).toThrow(TypeError);
  });

  it('throws TypeError on null input', () => {
    expect(() => summarize(null)).toThrow(TypeError);
  });

  it('throws TypeError when "suites" key is missing', () => {
    expect(() => summarize({ stats: {} })).toThrow(/missing "suites"/);
  });
});

describe('summarize — sort order (AC #6 deterministic ordering)', () => {
  it('sorts axe violations by route → rule → selector', () => {
    const messageFor = (rule: string, impact: string, selector: string): string =>
      `[{"id":"${rule}","impact":"${impact}","nodes":[{"target":["${selector}"]}]}]`;

    const unsortedReport = {
      ...stableStats({ unexpected: 3 }),
      suites: [
        {
          title: 'a11y/multi.spec.ts',
          file: 'e2e/a11y/multi.spec.ts',
          specs: [
            {
              title: '/zebra path',
              line: 10,
              tests: [{ results: [{ status: 'failed', errors: [{ message: messageFor('color-contrast', 'serious', '.zebra-btn') }] }] }],
            },
            {
              title: '/alpha path',
              line: 20,
              tests: [{ results: [{ status: 'failed', errors: [{ message: messageFor('aria-required', 'moderate', '.alpha-tab') }] }] }],
            },
            {
              title: '/alpha path 2',
              line: 30,
              tests: [{ results: [{ status: 'failed', errors: [{ message: messageFor('color-contrast', 'serious', '.alpha-text') }] }] }],
            },
          ],
        },
      ],
    };

    const result = summarize(unsortedReport);
    const routes = result.axeViolations.map(v => v.route);
    expect(routes).toEqual(['/alpha', '/alpha', '/zebra']);
    // within same route, sorted by rule
    expect(result.axeViolations[0].rule).toBe('aria-required');
    expect(result.axeViolations[1].rule).toBe('color-contrast');
  });
});
