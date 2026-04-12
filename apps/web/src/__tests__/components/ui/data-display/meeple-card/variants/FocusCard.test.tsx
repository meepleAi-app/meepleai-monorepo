import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

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

import { FocusCard } from '@/components/ui/data-display/meeple-card/variants/FocusCard';

const baseProps = {
  entity: 'game' as const,
  title: 'Catan',
  subtitle: 'Klaus Teuber',
  imageUrl: '/catan.jpg',
  rating: 7.5,
  ratingMax: 10,
};

describe('FocusCard', () => {
  it('renders title and subtitle', () => {
    render(<FocusCard {...baseProps} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
  });

  it('renders navItem chips when provided', () => {
    render(
      <FocusCard
        {...baseProps}
        navItems={[
          {
            icon: <span>🎮</span>,
            label: 'Sessioni',
            entity: 'session',
            count: 5,
            href: '/games/1/sessions',
          },
          { icon: <span>📚</span>, label: 'Docs', entity: 'kb', count: 2, href: '/games/1/kb' },
        ]}
      />
    );
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('Docs')).toBeInTheDocument();
  });

  it('renders navItem count when provided', () => {
    render(
      <FocusCard
        {...baseProps}
        navItems={[
          {
            icon: <span>🎮</span>,
            label: 'Sessioni',
            entity: 'session',
            count: 12,
            href: '/games/1/sessions',
          },
        ]}
      />
    );
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('applies data-testid', () => {
    render(<FocusCard {...baseProps} data-testid="focus-card-test" />);
    expect(screen.getByTestId('focus-card-test')).toBeInTheDocument();
  });
});
