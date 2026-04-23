'use client';

import { useId, type ReactNode } from 'react';

import { Plus } from 'lucide-react';
import Link from 'next/link';

import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays/popover';

import { entityLabel } from '../tokens';
import { entityTokens } from '../tokens';
import { ENTITY_ICON_STROKE, entityIcons } from './entity-icons';

import type { ConnectionItem, MeepleEntityType } from '../types';

export interface ConnectionChipPopoverProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  items: ConnectionItem[];
  onCreate?: () => void;
  createLabel?: string;
  entityType: MeepleEntityType;
  children: ReactNode;
}

export function ConnectionChipPopover({
  open,
  onOpenChange,
  items,
  onCreate,
  createLabel = 'Create',
  entityType,
  children,
}: ConnectionChipPopoverProps) {
  const tokens = entityTokens(entityType);
  const Icon = entityIcons[entityType];
  const label = entityLabel[entityType];
  const headerId = useId();

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger asChild>{children}</PopoverTrigger>

      <PopoverContent
        align="start"
        sideOffset={6}
        aria-labelledby={headerId}
        className="w-56 p-0 overflow-hidden"
        style={{
          border: `1px solid ${tokens.border}`,
          background: 'var(--mc-bg-card, hsl(222 47% 11%))',
        }}
      >
        <div
          id={headerId}
          className="flex items-center gap-2 border-b px-3 py-2 text-xs font-semibold uppercase tracking-wide"
          style={{ borderColor: tokens.border, color: tokens.solid }}
        >
          <Icon size={14} strokeWidth={ENTITY_ICON_STROKE} aria-hidden="true" />
          <span>
            {label} ({items.length})
          </span>
        </div>

        {items.length > 0 && (
          <ul className="max-h-60 overflow-y-auto py-1" role="list">
            {items.map(item => (
              <li key={item.id}>
                <Link
                  href={item.href}
                  onClick={() => onOpenChange(false)}
                  className="flex items-center gap-2 px-3 py-1.5 text-sm transition-colors hover:bg-white/5"
                  style={{ color: 'var(--mc-text, inherit)' }}
                >
                  <span className="shrink-0" style={{ color: tokens.solid }}>
                    <Icon size={14} strokeWidth={ENTITY_ICON_STROKE} aria-hidden="true" />
                  </span>
                  <span className="truncate">{item.label}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}

        {onCreate && (
          <div className="border-t px-3 py-2" style={{ borderColor: tokens.border }}>
            <button
              type="button"
              onClick={() => {
                onCreate();
                onOpenChange(false);
              }}
              className="flex w-full items-center gap-2 rounded-sm px-1 py-1 text-sm font-medium transition-colors hover:opacity-80"
              style={{ color: tokens.solid }}
            >
              <Plus size={14} aria-hidden="true" />
              <span>{createLabel}</span>
            </button>
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
}
