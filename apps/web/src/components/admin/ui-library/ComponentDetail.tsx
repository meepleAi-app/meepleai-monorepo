'use client';

import React, { useState, Suspense } from 'react';

import { ArrowLeft, Zap, Camera } from 'lucide-react';
import dynamic from 'next/dynamic';
import Link from 'next/link';

import { ShowcaseCanvas } from '@/components/showcase/showcase-canvas';
import { ShowcaseControls } from '@/components/showcase/showcase-controls';
import { STORY_MAP } from '@/components/showcase/stories';
import type { PropsState } from '@/components/showcase/types';
import { Badge } from '@/components/ui/data-display/badge';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { ComponentEntry } from '@/config/component-registry';

import { PropsTable } from './PropsTable';
import { StaticRenderer } from './StaticRenderer';

// ─── Lazy-loaded component map (MUST be at module level) ──────────────────────

const LazyComponentMap = dynamic(
  () =>
    import('./component-map').then(mod => ({
      default: function ComponentMapResolver({ entry }: { entry: ComponentEntry }) {
        const Component = mod.COMPONENT_MAP[entry.id];
        return (
          <StaticRenderer
            component={Component}
            mockProps={entry.mockProps}
            mockVariants={entry.mockVariants}
          />
        );
      },
    })),
  { loading: () => <Skeleton className="h-64 w-full" /> }
);

// ─── Interactive preview (uses showcase story) ────────────────────────────────

interface InteractivePreviewProps {
  storyId: string;
}

function InteractivePreview({ storyId }: InteractivePreviewProps) {
  const story = STORY_MAP[storyId];

  const [props, setProps] = useState<PropsState>(story ? (story.defaultProps as PropsState) : {});
  const [bgMode, setBgMode] = useState<'light' | 'dark' | 'grid'>('light');
  const [zoom, setZoom] = useState(1);

  if (!story) {
    return (
      <div className="rounded-xl border border-dashed border-border/50 p-8 text-center">
        <p className="font-nunito text-sm text-muted-foreground">
          Story not found for this component.
        </p>
      </div>
    );
  }

  const StoryComponent = story.component;
  const Decorator = story.decorator;

  const handleChange = (key: string, value: string | boolean | number) => {
    setProps(prev => ({ ...prev, [key]: value }));
  };

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const StoryComp = StoryComponent as React.ComponentType<any>;
  const rendered = Decorator ? (
    <Decorator>
      <StoryComp {...props} />
    </Decorator>
  ) : (
    <StoryComp {...props} />
  );

  return (
    <div className="overflow-hidden rounded-xl border border-border/60">
      <div className="grid" style={{ gridTemplateColumns: '1fr 240px', minHeight: '320px' }}>
        {/* Canvas */}
        <div className="border-r border-border/60">
          <ShowcaseCanvas bgMode={bgMode} zoom={zoom} onBgChange={setBgMode} onZoomChange={setZoom}>
            {rendered}
          </ShowcaseCanvas>
        </div>

        {/* Controls panel */}
        <div className="bg-card/50">
          <ShowcaseControls
            controls={
              story.controls as Partial<
                Record<string, import('@/components/showcase/types').ControlDef>
              >
            }
            values={props}
            onChange={handleChange}
          />
        </div>
      </div>
    </div>
  );
}

// ─── ComponentDetail ──────────────────────────────────────────────────────────

interface ComponentDetailProps {
  entry: ComponentEntry;
}

export function ComponentDetail({ entry }: ComponentDetailProps) {
  const isInteractive = entry.tier === 'interactive';
  const hasStory = isInteractive && Boolean(STORY_MAP[entry.id]);

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="space-y-4">
        {/* Back link */}
        <Link
          href="/admin/ui-library"
          className="inline-flex items-center gap-1.5 font-nunito text-sm text-muted-foreground transition-colors hover:text-foreground"
          aria-label="Back to UI Library"
        >
          <ArrowLeft className="h-4 w-4" />
          UI Library
        </Link>

        {/* Title row */}
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <h1 className="font-quicksand text-2xl font-bold text-foreground">{entry.name}</h1>
            <p className="font-nunito text-base text-muted-foreground">{entry.description}</p>
          </div>

          {/* Tier indicator */}
          {isInteractive ? (
            <div
              className="flex shrink-0 items-center gap-1.5 rounded-full bg-amber-100 px-3 py-1 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300"
              aria-label="Interactive"
            >
              <Zap className="h-3.5 w-3.5" aria-hidden="true" />
              <span className="font-nunito text-xs font-semibold">Interactive</span>
            </div>
          ) : (
            <div
              className="flex shrink-0 items-center gap-1.5 rounded-full bg-muted px-3 py-1 text-muted-foreground"
              aria-label="Static"
            >
              <Camera className="h-3.5 w-3.5" aria-hidden="true" />
              <span className="font-nunito text-xs font-semibold">Static</span>
            </div>
          )}
        </div>

        {/* Meta badges */}
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="outline" className="font-nunito text-xs">
            {entry.category}
          </Badge>
          {entry.areas.map(area => (
            <Badge key={area} variant="secondary" className="font-nunito text-xs capitalize">
              {area}
            </Badge>
          ))}
          {entry.tags?.map(tag => (
            <Badge
              key={tag}
              variant="outline"
              className="font-nunito text-xs text-muted-foreground"
            >
              {tag}
            </Badge>
          ))}
        </div>
      </div>

      {/* Import path */}
      <div className="rounded-lg border border-border/60 bg-muted/30 px-4 py-3">
        <p className="font-quicksand text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-1">
          Import Path
        </p>
        <code className="font-mono text-sm text-foreground" data-testid="import-path">
          {`import { ${entry.name} } from '${entry.importPath}';`}
        </code>
      </div>

      {/* Preview section */}
      <div className="space-y-3">
        <h2 className="font-quicksand text-lg font-semibold text-foreground">Preview</h2>
        {hasStory ? (
          <Suspense fallback={<Skeleton className="h-80 w-full rounded-xl" />}>
            <InteractivePreview storyId={entry.id} />
          </Suspense>
        ) : (
          <Suspense fallback={<Skeleton className="h-64 w-full rounded-xl" />}>
            <LazyComponentMap entry={entry} />
          </Suspense>
        )}
      </div>

      {/* Compositions links */}
      {entry.compositions && entry.compositions.length > 0 && (
        <div className="space-y-3">
          <h2 className="font-quicksand text-lg font-semibold text-foreground">
            Used in Compositions
          </h2>
          <div className="flex flex-wrap gap-2">
            {entry.compositions.map(comp => (
              <Link
                key={comp}
                href={`/admin/ui-library/compositions#${comp}`}
                className="inline-flex items-center rounded-lg border border-border/60 bg-background px-3 py-1.5 font-nunito text-sm text-muted-foreground transition-colors hover:border-amber-200 hover:bg-amber-50 hover:text-amber-900 dark:hover:bg-amber-950/20 dark:hover:text-amber-300"
              >
                {comp}
              </Link>
            ))}
          </div>
        </div>
      )}

      {/* Props table */}
      <div className="space-y-3">
        <h2 className="font-quicksand text-lg font-semibold text-foreground">Props</h2>
        <PropsTable mockProps={entry.mockProps} mockVariants={entry.mockVariants} />
      </div>
    </div>
  );
}
