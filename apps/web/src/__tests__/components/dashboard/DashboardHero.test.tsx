import { render, screen, act } from '@testing-library/react';
import { describe, it, expect, beforeAll, afterAll, vi } from 'vitest';

import { DashboardHero } from '@/components/dashboard/DashboardHero';

describe('DashboardHero', () => {
  beforeAll(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-05-12T10:00:00Z'));
  });

  afterAll(() => {
    vi.useRealTimers();
  });

  it('renders single h1 with displayName', () => {
    render(<DashboardHero displayName="Marco" />);
    const headings = screen.getAllByRole('heading', { level: 1 });
    expect(headings).toHaveLength(1);
    expect(headings[0]).toHaveTextContent('Marco');
  });

  it('gradient mark wraps user name only', () => {
    render(<DashboardHero displayName="Marco" />);
    const mark = screen.getByText('Marco');
    expect(mark.tagName.toLowerCase()).toBe('span');
    expect(mark).toHaveClass('hero-mark');
  });

  it('CTAs link to /sessions/new and /library?action=add', () => {
    render(<DashboardHero displayName="x" />);
    expect(screen.getByRole('link', { name: /Nuova sessione/i })).toHaveAttribute(
      'href',
      '/sessions/new'
    );
    expect(screen.getByRole('link', { name: /Aggiungi gioco/i })).toHaveAttribute(
      'href',
      '/library?action=add'
    );
  });

  it('kicker has aria-hidden=true', () => {
    render(<DashboardHero displayName="x" />);
    const kicker = screen.getByTestId('hero-kicker');
    expect(kicker).toHaveAttribute('aria-hidden', 'true');
  });

  it('kicker is empty on first render (SSR-safe), populates after useEffect', async () => {
    const { container } = render(<DashboardHero displayName="x" />);
    const kicker = container.querySelector('[data-testid="hero-kicker"]');
    expect(kicker).toBeInTheDocument();
    await act(async () => {
      await Promise.resolve();
    });
    expect(kicker?.textContent?.toLowerCase()).toMatch(/benvenuto/);
    expect(kicker?.textContent?.toLowerCase()).toMatch(/martedì/);
  });

  it('renders lead text', () => {
    render(<DashboardHero displayName="x" />);
    expect(screen.getByText(/La tua tavola da gioco/i)).toBeInTheDocument();
  });
});
