import { useEffect } from 'react';

import type { PanelUiState } from '@/dev-tools/panel/stores/panelUiStore';

import type { StoreApi } from 'zustand/vanilla';

export function useQueryStringPanelOpen(store: StoreApi<PanelUiState>): void {
  useEffect(() => {
    if (typeof window === 'undefined') return;
    const params = new URLSearchParams(window.location.search);
    if (params.get('devpanel') === '1') {
      store.getState().setOpen(true);
      params.delete('devpanel');
      const newSearch = params.toString();
      const newUrl =
        window.location.pathname + (newSearch ? `?${newSearch}` : '') + window.location.hash;
      window.history.replaceState({}, '', newUrl);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
}
