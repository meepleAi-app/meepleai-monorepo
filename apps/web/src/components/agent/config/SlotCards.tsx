/**
 * Slot Cards - Visual agent slot management
 * Issue #3240 (FRONT-004)
 */

'use client';

import { Lock, CircleDot } from 'lucide-react';
import { Button } from '@/components/ui/primitives/button';

interface Slot {
  id: string;
  status: 'active' | 'available' | 'locked';
  gameTitle?: string;
  typologyName?: string;
}

export function SlotCards() {
  // Mock data - replace with useSlotManagement hook
  const slots: Slot[] = [
    { id: '1', status: 'active', gameTitle: '7 Wonders', typologyName: 'Rules Helper' },
    { id: '2', status: 'active', gameTitle: 'Splendor', typologyName: 'Strategy' },
    { id: '3', status: 'available' },
    { id: '4', status: 'available' },
    { id: '5', status: 'available' },
    { id: '6', status: 'locked' },
    { id: '7', status: 'locked' },
    { id: '8', status: 'locked' },
  ];

  const activeCount = slots.filter(s => s.status === 'active').length;
  const totalSlots = slots.filter(s => s.status !== 'locked').length;
  const lockedCount = slots.filter(s => s.status === 'locked').length;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <label className="text-sm font-medium text-slate-200">Agent Slots</label>
        <span className="text-xs text-slate-400">
          {activeCount} / {totalSlots} slots used
        </span>
      </div>

      {/* Slot Grid */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {slots.map(slot => (
          <div
            key={slot.id}
            className={`
              relative rounded-lg border-2 p-4 min-h-[100px] transition-all
              ${
                slot.status === 'active'
                  ? 'border-cyan-400 bg-cyan-500/10'
                  : slot.status === 'locked'
                    ? 'border-purple-500/30 bg-purple-500/5'
                    : 'border-slate-700 bg-slate-900'
              }
            `}
          >
            {slot.status === 'active' && (
              <>
                <div className="text-2xl mb-1">🤖</div>
                <div className="text-xs font-medium text-white truncate">
                  {slot.gameTitle}
                </div>
                <div className="text-xs text-slate-400 truncate">{slot.typologyName}</div>
              </>
            )}

            {slot.status === 'available' && (
              <div className="flex items-center justify-center h-full">
                <CircleDot className="h-8 w-8 text-slate-600" />
              </div>
            )}

            {slot.status === 'locked' && (
              <div className="flex flex-col items-center justify-center h-full text-center">
                <Lock className="h-6 w-6 text-purple-400 mb-1" />
                <span className="text-xs text-purple-300">Locked</span>
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Upgrade CTA */}
      {lockedCount > 0 && (
        <Button
          variant="outline"
          className="w-full border-purple-500 text-purple-300 hover:bg-purple-500/10 agent-pulse-purple"
        >
          Upgrade for +{lockedCount} slots
        </Button>
      )}
    </div>
  );
}
