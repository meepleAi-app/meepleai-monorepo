import { describe, it, expect, beforeEach } from 'vitest';

import { useMiniNavConfigStore, type MiniNavConfig } from '../mini-nav-config-store';

describe('mini-nav-config-store', () => {
  beforeEach(() => {
    useMiniNavConfigStore.getState().clear();
  });

  it('starts with null config', () => {
    expect(useMiniNavConfigStore.getState().config).toBeNull();
  });

  it('setConfig stores the payload', () => {
    const config: MiniNavConfig = {
      breadcrumb: 'Libreria · Hub',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'personal', label: 'Personal', href: '/library?tab=personal', count: 47 },
      ],
      activeTabId: 'hub',
    };
    useMiniNavConfigStore.getState().setConfig(config);
    expect(useMiniNavConfigStore.getState().config).toEqual(config);
  });

  it('clear resets to null', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'x',
      tabs: [{ id: 'a', label: 'A', href: '/a' }],
      activeTabId: 'a',
    });
    useMiniNavConfigStore.getState().clear();
    expect(useMiniNavConfigStore.getState().config).toBeNull();
  });

  it('supports optional primary action', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Home',
      tabs: [{ id: 'a', label: 'A', href: '/' }],
      activeTabId: 'a',
      primaryAction: { label: '＋ Nuova partita', onClick: () => {} },
    });
    expect(useMiniNavConfigStore.getState().config?.primaryAction?.label).toBe('＋ Nuova partita');
  });
});
