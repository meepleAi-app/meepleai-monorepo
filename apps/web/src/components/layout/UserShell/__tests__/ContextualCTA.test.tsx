import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

import { usePathname } from 'next/navigation';
import { ContextualCTA } from '../ContextualCTA';

describe('ContextualCTA', () => {
  it('renders library CTA link on /library', () => {
    vi.mocked(usePathname).mockReturnValue('/library');
    render(<ContextualCTA />);
    const link = screen.getByRole('link', { name: /aggiungi gioco/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/catalog');
  });

  it('renders sessions CTA link on /sessions', () => {
    vi.mocked(usePathname).mockReturnValue('/sessions');
    render(<ContextualCTA />);
    expect(screen.getByRole('link', { name: /nuova sessione/i })).toBeInTheDocument();
  });

  it('renders chat CTA on /chat', () => {
    vi.mocked(usePathname).mockReturnValue('/chat');
    render(<ContextualCTA />);
    expect(screen.getByRole('link', { name: /nuova chat/i })).toBeInTheDocument();
  });

  it('renders nothing on /dashboard', () => {
    vi.mocked(usePathname).mockReturnValue('/dashboard');
    const { container } = render(<ContextualCTA />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing on /settings', () => {
    vi.mocked(usePathname).mockReturnValue('/settings');
    const { container } = render(<ContextualCTA />);
    expect(container).toBeEmptyDOMElement();
  });

  it('has data-testid contextual-cta', () => {
    vi.mocked(usePathname).mockReturnValue('/agents');
    render(<ContextualCTA />);
    expect(screen.getByTestId('contextual-cta')).toBeInTheDocument();
  });
});
