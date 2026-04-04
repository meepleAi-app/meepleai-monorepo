/**
 * Progress Story
 * Demonstrates the Progress feedback component with value and label controls.
 */

'use client';

import { Progress } from '@/components/ui/feedback/progress';

import type { ShowcaseStory } from '../types';

type ProgressShowcaseProps = {
  value: number;
  showLabel: boolean;
  label: string;
};

export const progressStory: ShowcaseStory<ProgressShowcaseProps> = {
  id: 'progress',
  title: 'Progress',
  category: 'Feedback',
  description: 'Linear progress bar for upload, loading, and completion states.',

  component: function ProgressStory({ value, showLabel, label }: ProgressShowcaseProps) {
    return (
      <div className="w-80 space-y-2 p-4">
        {showLabel && (
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">{label}</span>
            <span className="font-medium">{value}%</span>
          </div>
        )}
        <Progress value={value} />
      </div>
    );
  },

  defaultProps: {
    value: 45,
    showLabel: true,
    label: 'Uploading...',
  },

  controls: {
    value: { type: 'range', label: 'value', min: 0, max: 100, step: 1, default: 45 },
    showLabel: { type: 'boolean', label: 'showLabel', default: true },
    label: { type: 'text', label: 'label', default: 'Uploading...' },
  },

  presets: {
    partial: { label: '45%', props: { value: 45, label: 'Processing PDF...' } },
    complete: { label: '100%', props: { value: 100, label: 'Complete!' } },
    empty: { label: '0%', props: { value: 0, label: 'Waiting...' } },
    noLabel: { label: 'No Label', props: { showLabel: false, value: 60 } },
  },
};
