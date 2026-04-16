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

import { NavFooter } from '@/components/ui/data-display/meeple-card/parts/NavFooter';

describe('NavFooter href support', () => {
  it('renders link with correct href when href is provided', () => {
    render(
      <NavFooter
        items={[
          {
            icon: <span>🎮</span>,
            label: 'Sessioni',
            entity: 'session',
            href: '/games/abc/sessions',
          },
        ]}
      />
    );
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/games/abc/sessions');
  });

  it('renders div role=button when no href', () => {
    render(
      <NavFooter
        items={[
          {
            icon: <span>🎮</span>,
            label: 'Sessioni',
            entity: 'session',
          },
        ]}
      />
    );
    expect(screen.queryByRole('link')).toBeNull();
    expect(screen.getByRole('button')).toBeInTheDocument();
  });
});

describe('NavFooter plus-indicator position', () => {
  it('renders the "+" button in the top-right corner of the icon (not bottom)', () => {
    render(
      <NavFooter
        items={[
          {
            icon: <span>📚</span>,
            label: 'KB',
            entity: 'kb',
            showPlus: true,
          },
        ]}
      />
    );
    const plusButton = screen.getByLabelText('Aggiungi KB');
    // Plus overlay must share the count-badge anchor (top-right) to stay above the icon.
    expect(plusButton.className).toContain('-top-1');
    expect(plusButton.className).toContain('-right-1');
    expect(plusButton.className).not.toContain('-bottom-');
  });

  it('does not render "+" overlay when showPlus is false', () => {
    render(
      <NavFooter
        items={[
          {
            icon: <span>📚</span>,
            label: 'KB',
            entity: 'kb',
            count: 3,
          },
        ]}
      />
    );
    expect(screen.queryByLabelText('Aggiungi KB')).toBeNull();
  });
});
