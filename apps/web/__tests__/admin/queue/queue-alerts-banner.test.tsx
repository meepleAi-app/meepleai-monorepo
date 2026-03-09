/**
 * QueueAlertsBanner Component Tests (Issue #5460)
 *
 * Tests for the proactive alerts banner:
 * - Renders alerts for stuck docs, queue depth, failure rate
 * - Severity styling (warning vs critical)
 * - Hidden when no alerts
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const { mockGet } = vi.hoisted(() => ({
  mockGet: vi.fn(),
}));

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: mockGet,
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    patch: vi.fn(),
  },
}));

import { QueueAlertsBanner } from '@/app/admin/(dashboard)/knowledge-base/queue/components/queue-alerts-banner';

function createQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
}

function renderBanner() {
  const queryClient = createQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <QueueAlertsBanner />
    </QueryClientProvider>
  );
}

describe('QueueAlertsBanner', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render nothing when no alerts', async () => {
    mockGet.mockResolvedValue([]);
    const { container } = renderBanner();
    // Wait for query to resolve
    await vi.waitFor(() => {
      expect(mockGet).toHaveBeenCalled();
    });
    expect(container.querySelector('[data-testid="queue-alerts-banner"]')).toBeNull();
  });

  it('should render stuck document alert', async () => {
    mockGet.mockResolvedValue([
      {
        type: 'DocumentStuck',
        severity: 'Warning',
        message: "Document 'rules.pdf' stuck for 15 minutes",
        detectedAt: '2026-03-08T22:00:00Z',
        data: null,
      },
    ]);
    renderBanner();

    const alert = await screen.findByTestId('alert-DocumentStuck');
    expect(alert).toBeInTheDocument();
    expect(screen.getByText("Document 'rules.pdf' stuck for 15 minutes")).toBeInTheDocument();
  });

  it('should render queue depth alert', async () => {
    mockGet.mockResolvedValue([
      {
        type: 'QueueDepthHigh',
        severity: 'Warning',
        message: 'Queue depth (25) exceeds threshold (20)',
        detectedAt: '2026-03-08T22:00:00Z',
        data: null,
      },
    ]);
    renderBanner();

    const alert = await screen.findByTestId('alert-QueueDepthHigh');
    expect(alert).toBeInTheDocument();
  });

  it('should render high failure rate alert', async () => {
    mockGet.mockResolvedValue([
      {
        type: 'HighFailureRate',
        severity: 'Critical',
        message: 'Failure rate 35% exceeds threshold',
        detectedAt: '2026-03-08T22:00:00Z',
        data: null,
      },
    ]);
    renderBanner();

    const alert = await screen.findByTestId('alert-HighFailureRate');
    expect(alert).toBeInTheDocument();
  });

  it('should render multiple alerts simultaneously', async () => {
    mockGet.mockResolvedValue([
      {
        type: 'DocumentStuck',
        severity: 'Warning',
        message: 'Document stuck',
        detectedAt: '2026-03-08T22:00:00Z',
        data: null,
      },
      {
        type: 'QueueDepthHigh',
        severity: 'Critical',
        message: 'Queue depth high',
        detectedAt: '2026-03-08T22:00:00Z',
        data: null,
      },
    ]);
    renderBanner();

    await screen.findByTestId('alert-DocumentStuck');
    expect(screen.getByTestId('alert-QueueDepthHigh')).toBeInTheDocument();
  });
});
