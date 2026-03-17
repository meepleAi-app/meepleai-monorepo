'use client';

import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

interface ContextualBottomNavItemProps {
  id: string;
  label: string;
  icon: LucideIcon;
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive';
  onClick: () => void;
}

export function ContextualBottomNavItem({
  label,
  icon: Icon,
  variant,
  onClick,
}: ContextualBottomNavItemProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'flex flex-col items-center gap-1 px-3 py-2 rounded-lg flex-1',
        'text-xs font-medium font-nunito',
        'transition-colors',
        variant === 'primary' && 'text-primary',
        variant === 'destructive' && 'text-destructive',
        (!variant || variant === 'secondary' || variant === 'ghost') && 'text-muted-foreground',
        'hover:bg-muted/50'
      )}
    >
      <Icon className="w-5 h-5" />
      <span className="truncate max-w-[80px]">{label}</span>
    </button>
  );
}
