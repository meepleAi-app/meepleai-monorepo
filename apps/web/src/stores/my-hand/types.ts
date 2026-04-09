export type MyHandSlotType = 'toolkit' | 'game' | 'session' | 'ai';

export interface MyHandSlot {
  slotType: MyHandSlotType;
  entityId: string | null;
  entityType: string | null;
  entityLabel: string | null;
  entityImageUrl: string | null;
  pinnedAt: string | null;
  isEntityValid: boolean;
}

export interface AssignSlotPayload {
  entityId: string;
  entityType: string;
  entityLabel: string | null;
  entityImageUrl: string | null;
}

const EMPTY_SLOT = (slotType: MyHandSlotType): MyHandSlot => ({
  slotType,
  entityId: null,
  entityType: null,
  entityLabel: null,
  entityImageUrl: null,
  pinnedAt: null,
  isEntityValid: true,
});

export const createEmptySlots = (): Record<MyHandSlotType, MyHandSlot> => ({
  toolkit: EMPTY_SLOT('toolkit'),
  game: EMPTY_SLOT('game'),
  session: EMPTY_SLOT('session'),
  ai: EMPTY_SLOT('ai'),
});
