/**
 * StatCard Component - Issue #4936 (redesigned)
 * Issue #4581 - originally created
 *
 * Single KPI stat card with warm glassmorphism design.
 * Aligned to mockup design tokens (admin-mockup-agents-section.png style).
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
    neutral:  'bg-[rgba(180,120,60,0.10)] text-[hsl(25,80%,40%)] dark:bg-[rgba(220,160,80,0.15)] dark:text-[hsl(25,80%,70%)]',
  }[badgeTone];

  return (
    <div
      className={cn(
        'relative rounded-2xl px-5 py-4',
        // Warm glassmorphism
        'bg-[rgba(255,255,255,0.80)] dark:bg-[rgba(30,27,24,0.80)]',
        'backdrop-blur-md',
        'border border-[rgba(200,180,160,0.25)] dark:border-[rgba(100,90,75,0.30)]',
        'shadow-[0_4px_16px_rgba(180,120,60,0.08)] dark:shadow-[0_4px_16px_rgba(0,0,0,0.20)]',
        'transition-all duration-200 hover:shadow-[0_8px_24px_rgba(180,120,60,0.12)]',
        className
      )}
    >
      {/* Badge (top-right) */}
      {badge && (
        <span
          className={cn(
            'absolute top-3 right-3 text-xs font-semibold px-1.5 py-0.5 rounded-full',
            badgeClass
          )}
        >
          {badge}
        </span>
      )}

      {/* Icon */}
      <div className="text-3xl mb-2 leading-none" role="img" aria-label={label}>
        {icon}
      </div>

      {/* Value */}
      <div className="text-3xl font-bold font-quicksand text-foreground leading-none mb-1">
        {value}
      </div>

      {/* Label */}
      <div className="text-sm font-medium font-nunito text-muted-foreground">{label}</div>

      {/* Sublabel */}
      {sublabel && (
        <div className="text-xs text-muted-foreground/70 font-nunito mt-0.5">{sublabel}</div>
      )}
    </div>
  );
}
