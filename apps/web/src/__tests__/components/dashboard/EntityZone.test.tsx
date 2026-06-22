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

  it('renders entity-tinted badge (role=presentation)', () => {
    render(
      <EntityZone entity="session" title="Sessioni" count={3}>
        <div>children</div>
      </EntityZone>
    );
    const badge = screen.getByTestId('entity-badge');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveAttribute('role', 'presentation');
  });

  it('title is an h2 with a useId-generated id', () => {
    render(
      <EntityZone entity="game" title="Giochi" count={3}>
        <span>x</span>
      </EntityZone>
    );
    const heading = screen.getByRole('heading', { level: 2, name: 'Giochi' });
    expect(heading).toHaveAttribute('id');
    expect(heading.id).toMatch(/.+/);
  });

  it('section aria-labelledby points to the title id', () => {
    const { container } = render(
      <EntityZone entity="game" title="Giochi" count={3}>
        <span>x</span>
      </EntityZone>
    );
    const section = container.querySelector('section');
    const heading = screen.getByRole('heading', { level: 2 });
    expect(section).toHaveAttribute('aria-labelledby', heading.id);
  });

  it('count uses tabular-nums', () => {
    const { container } = render(
      <EntityZone entity="game" title="Giochi" count={42}>
        <span>x</span>
      </EntityZone>
    );
    const count = container.querySelector('[data-testid="entity-count"]');
    expect(count).toBeInTheDocument();
    expect(count).toHaveClass('tabular-nums');
  });

  it('renders "Vedi tutti" link when viewAllHref provided', () => {
    render(
      <EntityZone entity="game" title="Giochi" count={12} viewAllHref="/games">
        <div>children</div>
      </EntityZone>
    );
    expect(screen.getByText(/Vedi tutti/)).toBeInTheDocument();
  });

  it('"Vedi tutti" link has explicit aria-label', () => {
    render(
      <EntityZone entity="session" title="Sessioni" count={1} viewAllHref="/sessions">
        <span>x</span>
      </EntityZone>
    );
    const link = screen.getByRole('link', { name: /Vedi tutti/i });
    expect(link).toHaveAttribute('aria-label', expect.stringContaining('Sessioni'));
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
