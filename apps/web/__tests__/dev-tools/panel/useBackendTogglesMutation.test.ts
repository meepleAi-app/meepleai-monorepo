import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { DevPanelClientError } from '@/dev-tools/panel/api/devPanelErrors';

// Mock the entire module so both named exports AND devPanelClient object methods are intercepted
vi.mock('@/dev-tools/panel/api/devPanelClient', async () => {
  const { DevPanelClientError: RealError } = await vi.importActual<
    typeof import('@/dev-tools/panel/api/devPanelClient')
  >('@/dev-tools/panel/api/devPanelClient');
  return {
    DevPanelClientError: RealError,
    getToggles: vi.fn(),
    patchToggles: vi.fn(),
    resetToggles: vi.fn(),
    devPanelClient: {
      getToggles: vi.fn(),
      patchToggles: vi.fn(),
      resetToggles: vi.fn(),
    },
  };
});

import { useBackendTogglesMutation } from '@/dev-tools/panel/hooks/useBackendTogglesMutation';
import { devPanelClient } from '@/dev-tools/panel/api/devPanelClient';

const mockPatch = devPanelClient.patchToggles as ReturnType<typeof vi.fn>;
const mockReset = devPanelClient.resetToggles as ReturnType<typeof vi.fn>;

describe('useBackendTogglesMutation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('setToggle calls patchToggles', async () => {
    mockPatch.mockResolvedValue({ updated: ['llm'], toggles: { llm: false } });
    const { result } = renderHook(() => useBackendTogglesMutation());
    await act(async () => {
      await result.current.setToggle('llm', false);
    });
    expect(mockPatch).toHaveBeenCalledWith({ llm: false });
    expect(result.current.mutationError).toBeNull();
  });

  it('setToggle ignores concurrent click on same name', async () => {
    let resolveFirst: () => void = () => {};
    const firstPromise = new Promise<{ updated: string[]; toggles: Record<string, boolean> }>(
      resolve => {
        resolveFirst = () => resolve({ updated: ['llm'], toggles: { llm: false } });
      }
    );
    mockPatch.mockImplementation(() => firstPromise);
    const { result } = renderHook(() => useBackendTogglesMutation());
    void act(() => {
      void result.current.setToggle('llm', false);
    });
    await act(async () => {
      await result.current.setToggle('llm', true);
    });
    expect(mockPatch).toHaveBeenCalledTimes(1);
    resolveFirst();
  });

  it('setToggle on different names does NOT block', async () => {
    mockPatch.mockResolvedValue({ updated: [], toggles: {} });
    const { result } = renderHook(() => useBackendTogglesMutation());
    await act(async () => {
      await Promise.all([
        result.current.setToggle('llm', false),
        result.current.setToggle('embedding', true),
      ]);
    });
    expect(mockPatch).toHaveBeenCalledTimes(2);
  });

  it('exposes mutationError on failure', async () => {
    mockPatch.mockRejectedValue(new DevPanelClientError('boom', 500));
    const { result } = renderHook(() => useBackendTogglesMutation());
    await act(async () => {
      try {
        await result.current.setToggle('llm', false);
      } catch {
        /* expected */
      }
    });
    expect(result.current.mutationError).not.toBeNull();
    expect(result.current.mutationError?.status).toBe(500);
  });

  it('resetAll calls resetToggles', async () => {
    mockReset.mockResolvedValue({ toggles: { llm: true }, knownServices: ['llm'] });
    const { result } = renderHook(() => useBackendTogglesMutation());
    await act(async () => {
      await result.current.resetAll();
    });
    expect(mockReset).toHaveBeenCalledOnce();
  });
});
