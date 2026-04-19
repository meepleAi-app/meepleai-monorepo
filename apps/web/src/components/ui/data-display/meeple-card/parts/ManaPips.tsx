'use client';

import { useState } from 'react';

import { entityHsl, entityLabel } from '../tokens';
import { ManaPipPopover } from './ManaPipPopover';

import type { MeepleEntityType } from '../types';

export interface ManaPipItem {
  id: string;
  label: string;
  href: string;
}

export interface ManaPip {
  entityType: MeepleEntityType;
  count?: number;
  items?: ManaPipItem[];
  onCreate?: () => void;
  createLabel?: string;
  colorOverride?: string;
}

export interface KbPipState {
  kbIndexedCount: number;
  kbProcessingCount: number;
}

export function getKbPipColor(state: KbPipState): string {
  if (state.kbIndexedCount > 0) return 'hsl(142, 71%, 45%)'; // green
  if (state.kbProcessingCount > 0) return 'hsl(45, 93%, 47%)'; // yellow
  return 'hsl(0, 0%, 60%)'; // grey
}

interface ManaPipsProps {
  pips: ManaPip[];
  /** sm = 6px pip no badge; md = 8px pip with count badge; lg = 12px pip with text label */
  size?: 'sm' | 'md' | 'lg';
}

const MAX_VISIBLE = 3;

export function ManaPips({ pips, size = 'md' }: ManaPipsProps) {
  if (pips.length === 0) return null;

  const visible = pips.slice(0, MAX_VISIBLE);
  const overflow = pips.length - MAX_VISIBLE;
  const dotSize = size === 'lg' ? 12 : size === 'md' ? 8 : 6;

  return (
    <div className="flex items-center gap-1 px-3 pb-2 pt-0.5">
      {visible.map(pip => (
        <PipRenderer key={pip.entityType} pip={pip} size={size} dotSize={dotSize} />
      ))}
      {overflow > 0 && (
        <span className="text-[9px] font-semibold text-[var(--mc-text-muted,#94a3b8)]">
          +{overflow}
        </span>
      )}
    </div>
  );
}

function PipRenderer({
  pip,
  size,
  dotSize,
}: {
  pip: ManaPip;
  size: 'sm' | 'md' | 'lg';
  dotSize: number;
}) {
  const [popoverOpen, setPopoverOpen] = useState(false);
  const color = pip.colorOverride ?? entityHsl(pip.entityType);
  const isInteractive = pip.onCreate !== undefined;
  const count = pip.count ?? 0;

  const dot = (
    <span
      className="relative inline-flex items-center justify-center rounded-full"
      style={{ width: dotSize, height: dotSize, background: color, flexShrink: 0 }}
    >
      {size !== 'sm' && count > 0 && size !== 'lg' && (
        <span
          className="absolute -top-2 left-1/2 -translate-x-1/2 whitespace-nowrap rounded-full px-1 text-[7px] font-bold text-white"
          style={{ background: color, lineHeight: '10px', minWidth: 12, textAlign: 'center' }}
        >
          {count}
        </span>
      )}
    </span>
  );

  const lgLabel =
    size === 'lg' ? (
      <span className="ml-1.5 text-[11px] font-semibold" style={{ color }}>
        {count} {entityLabel[pip.entityType]}
      </span>
    ) : null;

  if (!isInteractive) {
    return (
      <span data-pip title={pip.entityType} className="inline-flex items-center">
        {dot}
        {lgLabel}
      </span>
    );
  }

  const handleClick = () => {
    if (count === 0) {
      pip.onCreate?.();
    } else {
      setPopoverOpen(true);
    }
  };

  const buttonContent = (
    <button
      type="button"
      data-pip
      aria-label={pip.entityType}
      title={pip.entityType}
      className="inline-flex items-center cursor-pointer transition-transform hover:scale-125 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-1"
      onClick={handleClick}
    >
      {dot}
      {lgLabel}
    </button>
  );

  // count > 0 + md/lg size: wrap in popover
  if (count > 0 && size !== 'sm') {
    return (
      <ManaPipPopover
        open={popoverOpen}
        onOpenChange={setPopoverOpen}
        items={pip.items ?? []}
        onCreate={pip.onCreate}
        createLabel={pip.createLabel}
        entityType={pip.entityType}
      >
        {buttonContent}
      </ManaPipPopover>
    );
  }

  return buttonContent;
}
