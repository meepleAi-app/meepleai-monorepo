'use client';

interface HandRailToolbarProps {
  onTogglePin: () => void;
  onToggleExpand: () => void;
  isPinned?: boolean;
  isExpanded?: boolean;
}

export function HandRailToolbar({
  onTogglePin,
  onToggleExpand,
  isPinned = false,
  isExpanded = false,
}: HandRailToolbarProps) {
  return (
    <div className="w-full border-t border-[var(--nh-border-default)] pt-2.5 mt-1.5 flex flex-col items-center gap-1.5">
      <button
        type="button"
        aria-label={isPinned ? 'Unpin current card' : 'Pin current card'}
        onClick={onTogglePin}
        className="flex h-[34px] w-[34px] items-center justify-center rounded-lg border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] text-[13px] hover:bg-white hover:shadow-[var(--shadow-warm-sm)] transition-all"
      >
        📌
      </button>
      <button
        type="button"
        aria-label={isExpanded ? 'Collapse rail' : 'Expand rail'}
        onClick={onToggleExpand}
        className="flex h-[34px] w-[34px] items-center justify-center rounded-lg border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] text-[13px] hover:bg-white hover:shadow-[var(--shadow-warm-sm)] transition-all"
      >
        ⇔
      </button>
    </div>
  );
}
