/**
 * Badge Story
 * Demonstrates all variants of the Badge data-display component.
 */

'use client';

import { Badge } from '@/components/ui/data-display/badge';

import type { ShowcaseStory } from '../types';

type BadgeShowcaseProps = {
  variant: string;
  label: string;
};

export const badgeStory: ShowcaseStory<BadgeShowcaseProps> = {
  id: 'badge',
  title: 'Badge',
  category: 'Tags',
  description: 'Compact label chip for status, category, and metadata tagging.',

  component: function BadgeStory({ variant, label }: BadgeShowcaseProps) {
    return (
      <div className="flex flex-wrap items-center gap-3 p-4">
        <Badge variant={variant as 'default' | 'secondary' | 'destructive' | 'outline'}>
          {label}
        </Badge>
        <div className="flex flex-wrap gap-2 pt-2 w-full border-t border-border/30">
          <Badge variant="default">Default</Badge>
          <Badge variant="secondary">Secondary</Badge>
          <Badge variant="destructive">Destructive</Badge>
          <Badge variant="outline">Outline</Badge>
        </div>
      </div>
    );
  },

  defaultProps: {
    variant: 'default',
    label: 'New',
  },

  controls: {
    variant: {
      type: 'select',
      label: 'variant',
      options: ['default', 'secondary', 'destructive', 'outline'],
      default: 'default',
    },
    label: { type: 'text', label: 'label', default: 'New' },
  },

  presets: {
    status: { label: 'Status', props: { variant: 'default', label: 'Active' } },
    category: { label: 'Category', props: { variant: 'secondary', label: 'Strategy' } },
    error: { label: 'Error', props: { variant: 'destructive', label: 'Failed' } },
    outline: { label: 'Outline', props: { variant: 'outline', label: 'Beta' } },
  },
};
