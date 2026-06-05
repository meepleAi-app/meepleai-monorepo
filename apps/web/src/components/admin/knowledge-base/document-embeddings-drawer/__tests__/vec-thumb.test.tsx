import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { VecThumb } from '../vec-thumb';

describe('VecThumb', () => {
  it('renders deterministic gradient for same string seed', () => {
    const { container: a } = render(<VecThumb seed="chunk-42" />);
    const { container: b } = render(<VecThumb seed="chunk-42" />);
    const aBg = a.firstElementChild?.getAttribute('style') ?? '';
    const bBg = b.firstElementChild?.getAttribute('style') ?? '';
    expect(aBg).toBe(bBg);
    expect(aBg).toContain('linear-gradient');
  });

  it('renders deterministic gradient for numeric seed via String coercion (FIX-4)', () => {
    const { container: a } = render(<VecThumb seed={42} />);
    const { container: b } = render(<VecThumb seed={42} />);
    expect(a.firstElementChild?.getAttribute('style')).toBe(
      b.firstElementChild?.getAttribute('style')
    );
  });

  it('is decorative (aria-hidden) with no textual content', () => {
    const { container } = render(<VecThumb seed={1} />);
    const root = container.firstElementChild;
    expect(root).toHaveAttribute('aria-hidden', 'true');
    expect(root?.textContent).toBe('');
  });
});
