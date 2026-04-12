import { createStore, type StoreApi } from 'zustand/vanilla';

import type { GroupToggles, EndpointOverrides, ToggleConfig } from './types';

export interface MockControlState {
  toggles: ToggleConfig;
  setGroup: (group: string, enabled: boolean) => void;
  setEndpointOverride: (key: string, enabled: boolean) => void;
  clearEndpointOverride: (key: string) => void;
}

export interface MockControlInit {
  allGroups: string[];
  enableList: string[];
  disableList: string[];
}

export function parseGroupList(raw: string | null | undefined): string[] {
  if (!raw) return [];
  return raw
    .split(',')
    .map(s => s.trim())
    .filter(s => s.length > 0);
}

export function computeGroupToggles(
  allGroups: string[],
  enableList: string[],
  disableList: string[]
): GroupToggles {
  const result: GroupToggles = {};
  const hasEnableList = enableList.length > 0;
  for (const g of allGroups) {
    if (disableList.includes(g)) {
      result[g] = false;
    } else if (hasEnableList) {
      result[g] = enableList.includes(g);
    } else {
      result[g] = true;
    }
  }
  return result;
}

export function createMockControlStore(init: MockControlInit): StoreApi<MockControlState> {
  const initialGroups = computeGroupToggles(init.allGroups, init.enableList, init.disableList);
  return createStore<MockControlState>((set, get) => ({
    toggles: { groups: initialGroups, overrides: {} },
    setGroup: (group, enabled) =>
      set({ toggles: { ...get().toggles, groups: { ...get().toggles.groups, [group]: enabled } } }),
    setEndpointOverride: (key, enabled) =>
      set({
        toggles: { ...get().toggles, overrides: { ...get().toggles.overrides, [key]: enabled } },
      }),
    clearEndpointOverride: key => {
      const next: EndpointOverrides = { ...get().toggles.overrides };
      delete next[key];
      set({ toggles: { ...get().toggles, overrides: next } });
    },
  }));
}
