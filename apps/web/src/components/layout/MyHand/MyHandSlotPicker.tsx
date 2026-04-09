'use client';

import React from 'react';

import type { AssignSlotPayload, MyHandSlotType } from '@/stores/my-hand/types';

interface MyHandSlotPickerProps {
  isOpen: boolean;
  slotType: MyHandSlotType | null;
  onClose: () => void;
  onConfirm: (slotType: MyHandSlotType, payload: AssignSlotPayload) => void;
}

export function MyHandSlotPicker({ isOpen }: MyHandSlotPickerProps): React.JSX.Element | null {
  if (!isOpen) return null;
  return <div data-testid="slot-picker">Picker (TODO)</div>;
}
