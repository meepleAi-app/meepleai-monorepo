'use client';

import { type LucideIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

interface EmptyStateCardProps {
  title: string;
  description: string;
  ctaLabel: string;
  onCtaClick: () => void;
  icon?: LucideIcon;
  entityColor?: string; // HSL string like "25 95% 45%"
  className?: string;
}

export function EmptyStateCard({
  title,
  description,
  ctaLabel,
  onCtaClick,
  icon: Icon,
  entityColor = '220 15% 45%', // default: custom/silver
  className,
}: EmptyStateCardProps) {
  return (
    <div
      className={cn(
        'rounded-xl border bg-card p-6 flex flex-col items-center justify-center text-center gap-4',
        'min-h-[200px] shadow-sm hover:shadow-md transition-shadow duration-300',
        className
      )}
      style={{
        borderColor: `hsl(${entityColor} / 0.2)`,
      }}
    >
      {Icon && (
        <div
          className="w-12 h-12 rounded-full flex items-center justify-center"
          style={{ backgroundColor: `hsl(${entityColor} / 0.1)` }}
        >
          <Icon className="w-6 h-6" style={{ color: `hsl(${entityColor})` }} />
        </div>
      )}
      <div>
        <h3 className="font-quicksand font-bold text-lg">{title}</h3>
        <p className="text-sm text-muted-foreground mt-1">{description}</p>
      </div>
      <button
        onClick={onCtaClick}
        className="px-4 py-2 rounded-lg text-sm font-semibold text-white transition-colors"
        style={{ backgroundColor: `hsl(${entityColor})` }}
      >
        {ctaLabel}
      </button>
    </div>
  );
}
