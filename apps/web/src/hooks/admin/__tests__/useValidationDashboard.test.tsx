/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useValidationDashboard } from '../useValidationDashboard';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getDashboard: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';

const mockGetDashboard = api.admin.getDashboard as ReturnType<typeof vi.fn>;

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('useValidationDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns dashboard rows on success', async () => {
    const mockResponse = [
      {
        sharedGameId: '11111111-1111-1111-1111-111111111111',
        name: 'Wingspan',
        status: 'Certified' as const,
        overallScore: 0.92,
        lastComputedAt: '2026-04-25T10:00:00.000+00:00',
      },
    ];
    mockGetDashboard.mockResolvedValue(mockResponse);

    const { result } = renderHook(() => useValidationDashboard(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockResponse);
    expect(mockGetDashboard).toHaveBeenCalledTimes(1);
  });
});
