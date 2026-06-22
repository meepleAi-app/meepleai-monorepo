import { describe, it, expect } from 'vitest';

import { CardFooter, MenuPlaceholder } from '../index';

describe('parts/index barrel', () => {
  it('exports CardFooter as a function component', () => {
    expect(typeof CardFooter).toBe('function');
  });

  it('exports MenuPlaceholder as a function component', () => {
    expect(typeof MenuPlaceholder).toBe('function');
  });
});
