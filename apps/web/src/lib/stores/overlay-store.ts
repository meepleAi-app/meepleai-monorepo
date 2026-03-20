import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

// Use string type to avoid coupling store layer to UI component types
type EntityType = string;

interface OverlayDeckItem {
  entityType: EntityType;
  entityId: string;
}

interface OverlayState {
  isOpen: boolean;
  entityType: EntityType | null;
  entityId: string | null;
  deckItems: OverlayDeckItem[] | null;
  deckIndex: number;
  open: (entityType: EntityType, entityId: string) => void;
  openDeck: (items: OverlayDeckItem[], startIndex?: number) => void;
  setDeckIndex: (index: number) => void;
  close: () => void;
  toUrlParam: () => string | null;
  fromUrlParam: (param: string | null) => void;
}

const INITIAL_STATE = {
  isOpen: false,
  entityType: null as EntityType | null,
  entityId: null as string | null,
  deckItems: null as OverlayDeckItem[] | null,
  deckIndex: 0,
};

export const useOverlayStore = create<OverlayState>()(
  devtools(
    (set, get) => ({
      ...INITIAL_STATE,
      open: (entityType, entityId) =>
        set({ isOpen: true, entityType, entityId, deckItems: null, deckIndex: 0 }),
      openDeck: (items, startIndex = 0) =>
        set({
          isOpen: true,
          entityType: items[startIndex]?.entityType ?? null,
          entityId: items[startIndex]?.entityId ?? null,
          deckItems: items,
          deckIndex: startIndex,
        }),
      setDeckIndex: index => {
        const { deckItems } = get();
        if (!deckItems || index < 0 || index >= deckItems.length) return;
        set({
          deckIndex: index,
          entityType: deckItems[index].entityType,
          entityId: deckItems[index].entityId,
        });
      },
      close: () => set({ ...INITIAL_STATE }),
      toUrlParam: () => {
        const { isOpen, entityType, entityId } = get();
        if (!isOpen || !entityType || !entityId) return null;
        return `${entityType}:${entityId}`;
      },
      fromUrlParam: param => {
        if (!param) {
          set({ ...INITIAL_STATE });
          return;
        }
        const colonIndex = param.indexOf(':');
        if (colonIndex === -1) return;
        const entityType = param.slice(0, colonIndex);
        const entityId = param.slice(colonIndex + 1);
        if (entityType && entityId) {
          set({ isOpen: true, entityType, entityId, deckItems: null, deckIndex: 0 });
        }
      },
    }),
    { name: 'overlay-store' }
  )
);
