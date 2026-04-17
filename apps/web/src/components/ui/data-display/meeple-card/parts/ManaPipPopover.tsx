'use client';

/**
 * ManaPipPopover
 *
 * Radix Popover that shows when a ManaPip with count > 0 is clicked.
 * Displays a scrollable list of linked entities and an optional "Create" button.
 */

import { type ReactNode } from 'react';

import { Plus } from 'lucide-react';
import Link from 'next/link';

import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays/popover';

import { entityHsl, entityIcon } from '../tokens';

import type { MeepleEntityType } from '../types';
import type { ManaPipItem } from './ManaPips';

export interface ManaPipPopoverProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  items: ManaPipItem[];
  onCreate?: () => void;
  createLabel?: string;
  entityType: MeepleEntityType;
  children: ReactNode;
}

export function ManaPipPopover({
  open,
  onOpenChange,
  items,
  onCreate,
  createLabel = 'Create',
  entityType,
  children,
}: ManaPipPopoverProps) {
  const color = entityHsl(entityType);
  const icon = entityIcon[entityType];

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger asChild>{children}</PopoverTrigger>

      <PopoverContent
        align="start"
        sideOffset={6}
        className="w-56 p-0 overflow-hidden"
        style={{
          border: '1px solid var(--mc-border, hsl(215 20% 65% / 0.2))',
          background: 'var(--mc-bg-card, hsl(222 47% 11%))',
        }}
      >
        {/* Item list */}
        {items.length > 0 && (
          <ul className="max-h-48 overflow-y-auto py-1" role="list">
            {items.map(item => (
              <li key={item.id}>
                <Link
                  href={item.href}
                  onClick={() => onOpenChange(false)}
                  className="flex items-center gap-2 px-3 py-1.5 text-sm hover:bg-white/5 transition-colors"
                  style={{ color: 'var(--mc-text, inherit)' }}
                >
                  <span aria-hidden="true" className="shrink-0 text-base leading-none">
                    {icon}
                  </span>
                  <span className="truncate">{item.label}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}

        {/* Create button */}
        {onCreate && (
          <div
            className="border-t px-3 py-2"
            style={{ borderColor: 'var(--mc-border, hsl(215 20% 65% / 0.2))' }}
          >
            <button
              type="button"
              onClick={() => {
                onCreate();
                onOpenChange(false);
              }}
              className="flex w-full items-center gap-2 rounded-sm px-1 py-1 text-sm font-medium transition-colors hover:opacity-80"
              style={{ color }}
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
