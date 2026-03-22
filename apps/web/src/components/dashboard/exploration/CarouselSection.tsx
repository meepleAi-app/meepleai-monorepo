'use client';

import type { ReactNode } from 'react';

import Link from 'next/link';

interface CarouselSectionProps {
  title: string;
  icon: string;
  seeAllHref: string;
  seeAllLabel: string;
  accentColor?: string;
  children: ReactNode;
}

export function CarouselSection({
  title,
  icon,
  seeAllHref,
  seeAllLabel,
  accentColor = 'hsl(25,95%,38%)',
  children,
}: CarouselSectionProps) {
  return (
    <section aria-label={title} className="space-y-3">
      <div className="flex items-center justify-between gap-2">
        <h2 className="font-quicksand text-base font-bold text-foreground flex items-center gap-2">
          <span aria-hidden>{icon}</span>
          {title}
        </h2>

        <Link
          href={seeAllHref}
          className="font-nunito text-xs font-semibold transition-colors hover:opacity-80"
          style={{ color: accentColor }}
          aria-label={seeAllLabel}
        >
          Vedi tutti →
        </Link>
      </div>

      <div
        className="flex gap-3 overflow-x-auto pb-2 scrollbar-none snap-x snap-mandatory"
        role="list"
        aria-label={`Lista: ${title}`}
      >
        {children}
      </div>
    </section>
  );
}
