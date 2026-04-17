import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { GreetingStrip } from '@/components/dashboard/GreetingStrip';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe('GreetingStrip', () => {
  it('renders user display name', () => {
    render(<GreetingStrip displayName="Aaron" stats={{ games: 12, sessions: 8, agents: 4 }} />);
    expect(screen.getByText(/Ciao, Aaron/)).toBeInTheDocument();
  });

  it('renders stats summary', () => {
    render(<GreetingStrip displayName="Aaron" stats={{ games: 12, sessions: 8, agents: 4 }} />);
    expect(screen.getByText(/12 giochi/)).toBeInTheDocument();
    expect(screen.getByText(/8 sessioni/)).toBeInTheDocument();
  });

  it('renders quick action buttons', () => {
    render(<GreetingStrip displayName="Aaron" stats={{ games: 0, sessions: 0, agents: 0 }} />);
    expect(screen.getByRole('link', { name: /Aggiungi gioco/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Nuova sessione/i })).toBeInTheDocument();
  });

  it('renders user initial in avatar', () => {
    render(<GreetingStrip displayName="Aaron" stats={{ games: 0, sessions: 0, agents: 0 }} />);
    expect(screen.getByTestId('greeting-avatar')).toHaveTextContent('A');
  });

  it('omits zero-count stats from summary', () => {
    render(<GreetingStrip displayName="Test" stats={{ games: 5, sessions: 0, agents: 0 }} />);
    expect(screen.getByText(/5 giochi/)).toBeInTheDocument();
    expect(screen.queryByText(/0 sessioni/)).not.toBeInTheDocument();
  });
});
