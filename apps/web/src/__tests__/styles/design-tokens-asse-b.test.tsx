import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * Verifies the 3 new tokens added by Asse B WP1 T1 (MAJ-9 gap #37 + #38)
 * to `design-tokens-canonical.css`:
 *   - --c-warning-ink  (Gap #37 — AA-safe warning text on cream / dark)
 *   - --c-overlay-scrim (Gap #38 — drawer/modal backdrop)
 *   - --c-overlay-gradient-end (Gap #38 — hero gradient end stop)
 *
 * Pattern note (T1 implementation):
 *   The original task prompt suggested a render-based assertion using
 *   `getComputedStyle(div).getPropertyValue('--c-warning-ink')` on a child
 *   `<div data-theme="...">`. In jsdom, stylesheet-scoped `:root` / attribute
 *   selectors do NOT propagate computed values of custom properties to child
 *   elements reliably, so we use the file-read pattern established by
 *   `entity-theme-tokens.test.ts`. This is the same approach DS-15/16 uses to
 *   validate canonical token presence + theme-specific values.
 */
describe('Asse B token additions (MAJ-9 gap #37/#38)', () => {
  const css = readFileSync(resolve(__dirname, '../../styles/design-tokens-canonical.css'), 'utf8');

  // Split light (`:root`) vs dark (`[data-theme="dark"]`) blocks so we can
  // assert per-theme values without false positives across the file.
  // Light block = `:root {` until first `}`; dark block = `[data-theme="dark"]`
  // selector union until its closing `}`.
  const lightBlockMatch = css.match(/:root\s*\{([\s\S]*?)\n\}/);
  const darkBlockMatch = css.match(/:root\[data-theme="dark"\][\s\S]*?\{([\s\S]*?)\n\}/);

  it('extracts light + dark blocks from canonical CSS (sanity)', () => {
    expect(lightBlockMatch, 'light :root block must exist').toBeTruthy();
    expect(darkBlockMatch, 'dark [data-theme="dark"] block must exist').toBeTruthy();
  });

  describe('Gap #37 — --c-warning-ink', () => {
    // #3010 FU2: 38 92% 32% measured ~4.40:1 vs the literal mockup cream
    // (#f7f3ee) — below the 4.5:1 AA floor. Darkened to 38 92% 30%, which
    // clears AA with margin: ~5.07:1 vs --background, ~4.89:1 vs #f7f3ee.
    it('defines --c-warning-ink for light theme (HSL 38 92% 30%, ~4.89:1 on mockup cream)', () => {
      const lightBlock = lightBlockMatch?.[1] ?? '';
      expect(lightBlock).toMatch(/--c-warning-ink:\s*38\s+92%\s+30%\s*;/);
    });

    it('defines --c-warning-ink for dark theme (HSL 38 92% 60%, ~10.4:1 on dark)', () => {
      const darkBlock = darkBlockMatch?.[1] ?? '';
      expect(darkBlock).toMatch(/--c-warning-ink:\s*38\s+92%\s+60%\s*;/);
    });
  });

  describe('Gap #38 — --c-overlay-scrim (built-in alpha)', () => {
    it('defines --c-overlay-scrim light theme (0 0% 0% / 0.6, 60% black scrim)', () => {
      const lightBlock = lightBlockMatch?.[1] ?? '';
      expect(lightBlock).toMatch(/--c-overlay-scrim:\s*0\s+0%\s+0%\s*\/\s*0\.6\s*;/);
    });

    it('defines --c-overlay-scrim dark theme (0 0% 0% / 0.75, heavier scrim)', () => {
      const darkBlock = darkBlockMatch?.[1] ?? '';
      expect(darkBlock).toMatch(/--c-overlay-scrim:\s*0\s+0%\s+0%\s*\/\s*0\.75\s*;/);
    });
  });

  describe('Gap #38 — --c-overlay-gradient-end (pure HSL for alpha composability)', () => {
    it('defines --c-overlay-gradient-end light theme (HSL 25 95% 38%, darker orange)', () => {
      const lightBlock = lightBlockMatch?.[1] ?? '';
      expect(lightBlock).toMatch(/--c-overlay-gradient-end:\s*25\s+95%\s+38%\s*;/);
    });

    it('defines --c-overlay-gradient-end dark theme (HSL 25 95% 50%, lighter orange)', () => {
      const darkBlock = darkBlockMatch?.[1] ?? '';
      expect(darkBlock).toMatch(/--c-overlay-gradient-end:\s*25\s+95%\s+50%\s*;/);
    });
  });

  it('keeps tokens HSL-triplet without hsl() wrapper for Tailwind alpha composability', () => {
    // --c-warning-ink and --c-overlay-gradient-end should NOT be wrapped in hsl(),
    // so consumers can write `hsl(var(--c-warning-ink) / 0.8)` for alpha.
    // --c-overlay-scrim is the documented exception — built-in alpha channel.
    expect(css).not.toMatch(/--c-warning-ink:\s*hsl\(/);
    expect(css).not.toMatch(/--c-overlay-gradient-end:\s*hsl\(/);
  });
});
