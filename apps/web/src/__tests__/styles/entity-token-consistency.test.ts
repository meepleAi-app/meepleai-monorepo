import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, it, expect } from 'vitest';

/**
 * Cross-file entity-token consistency gate (issue #2856, follow-up of audit
 * docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md).
 *
 * The `--c-{entity}` accent tokens are (currently) declared in three runtime
 * stylesheets loaded together: `design-tokens-canonical.css`, `globals.css`,
 * and `design-tokens.css` (the last two @imported). Their values MUST be
 * identical everywhere, otherwise the winning value depends on CSS import-order
 * — exactly the silent drift the audit surfaced (canonical 45% vs globals 38%).
 *
 * This gate asserts the light + dark values of every entity token match across
 * all three files. Paired with entity-token-golden.test.ts (canonical ↔ mockup),
 * it pins the full chain: mockup == canonical == globals == design-tokens.
 *
 * The dead v1 "short aliases" block in design-tokens.css was removed in #2857.
 * The remaining physical de-duplication (collapse to a single source) is a
 * follow-up; this gate makes any new divergence a red build in the meantime.
 */

const HERE = path.dirname(fileURLToPath(import.meta.url));
const STYLES = path.resolve(HERE, '../../styles');

const ENTITIES = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chat',
  'event',
  'toolkit',
  'tool',
] as const;

function tokenValues(css: string, entity: string): string[] {
  // Matches `--c-game: ...;` but NOT `--c-game-text: ...;`.
  const re = new RegExp('--c-' + entity + ':\\s*([^;]+);', 'g');
  return [...css.matchAll(re)].map(m => m[1].trim()).sort();
}

describe('entity token consistency across runtime sources (#2856)', () => {
  const canonical = fs.readFileSync(path.join(STYLES, 'design-tokens-canonical.css'), 'utf8');
  const globals = fs.readFileSync(path.join(STYLES, 'globals.css'), 'utf8');
  const designTokens = fs.readFileSync(path.join(STYLES, 'design-tokens.css'), 'utf8');

  it.each(ENTITIES)('--c-%s matches across canonical, globals, design-tokens', entity => {
    const c = tokenValues(canonical, entity);
    const g = tokenValues(globals, entity);
    const d = tokenValues(designTokens, entity);
    expect(c.length).toBeGreaterThan(0);
    expect(g).toEqual(c);
    expect(d).toEqual(c);
  });
});
