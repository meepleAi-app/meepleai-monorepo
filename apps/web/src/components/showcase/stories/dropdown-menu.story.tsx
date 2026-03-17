/**
 * DropdownMenu Story
 * Demonstrates DropdownMenu with configurable item sets.
 */

'use client';

import { Edit, MoreHorizontal, Star, Trash2 } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';

import type { ShowcaseStory } from '../types';

type DropdownMenuShowcaseProps = {
  triggerLabel: string;
  showIcons: boolean;
};

export const dropdownMenuStory: ShowcaseStory<DropdownMenuShowcaseProps> = {
  id: 'dropdown-menu',
  title: 'DropdownMenu',
  category: 'Navigation',
  description: 'Context menu with grouped items, separators, and keyboard navigation.',

  component: function DropdownMenuStory({ triggerLabel, showIcons }: DropdownMenuShowcaseProps) {
    return (
      <div className="flex items-center justify-center p-12">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline">
              {triggerLabel}
              <MoreHorizontal className="h-4 w-4 ml-1" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel>Game Actions</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem>
              {showIcons && <Star className="h-4 w-4 mr-2" />}
              Add to Wishlist
            </DropdownMenuItem>
            <DropdownMenuItem>
              {showIcons && <Edit className="h-4 w-4 mr-2" />}
              Edit Details
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="text-destructive focus:text-destructive">
              {showIcons && <Trash2 className="h-4 w-4 mr-2" />}
              Remove
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    );
  },

  defaultProps: {
    triggerLabel: 'Actions',
    showIcons: true,
  },

  controls: {
    triggerLabel: { type: 'text', label: 'triggerLabel', default: 'Actions' },
    showIcons: { type: 'boolean', label: 'showIcons', default: true },
  },

  presets: {
    withIcons: { label: 'With Icons', props: { showIcons: true, triggerLabel: 'Options' } },
    noIcons: { label: 'No Icons', props: { showIcons: false, triggerLabel: 'More' } },
  },
};
