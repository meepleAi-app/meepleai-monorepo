'use client';

import React from 'react';

import { cn } from '@/lib/utils';
import type { TagStripProps } from '@/types/tags';

import { TagBadge } from './TagBadge';
import { TagOverflow } from './TagOverflow';

export function TagStrip({ tags, maxVisible = 3, variant = 'desktop', position = 'left' }: TagStripProps) {
  if (!tags || tags.length === 0) return null;

  const visibleTags = tags.slice(0, maxVisible);
  const hiddenTags = tags.slice(maxVisible);

  const stripWidth = { desktop: 'w-8', tablet: 'w-7', mobile: 'w-6' }[variant];
  const positionClasses = position === 'left' ? 'left-0 border-r' : 'right-0 border-l';

  return (
    <div
      className={cn(
        'absolute top-0 bottom-0 flex flex-col items-center gap-1 p-1',
        stripWidth,
        positionClasses,
        'bg-gradient-to-b from-black/20 via-black/10 to-black/5 backdrop-blur-sm border-white/10 z-10'
      )}
      role="list"
      aria-label="Entity tags"
    >
      {visibleTags.map((tag, i) => (
        <div key={tag.id} className="animate-in fade-in" style={{ animationDelay: `${i * 50}ms` }}>
          <TagBadge tag={tag} variant={variant} showText={variant !== 'mobile'} />
        </div>
      ))}
      {hiddenTags.length > 0 && <TagOverflow hiddenTags={hiddenTags} count={hiddenTags.length} variant={variant} />}
    </div>
  );
}
