import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { ActionPill } from '@/components/layout/ActionPill';
import { useCardHand } from '@/lib/stores/card-hand-store';

const mockPathname = vi.fn();
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
}));
vi.mock('next/link', () => ({
  default: ({
    href,
    children,
    ...props
  }: {
    href: string;
    children: React.ReactNode;
    [k: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

beforeEach(() => {
  useCardHand.setState({ cards: [] });
});

describe('ActionPill', () => {
  it('non renderizza se path sconosciuto e nessuna card', () => {
    mockPathname.mockReturnValue('/unknown');
    const { container } = render(<ActionPill />);
    expect(container.firstChild).toBeNull();
  });

  it('renderizza CTA "▶ Nuova sessione" su /sessions', () => {
    mockPathname.mockReturnValue('/sessions');
    render(<ActionPill />);
    expect(screen.getByTestId('action-pill-cta')).toHaveTextContent('▶ Nuova sessione');
  });

  it('renderizza CTA "↑ Carica PDF" su /games/:id/kb', () => {
    mockPathname.mockReturnValue('/games/abc/kb');
    render(<ActionPill />);
    expect(screen.getByTestId('action-pill-cta')).toHaveTextContent('↑ Carica PDF');
  });

  it('renderizza CTA "💬 Inizia chat" su /agents/:id', () => {
    mockPathname.mockReturnValue('/agents/expert-1');
    render(<ActionPill />);
    expect(screen.getByTestId('action-pill-cta')).toHaveTextContent('💬 Inizia chat');
  });

  it('renderizza breadcrumb con entity label se card in mano', () => {
    mockPathname.mockReturnValue('/games/abc');
    useCardHand.getState().drawCard({
      id: 'game:abc',
      entityType: 'game',
      entityId: 'abc',
      label: 'Catan',
      href: '/games/abc',
      pinned: false,
    });
    render(<ActionPill />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Breadcrumb' })).toBeInTheDocument();
  });

  it('renderizza breadcrumb "Libreria" su /library', () => {
    mockPathname.mockReturnValue('/library');
    render(<ActionPill />);
    expect(screen.getByText('Libreria')).toBeInTheDocument();
  });
});
