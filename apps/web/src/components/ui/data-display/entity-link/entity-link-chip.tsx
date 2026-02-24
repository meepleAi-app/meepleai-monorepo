'use client';

/**
 * EntityLinkChip — C3
 *
 * Small pill-shaped chip displaying a link type with its color, label,
 * and directional symbol. Reusable across card, drawer, modal, and graph.
 *
 * Issue #5159 — Epic C3
 */

import React from 'react';

import { cn } from '@/lib/utils';

import { LINK_TYPE_CONFIG, type EntityLinkType } from './entity-link-types';

export interface EntityLinkChipProps {
  linkType: EntityLinkType;
  /** When true, the chip is slightly larger (default: false = compact) */
  size?: 'sm' | 'md';
  className?: string;
}

export function EntityLinkChip({ linkType, size = 'sm', className }: EntityLinkChipProps) {
  const config = LINK_TYPE_CONFIG[linkType];
  const { label, directionIcon, color } = config;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-0.5',
        'rounded-full font-medium font-nunito leading-none',
        'select-none whitespace-nowrap',
        size === 'sm' ? 'px-1.5 py-0.5 text-[10px]' : 'px-2 py-1 text-xs',
        className
      )}
      style={{
        background: `hsl(${color} / 0.15)`,
        border: `1px solid hsl(${color} / 0.4)`,
        color: `hsl(${color})`,
      }}
      aria-label={`${label} ${directionIcon}`}
    >
      {label} {directionIcon}
    </span>
  );
}

export default EntityLinkChip;
