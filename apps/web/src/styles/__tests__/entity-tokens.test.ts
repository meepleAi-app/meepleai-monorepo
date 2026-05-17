import { describe, it, expect } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';

/**
 * P2 #807 token redesign — AA-compliant values (audit Iter 2, 2026-05-09)
 *
 * Source of truth: docs/for-developers/frontend/v2-a11y-token-audit.md
 *
 * Post DS-16 (CSS variable migration + bridge removal), the `--e-*` bridge
 * tokens were removed; canonical names are `--c-*` only. AA-aligned values:
 *
 *   --c-kb:      174 60% 30%   (KB teal — darkened from L=40% for AA on white)
 *   --c-toolkit: 142 70% 30%   (Toolkit green — darkened from L=45% for AA on white)
 */
describe('entity tokens alignment (post-#807 AA, post DS-16)', () => {
  const css = fs.readFileSync(path.join(__dirname, '../globals.css'), 'utf8');

  it('--c-kb matches AA-aligned value (174 60% 30%)', () => {
    expect(css).toMatch(/--c-kb:\s*174\s+60%\s+30%/);
  });

  it('--c-toolkit matches AA-aligned value (142 70% 30%)', () => {
    expect(css).toMatch(/--c-toolkit:\s*142\s+70%\s+30%/);
  });
});
