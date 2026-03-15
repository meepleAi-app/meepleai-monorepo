// apps/web/src/components/dashboard-v2/__tests__/activity-feed.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ActivityFeed } from '../activity-feed';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

describe('ActivityFeed', () => {
  it('renders activity items sorted by priority', () => {
    render(
      <ActivityFeed
        items={[
          {
            id: 'a1',
            priority: 'info',
            message: 'Nuova FAQ su Catan',
            href: '/games/g1/faqs',
            icon: 'faq',
          },
          {
            id: 'a2',
            priority: 'high',
            message: 'Sessione da completare',
            href: '/sessions/s1',
            icon: 'session',
          },
          {
            id: 'a3',
            priority: 'medium',
            message: 'Invito game night',
            href: '/game-nights/gn1',
            icon: 'event',
          },
        ]}
      />
    );
    const items = screen.getAllByTestId(/^activity-item/);
    expect(items[0]).toHaveTextContent('Sessione da completare');
    expect(items[1]).toHaveTextContent('Invito game night');
    expect(items[2]).toHaveTextContent('Nuova FAQ su Catan');
  });

  it('renders empty state when no items', () => {
    render(<ActivityFeed items={[]} />);
    expect(screen.getByText(/nessuna attività/i)).toBeInTheDocument();
  });

  it('is hidden when isSessionMode is true', () => {
    const { container } = render(
      <ActivityFeed
        items={[{ id: 'a1', priority: 'info', message: 'test', href: '/', icon: 'faq' }]}
        isSessionMode
      />
    );
    expect(container.firstChild).toBeNull();
  });
});
