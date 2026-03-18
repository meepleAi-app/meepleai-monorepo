/**
 * Skeleton Story
 * Demonstrates the Skeleton loading placeholder with size controls.
 */

'use client';

import { Skeleton } from '@/components/ui/feedback/skeleton';

import type { ShowcaseStory } from '../types';

type SkeletonShowcaseProps = {
  pattern: string;
};

export const skeletonStory: ShowcaseStory<SkeletonShowcaseProps> = {
  id: 'skeleton',
  title: 'Skeleton',
  category: 'Feedback',
  description:
    'Pulsing placeholder for loading states. Compose into card, list, and form patterns.',

  component: function SkeletonStory({ pattern }: SkeletonShowcaseProps) {
    if (pattern === 'card') {
      return (
        <div className="w-64 space-y-3 p-4">
          <Skeleton className="h-40 w-full rounded-lg" />
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-4 w-1/2" />
          <div className="flex gap-2">
            <Skeleton className="h-6 w-16 rounded-full" />
            <Skeleton className="h-6 w-16 rounded-full" />
          </div>
        </div>
      );
    }

    if (pattern === 'list') {
      return (
        <div className="w-80 space-y-3 p-4">
          {[0, 1, 2, 3].map(i => (
            <div key={i} className="flex items-center gap-3">
              <Skeleton className="h-10 w-10 rounded-full" />
              <div className="flex-1 space-y-1.5">
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-3 w-1/2" />
              </div>
            </div>
          ))}
        </div>
      );
    }

    if (pattern === 'form') {
      return (
        <div className="w-80 space-y-4 p-4">
          <div className="space-y-1.5">
            <Skeleton className="h-4 w-16" />
            <Skeleton className="h-10 w-full rounded-lg" />
          </div>
          <div className="space-y-1.5">
            <Skeleton className="h-4 w-24" />
            <Skeleton className="h-10 w-full rounded-lg" />
          </div>
          <Skeleton className="h-10 w-32 rounded-lg" />
        </div>
      );
    }

    // Single
    return (
      <div className="p-4 space-y-2">
        <Skeleton className="h-6 w-48" />
        <Skeleton className="h-4 w-64" />
        <Skeleton className="h-4 w-56" />
      </div>
    );
  },

  defaultProps: {
    pattern: 'card',
  },

  controls: {
    pattern: {
      type: 'select',
      label: 'pattern',
      options: ['card', 'list', 'form', 'text'],
      default: 'card',
    },
  },

  presets: {
    card: { label: 'Card', props: { pattern: 'card' } },
    list: { label: 'List', props: { pattern: 'list' } },
    form: { label: 'Form', props: { pattern: 'form' } },
    text: { label: 'Text', props: { pattern: 'text' } },
  },
};
