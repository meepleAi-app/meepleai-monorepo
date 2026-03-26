'use client';

import { ArrowRight } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

interface LibraryHeroBannerProps {
  hide?: boolean;
  className?: string;
}

/**
 * Discovery banner for the library page.
 *
 * When sessions scheduling API is ready, add a session variant
 * using useNextSession() hook (see spec for details).
 */
export function LibraryHeroBanner({ hide, className }: LibraryHeroBannerProps) {
  if (hide) return null;

  return (
    <div
      role="region"
      aria-label="Esplora il catalogo"
      className={cn(
        'flex items-center justify-between rounded-[14px] border border-primary/20 px-5 py-4',
        'bg-gradient-to-r from-primary/15 via-accent/8 to-blue-500/8',
        'flex-col gap-2 sm:flex-row sm:gap-4',
        className
      )}
    >
      <div className="flex flex-col gap-1">
        <span className="font-quicksand text-base font-bold sm:text-lg">Scopri nuovi giochi</span>
        <span className="text-xs text-muted-foreground sm:text-sm">
          Esplora il catalogo e arricchisci la tua collezione
        </span>
      </div>
      <Button asChild size="sm" variant="default" className="shrink-0">
        <Link href="/library?tab=public">
          Esplora Catalogo <ArrowRight className="ml-1 h-3.5 w-3.5" />
        </Link>
      </Button>
    </div>
  );
}
