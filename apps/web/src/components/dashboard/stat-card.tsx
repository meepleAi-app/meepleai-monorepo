/**
 * StatCard Component - Issue #4936 (redesigned)
 * Issue #4581 - originally created
 *
 * Compact KPI stat card with warm glassmorphism.
 * Icon + value on same row for density.
 */

import { cn } from '@/lib/utils';

interface StatCardProps {
  icon: string;
  value: number | string;
  label: string;
  sublabel?: string;
  /** Optional percentage change badge (e.g. "+12%") */
  badge?: string;
  /** Whether change is positive (green) or negative (red). Default: neutral. */
  badgeTone?: 'positive' | 'negative' | 'neutral';
  className?: string;
}

export function StatCard({
  icon,
  value,
  label,
  sublabel,
  badge,
  badgeTone = 'neutral',
  className = '',
}: StatCardProps) {
  const badgeClass = {
    positive: 'bg-emerald-100/70 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400',
    negative: 'bg-red-100/70 text-red-700 dark:bg-red-900/40 dark:text-red-400',
    neutral:
      'bg-[rgba(180,120,60,0.10)] text-[hsl(25,80%,40%)] dark:bg-[rgba(220,160,80,0.15)] dark:text-[hsl(25,80%,70%)]',
  }[badgeTone];

  return (
    <div
      className={cn(
        'relative rounded-xl px-4 py-3',
        // Warm glassmorphism
        'bg-[rgba(255,255,255,0.75)] dark:bg-[rgba(30,27,24,0.75)]',
        'backdrop-blur-md',
        'border border-[rgba(200,180,160,0.20)] dark:border-[rgba(100,90,75,0.25)]',
        'shadow-[0_2px_12px_rgba(180,120,60,0.06)] dark:shadow-[0_2px_12px_rgba(0,0,0,0.15)]',
        'hover:shadow-[0_4px_16px_rgba(180,120,60,0.10)] transition-shadow duration-200',
        className
      )}
    >
      {/* Badge (top-right) */}
      {badge && (
        <span
          className={cn(
            'absolute top-2 right-2 text-[10px] font-bold px-1.5 py-0.5 rounded-full leading-none',
            badgeClass
          )}
        >
          {badge}
        </span>
      )}

      {/* Icon + Value row */}
      <div className="flex items-center gap-2.5 mb-1">
        <span className="text-xl leading-none" role="img" aria-label={label}>
          {icon}
        </span>
        <span className="text-2xl font-bold font-quicksand text-foreground leading-none">
          {value}
        </span>
      </div>

      {/* Label */}
      <div className="text-xs font-medium font-nunito text-muted-foreground">{label}</div>

      {/* Sublabel */}
      {sublabel && (
        <div className="text-[10px] text-muted-foreground/70 font-nunito mt-0.5">{sublabel}</div>
      )}
    </div>
  );
}
