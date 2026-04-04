/**
 * Tooltip Story
 * Demonstrates the Tooltip overlay with text and side controls.
 */

'use client';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';

import type { ShowcaseStory } from '../types';

type TooltipShowcaseProps = {
  content: string;
  side: string;
};

export const tooltipStory: ShowcaseStory<TooltipShowcaseProps> = {
  id: 'tooltip',
  title: 'Tooltip',
  category: 'Overlays',
  description: 'Contextual hint on hover with configurable position and primary-color styling.',

  component: function TooltipStory({ content, side }: TooltipShowcaseProps) {
    return (
      <div className="flex items-center justify-center p-12">
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button variant="outline">Hover me</Button>
            </TooltipTrigger>
            <TooltipContent side={side as 'top' | 'bottom' | 'left' | 'right'}>
              {content}
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      </div>
    );
  },

  defaultProps: {
    content: 'This is a helpful tooltip',
    side: 'top',
  },

  controls: {
    content: { type: 'text', label: 'content', default: 'This is a helpful tooltip' },
    side: {
      type: 'select',
      label: 'side',
      options: ['top', 'bottom', 'left', 'right'],
      default: 'top',
    },
  },

  presets: {
    top: { label: 'Top', props: { content: 'Appears above', side: 'top' } },
    bottom: { label: 'Bottom', props: { content: 'Appears below', side: 'bottom' } },
    left: { label: 'Left', props: { content: 'Appears left', side: 'left' } },
    right: { label: 'Right', props: { content: 'Click to expand', side: 'right' } },
  },
};
