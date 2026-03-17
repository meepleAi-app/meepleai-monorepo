import { render } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { ManaCostBar } from '../ManaCostBar';

describe('ManaCostBar', () => {
  it('renders mana pips for given entity types', () => {
    const { container } = render(<ManaCostBar relatedTypes={['session', 'kb', 'agent']} />);
    const pips = container.querySelectorAll('[data-mana-pip]');
    expect(pips).toHaveLength(3);
  });

  it('renders nothing when relatedTypes is empty', () => {
    const { container } = render(<ManaCostBar relatedTypes={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('has backdrop blur', () => {
    const { container } = render(<ManaCostBar relatedTypes={['game']} />);
    const bar = container.firstChild as HTMLElement;
    expect(bar).toBeTruthy();
    expect(bar.className).toContain('backdrop-blur');
  });

  it('is not interactive (no role or tabindex)', () => {
    const { container } = render(<ManaCostBar relatedTypes={['session', 'kb']} />);
    const pips = container.querySelectorAll('[data-mana-pip]');
    pips.forEach(pip => {
      expect(pip.getAttribute('role')).toBeNull();
      expect(pip.getAttribute('tabindex')).toBeNull();
    });
  });
});
