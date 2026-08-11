import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
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

describe('GridCard coverEditSlot (#3470 Slice 1d-c)', () => {
  it('renders the slot OUTSIDE the anchor (no nested interactive) when href + slot are present', () => {
    render(
      <GridCard
        entity="game"
        variant="grid"
        title="Catan"
        href="/shared-games/1"
        data-testid="card"
        coverEditSlot={<button type="button">edit-cover</button>}
      />
    );

    const anchor = screen.getByRole('link');
    const editBtn = screen.getByRole('button', { name: 'edit-cover' });

    expect(anchor).toBeInTheDocument();
    expect(editBtn).toBeInTheDocument();
    // The interactive slot must NOT be nested inside the anchor (axe nested-interactive).
    expect(anchor.contains(editBtn)).toBe(false);
    // The testid stays on the anchor root.
    expect(screen.getByTestId('card').tagName).toBe('A');
  });

  it('renders the anchor directly (no slot) when coverEditSlot is absent', () => {
    render(<GridCard entity="game" variant="grid" title="Catan" href="/x" data-testid="card" />);
    expect(screen.getByTestId('card').tagName).toBe('A');
    expect(screen.queryByRole('button', { name: 'edit-cover' })).toBeNull();
  });

  it('renders the slot OUTSIDE the button root when no href (onClick) + slot are present', () => {
    render(
      <GridCard
        entity="game"
        variant="grid"
        title="Catan"
        onClick={() => {}}
        data-testid="card"
        coverEditSlot={<button type="button">edit-cover</button>}
      />
    );
    const rootButton = screen.getByRole('button', { name: /catan/i });
    const editBtn = screen.getByRole('button', { name: 'edit-cover' });
    expect(rootButton.contains(editBtn)).toBe(false);
  });

  it('has no axe AA violations with an interactive cover-edit slot (no nested interactive)', async () => {
    const { container } = render(
      <GridCard
        entity="game"
        variant="grid"
        title="Catan"
        href="/shared-games/1"
        coverEditSlot={
          <button type="button" aria-label="Modifica copertina">
            edit
          </button>
        }
      />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
