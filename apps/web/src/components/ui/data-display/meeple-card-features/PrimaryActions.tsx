import { memo } from 'react';

import { cn } from '@/lib/utils';

export interface PrimaryAction {
  icon: string;
  label: string;
  onClick: () => void;
}

interface PrimaryActionsProps {
  actions: PrimaryAction[];
  className?: string;
}

export const PrimaryActions = memo(function PrimaryActions({
  actions,
  className,
}: PrimaryActionsProps) {
  const visible = actions.slice(0, 2);
  if (visible.length === 0) return null;

  return (
    <div className={cn('flex gap-1.5', className)}>
      {visible.map(action => (
        <button
          key={action.label}
          type="button"
          onClick={e => {
            e.stopPropagation();
            action.onClick();
          }}
          title={action.label}
          className={cn(
            'w-[30px] h-[30px] rounded-lg border-none',
            'bg-black/50 backdrop-blur-[8px] text-slate-200',
            'text-sm flex items-center justify-center',
            'transition-colors duration-150 hover:bg-white/20',
            'cursor-pointer'
          )}
        >
          {action.icon}
        </button>
      ))}
    </div>
  );
});
