import { describe, it, expect } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';

/**
 * P2 #807 token redesign — AA-compliant values (audit Iter 2, 2026-05-09)
 *
 * Source of truth: docs/for-developers/frontend/v2-a11y-token-audit.md
 * Mockup `admin-mockups/design_files/tokens.css` values were not WCAG 2.1 AA
 * compliant; redesign collapsed --c-* (design-tokens.css) and --e-* (globals.css)
 * onto the same AA-aligned palette per #807.
 *
 *   --e-document (kb): 174 60% 30%   (was 174 60% 40% — Iter 2 darken for AA on white)
 *   --e-toolkit:       142 70% 30%   (was 142 70% 45% — Iter 2 darken for AA on white)
 */
describe('entity tokens alignment (post-#807 AA)', () => {
  const css = fs.readFileSync(path.join(__dirname, '../globals.css'), 'utf8');

  it('--e-document matches AA-aligned --c-kb (174 60% 30%)', () => {
    expect(css).toMatch(/--e-document:\s*174\s+60%\s+30%/);
  });

  it('--e-toolkit matches AA-aligned --c-toolkit (142 70% 30%)', () => {
    expect(css).toMatch(/--e-toolkit:\s*142\s+70%\s+30%/);
  });
});
