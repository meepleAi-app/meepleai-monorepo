import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { EntityZone } from '@/components/dashboard/EntityZone';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('EntityZone', () => {
  it('renders title and count', () => {
    render(
      <EntityZone entity="game" title="Giochi" count={12} viewAllHref="/games">
        <div>children</div>
      </EntityZone>
    );
    expect(screen.getByText('Giochi')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('renders entity-colored dot', () => {
    render(
      <EntityZone entity="session" title="Sessioni" count={3}>
        <div>children</div>
      </EntityZone>
    );
    const dot = screen.getByTestId('entity-dot');
    expect(dot).toBeInTheDocument();
  });

  it('renders "Vedi tutti" link when viewAllHref provided', () => {
    render(
      <EntityZone entity="game" title="Giochi" count={12} viewAllHref="/games">
        <div>children</div>
      </EntityZone>
    );
    expect(screen.getByText(/Vedi tutti/)).toBeInTheDocument();
  });

  it('does not render "Vedi tutti" when viewAllHref omitted', () => {
    render(
      <EntityZone entity="toolkit" title="Strumenti" count={4}>
        <div>children</div>
      </EntityZone>
    );
    expect(screen.queryByText(/Vedi tutti/)).not.toBeInTheDocument();
  });

  it('renders children', () => {
    render(
      <EntityZone entity="agent" title="Agenti" count={0}>
        <div data-testid="child">hello</div>
      </EntityZone>
    );
    expect(screen.getByTestId('child')).toBeInTheDocument();
  });
});
