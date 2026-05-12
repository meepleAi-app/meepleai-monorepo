import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * Verifies the canonical entity-token schema in globals.css post DS-15/16:
 * - `--c-*` are the canonical raw HSL tokens (renamed from `--e-*` in DS-16
 *   CSS variable migration + bridge removal)
 * - `--color-entity-*` are the Tailwind 4 @theme utilities consuming `--c-*`
 */
describe('globals.css entity tokens (Tailwind 4 @theme, post DS-16)', () => {
  const css = readFileSync(resolve(__dirname, '../styles/globals.css'), 'utf8');

  it('defines canonical raw HSL --c-* vars for all 9 entities in :root', () => {
    const rawTokens = [
      '--c-game',
      '--c-player',
      '--c-session',
      '--c-agent',
      '--c-kb',
      '--c-chat',
      '--c-event',
      '--c-toolkit',
      '--c-tool',
    ];
    rawTokens.forEach(t => expect(css).toContain(t));
  });

  it('defines --color-entity-* Tailwind utilities in @theme for all 9 entities', () => {
    const utilityTokens = [
      '--color-entity-game',
      '--color-entity-player',
      '--color-entity-session',
      '--color-entity-agent',
      '--color-entity-kb',
      '--color-entity-chat',
      '--color-entity-event',
      '--color-entity-toolkit',
      '--color-entity-tool',
    ];
    utilityTokens.forEach(t => expect(css).toContain(t));
  });

  it('maps --color-entity-kb to --c-kb (post DS-16: bridge removed, e-document → c-kb)', () => {
    expect(css).toMatch(/--color-entity-kb:\s*hsl\(var\(--c-kb\)\)/);
  });
});
