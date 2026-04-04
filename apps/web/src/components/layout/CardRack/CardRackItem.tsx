'use client';

import Link from 'next/link';

import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

export interface CardRackItemProps {
  href: string;
  icon: LucideIcon;
  label: string;
  isActive?: boolean;
  isExpanded?: boolean;
  badge?: number | string;
}

export function CardRackItem({
  href,
  icon: Icon,
  label,
  isActive = false,
  isExpanded = false,
  badge,
}: CardRackItemProps) {
  return (
    <Link
      href={href}
      aria-label={label}
      aria-current={isActive ? 'page' : undefined}
      className={cn(
        'relative flex items-center rounded-lg',
        'min-h-[44px] transition-colors duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring',
        isExpanded ? 'gap-3 px-3 py-2' : 'justify-center px-2 py-2',
        isActive
          ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'
          : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
      )}
    >
      <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
      {isExpanded && <span className="text-sm font-medium truncate">{label}</span>}
      {badge != null && (
        <span
          className={cn(
            'absolute flex items-center justify-center',
            'min-w-[18px] h-[18px] rounded-full',
            'bg-destructive text-destructive-foreground',
            'text-[10px] font-bold px-1',
            isExpanded ? 'right-2' : 'top-1 right-1'
          )}
        >
          {typeof badge === 'number' && badge > 99 ? '99+' : badge}
        </span>
      )}
    </Link>
  );
}
