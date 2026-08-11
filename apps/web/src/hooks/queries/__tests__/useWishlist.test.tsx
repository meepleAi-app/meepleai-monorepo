/**
 * @vitest-environment jsdom
 *
 * useWishlist mutation hooks — #3231: the "Rimuovi" action must surface failures
 * (previously useRemoveFromWishlist had no onError, so it failed silently).
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { toast } from '@/components/layout/Toast';

import { useRemoveFromWishlist } from '../useWishlist';

const mockRemove = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({ api: { wishlist: { remove: mockRemove } } }));
vi.mock('@/components/layout/Toast', () => ({
  toast: { error: vi.fn(), success: vi.fn() },
}));

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

describe('useRemoveFromWishlist', () => {
  beforeEach(() => vi.clearAllMocks());

  it('shows an error toast when removal fails', async () => {
    mockRemove.mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useRemoveFromWishlist(), { wrapper });

    await expect(result.current.mutateAsync('id-1')).rejects.toThrow('boom');
    await waitFor(() => expect(vi.mocked(toast.error)).toHaveBeenCalled());
  });

  it('does not show an error toast on success', async () => {
    mockRemove.mockResolvedValue(undefined);

    const { result } = renderHook(() => useRemoveFromWishlist(), { wrapper });

    await result.current.mutateAsync('id-1');
    expect(vi.mocked(toast.error)).not.toHaveBeenCalled();
  });
});
