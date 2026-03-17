/**
 * Dialog Story
 * Demonstrates the Dialog overlay with trigger, title, description, and actions.
 */

'use client';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

import type { ShowcaseStory } from '../types';

type DialogShowcaseProps = {
  title: string;
  description: string;
  triggerLabel: string;
};

export const dialogStory: ShowcaseStory<DialogShowcaseProps> = {
  id: 'dialog',
  title: 'Dialog',
  category: 'Overlays',
  description: 'Modal dialog with glassmorphic panel, animations, and accessible close button.',

  component: function DialogStory({ title, description, triggerLabel }: DialogShowcaseProps) {
    return (
      <div className="p-4">
        <Dialog>
          <DialogTrigger asChild>
            <Button variant="outline">{triggerLabel}</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>{title}</DialogTitle>
              <DialogDescription>{description}</DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button variant="outline">Cancel</Button>
              <Button>Confirm</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    );
  },

  defaultProps: {
    title: 'Confirm Action',
    description: 'Are you sure you want to proceed? This action cannot be undone.',
    triggerLabel: 'Open Dialog',
  },

  controls: {
    title: { type: 'text', label: 'title', default: 'Confirm Action' },
    description: {
      type: 'text',
      label: 'description',
      default: 'Are you sure you want to proceed?',
    },
    triggerLabel: { type: 'text', label: 'triggerLabel', default: 'Open Dialog' },
  },

  presets: {
    confirm: {
      label: 'Confirm',
      props: { title: 'Confirm Action', description: 'This action cannot be undone.' },
    },
    delete: {
      label: 'Delete',
      props: {
        title: 'Delete Game',
        description: 'This will permanently remove the game from your library.',
        triggerLabel: 'Delete Game',
      },
    },
    info: {
      label: 'Info',
      props: {
        title: 'Game Rules',
        description: 'View the complete rulebook for this game.',
        triggerLabel: 'View Rules',
      },
    },
  },
};
