/**
 * Slot Cards - Visual agent slot management
 * Issue #3240 (FRONT-004)
 * Issue #3247 (FRONT-011) - LockedSlotCard integration
 */

'use client';

import { CircleDot, Loader2 } from 'lucide-react';

import { useAgentSlots } from '@/hooks/queries/useAgentSlots';

import { LockedSlotCard } from '../slots/LockedSlotCard';

interface SlotCardsProps {
  /** User's current tier */
  currentTier?: string;
  /** Callback when upgrade is clicked */
  onUpgradeClick?: () => void;
}

export function SlotCards({ currentTier = 'free', onUpgradeClick }: SlotCardsProps) {
  const { data, isLoading } = useAgentSlots();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-8">
        <Loader2 className="h-6 w-6 animate-spin text-slate-400" />
      </div>
    );
  }

  const slots = data?.slots ?? [];
  const activeCount = data?.used ?? 0;
  const totalSlots = data?.total ?? 0;
  const nonLockedSlots = slots.filter(s => s.status !== 'locked');
  const lockedCount = slots.filter(s => s.status === 'locked').length;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <label className="text-sm font-medium text-slate-200">Agent Slots</label>
        <span className="text-xs text-slate-400">
          {activeCount} / {totalSlots} slots used
        </span>
      </div>

      {/* Slot Grid - Active and Available only */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {nonLockedSlots.map(slot => (
          <div
            key={slot.slotIndex}
            className={`
              relative rounded-lg border-2 p-4 min-h-[100px] transition-all
              ${
                slot.status === 'active'
                  ? 'border-cyan-400 bg-cyan-500/10'
                  : 'border-slate-700 bg-slate-900'
              }
            `}
          >
            {slot.status === 'active' && (
              <>
                <div className="text-2xl mb-1">🤖</div>
                <div className="text-xs font-medium text-white truncate">
                  {slot.agentName ?? 'Agent'}
                </div>
                <div className="text-xs text-slate-400 truncate">{slot.gameId ?? ''}</div>
              </>
            )}

            {slot.status === 'available' && (
              <div className="flex items-center justify-center h-full">
                <CircleDot className="h-8 w-8 text-slate-600" />
              </div>
            )}
          </div>
        ))}

        {nonLockedSlots.length === 0 && (
          <div className="col-span-4 text-center py-4 text-sm text-slate-500">
            No slots available
          </div>
        )}
      </div>

      {/* Premium Upgrade Card - Issue #3247 */}
      {lockedCount > 0 && (
        <LockedSlotCard
          lockedCount={lockedCount}
          currentTier={currentTier}
          onUpgradeClick={onUpgradeClick}
        />
      )}
    </div>
  );
}
