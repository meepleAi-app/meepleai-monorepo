import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GridCard } from '../GridCard';

vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    prefetch: _prefetch,
    ...rest
  }: {
    href: string;
    children: React.ReactNode;
    prefetch?: boolean;
  } & React.AnchorHTMLAttributes<HTMLAnchorElement>) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

describe('GridCard href (Issue #2858)', () => {
  it('renders the root as an anchor with href when href is provided', () => {
    render(<GridCard entity="game" variant="grid" title="Catan" href="/shared-games/1" />);
    expect(screen.getByRole('link')).toHaveAttribute('href', '/shared-games/1');
    expect(screen.getByRole('heading', { name: 'Catan' })).toBeInTheDocument();
  });

  it('renders the root as a div (no anchor) when href is absent', () => {
    const { container } = render(<GridCard entity="game" variant="grid" title="Catan" />);
    expect(container.querySelector('a')).toBeNull();
  });

  it('keeps role=button + onClick when href is absent and onClick is provided', () => {
    const onClick = vi.fn();
    render(<GridCard entity="game" variant="grid" title="Catan" onClick={onClick} />);
    // The card root is the only <button> — MenuPlaceholder is now a decorative
    // <div> (#3289), not a nested button, so getByRole('button') is unambiguous.
    const rootButton = screen.getByRole('button');
    fireEvent.click(rootButton);
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('forwards data-testid to the anchor root when href is provided', () => {
    render(
      <GridCard
        entity="game"
        variant="grid"
        title="Catan"
        href="/x"
        data-testid="shared-games-card"
      />
    );
    expect(screen.getByTestId('shared-games-card').tagName).toBe('A');
  });
});
