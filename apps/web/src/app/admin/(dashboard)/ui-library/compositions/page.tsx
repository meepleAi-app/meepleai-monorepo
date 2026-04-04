'use client';

import { ArrowLeft, Layers } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { COMPOSITIONS } from '@/config/component-compositions';

export default function CompositionsPage() {
  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div className="space-y-4">
        <Link
          href="/admin/ui-library"
          className="inline-flex items-center gap-1.5 font-nunito text-sm text-muted-foreground transition-colors hover:text-foreground"
          aria-label="Back to UI Library"
        >
          <ArrowLeft className="h-4 w-4" />
          UI Library
        </Link>

        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-amber-100 dark:bg-amber-950/40">
            <Layers className="h-5 w-5 text-amber-700 dark:text-amber-400" aria-hidden="true" />
          </div>
          <div>
            <h1 className="font-quicksand text-2xl font-bold text-foreground">Compositions</h1>
            <p className="font-nunito text-sm text-muted-foreground">
              Grouped scenes showing components working together in realistic layouts.
            </p>
          </div>
        </div>
      </div>

      {/* Grid of composition cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {COMPOSITIONS.map(composition => (
          <Link
            key={composition.id}
            href={`/admin/ui-library/compositions/${composition.id}`}
            className="group flex flex-col gap-3 rounded-xl border border-border/50 bg-card/50 p-5 backdrop-blur-sm transition-all duration-200 hover:border-amber-300/50 hover:bg-card/70 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {/* Title + area badge */}
            <div className="flex items-start justify-between gap-2">
              <h2 className="font-quicksand text-base font-semibold leading-tight text-foreground transition-colors group-hover:text-amber-900 dark:group-hover:text-amber-300">
                {composition.name}
              </h2>
              <Badge
                variant="secondary"
                className="shrink-0 bg-amber-50 text-[10px] text-amber-800 dark:bg-amber-950/30 dark:text-amber-300"
              >
                {composition.area}
              </Badge>
            </div>

            {/* Description */}
            <p className="font-nunito text-sm text-muted-foreground line-clamp-2">
              {composition.description}
            </p>

            {/* Component count */}
            <p className="font-nunito text-xs text-muted-foreground/70">
              {composition.componentIds.length} component
              {composition.componentIds.length !== 1 ? 's' : ''}
            </p>
          </Link>
        ))}
      </div>
    </div>
  );
}
