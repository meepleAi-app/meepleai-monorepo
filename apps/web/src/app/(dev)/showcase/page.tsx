/**
 * Showcase Homepage — Server Component
 *
 * Grid of all available component stories, grouped by category.
 * Each card links to /showcase/[component-id].
 *
 * Imports from `stories/metadata.ts` (static data only) instead of
 * `stories/index.ts` (which pulls in client components with hooks).
 */

import { Gamepad2, Layers, SlidersHorizontal } from 'lucide-react';
import Link from 'next/link';

import { STORY_METADATA } from '@/components/showcase/stories/metadata';
import type { ShowcaseCategory } from '@/components/showcase/types';
import { cn } from '@/lib/utils';

const CATEGORY_COLORS: Record<ShowcaseCategory, string> = {
  'Data Display': 'bg-blue-50 text-blue-700 dark:bg-blue-900/20 dark:text-blue-300',
  Navigation: 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-300',
  Feedback: 'bg-green-50 text-green-700 dark:bg-green-900/20 dark:text-green-300',
  Tags: 'bg-purple-50 text-purple-700 dark:bg-purple-900/20 dark:text-purple-300',
  Animations: 'bg-pink-50 text-pink-700 dark:bg-pink-900/20 dark:text-pink-300',
  Gates: 'bg-rose-50 text-rose-700 dark:bg-rose-900/20 dark:text-rose-300',
};

export default function ShowcaseHomePage() {
  // Group by category
  const grouped = STORY_METADATA.reduce(
    (acc, story) => {
      if (!acc[story.category]) acc[story.category] = [];
      acc[story.category].push(story);
      return acc;
    },
    {} as Record<string, typeof STORY_METADATA>
  );

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-white to-amber-50/30">
      {/* Header */}
      <div className="border-b border-border/60 bg-white/80 backdrop-blur-sm px-8 py-6">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100">
            <Gamepad2 className="h-5 w-5 text-amber-600" />
          </div>
          <div>
            <h1 className="font-quicksand text-2xl font-bold text-foreground">MeepleAI Showcase</h1>
            <p className="text-sm text-muted-foreground">
              Component library — {STORY_METADATA.length} components · dev only
            </p>
          </div>
        </div>
      </div>

      {/* Content */}
      <div className="mx-auto max-w-5xl space-y-10 px-8 py-8">
        {Object.entries(grouped).map(([category, stories]) => (
          <section key={category}>
            {/* Category header */}
            <div className="mb-4 flex items-center gap-2">
              <Layers className="h-4 w-4 text-muted-foreground" />
              <h2 className="font-quicksand text-sm font-semibold uppercase tracking-wider text-muted-foreground">
                {category}
              </h2>
              <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] text-muted-foreground">
                {stories.length}
              </span>
            </div>

            {/* Story cards */}
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
              {stories.map((story) => {
                const colorClass =
                  CATEGORY_COLORS[story.category] ?? 'bg-muted text-muted-foreground';

                return (
                  <Link
                    key={story.id}
                    href={`/showcase/${story.id}`}
                    className={cn(
                      'group flex flex-col rounded-xl border border-border/60 bg-white p-4 shadow-sm',
                      'transition-all duration-200 hover:border-amber-300 hover:shadow-md'
                    )}
                  >
                    {/* Category badge */}
                    <span
                      className={cn(
                        'mb-3 w-fit rounded-md px-2 py-0.5 text-[10px] font-semibold',
                        colorClass
                      )}
                    >
                      {story.category}
                    </span>

                    {/* Title */}
                    <h3 className="font-quicksand text-sm font-bold text-foreground transition-colors group-hover:text-amber-700">
                      {story.title}
                    </h3>

                    {/* Description */}
                    {story.description && (
                      <p className="mt-1 line-clamp-2 text-xs text-muted-foreground">
                        {story.description}
                      </p>
                    )}

                    {/* Stats */}
                    <div className="mt-auto flex items-center gap-3 pt-3 text-[10px] text-muted-foreground/70">
                      <span className="flex items-center gap-1">
                        <SlidersHorizontal className="h-2.5 w-2.5" />
                        {story.controlCount} controls
                      </span>
                      {story.presetCount > 0 && <span>{story.presetCount} presets</span>}
                    </div>
                  </Link>
                );
              })}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
