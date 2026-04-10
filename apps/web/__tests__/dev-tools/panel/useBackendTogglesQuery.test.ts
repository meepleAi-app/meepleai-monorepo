import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useBackendTogglesQuery } from '@/dev-tools/panel/hooks/useBackendTogglesQuery';
import { devPanelClient, DevPanelClientError } from '@/dev-tools/panel/api/devPanelClient';

describe('useBackendTogglesQuery', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('fetches toggles on mount and exposes loading state', async () => {
    vi.spyOn(devPanelClient, 'getToggles').mockResolvedValue({
      toggles: { llm: true, embedding: false },
      knownServices: ['llm', 'embedding'],
    });
    const { result } = renderHook(() => useBackendTogglesQuery());
    expect(result.current.isLoading).toBe(true);
    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });
    expect(result.current.toggles.llm).toBe(true);
    expect(result.current.toggles.embedding).toBe(false);
    expect(result.current.knownServices).toEqual(['llm', 'embedding']);
    expect(result.current.error).toBeNull();
  });

  it('exposes error when fetch fails', async () => {
    vi.spyOn(devPanelClient, 'getToggles').mockRejectedValue(
      new DevPanelClientError('Backend down', 0)
    );
    const { result } = renderHook(() => useBackendTogglesQuery());
    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });
    expect(result.current.error).not.toBeNull();
    expect(result.current.error?.status).toBe(0);
  });

  it('refetch fires a new request', async () => {
    const spy = vi
      .spyOn(devPanelClient, 'getToggles')
      .mockResolvedValue({ toggles: { llm: true }, knownServices: ['llm'] });
    const { result } = renderHook(() => useBackendTogglesQuery());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledTimes(1);
    await act(async () => {
      await result.current.refetch();
    });
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it('uses exponential backoff on error (10s, 30s, 60s, 300s)', async () => {
    vi.useFakeTimers();
    let callCount = 0;
    vi.spyOn(devPanelClient, 'getToggles').mockImplementation(() => {
      callCount++;
      return Promise.reject(new DevPanelClientError('down', 0));
    });
    renderHook(() => useBackendTogglesQuery());
    await vi.advanceTimersByTimeAsync(0);
    expect(callCount).toBe(1);
    await vi.advanceTimersByTimeAsync(10_000);
    expect(callCount).toBe(2);
    await vi.advanceTimersByTimeAsync(30_000);
    expect(callCount).toBe(3);
    await vi.advanceTimersByTimeAsync(60_000);
    expect(callCount).toBe(4);
    await vi.advanceTimersByTimeAsync(300_000);
    expect(callCount).toBe(5);
    await vi.advanceTimersByTimeAsync(600_000);
    expect(callCount).toBe(5); // stops after 5th
  });
});
