/**
 * RadioGroup Story
 * Demonstrates the RadioGroup primitive for single selection.
 */

'use client';

import React from 'react';

import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';

import type { ShowcaseStory } from '../types';

type RadioGroupShowcaseProps = {
  disabled: boolean;
  optionSet: string;
};

const OPTION_SETS: Record<string, { value: string; label: string }[]> = {
  difficulty: [
    { value: 'easy', label: 'Easy' },
    { value: 'medium', label: 'Medium' },
    { value: 'hard', label: 'Hard' },
    { value: 'expert', label: 'Expert' },
  ],
  player_count: [
    { value: '2', label: '2 Players' },
    { value: '3', label: '3 Players' },
    { value: '4', label: '4 Players' },
  ],
  tier: [
    { value: 'free', label: 'Free' },
    { value: 'premium', label: 'Premium' },
    { value: 'contributor', label: 'Contributor' },
  ],
};

export const radioGroupStory: ShowcaseStory<RadioGroupShowcaseProps> = {
  id: 'radio-group',
  title: 'RadioGroup',
  category: 'Forms',
  description: 'Radio button group for exclusive single-value selection.',

  component: function RadioGroupStory({ disabled, optionSet }: RadioGroupShowcaseProps) {
    const options = OPTION_SETS[optionSet] ?? OPTION_SETS.difficulty;
    const [value, setValue] = React.useState(options[0]?.value ?? '');

    return (
      <div className="p-4">
        <RadioGroup value={value} onValueChange={setValue} disabled={disabled} className="gap-3">
          {options.map(opt => (
            <div key={opt.value} className="flex items-center gap-2">
              <RadioGroupItem value={opt.value} id={`radio-${opt.value}`} />
              <label
                htmlFor={`radio-${opt.value}`}
                className="text-sm font-medium cursor-pointer select-none"
              >
                {opt.label}
              </label>
            </div>
          ))}
        </RadioGroup>
      </div>
    );
  },

  defaultProps: {
    disabled: false,
    optionSet: 'difficulty',
  },

  controls: {
    optionSet: {
      type: 'select',
      label: 'optionSet',
      options: ['difficulty', 'player_count', 'tier'],
      default: 'difficulty',
    },
    disabled: { type: 'boolean', label: 'disabled', default: false },
  },

  presets: {
    difficulty: { label: 'Difficulty', props: { optionSet: 'difficulty' } },
    players: { label: 'Player Count', props: { optionSet: 'player_count' } },
    tier: { label: 'Tier', props: { optionSet: 'tier' } },
    disabled: { label: 'Disabled', props: { disabled: true } },
  },
};
