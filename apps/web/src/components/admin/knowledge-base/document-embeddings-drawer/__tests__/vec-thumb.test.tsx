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

  it('renders "768d · float32" label and aria-hidden (decorative)', () => {
    const { container, getByText } = render(<VecThumb seed={1} />);
    expect(getByText(/768d · float32/)).toBeInTheDocument();
    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true');
  });
});
