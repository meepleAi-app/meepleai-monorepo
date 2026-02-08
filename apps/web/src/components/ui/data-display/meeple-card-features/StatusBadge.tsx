/**
 * StatusBadge - Collection Status Indicator for MeepleCard
 * Issue #3826
 */

'use client';

import { CheckCircle, Heart, Play, ArrowLeftRight, Repeat, type LucideIcon } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

export type GameStatus = 'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade';

export interface StatusBadgeProps {
  status: GameStatus | GameStatus[];
  showIcon?: boolean;
  size?: 'sm' | 'md';
  className?: string;
}

const statusConfig: Record<
  GameStatus,
  { label: string; color: string; bgClass: string; textClass: string; icon: LucideIcon; description: string }
> = {
  owned: {
    label: 'Posseduto',
    color: 'teal',
    bgClass: 'bg-teal-100 dark:bg-teal-900',
    textClass: 'text-teal-900 dark:text-teal-100',
    icon: CheckCircle,
    description: 'Questo gioco è nella tua collezione',
  },
  wishlisted: {
    label: 'Wishlist',
    color: 'amber',
    bgClass: 'bg-amber-100 dark:bg-amber-900',
    textClass: 'text-amber-900 dark:text-amber-100',
    icon: Heart,
    description: 'Questo gioco è nella tua wishlist',
  },
  played: {
    label: 'Giocato',
    color: 'blue',
    bgClass: 'bg-blue-100 dark:bg-blue-900',
    textClass: 'text-blue-900 dark:text-blue-100',
    icon: Play,
    description: 'Hai già giocato a questo gioco',
  },
  borrowed: {
    label: 'Prestato',
    color: 'purple',
    bgClass: 'bg-purple-100 dark:bg-purple-900',
    textClass: 'text-purple-900 dark:text-purple-100',
    icon: ArrowLeftRight,
    description: 'Questo gioco è in prestito',
  },
  'for-trade': {
    label: 'In scambio',
    color: 'orange',
    bgClass: 'bg-orange-100 dark:bg-orange-900',
    textClass: 'text-orange-900 dark:text-orange-100',
    icon: Repeat,
    description: 'Questo gioco è disponibile per scambio',
  },
};

export function StatusBadge({ status, showIcon = false, size = 'sm', className }: StatusBadgeProps) {
  const statuses = Array.isArray(status) ? status : [status];

  if (statuses.length === 0) return null;

  return (
    <div className={cn('flex flex-col gap-1', className)} data-testid="status-badge-container">
      {statuses.map((s) => {
        const config = statusConfig[s];
        const Icon = config.icon;

        return (
          <TooltipProvider key={s}>
            <Tooltip>
              <TooltipTrigger asChild>
                <span
                  className={cn(
                    'inline-flex items-center gap-1 px-2 py-0.5 rounded-md font-semibold',
                    size === 'sm' ? 'text-[10px]' : 'text-xs',
                    config.bgClass,
                    config.textClass
                  )}
                  data-testid={`status-badge-${s}`}
                >
                  {showIcon && <Icon className="w-3 h-3" />}
                  {config.label}
                </span>
              </TooltipTrigger>
              <TooltipContent>
                <p>{config.description}</p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        );
      })}
    </div>
  );
}
