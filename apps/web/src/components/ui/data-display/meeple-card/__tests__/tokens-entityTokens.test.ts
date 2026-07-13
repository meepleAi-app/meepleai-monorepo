import { describe, expect, it } from 'vitest';

import { entityTokens } from '../tokens';

describe('entityTokens()', () => {
  // #2862 (C5): entityTokens now emits theme-aware canonical --c-* CSS vars.
  it('returns solid color as the canonical --c-* var', () => {
    const t = entityTokens('game');
    expect(t.solid).toBe('hsl(var(--c-game))');
  });

  it('returns fill with 0.12 alpha', () => {
    const t = entityTokens('game');
    expect(t.fill).toBe('hsl(var(--c-game) / 0.12)');
  });

  it('returns border with 0.35 alpha', () => {
    const t = entityTokens('game');
    expect(t.border).toBe('hsl(var(--c-game) / 0.35)');
  });

  it('returns named tokens for hover, glow, shadow, muted, dashed', () => {
    const t = entityTokens('kb');
    expect(t.hover).toBe('hsl(var(--c-kb) / 0.22)');
    expect(t.glow).toBe('hsl(var(--c-kb) / 0.18)');
    expect(t.shadow).toBe('hsl(var(--c-kb) / 0.25)');
    expect(t.muted).toBe('hsl(var(--c-kb) / 0.06)');
    expect(t.dashed).toBe('hsl(var(--c-kb) / 0.25)');
  });

  it('returns textOn = #ffffff', () => {
    expect(entityTokens('agent').textOn).toBe('#ffffff');
  });
});
