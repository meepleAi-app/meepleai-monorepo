'use client';

import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

interface CompactIconButtonProps {
  icon: LucideIcon;
  count: number;
  label: string;
  entityColor: string; // HSL without hsl() wrapper, e.g. "240 60% 55%"
  onClick?: () => void;
  className?: string;
}

export function CompactIconButton({
  icon: Icon,
  count,
  label,
  entityColor,
  onClick,
  className,
}: CompactIconButtonProps) {
  if (count <= 0) return null;
  const ariaLabel = `${count} ${label}`;
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={ariaLabel}
      className={cn(
        'group/ib relative flex h-9 w-9 items-center justify-center rounded-[10px]',
        'border border-transparent bg-transparent',
        'cursor-pointer transition-all duration-200',
        'hover:-translate-y-0.5 hover:bg-white/60 hover:shadow-[var(--shadow-warm-sm)]',
        'active:scale-[0.92]',
        className
      )}
      style={{ color: `hsl(${entityColor})` }}
    >
      <Icon className="h-4 w-4" />
      <span
        className="absolute -right-1 -top-1 flex h-3.5 min-w-[14px] items-center justify-center rounded-full px-1 text-[8px] font-bold font-quicksand leading-none text-white"
        style={{ backgroundColor: `hsl(${entityColor})`, border: '1.5px solid hsl(var(--card))' }}
      >
        {count}
      </span>
      <span className="pointer-events-none absolute bottom-full left-1/2 mb-1.5 -translate-x-1/2 whitespace-nowrap rounded-md bg-foreground px-2 py-1 text-[9px] font-semibold font-quicksand text-background opacity-0 transition-opacity duration-150 group-hover/ib:opacity-100">
        {ariaLabel}
        <span className="absolute left-1/2 top-full -translate-x-1/2 border-4 border-transparent border-t-foreground" />
      </span>
    </button>
  );
}
