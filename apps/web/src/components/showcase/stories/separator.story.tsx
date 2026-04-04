/**
 * Separator Story
 * Demonstrates the Separator layout component with orientation control.
 */

'use client';

import { Separator } from '@/components/ui/navigation/separator';

import type { ShowcaseStory } from '../types';

type SeparatorShowcaseProps = {
  orientation: string;
  label: string;
};

export const separatorStory: ShowcaseStory<SeparatorShowcaseProps> = {
  id: 'separator',
  title: 'Separator',
  category: 'Layout',
  description: 'Horizontal or vertical divider for grouping content sections.',

  component: function SeparatorStory({ orientation, label }: SeparatorShowcaseProps) {
    if (orientation === 'vertical') {
      return (
        <div className="flex items-center gap-4 p-8 h-24">
          <span className="text-sm text-muted-foreground">Left</span>
          <Separator orientation="vertical" className="h-full" />
          <span className="text-sm text-muted-foreground">Right</span>
        </div>
      );
    }

    return (
      <div className="w-80 space-y-3 p-4">
        <div className="text-sm font-medium">{label || 'Section Above'}</div>
        <Separator />
        <p className="text-sm text-muted-foreground">Content below the separator.</p>
      </div>
    );
  },

  defaultProps: {
    orientation: 'horizontal',
    label: 'Section Title',
  },

  controls: {
    orientation: {
      type: 'select',
      label: 'orientation',
      options: ['horizontal', 'vertical'],
      default: 'horizontal',
    },
    label: { type: 'text', label: 'label', default: 'Section Title' },
  },

  presets: {
    horizontal: {
      label: 'Horizontal',
      props: { orientation: 'horizontal', label: 'Game Details' },
    },
    vertical: { label: 'Vertical', props: { orientation: 'vertical', label: '' } },
  },
};
