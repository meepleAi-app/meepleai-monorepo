'use client';

import { use } from 'react';

import { ArrowLeft, Layers } from 'lucide-react';
import Link from 'next/link';
import { notFound } from 'next/navigation';

import { CompositionScene } from '@/components/admin/ui-library/CompositionScene';
import { Badge } from '@/components/ui/data-display/badge';
import { getComposition } from '@/config/component-compositions';

interface CompositionDetailPageProps {
  params: Promise<{ id: string }>;
}

export default function CompositionDetailPage({ params }: CompositionDetailPageProps) {
  const { id } = use(params);
  const composition = getComposition(id);

  if (!composition) {
    notFound();
  }

  return (
    <div className="space-y-8 p-6">
      {/* Header */}
      <div className="space-y-4">
        <Link
          href="/admin/ui-library/compositions"
          className="inline-flex items-center gap-1.5 font-nunito text-sm text-muted-foreground transition-colors hover:text-foreground"
          aria-label="Back to Compositions"
        >
          <ArrowLeft className="h-4 w-4" />
          Compositions
        </Link>

        <div className="flex items-start gap-4">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-amber-100 dark:bg-amber-950/40">
            <Layers className="h-5 w-5 text-amber-700 dark:text-amber-400" aria-hidden="true" />
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="font-quicksand text-2xl font-bold text-foreground">
                {composition.name}
              </h1>
              <Badge
                variant="secondary"
                className="bg-amber-50 text-xs text-amber-800 dark:bg-amber-950/30 dark:text-amber-300"
              >
                {composition.area}
              </Badge>
            </div>
            <p className="mt-1 font-nunito text-base text-muted-foreground">
              {composition.description}
            </p>
          </div>
        </div>
      </div>

      {/* Scene */}
      <section className="space-y-3">
        <h2 className="font-quicksand text-lg font-semibold text-foreground">Scene</h2>
        <CompositionScene composition={composition} />
      </section>

      {/* Component list */}
      <section className="space-y-3">
        <h2 className="font-quicksand text-lg font-semibold text-foreground">
          Components in this Composition
        </h2>
        <div className="flex flex-wrap gap-2">
          {composition.componentIds.map(componentId => (
            <Link
              key={componentId}
              href={`/admin/ui-library/${componentId}`}
              className="inline-flex items-center rounded-lg border border-border/60 bg-background px-3 py-1.5 font-nunito text-sm text-muted-foreground transition-colors hover:border-amber-200 hover:bg-amber-50 hover:text-amber-900 dark:hover:bg-amber-950/20 dark:hover:text-amber-300"
            >
              {componentId}
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}
