import { describe, it, expect } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';

/**
 * M6 Task 0 — Reconcile token drift with Claude Design v1
 *
 * Mockup source of truth: admin-mockups/design_files/tokens.css
 *   --c-kb:      174 60% 40%
 *   --c-toolkit: 142 70% 45%
 *
 * App currently has (pre-M6):
 *   --e-document: 210 40% 55%   (drifted — was generic "document" blue)
 *   --e-toolkit:  160 70% 45%   (drifted — was pre-v1 teal)
 *
 * This test enforces alignment with the Claude Design v1 mockups.
 */
describe('entity tokens alignment with Claude Design v1', () => {
  const css = fs.readFileSync(path.join(__dirname, '../globals.css'), 'utf8');

  it('--e-document matches mockup --c-kb (174 60% 40%)', () => {
    expect(css).toMatch(/--e-document:\s*174\s+60%\s+40%/);
  });

  it('--e-toolkit matches mockup --c-toolkit (142 70% 45%)', () => {
    expect(css).toMatch(/--e-toolkit:\s*142\s+70%\s+45%/);
  });
});
