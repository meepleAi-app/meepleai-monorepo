import { memo } from 'react';

import { cn } from '@/lib/utils';

import type { BlockAction } from '../block-types';

interface ActionsBlockProps {
  title: string;
  entityColor: string;
  data: { type: 'actions'; actions: BlockAction[] };
}

export const ActionsBlock = memo(function ActionsBlock({
  title,
  entityColor,
  data,
}: ActionsBlockProps) {
  return (
    <div className="px-4 py-2">
      <h4
        className="text-[11px] font-bold uppercase tracking-wider mb-1.5"
        style={{ color: `hsl(${entityColor})` }}
      >
        {title}
      </h4>
      <div className="border-t border-white/5 pt-1.5 space-y-0.5">
        {data.actions.map(action => (
          <button
            key={action.label}
            type="button"
            onClick={action.onClick}
            className={cn(
              'block w-full text-left text-xs py-0.5',
              'transition-colors hover:text-white',
              action.variant === 'danger' ? 'text-red-400 hover:text-red-300' : 'text-slate-400'
            )}
          >
            → {action.label}
          </button>
        ))}
      </div>
    </div>
  );
});
