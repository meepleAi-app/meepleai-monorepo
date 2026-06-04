import { describe, it, expectTypeOf } from 'vitest';

import type { MeepleCardProps } from '../types';

describe('MeepleCardProps coverEmoji contract', () => {
  it('accepts coverEmoji as optional string', () => {
    expectTypeOf<MeepleCardProps>()
      .toHaveProperty('coverEmoji')
      .toEqualTypeOf<string | undefined>();
  });

  it('allows omitting coverEmoji', () => {
    const props: MeepleCardProps = { entity: 'game', title: 'Catan' };
    expectTypeOf(props.coverEmoji).toEqualTypeOf<string | undefined>();
  });

  it('allows passing coverEmoji as string', () => {
    const props: MeepleCardProps = { entity: 'game', title: 'Catan', coverEmoji: '🎲' };
    expectTypeOf(props.coverEmoji).toEqualTypeOf<string | undefined>();
  });
});
