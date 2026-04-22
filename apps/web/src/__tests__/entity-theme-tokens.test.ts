import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

describe('globals.css entity tokens (Tailwind 4 @theme)', () => {
  const css = readFileSync(resolve(__dirname, '../styles/globals.css'), 'utf8');

  it('defines raw HSL --e-* vars for all 9 entities in :root', () => {
    const rawTokens = [
      '--e-game',
      '--e-player',
      '--e-session',
      '--e-agent',
      '--e-document',
      '--e-chat',
      '--e-event',
      '--e-toolkit',
      '--e-tool',
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

  it('maps --color-entity-kb to --e-document (alias)', () => {
    expect(css).toMatch(/--color-entity-kb:\s*hsl\(var\(--e-document\)\)/);
  });
});
