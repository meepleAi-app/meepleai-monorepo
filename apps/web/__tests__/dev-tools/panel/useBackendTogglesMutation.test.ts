import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useBackendTogglesMutation } from '@/dev-tools/panel/hooks/useBackendTogglesMutation';
import * as client from '@/dev-tools/panel/api/devPanelClient';

describe('useBackendTogglesMutation', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('setToggle calls patchToggles', async () => {
    const spy = vi
      .spyOn(client, 'patchToggles')
      .mockResolvedValue({ updated: ['llm'], toggles: { llm: false } });
    const { result } = renderHook(() => useBackendTogglesMutation());
    await act(async () => {
      await result.current.setToggle('llm', false);
    });
    expect(spy).toHaveBeenCalledWith({ llm: false });
    expect(result.current.mutationError).toBeNull();
  });

  it('setToggle ignores concurrent click on same name', async () => {
    let resolveFirst: () => void = () => {};
    const firstPromise = new Promise<{ updated: string[]; toggles: Record<string, boolean> }>(
      resolve => {
        resolveFirst = () => resolve({ updated: ['llm'], toggles: { llm: false } });
      }
    );
    const spy = vi.spyOn(client, 'patchToggles').mockImplementation(() => firstPromise);
    const { result } = renderHook(() => useBackendTogglesMutation());
    void act(() => {
      void result.current.setToggle('llm', false);
    });
    await act(async () => {
      await result.current.setToggle('llm', true);
    });
    expect(spy).toHaveBeenCalledTimes(1);
    resolveFirst();
  });

  it('setToggle on different names does NOT block', async () => {
    const spy = vi.spyOn(client, 'patchToggles').mockResolvedValue({ updated: [], toggles: {} });
    const { result } = renderHook(() => useBackendTogglesMutation());
    await act(async () => {
      await Promise.all([
        result.current.setToggle('llm', false),
        result.current.setToggle('embedding', true),
      ]);
    });
    expect(spy).toHaveBeenCalledTimes(2);
  });

  it('exposes mutationError on failure', async () => {
    vi.spyOn(client, 'patchToggles').mockRejectedValue(new client.DevPanelClientError('boom', 500));
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
    const spy = vi
      .spyOn(client, 'resetToggles')
      .mockResolvedValue({ toggles: { llm: true }, knownServices: ['llm'] });
    const { result } = renderHook(() => useBackendTogglesMutation());
    await act(async () => {
      await result.current.resetAll();
    });
    expect(spy).toHaveBeenCalledOnce();
  });
});
