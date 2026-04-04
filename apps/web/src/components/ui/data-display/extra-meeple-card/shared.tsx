'use client';

/**
 * Shared infrastructure for Entity ExtraMeepleCard variants.
 * Contains color constants, common sub-components (EntityHeader, EntityTabTrigger,
 * StatCard, EntityLoadingState, EntityErrorState), and the EntityVariant type.
 */

import React from 'react';

import { AlertCircle, Loader2 } from 'lucide-react';

import { TabsTrigger } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

// ============================================================================
// Entity Color Constants (from MeepleCard v2 tokens)
// ============================================================================

export const ENTITY_COLORS = {
  game: {
    hsl: '25 95% 45%',
    accent: 'text-orange-700',
    accentBg: 'bg-orange-100',
    accentBorder: 'border-orange-200/60',
    activeAccent: 'data-[state=active]:text-orange-700',
  },
  player: {
    hsl: '262 83% 58%',
    accent: 'text-purple-700',
    accentBg: 'bg-purple-100',
    accentBorder: 'border-purple-200/60',
    activeAccent: 'data-[state=active]:text-purple-700',
  },
  collection: {
    hsl: '174 60% 40%',
    accent: 'text-teal-700',
    accentBg: 'bg-teal-100',
    accentBorder: 'border-teal-200/60',
    activeAccent: 'data-[state=active]:text-teal-700',
  },
  agent: {
    hsl: '220 70% 55%',
    accent: 'text-blue-700',
    accentBg: 'bg-blue-100',
    accentBorder: 'border-blue-200/60',
    activeAccent: 'data-[state=active]:text-blue-700',
  },
  chat: {
    hsl: '262 83% 58%',
    accent: 'text-violet-700',
    accentBg: 'bg-violet-100',
    accentBorder: 'border-violet-200/60',
    activeAccent: 'data-[state=active]:text-violet-700',
  },
  kb: {
    hsl: '174 60% 40%',
    accent: 'text-teal-700',
    accentBg: 'bg-teal-100',
    accentBorder: 'border-teal-200/60',
    activeAccent: 'data-[state=active]:text-teal-700',
  },
} as const;

export type EntityVariant = keyof typeof ENTITY_COLORS;

// ============================================================================
// EntityHeader
// ============================================================================

export function EntityHeader({
  title,
  subtitle,
  imageUrl,
  color,
  badge,
  badgeIcon,
}: {
  title: string;
  subtitle?: string;
  imageUrl?: string;
  color: string;
  badge?: string;
  badgeIcon?: React.ReactNode;
}) {
  return (
    <div className="relative h-[140px] overflow-hidden">
      {imageUrl ? (
        <div
          className="absolute inset-0 bg-cover bg-center"
          style={{ backgroundImage: `url(${imageUrl})` }}
        />
      ) : (
        <div className="absolute inset-0" style={{ background: `hsl(${color})` }} />
      )}
      <div className="absolute inset-0 bg-gradient-to-b from-black/40 via-black/50 to-black/70" />
      <div
        className="absolute bottom-0 left-0 right-0 h-1"
        style={{ background: `hsl(${color})` }}
      />

      <div className="relative flex h-full flex-col justify-end p-5">
        <div className="flex items-end justify-between">
          <div className="space-y-0.5">
            <h2 className="font-quicksand text-xl font-bold text-white leading-tight line-clamp-2">
              {title}
            </h2>
            {subtitle && <p className="font-nunito text-sm text-white/70">{subtitle}</p>}
          </div>
          {badge && (
            <div className="flex items-center gap-1 rounded-full bg-white/20 backdrop-blur-sm px-2.5 py-1">
              {badgeIcon}
              <span className="font-quicksand text-sm font-bold text-white">{badge}</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// EntityTabTrigger
// ============================================================================

export function EntityTabTrigger({
  value,
  icon: Icon,
  label,
  activeAccent,
  badge,
}: {
  value: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  activeAccent: string;
  badge?: string | number;
}) {
  return (
    <TabsTrigger
      value={value}
      className={cn(
        'flex items-center gap-1.5 px-3 py-1.5 font-nunito text-xs font-medium',
        'data-[state=active]:bg-white data-[state=active]:shadow-sm',
        activeAccent,
        'transition-all duration-200'
      )}
    >
      <Icon className="h-3.5 w-3.5" />
      <span>{label}</span>
      {badge != null && (
        <span className="ml-0.5 rounded-full bg-slate-200/80 px-1.5 py-0.5 font-nunito text-[9px] font-bold text-slate-700 leading-none">
          {badge}
        </span>
      )}
    </TabsTrigger>
  );
}

// ============================================================================
// StatCard
// ============================================================================

export function StatCard({
  label,
  value,
  icon: Icon,
  variant,
}: {
  label: string;
  value: string;
  icon: React.ComponentType<{ className?: string }>;
  variant: EntityVariant;
}) {
  const colors = ENTITY_COLORS[variant];
  return (
    <div className={cn('rounded-lg border p-2.5', colors.accentBorder, `${colors.accentBg}/30`)}>
      <div className="flex items-center gap-1.5 mb-1">
        <Icon className={cn('h-3.5 w-3.5', colors.accent)} />
        <span className="font-nunito text-[10px] text-slate-500">{label}</span>
      </div>
      <p className={cn('font-quicksand text-sm font-bold', colors.accent)}>{value}</p>
    </div>
  );
}

// ============================================================================
// EntityLoadingState
// ============================================================================

export function EntityLoadingState({ className, testId }: { className?: string; testId?: string }) {
  return (
    <div
      className={cn(
        'flex h-[600px] w-[600px] items-center justify-center rounded-2xl',
        'bg-white/70 backdrop-blur-md shadow-lg border border-white/20',
        'max-md:w-full',
        className
      )}
      data-testid={testId}
    >
      <div className="flex flex-col items-center gap-3 text-slate-400">
        <Loader2 className="h-8 w-8 animate-spin" />
        <span className="font-nunito text-sm">Loading...</span>
      </div>
    </div>
  );
}

// ============================================================================
// EntityErrorState
// ============================================================================

export function EntityErrorState({
  error,
  className,
  testId,
}: {
  error: string;
  className?: string;
  testId?: string;
}) {
  return (
    <div
      className={cn(
        'flex h-[600px] w-[600px] items-center justify-center rounded-2xl',
        'bg-white/70 backdrop-blur-md shadow-lg border border-white/20',
        'max-md:w-full',
        className
      )}
      data-testid={testId}
    >
      <div className="flex flex-col items-center gap-3 text-red-400">
        <AlertCircle className="h-8 w-8" />
        <span className="font-nunito text-sm">{error}</span>
      </div>
    </div>
  );
}
