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

  it('renders connection chips when provided', () => {
    render(
      <FocusCard
        {...baseProps}
        connections={[
          { entityType: 'session', label: 'Sessioni', count: 5 },
          { entityType: 'kb', label: 'Docs', count: 2 },
        ]}
      />
    );
    // FocusCard uses inline strip (no visible labels) — assert via aria-label.
    expect(screen.getByLabelText('5 Sessioni')).toBeInTheDocument();
    expect(screen.getByLabelText('2 Docs')).toBeInTheDocument();
  });

  it('renders connection count when provided', () => {
    render(
      <FocusCard
        {...baseProps}
        connections={[{ entityType: 'session', label: 'Sessioni', count: 12 }]}
      />
    );
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('applies data-testid', () => {
    render(<FocusCard {...baseProps} data-testid="focus-card-test" />);
    expect(screen.getByTestId('focus-card-test')).toBeInTheDocument();
  });
});
