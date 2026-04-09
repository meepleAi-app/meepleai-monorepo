'use client';

import React, { useState } from 'react';

import { ChevronDown, ChevronUp } from 'lucide-react';

import { clearHandSlot } from '@/lib/api/my-hand';
import { cn } from '@/lib/utils';
import { useMyHandStore } from '@/stores/my-hand/store';
import type { MyHandSlotType } from '@/stores/my-hand/types';

import { MyHandSlot } from './MyHandSlot';
import { MyHandSlotPicker } from './MyHandSlotPicker';

const SLOT_TYPES: MyHandSlotType[] = ['toolkit', 'game', 'session', 'ai'];

export function MyHandBottomBar(): React.JSX.Element {
  const slots = useMyHandStore(s => s.slots);
  const isMobileExpanded = useMyHandStore(s => s.isMobileExpanded);
  const toggleMobileExpanded = useMyHandStore(s => s.toggleMobileExpanded);
  const assignSlot = useMyHandStore(s => s.assignSlot);
  const clearSlot = useMyHandStore(s => s.clearSlot);

  const [pickerOpen, setPickerOpen] = useState(false);
  const [activePickerSlot, setActivePickerSlot] = useState<MyHandSlotType | null>(null);

  const handleAssign = (slotType: MyHandSlotType) => {
    setActivePickerSlot(slotType);
    setPickerOpen(true);
  };

  const handleClear = (slotType: MyHandSlotType) => {
    clearSlot(slotType);
    clearHandSlot(slotType).catch(() => {});
  };

  return (
    <div
      className={cn(
        'fixed bottom-0 left-0 right-0 z-30 border-t border-border bg-background/95 backdrop-blur-md',
        'pb-[env(safe-area-inset-bottom)]'
      )}
    >
      <button
        aria-label={isMobileExpanded ? 'Comprimi La Mia Mano' : 'Espandi La Mia Mano'}
        onClick={toggleMobileExpanded}
        className="absolute -top-7 right-4 flex h-6 w-12 items-center justify-center rounded-t-full border border-b-0 border-border bg-background"
      >
        {isMobileExpanded ? <ChevronDown className="h-3 w-3" /> : <ChevronUp className="h-3 w-3" />}
      </button>

      {!isMobileExpanded ? (
        <div className="flex items-center justify-around px-4 py-2">
          {SLOT_TYPES.map(slotType => (
            <MyHandSlot
              key={slotType}
              slotType={slotType}
              slot={slots[slotType]}
              onAssign={handleAssign}
              onClear={handleClear}
              compact
            />
          ))}
        </div>
      ) : (
        <div
          className="grid grid-cols-2 gap-2 px-4 pb-4 pt-1"
          style={{ maxHeight: '40vh', overflowY: 'auto' }}
        >
          {SLOT_TYPES.map(slotType => (
            <MyHandSlot
              key={slotType}
              slotType={slotType}
              slot={slots[slotType]}
              onAssign={handleAssign}
              onClear={handleClear}
            />
          ))}
        </div>
      )}

      <MyHandSlotPicker
        isOpen={pickerOpen}
        slotType={activePickerSlot}
        onClose={() => setPickerOpen(false)}
        onConfirm={(slotType, payload) => {
          assignSlot(slotType, payload);
          setPickerOpen(false);
        }}
      />
    </div>
  );
}
