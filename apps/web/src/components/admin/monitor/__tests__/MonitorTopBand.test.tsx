import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { usePathname, useSearchParams } from 'next/navigation';

import { MonitorTopBand } from '../MonitorTopBand';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useSearchParams: vi.fn(),
}));

const mockUsePathname = usePathname as ReturnType<typeof vi.fn>;
const mockUseSearchParams = useSearchParams as ReturnType<typeof vi.fn>;

function mockNav(pathname = '/admin/monitor', tab?: string) {
  mockUsePathname.mockReturnValue(pathname);
  mockUseSearchParams.mockReturnValue({
    get: (key: string) => (key === 'tab' ? (tab ?? null) : null),
  });
}

describe('MonitorTopBand', () => {
  it('renders a single h1 with text "Monitor"', () => {
    mockNav();
    render(<MonitorTopBand />);
    const h1s = screen.getAllByRole('heading', { level: 1 });
    expect(h1s).toHaveLength(1);
    expect(h1s[0]).toHaveTextContent('Monitor');
  });

  it('renders the breadcrumb navigation (MonitorCrumbs slot)', () => {
    mockNav();
    render(<MonitorTopBand />);
    expect(screen.getByRole('navigation', { name: /breadcrumb/i })).toBeInTheDocument();
  });

  it('renders the Pause stream action button', () => {
    mockNav();
    render(<MonitorTopBand />);
    expect(screen.getByRole('button', { name: /pause stream/i })).toBeInTheDocument();
  });

  it('renders the Export ndjson action button', () => {
    mockNav();
    render(<MonitorTopBand />);
    expect(screen.getByRole('button', { name: /export ndjson/i })).toBeInTheDocument();
  });

  it('renders the search input with ⌘K hint', () => {
    mockNav();
    render(<MonitorTopBand />);
    expect(screen.getByRole('textbox', { name: /search monitor/i })).toBeInTheDocument();
    expect(screen.getByText('⌘K')).toBeInTheDocument();
  });

  it('renders with correct section aria-label', () => {
    mockNav('/admin/monitor', 'alerts');
    render(<MonitorTopBand />);
    expect(screen.getByRole('banner', { name: /monitor section/i })).toBeInTheDocument();
  });

  it('reflects active tab label in crumbs when tab=cache', () => {
    mockNav('/admin/monitor', 'cache');
    render(<MonitorTopBand />);
    expect(screen.getByText(/Metrics/)).toBeInTheDocument();
  });
});
