/**
 * Sheet Story
 * Demonstrates the Sheet overlay sliding from configurable sides.
 */

'use client';

import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';

import type { ShowcaseStory } from '../types';

type SheetShowcaseProps = {
  side: string;
  title: string;
};

export const sheetStory: ShowcaseStory<SheetShowcaseProps> = {
  id: 'sheet',
  title: 'Sheet',
  category: 'Overlays',
  description: 'Slide-in panel from any screen edge. Used for nav drawers and detail panels.',

  component: function SheetStory({ side, title }: SheetShowcaseProps) {
    return (
      <div className="p-4">
        <Sheet>
          <SheetTrigger asChild>
            <Button variant="outline">Open Sheet ({side})</Button>
          </SheetTrigger>
          <SheetContent side={side as 'left' | 'right' | 'top' | 'bottom'}>
            <SheetHeader>
              <SheetTitle>{title}</SheetTitle>
              <SheetDescription>
                This sheet slides in from the {side}. Use it for navigation drawers, filter panels,
                or detail views.
              </SheetDescription>
            </SheetHeader>
            <div className="mt-4 space-y-2">
              <p className="text-sm text-muted-foreground">Sheet content goes here.</p>
            </div>
          </SheetContent>
        </Sheet>
      </div>
    );
  },

  defaultProps: {
    side: 'right',
    title: 'Sheet Panel',
  },

  controls: {
    side: {
      type: 'select',
      label: 'side',
      options: ['left', 'right', 'top', 'bottom'],
      default: 'right',
    },
    title: { type: 'text', label: 'title', default: 'Sheet Panel' },
  },

  presets: {
    right: { label: 'Right Nav', props: { side: 'right', title: 'Filters' } },
    left: { label: 'Left Drawer', props: { side: 'left', title: 'Navigation' } },
    bottom: { label: 'Bottom Sheet', props: { side: 'bottom', title: 'Game Actions' } },
  },
};
