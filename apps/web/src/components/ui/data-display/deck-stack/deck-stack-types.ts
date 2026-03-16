import type { MeepleEntityType } from '../meeple-card-styles';

export type DeckStackPresentation = 'popover' | 'bottomSheet';

export interface DeckStackItem {
  id: string;
  entityType: MeepleEntityType;
  title: string;
  status?: string;
}

export interface DeckStackState {
  isOpen: boolean;
  sourceEntityType: MeepleEntityType | null;
  items: DeckStackItem[];
  anchorRect: DOMRect | null;
}

export interface DeckStackContextValue {
  state: DeckStackState;
  openDeckStack: (
    entityType: MeepleEntityType,
    items: DeckStackItem[],
    anchorRect: DOMRect
  ) => void;
  closeDeckStack: () => void;
}
