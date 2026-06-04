import { describe, it, expectTypeOf } from 'vitest';

import type { MeepleCardProps } from '../types';

describe('MeepleCardProps headingLevel contract', () => {
  it('accepts headingLevel as optional 2 | 3 | 4 union', () => {
    expectTypeOf<MeepleCardProps>()
      .toHaveProperty('headingLevel')
      .toEqualTypeOf<2 | 3 | 4 | undefined>();
  });

  it('allows omitting headingLevel (backwards compat)', () => {
    const props: MeepleCardProps = { entity: 'game', title: 'Catan' };
    expectTypeOf(props.headingLevel).toEqualTypeOf<2 | 3 | 4 | undefined>();
  });

  it('allows passing headingLevel=2', () => {
    const props: MeepleCardProps = { entity: 'game', title: 'Catan', headingLevel: 2 };
    expectTypeOf(props.headingLevel).toEqualTypeOf<2 | 3 | 4 | undefined>();
  });
});
