import { describe, expect, it } from 'vitest';

import { entityTokens } from '../tokens';

describe('entityTokens()', () => {
  it('returns solid color using existing entityHsl', () => {
    const t = entityTokens('game');
    expect(t.solid).toBe('hsl(25, 95%, 45%)');
  });

  it('returns fill with 0.12 alpha', () => {
    const t = entityTokens('game');
    expect(t.fill).toBe('hsla(25, 95%, 45%, 0.12)');
  });

  it('returns border with 0.35 alpha', () => {
    const t = entityTokens('game');
    expect(t.border).toBe('hsla(25, 95%, 45%, 0.35)');
  });

  it('returns named tokens for hover, glow, shadow, muted, dashed', () => {
    const t = entityTokens('kb');
    expect(t.hover).toBe('hsla(210, 40%, 55%, 0.22)');
    expect(t.glow).toBe('hsla(210, 40%, 55%, 0.18)');
    expect(t.shadow).toBe('hsla(210, 40%, 55%, 0.25)');
    expect(t.muted).toBe('hsla(210, 40%, 55%, 0.06)');
    expect(t.dashed).toBe('hsla(210, 40%, 55%, 0.25)');
  });

  it('returns textOn = #ffffff', () => {
    expect(entityTokens('agent').textOn).toBe('#ffffff');
  });
});
