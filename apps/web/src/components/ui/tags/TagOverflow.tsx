/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 primitive — see token-bridge-map.md for migration plan. */
'use client';

import React from 'react';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';
import type { Tag } from '@/types/tags';

export function TagOverflow({ hiddenTags, count, variant }: { hiddenTags: Tag[]; count: number; variant: string }) {
  if (count === 0) return null;

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div className={cn('flex items-center justify-center rounded-full bg-card/20 backdrop-blur-sm border border-border text-xs font-bold text-white', variant === 'mobile' ? 'w-5 h-5' : 'w-6 h-6')}>
            +{count}
          </div>
        </TooltipTrigger>
        <TooltipContent side="right">
          <div className="flex flex-col gap-1">
            {hiddenTags.map(tag => (
              <div key={tag.id} className="flex items-center gap-1.5">
                {tag.icon && <tag.icon className="w-3 h-3" />}
                <span className="text-sm">{tag.label}</span>
              </div>
            ))}
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
