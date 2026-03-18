/**
 * Popover Story
 * Demonstrates the Popover overlay with trigger and content controls.
 */

'use client';

import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays/popover';
import { Button } from '@/components/ui/primitives/button';

import type { ShowcaseStory } from '../types';

type PopoverShowcaseProps = {
  triggerLabel: string;
  contentText: string;
  align: string;
};

export const popoverStory: ShowcaseStory<PopoverShowcaseProps> = {
  id: 'popover',
  title: 'Popover',
  category: 'Overlays',
  description: 'Floating content panel attached to a trigger element with blur backdrop.',

  component: function PopoverStory({ triggerLabel, contentText, align }: PopoverShowcaseProps) {
    return (
      <div className="flex items-center justify-center p-12">
        <Popover>
          <PopoverTrigger asChild>
            <Button variant="outline">{triggerLabel}</Button>
          </PopoverTrigger>
          <PopoverContent align={align as 'center' | 'start' | 'end'}>
            <p className="text-sm">{contentText}</p>
          </PopoverContent>
        </Popover>
      </div>
    );
  },

  defaultProps: {
    triggerLabel: 'Open Popover',
    contentText: 'This is popover content with some helpful information.',
    align: 'center',
  },

  controls: {
    triggerLabel: { type: 'text', label: 'triggerLabel', default: 'Open Popover' },
    contentText: { type: 'text', label: 'contentText', default: 'This is popover content.' },
    align: {
      type: 'select',
      label: 'align',
      options: ['center', 'start', 'end'],
      default: 'center',
    },
  },

  presets: {
    default: {
      label: 'Default',
      props: { triggerLabel: 'More Info', contentText: 'Additional details about this item.' },
    },
    filter: {
      label: 'Filter',
      props: {
        triggerLabel: 'Filters',
        contentText: 'Apply filters to narrow down results.',
        align: 'end',
      },
    },
    hint: {
      label: 'Hint',
      props: { triggerLabel: '?', contentText: 'This feature requires a Premium subscription.' },
    },
  },
};
