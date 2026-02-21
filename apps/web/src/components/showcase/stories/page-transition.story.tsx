/**
 * PageTransition Story
 */

'use client';

import { useState } from 'react';

import { AnimatePresence } from 'framer-motion';

import { PageTransition } from '@/components/ui/animations/PageTransition';

import type { ShowcaseStory } from '../types';

type PageTransitionShowcaseProps = {
  variant: string;
};

function PageTransitionDemo({ variant }: PageTransitionShowcaseProps) {
  const [key, setKey] = useState(0);

  return (
    <div className="flex flex-col gap-4 items-center">
      <AnimatePresence mode="wait">
        <PageTransition key={key} variant={variant as 'fade' | 'slide' | 'scale'}>
          <div className="w-72 rounded-xl border border-border/40 bg-white/80 p-6 shadow-sm text-center">
            <h3 className="font-quicksand font-bold text-lg text-foreground">Page {key + 1}</h3>
            <p className="text-sm text-muted-foreground mt-1">
              Transition: <strong>{variant}</strong>
            </p>
          </div>
        </PageTransition>
      </AnimatePresence>
      <button
        onClick={() => setKey((k) => k + 1)}
        className="rounded-lg bg-amber-100 px-4 py-2 text-sm font-medium text-amber-900 hover:bg-amber-200 transition-colors"
      >
        Trigger Transition
      </button>
    </div>
  );
}

export const pageTransitionStory: ShowcaseStory<PageTransitionShowcaseProps> = {
  id: 'page-transition',
  title: 'PageTransition',
  category: 'Animations',
  description: 'Smooth page transition animations using Framer Motion.',

  component: PageTransitionDemo,

  defaultProps: {
    variant: 'fade',
  },

  controls: {
    variant: {
      type: 'select',
      label: 'variant',
      options: ['fade', 'slide', 'scale'],
      default: 'fade',
    },
  },

  presets: {
    fade: { label: 'Fade', props: { variant: 'fade' } },
    slide: { label: 'Slide', props: { variant: 'slide' } },
    scale: { label: 'Scale', props: { variant: 'scale' } },
  },
};
