import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MeepleCardGame, type MeepleCardGameLabels } from './meeple-card-game';

// Stub next/link to render a plain <a> in jsdom.
// `prefetch` is a Next-only prop; strip it before spreading on <a> to avoid
// React DOM "non-boolean attribute" warnings.
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

const labels: MeepleCardGameLabels = {
  ratingAriaLabel: 'Voto',
  toolkitLabel: 'tk',
  agentLabel: 'ag',
  newWeekAriaLabel: count => `${count} nuovi questa settimana`,
};

const baseProps = {
  id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
  title: 'Catan',
  year: 1995,
  rating: 4,
  toolkitsCount: 3,
  agentsCount: 2,
  kbsCount: 1,
  newThisWeekCount: 0,
  labels,
};

describe('MeepleCardGame (v2)', () => {
  it('links to /shared-games/{id}', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByRole('link')).toHaveAttribute('href', `/shared-games/${baseProps.id}`);
  });

  it('emits data-slot=shared-games-card with data-game-id', () => {
    const { container } = render(<MeepleCardGame {...baseProps} />);
    const root = container.querySelector('[data-slot="shared-games-card"]');
    expect(root).not.toBeNull();
    expect(root).toHaveAttribute('data-game-id', baseProps.id);
  });

  it('renders the title in an h3', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByRole('heading', { level: 3, name: 'Catan' })).toBeInTheDocument();
  });

  it('renders year in meta line when provided', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByText('1995')).toBeInTheDocument();
  });

  it('omits year line when null', () => {
    render(<MeepleCardGame {...baseProps} year={null} />);
    expect(screen.queryByText('1995')).not.toBeInTheDocument();
  });

  it('renders 5-star rating as role=img with aria-label', () => {
    render(<MeepleCardGame {...baseProps} rating={4} />);
    expect(screen.getByRole('img', { name: /Voto 4 di 5/i })).toBeInTheDocument();
  });

  it('clamps rating to [0..5] range visually (rating=12 → 5 stars)', () => {
    render(<MeepleCardGame {...baseProps} rating={12} />);
    expect(screen.getByRole('img', { name: /Voto 5 di 5/i })).toBeInTheDocument();
  });

  it('renders toolkits/agents/kbs chips when count > 0', () => {
    render(<MeepleCardGame {...baseProps} />);
    expect(screen.getByText('3 tk')).toBeInTheDocument();
    expect(screen.getByText('2 ag')).toBeInTheDocument();
    // kb chip is count-only (label="").
    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('omits all entity chips when all counts are 0', () => {
    const { container } = render(
      <MeepleCardGame {...baseProps} toolkitsCount={0} agentsCount={0} kbsCount={0} />
    );
    expect(container.textContent).not.toContain('tk');
    expect(container.textContent).not.toContain('ag');
  });

  it('does not render newWeek badge when count < 2', () => {
    render(<MeepleCardGame {...baseProps} newThisWeekCount={1} />);
    expect(screen.queryByText(/^\+1$/)).not.toBeInTheDocument();
  });

  it('renders newWeek badge when count >= 2 with aria-label', () => {
    render(<MeepleCardGame {...baseProps} newThisWeekCount={3} />);
    expect(screen.getByText('+3')).toBeInTheDocument();
    expect(screen.getByLabelText('3 nuovi questa settimana')).toBeInTheDocument();
  });

  it('renders 🎲 emoji placeholder when coverUrl is missing', () => {
    render(<MeepleCardGame {...baseProps} coverUrl={null} />);
    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('renders <img> when coverUrl is provided', () => {
    const { container } = render(
      <MeepleCardGame {...baseProps} coverUrl="https://cdn.example/c.jpg" />
    );
    const img = container.querySelector('img');
    expect(img).not.toBeNull();
    expect(img).toHaveAttribute('src', 'https://cdn.example/c.jpg');
  });

  it('cover img has empty alt (decorative; title is the link label)', () => {
    const { container } = render(
      <MeepleCardGame {...baseProps} coverUrl="https://cdn.example/c.jpg" />
    );
    expect(container.querySelector('img')).toHaveAttribute('alt', '');
  });
});
