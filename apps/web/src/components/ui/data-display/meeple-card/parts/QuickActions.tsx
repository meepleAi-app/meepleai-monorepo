import type { MeepleCardAction } from '../types';

interface QuickActionsProps {
  actions: MeepleCardAction[];
}

export function QuickActions({ actions }: QuickActionsProps) {
  if (actions.length === 0) return null;

  return (
    <div className="absolute right-2 top-2 z-10 flex flex-col gap-1.5 opacity-0 transition-opacity duration-300 group-hover:opacity-100">
      {actions.map((action, i) => (
        <button
          key={i}
          onClick={e => {
            e.stopPropagation();
            action.onClick();
          }}
          disabled={action.disabled}
          title={action.label}
          className="flex h-[30px] w-[30px] items-center justify-center rounded-full border border-[var(--mc-nav-icon-border)] bg-[var(--mc-nav-icon-bg)] text-sm backdrop-blur-lg transition-transform duration-300 hover:scale-110 hover:shadow-[var(--mc-shadow-md)] disabled:cursor-not-allowed disabled:opacity-45"
        >
          {action.icon}
        </button>
      ))}
    </div>
  );
}
