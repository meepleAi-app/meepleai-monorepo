import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import { createEmptySlots } from './types';

import type { MyHandSlotType, MyHandSlot, AssignSlotPayload } from './types';

interface MyHandState {
  slots: Record<MyHandSlotType, MyHandSlot>;
  isSidebarCollapsed: boolean;
  isMobileExpanded: boolean;
  isLoading: boolean;
  assignSlot: (slotType: MyHandSlotType, payload: AssignSlotPayload) => void;
  clearSlot: (slotType: MyHandSlotType) => void;
  markSlotInvalid: (slotType: MyHandSlotType) => void;
  toggleSidebarCollapsed: () => void;
  toggleMobileExpanded: () => void;
  setLoading: (loading: boolean) => void;
  hydrateFromServer: (
    slots: Array<{
      slotType: string;
      entityId: string | null;
      entityType: string | null;
      entityLabel: string | null;
      entityImageUrl: string | null;
      pinnedAt: string | null;
    }>
  ) => void;
}

const INITIAL_STATE = {
  slots: createEmptySlots(),
  isSidebarCollapsed: false,
  isMobileExpanded: false,
  isLoading: false,
};

export const useMyHandStore = create<MyHandState>()(
  devtools(
    immer(set => ({
      ...INITIAL_STATE,

      assignSlot: (slotType, payload) =>
        set(state => {
          state.slots[slotType].entityId = payload.entityId;
          state.slots[slotType].entityType = payload.entityType;
          state.slots[slotType].entityLabel = payload.entityLabel;
          state.slots[slotType].entityImageUrl = payload.entityImageUrl;
          state.slots[slotType].pinnedAt = new Date().toISOString();
          state.slots[slotType].isEntityValid = true;
        }),

      clearSlot: slotType =>
        set(state => {
          state.slots[slotType].entityId = null;
          state.slots[slotType].entityType = null;
          state.slots[slotType].entityLabel = null;
          state.slots[slotType].entityImageUrl = null;
          state.slots[slotType].pinnedAt = null;
          state.slots[slotType].isEntityValid = true;
        }),

      markSlotInvalid: slotType =>
        set(state => {
          state.slots[slotType].isEntityValid = false;
        }),

      toggleSidebarCollapsed: () =>
        set(state => {
          state.isSidebarCollapsed = !state.isSidebarCollapsed;
        }),

      toggleMobileExpanded: () =>
        set(state => {
          state.isMobileExpanded = !state.isMobileExpanded;
        }),

      setLoading: loading =>
        set(state => {
          state.isLoading = loading;
        }),

      hydrateFromServer: serverSlots =>
        set(state => {
          for (const s of serverSlots) {
            const slotType = s.slotType as MyHandSlotType;
            if (!['toolkit', 'game', 'session', 'ai'].includes(slotType)) continue;
            state.slots[slotType].entityId = s.entityId;
            state.slots[slotType].entityType = s.entityType;
            state.slots[slotType].entityLabel = s.entityLabel;
            state.slots[slotType].entityImageUrl = s.entityImageUrl;
            state.slots[slotType].pinnedAt = s.pinnedAt;
            state.slots[slotType].isEntityValid = true;
          }
        }),
    })),
    { name: 'my-hand-store' }
  )
);

// Expose getInitialState for test resets (Zustand devtools/immer does not auto-expose this)
(useMyHandStore as unknown as { getInitialState: () => typeof INITIAL_STATE }).getInitialState =
  () => INITIAL_STATE;

// Selectors
export const selectSlot = (slotType: MyHandSlotType) => (s: MyHandState) => s.slots[slotType];
export const selectIsSidebarCollapsed = (s: MyHandState) => s.isSidebarCollapsed;
export const selectIsMobileExpanded = (s: MyHandState) => s.isMobileExpanded;
