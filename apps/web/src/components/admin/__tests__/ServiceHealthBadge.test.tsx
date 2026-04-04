import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// ============================================================================
// Mock next/navigation
// ============================================================================

const mockPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

import { ServiceHealthBadge } from '../ServiceHealthBadge';

// ============================================================================
// Test Utilities
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
        staleTime: 0,
      },
    },
  });
}

function renderWithProviders(component: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(<QueryClientProvider client={queryClient}>{component}</QueryClientProvider>);
}

// ============================================================================
// Setup
// ============================================================================

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ============================================================================
// Tests
// ============================================================================

describe('ServiceHealthBadge', () => {
  it('renders green dot when there are no active alerts', async () => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve([]),
      } as Response)
    );

    const { container } = renderWithProviders(<ServiceHealthBadge />);

    await waitFor(() => {
      const dot = container.querySelector('[data-testid="service-health-badge"] span');
      expect(dot).toHaveClass('bg-green-500');
    });
  });

  it('renders yellow dot with count when 1 active alert', async () => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve([{ id: 'a1', isActive: true, severity: 'Warning' }]),
      } as Response)
    );

    renderWithProviders(<ServiceHealthBadge />);

    await waitFor(() => {
      expect(screen.getByText('1')).toBeInTheDocument();
    });

    const badge = screen.getByTestId('service-health-badge');
    const dot = badge.querySelector('span');
    expect(dot).toHaveClass('bg-yellow-500');
  });

  it('renders yellow dot with count when 2 active alerts', async () => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve([
            { id: 'a1', isActive: true, severity: 'Warning' },
            { id: 'a2', isActive: true, severity: 'Info' },
          ]),
      } as Response)
    );

    renderWithProviders(<ServiceHealthBadge />);

    await waitFor(() => {
      expect(screen.getByText('2')).toBeInTheDocument();
    });

    const badge = screen.getByTestId('service-health-badge');
    const dot = badge.querySelector('span');
    expect(dot).toHaveClass('bg-yellow-500');
  });

  it('renders red dot with count when 3 or more active alerts', async () => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve([
            { id: 'a1', isActive: true, severity: 'Critical' },
            { id: 'a2', isActive: true, severity: 'Critical' },
            { id: 'a3', isActive: true, severity: 'Warning' },
          ]),
      } as Response)
    );

    renderWithProviders(<ServiceHealthBadge />);

    await waitFor(() => {
      expect(screen.getByText('3')).toBeInTheDocument();
    });

    const badge = screen.getByTestId('service-health-badge');
    const dot = badge.querySelector('span');
    expect(dot).toHaveClass('bg-red-500');
  });

  it('does not show count when there are no active alerts', async () => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve([]),
      } as Response)
    );

    renderWithProviders(<ServiceHealthBadge />);

    // Wait for query to resolve
    await waitFor(() => {
      expect(screen.getByTestId('service-health-badge')).toBeInTheDocument();
    });

    // Count span should not be present
    const badge = screen.getByTestId('service-health-badge');
    expect(badge.querySelectorAll('span').length).toBe(1); // only the dot
  });

  it('gracefully handles fetch failure by defaulting to green dot', async () => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: false,
        status: 500,
      } as Response)
    );

    const { container } = renderWithProviders(<ServiceHealthBadge />);

    await waitFor(() => {
      const dot = container.querySelector('[data-testid="service-health-badge"] span');
      expect(dot).toHaveClass('bg-green-500');
    });
  });
});
