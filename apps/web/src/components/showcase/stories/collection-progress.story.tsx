/**
 * CollectionProgress Story
 * Demonstrates quota usage bars with color-coded warning states.
 */

'use client';

import { CollectionProgressBar } from '@/components/ui/feedback/collection-progress-bar';

import type { ShowcaseStory } from '../types';

type CollectionProgressShowcaseProps = {
  current: number;
  max: number;
  label: string;
  unit: string;
};

export const collectionProgressStory: ShowcaseStory<CollectionProgressShowcaseProps> = {
  id: 'collection-progress',
  title: 'CollectionProgress',
  category: 'Gates',
  description:
    'Quota progress bar with color-coded warnings: green (<75%), amber (75-90%), red (>90%).',

  component: function CollectionProgressStory({
    current,
    max,
    label,
    unit,
  }: CollectionProgressShowcaseProps) {
    return (
      <div className="w-80 space-y-4 p-6">
        <CollectionProgressBar current={current} max={max} label={label} unit={unit} showWarning />
      </div>
    );
  },

  defaultProps: {
    current: 45,
    max: 100,
    label: 'Games',
    unit: 'games',
  },

  controls: {
    current: { type: 'range', label: 'current', min: 0, max: 100, step: 1, default: 45 },
    max: { type: 'range', label: 'max', min: 10, max: 500, step: 10, default: 100 },
    label: { type: 'text', label: 'label', default: 'Games' },
    unit: { type: 'text', label: 'unit', default: 'games' },
  },

  presets: {
    safe: { label: 'Safe (45%)', props: { current: 45, max: 100, label: 'Games', unit: 'games' } },
    warning: {
      label: 'Warning (80%)',
      props: { current: 80, max: 100, label: 'Storage', unit: 'GB' },
    },
    critical: {
      label: 'Critical (95%)',
      props: { current: 95, max: 100, label: 'Sessions', unit: 'sessions' },
    },
    storage: { label: 'Storage', props: { current: 3.2, max: 5, label: 'Storage', unit: 'GB' } },
  },
};
