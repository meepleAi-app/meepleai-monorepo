import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { GameCoverPlaceholder } from '../GameCoverPlaceholder';

describe('GameCoverPlaceholder', () => {
  it('renders the testid hook for E2E coverage (#1822)', () => {
    render(<GameCoverPlaceholder gameId="g1" title="Catan" />);
    expect(screen.getByTestId('game-cover-placeholder')).toBeInTheDocument();
  });

  it('renders extracted initials', () => {
    render(<GameCoverPlaceholder gameId="g1" title="7 Wonders" />);
    expect(screen.getByText('7W')).toBeInTheDocument();
  });

  it('renders the gameId attribute for debug / inspection', () => {
    render(<GameCoverPlaceholder gameId="cc1678e8-f460-4b53-81f6-6d6539f82b65" title="Catan" />);
    const el = screen.getByTestId('game-cover-placeholder');
    expect(el.getAttribute('data-game-id')).toBe('cc1678e8-f460-4b53-81f6-6d6539f82b65');
  });

  it('is aria-hidden so screen readers do not announce the decoration', () => {
    render(<GameCoverPlaceholder gameId="g1" title="Catan" />);
    expect(screen.getByTestId('game-cover-placeholder')).toHaveAttribute('aria-hidden', 'true');
  });

  it('produces a stable background gradient for the same gameId', () => {
    const { container, rerender } = render(
      <GameCoverPlaceholder gameId="stable-id" title="Catan" />
    );
    const before = container.firstChild as HTMLElement;
    const bg1 = before.style.background;

    rerender(<GameCoverPlaceholder gameId="stable-id" title="Catan" />);
    const after = container.firstChild as HTMLElement;
    expect(after.style.background).toBe(bg1);
  });

  it('produces different gradients for distinct gameIds (smoke check)', () => {
    const { container: c1 } = render(<GameCoverPlaceholder gameId="aaa" title="A" />);
    const { container: c2 } = render(<GameCoverPlaceholder gameId="bbb" title="B" />);
    const bg1 = (c1.firstChild as HTMLElement).style.background;
    const bg2 = (c2.firstChild as HTMLElement).style.background;
    expect(bg1).not.toBe(bg2);
  });

  it('falls back gracefully when title is empty', () => {
    render(<GameCoverPlaceholder gameId="g1" title="" />);
    expect(screen.getByText('?')).toBeInTheDocument();
  });

  it('applies the className prop to the outer container', () => {
    const { container } = render(
      <GameCoverPlaceholder gameId="g1" title="Catan" className="aspect-[7/10]" />
    );
    expect(container.firstChild).toHaveClass('aspect-[7/10]');
  });
});
