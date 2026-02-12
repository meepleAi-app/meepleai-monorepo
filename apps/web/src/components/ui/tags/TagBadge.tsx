'use client';

import React from 'react';
import { cn } from '@/lib/utils';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import type { Tag } from '@/types/tags';

export function TagBadge({ tag, variant, showText = true }: { tag: Tag; variant: string; showText?: boolean }) {
  const Icon = tag.icon;
  const displayText = variant !== 'mobile' && showText;

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div
            className={cn(
              'flex items-center justify-center rounded text-[10px] font-medium',
              displayText ? 'px-1.5 py-1 gap-0.5 w-full' : 'w-6 h-6'
            )}
            style={{ backgroundColor: tag.bgColor ?? 'hsl(0 0% 100% / 0.2)', color: tag.color ?? 'hsl(0 0% 100%)' }}
            role="status"
            aria-label={tag.label}
          >
            {Icon && <Icon className={cn(displayText ? 'w-2.5 h-2.5' : 'w-3 h-3')} />}
            {displayText && <span className="truncate text-[9px] font-semibold">{tag.label}</span>}
          </div>
        </TooltipTrigger>
        {tag.tooltip && <TooltipContent side="right"><p>{tag.tooltip}</p></TooltipContent>}
      </Tooltip>
    </TooltipProvider>
  );
}
