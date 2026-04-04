'use client';

import Link from 'next/link';

import { cn } from '@/lib/utils';

const GRADIENTS = {
  primary: 'from-primary/15 via-accent/8 to-[hsl(220,70%,55%)]/8',
  session: 'from-[hsl(240,60%,55%)]/15 via-primary/8 to-accent/8',
  chat: 'from-[hsl(220,80%,55%)]/15 via-accent/8 to-primary/8',
} as const;

const BADGE_STYLES = {
  live: 'bg-primary text-white',
  featured: 'bg-primary text-white',
  new: 'bg-secondary text-white',
} as const;

interface HeroBannerProps {
  title: string;
  subtitle: string;
  badge?: { text: string; variant: 'live' | 'featured' | 'new' };
  cta?: { label: string; href: string; icon?: React.ReactNode };
  gradient?: 'primary' | 'session' | 'chat';
  className?: string;
}

export function HeroBanner({
  title,
  subtitle,
  badge,
  cta,
  gradient = 'primary',
  className,
}: HeroBannerProps) {
  return (
    <section
      className={cn(
        'relative rounded-[14px] overflow-hidden',
        'p-4 sm:p-5',
        'bg-gradient-to-r',
        GRADIENTS[gradient],
        'border border-primary/12',
        className
      )}
    >
      {badge && (
        <span
          className={cn(
            'absolute top-3 right-3 px-2.5 py-0.5 rounded-md',
            'text-[10px] font-bold tracking-wide',
            BADGE_STYLES[badge.variant]
          )}
        >
          {badge.text}
        </span>
      )}
      <h2 className="font-quicksand font-bold text-base sm:text-lg text-foreground pr-20">
        {title}
      </h2>
      <p className="text-xs sm:text-sm text-muted-foreground mt-1">{subtitle}</p>
      {cta && (
        <Link
          href={cta.href}
          className="inline-flex items-center gap-1.5 mt-3 px-3 py-1.5 rounded-lg bg-primary text-white text-xs font-bold font-quicksand hover:bg-primary/90 transition-colors"
        >
          {cta.icon}
          {cta.label}
        </Link>
      )}
    </section>
  );
}
