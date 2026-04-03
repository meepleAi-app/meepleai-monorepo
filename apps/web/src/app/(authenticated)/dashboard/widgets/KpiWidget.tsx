'use client';

import { useRouter } from 'next/navigation';

import { cn } from '@/lib/utils';

import { BentoWidget, WidgetLabel } from './BentoWidget';

interface KpiWidgetProps {
  label: string;
  value: string | number;
  badge?: string;
  badgePositive?: boolean;
  sub?: string;
  accentColor: string;
  colSpan: number;
  tabletColSpan?: number;
  rowSpan: number;
  href?: string;
}

export function KpiWidget({
  label,
  value,
  badge,
  badgePositive,
  sub,
  accentColor,
  colSpan,
  tabletColSpan,
  rowSpan,
  href,
}: KpiWidgetProps) {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={colSpan}
      tabletColSpan={tabletColSpan}
      rowSpan={rowSpan}
      accentColor={accentColor}
      onClick={href ? () => router.push(href) : undefined}
    >
      <WidgetLabel>{label}</WidgetLabel>
      <p
        className="font-quicksand text-[26px] font-extrabold leading-none tracking-tight"
        style={{ color: accentColor }}
      >
        {value}
      </p>
      {badge && (
        <span
          className={cn(
            'inline-block mt-1.5 text-[10px] font-bold px-2 py-0.5 rounded-full',
            badgePositive
              ? 'bg-emerald-500/15 text-emerald-400'
              : 'bg-muted/60 text-muted-foreground'
          )}
        >
          {badge}
        </span>
      )}
      {sub && !badge && <p className="text-[10px] text-muted-foreground mt-1">{sub}</p>}
    </BentoWidget>
  );
}
