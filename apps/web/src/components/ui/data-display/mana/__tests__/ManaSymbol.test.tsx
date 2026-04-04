import { render, screen } from '@testing-library/react';
import { ManaSymbol } from '../ManaSymbol';

describe('ManaSymbol', () => {
  it('renders with correct entity type', () => {
    render(<ManaSymbol entity="game" />);
    expect(screen.getByTestId('mana-symbol-game')).toBeInTheDocument();
  });

  it('renders at full size by default', () => {
    const { container } = render(<ManaSymbol entity="game" />);
    const symbol = container.firstChild as HTMLElement;
    // Check for full size class (w-16 = 64px)
    expect(symbol.querySelector('[data-testid="mana-symbol-game"]')?.className).toContain('w-16');
  });

  it('renders at medium size', () => {
    render(<ManaSymbol entity="session" size="medium" />);
    const el = screen.getByTestId('mana-symbol-session');
    expect(el.className).toContain('w-7');
  });

  it('renders at mini size', () => {
    render(<ManaSymbol entity="player" size="mini" />);
    const el = screen.getByTestId('mana-symbol-player');
    expect(el.className).toContain('w-6');
  });

  it('shows display name when showLabel is true', () => {
    render(<ManaSymbol entity="kb" showLabel />);
    expect(screen.getByText('Knowledge')).toBeInTheDocument();
  });

  it('does not show label by default', () => {
    render(<ManaSymbol entity="kb" />);
    expect(screen.queryByText('Knowledge')).not.toBeInTheDocument();
  });

  it('applies entity color as CSS custom property', () => {
    render(<ManaSymbol entity="game" />);
    const el = screen.getByTestId('mana-symbol-game');
    expect(el.style.getPropertyValue('--mana-color')).toBe('25 95% 45%');
  });

  it('supports customColor override for custom entity', () => {
    render(<ManaSymbol entity="custom" customColor="180 50% 50%" />);
    const el = screen.getByTestId('mana-symbol-custom');
    expect(el.style.getPropertyValue('--mana-color')).toBe('180 50% 50%');
  });
});
