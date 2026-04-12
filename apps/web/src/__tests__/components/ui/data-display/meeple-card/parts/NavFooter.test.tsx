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
