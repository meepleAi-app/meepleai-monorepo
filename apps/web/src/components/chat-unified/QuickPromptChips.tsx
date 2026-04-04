'use client';

import { cn } from '@/lib/utils';

interface QuickPromptChipsProps {
  prompts: string[];
  onSelect: (prompt: string) => void;
  hidden?: boolean;
  className?: string;
}

export function QuickPromptChips({ prompts, onSelect, hidden, className }: QuickPromptChipsProps) {
  if (hidden || prompts.length === 0) return null;

  return (
    <div className={cn('flex flex-row gap-2 overflow-x-auto scrollbar-hide pb-1', className)}>
      {prompts.map(prompt => (
        <button
          key={prompt}
          type="button"
          onClick={() => onSelect(prompt)}
          className={cn(
            'flex-shrink-0 rounded-full border px-3 py-1.5 text-sm',
            'bg-amber-500/10 text-amber-300 border-amber-500/20',
            'hover:bg-amber-500/20 transition-colors cursor-pointer whitespace-nowrap'
          )}
        >
          {prompt}
        </button>
      ))}
    </div>
  );
}
