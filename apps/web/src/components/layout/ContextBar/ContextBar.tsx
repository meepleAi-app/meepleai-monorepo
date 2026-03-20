'use client';

import { useScrollDirection } from '@/hooks/useScrollDirection';
import { useContextBarStore } from '@/lib/stores/context-bar-store';
import { cn } from '@/lib/utils';

export function ContextBar() {
  const { content, options } = useContextBarStore();
  const scrollDirection = useScrollDirection({ threshold: 50 });

  if (!content) return null;

  const isHidden = !options.alwaysVisible && scrollDirection === 'down';

  return (
    <div
      data-testid="context-bar"
      className={cn(
        'h-11 border-b border-border/50 bg-background/80 backdrop-blur-md',
        'flex items-center px-4 gap-2 overflow-x-auto',
        'transition-all duration-150 motion-reduce:transition-none',
        isHidden && '-translate-y-full opacity-0 pointer-events-none h-0 border-0'
      )}
    >
      {content}
    </div>
  );
}
