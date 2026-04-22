import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Confetti } from '../Confetti';

describe('Confetti', () => {
  it('renders 40 aria-hidden pieces', () => {
    const { container } = render(<Confetti />);
    const root = container.querySelector('[data-testid="onboarding-confetti"]');
    expect(root).not.toBeNull();
    expect(root?.getAttribute('aria-hidden')).toBe('true');
    expect(root?.children).toHaveLength(40);
  });

  it('accepts a custom count', () => {
    const { container } = render(<Confetti count={5} />);
    const root = container.querySelector('[data-testid="onboarding-confetti"]');
    expect(root?.children).toHaveLength(5);
  });
});
