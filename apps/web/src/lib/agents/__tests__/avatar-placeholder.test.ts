import { describe, expect, it } from 'vitest';

import { AVATAR_EMOJI_POOL, pickAvatarEmoji } from '../avatar-placeholder';

describe('pickAvatarEmoji', () => {
  it('returns a member of the emoji pool', () => {
    const emoji = pickAvatarEmoji('00000000-0000-4000-8000-000000000001');
    expect(AVATAR_EMOJI_POOL).toContain(emoji);
  });

  it('is deterministic for the same id (same input → same emoji)', () => {
    const id = '00000000-0000-4000-8000-000000000abc';
    expect(pickAvatarEmoji(id)).toBe(pickAvatarEmoji(id));
  });

  it('produces variety across different ids (sample distribution)', () => {
    const ids = Array.from(
      { length: 30 },
      (_, i) => `00000000-0000-4000-8000-${String(i).padStart(12, '0')}`
    );
    const seen = new Set(ids.map(pickAvatarEmoji));
    expect(seen.size).toBeGreaterThan(1);
  });

  it('returns the first pool entry for an empty string id', () => {
    expect(pickAvatarEmoji('')).toBe(AVATAR_EMOJI_POOL[0]);
  });

  it('handles short single-character ids', () => {
    const emoji = pickAvatarEmoji('a');
    expect(AVATAR_EMOJI_POOL).toContain(emoji);
  });

  it('handles UUID format consistently', () => {
    const uuid = '12345678-1234-4234-8234-123456789012';
    const result = pickAvatarEmoji(uuid);
    expect(AVATAR_EMOJI_POOL).toContain(result);
  });

  it('different ids may map to same emoji (pool is finite, no collision panic)', () => {
    // No assertion of distinctness — just ensure it never throws.
    expect(() => pickAvatarEmoji('id-1')).not.toThrow();
    expect(() => pickAvatarEmoji('id-2')).not.toThrow();
  });
});
