import { useCallback, useRef, useState } from 'react';

import { devPanelClient, DevPanelClientError } from '../api/devPanelClient';

export interface UseBackendTogglesMutationResult {
  setToggle: (name: string, value: boolean) => Promise<void>;
  resetAll: () => Promise<void>;
  isMutating: boolean;
  mutationError: DevPanelClientError | null;
}

export function useBackendTogglesMutation(): UseBackendTogglesMutationResult {
  const [isMutating, setIsMutating] = useState(false);
  const [mutationError, setMutationError] = useState<DevPanelClientError | null>(null);
  const inFlightToggles = useRef<Set<string>>(new Set());

  const setToggle = useCallback(async (name: string, value: boolean) => {
    if (inFlightToggles.current.has(name)) return;
    inFlightToggles.current.add(name);
    setIsMutating(true);
    try {
      await devPanelClient.patchToggles({ [name]: value });
      setMutationError(null);
    } catch (err) {
      const e = err instanceof DevPanelClientError ? err : new DevPanelClientError(String(err), 0);
      setMutationError(e);
      throw e;
    } finally {
      inFlightToggles.current.delete(name);
      setIsMutating(false);
    }
  }, []);

  const resetAll = useCallback(async () => {
    setIsMutating(true);
    try {
      await devPanelClient.resetToggles();
      setMutationError(null);
    } catch (err) {
      const e = err instanceof DevPanelClientError ? err : new DevPanelClientError(String(err), 0);
      setMutationError(e);
      throw e;
    } finally {
      setIsMutating(false);
    }
  }, []);

  return { setToggle, resetAll, isMutating, mutationError };
}
