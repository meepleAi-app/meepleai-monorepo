'use client';

import React, { useState } from 'react';

import { ChevronLeft, ChevronRight } from 'lucide-react';

import { clearHandSlot, updateHandSlot } from '@/lib/api/my-hand';
import { cn } from '@/lib/utils';
import { selectIsSidebarCollapsed, useMyHandStore } from '@/stores/my-hand/store';
import type { MyHandSlotType } from '@/stores/my-hand/types';

import { MyHandSlot } from './MyHandSlot';
import { MyHandSlotPicker } from './MyHandSlotPicker';

const SLOT_TYPES: MyHandSlotType[] = ['toolkit', 'game', 'session', 'ai'];

export function MyHandSidebar(): React.JSX.Element {
  const slots = useMyHandStore(s => s.slots);
  const isCollapsed = useMyHandStore(selectIsSidebarCollapsed);
  const toggleCollapsed = useMyHandStore(s => s.toggleSidebarCollapsed);
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
    <aside
      className={cn(
        'sticky top-16 flex h-[calc(100vh-64px)] flex-col border-l border-border bg-background transition-all duration-300',
        isCollapsed ? 'w-14' : 'w-[280px]'
      )}
    >
      <div className="flex items-center justify-between border-b border-border px-3 py-2">
        {!isCollapsed && <span className="text-sm font-semibold text-foreground">La Mia Mano</span>}
        <button
          aria-label={isCollapsed ? 'Espandi La Mia Mano' : 'Comprimi La Mia Mano'}
          onClick={toggleCollapsed}
          className="rounded p-1 hover:bg-muted"
        >
          {isCollapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
        </button>
      </div>

      <div className="flex flex-col gap-2 p-2">
        {SLOT_TYPES.map(slotType => (
          <MyHandSlot
            key={slotType}
            slotType={slotType}
            slot={slots[slotType]}
            onAssign={handleAssign}
            onClear={handleClear}
            compact={isCollapsed}
          />
        ))}
      </div>

      <MyHandSlotPicker
        isOpen={pickerOpen}
        slotType={activePickerSlot}
        onClose={() => setPickerOpen(false)}
        onConfirm={(slotType, payload) => {
          assignSlot(slotType, payload);
          setPickerOpen(false);
          updateHandSlot(slotType, payload).catch(() => {});
        }}
      />
    </aside>
  );
}
