/**
 * Select Story
 * Demonstrates the Select overlay component with options and disabled state.
 */

'use client';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

import type { ShowcaseStory } from '../types';

type SelectShowcaseProps = {
  placeholder: string;
  disabled: boolean;
  optionSet: string;
};

const OPTION_SETS: Record<string, { value: string; label: string }[]> = {
  games: [
    { value: 'catan', label: 'Catan' },
    { value: 'pandemic', label: 'Pandemic' },
    { value: 'agricola', label: 'Agricola' },
    { value: 'twilight', label: 'Twilight Imperium' },
  ],
  categories: [
    { value: 'strategy', label: 'Strategy' },
    { value: 'cooperative', label: 'Cooperative' },
    { value: 'worker-placement', label: 'Worker Placement' },
    { value: 'deck-building', label: 'Deck Building' },
  ],
  players: [
    { value: '2', label: '2 Players' },
    { value: '3', label: '3 Players' },
    { value: '4', label: '4 Players' },
    { value: '5', label: '5 Players' },
    { value: '6', label: '6 Players' },
  ],
};

export const selectStory: ShowcaseStory<SelectShowcaseProps> = {
  id: 'select',
  title: 'Select',
  category: 'Forms',
  description: 'Dropdown select with glassmorphic popover and keyboard navigation.',

  component: function SelectStory({ placeholder, disabled, optionSet }: SelectShowcaseProps) {
    const options = OPTION_SETS[optionSet] ?? OPTION_SETS.games;

    return (
      <div className="w-64 p-4">
        <Select disabled={disabled}>
          <SelectTrigger>
            <SelectValue placeholder={placeholder} />
          </SelectTrigger>
          <SelectContent>
            {options.map(opt => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    );
  },

  defaultProps: {
    placeholder: 'Select a game...',
    disabled: false,
    optionSet: 'games',
  },

  controls: {
    optionSet: {
      type: 'select',
      label: 'optionSet',
      options: ['games', 'categories', 'players'],
      default: 'games',
    },
    placeholder: { type: 'text', label: 'placeholder', default: 'Select a game...' },
    disabled: { type: 'boolean', label: 'disabled', default: false },
  },

  presets: {
    games: { label: 'Games', props: { optionSet: 'games', placeholder: 'Choose a game...' } },
    categories: {
      label: 'Categories',
      props: { optionSet: 'categories', placeholder: 'Select category...' },
    },
    players: {
      label: 'Players',
      props: { optionSet: 'players', placeholder: 'Number of players...' },
    },
    disabled: { label: 'Disabled', props: { disabled: true } },
  },
};
