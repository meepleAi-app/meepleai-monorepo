/**
 * MeepleAvatar Story
 * Demonstrates all AI states and size variants of the MeepleAvatar character.
 */

'use client';

import { MeepleAvatar } from '@/components/ui/meeple/meeple-avatar';

import type { ShowcaseStory } from '../types';

type MeepleAvatarShowcaseProps = {
  state: string;
  size: string;
};

export const meepleAvatarStory: ShowcaseStory<MeepleAvatarShowcaseProps> = {
  id: 'meeple-avatar',
  title: 'MeepleAvatar',
  category: 'Meeple',
  description: 'Animated meeple character representing the AI assistant with 5 activity states.',

  component: function MeepleAvatarStory({ state, size }: MeepleAvatarShowcaseProps) {
    return (
      <div className="flex items-center gap-4 p-6">
        <MeepleAvatar
          state={state as 'idle' | 'thinking' | 'confident' | 'searching' | 'uncertain'}
          size={size as 'sm' | 'md' | 'lg'}
        />
        <div className="text-sm">
          <div className="font-medium capitalize">{state}</div>
          <div className="text-muted-foreground">Size: {size}</div>
        </div>
      </div>
    );
  },

  defaultProps: {
    state: 'idle',
    size: 'md',
  },

  controls: {
    state: {
      type: 'select',
      label: 'state',
      options: ['idle', 'thinking', 'confident', 'searching', 'uncertain'],
      default: 'idle',
    },
    size: {
      type: 'select',
      label: 'size',
      options: ['sm', 'md', 'lg'],
      default: 'md',
    },
  },

  presets: {
    idle: { label: 'Idle', props: { state: 'idle', size: 'md' } },
    thinking: { label: 'Thinking', props: { state: 'thinking', size: 'md' } },
    confident: { label: 'Confident', props: { state: 'confident', size: 'lg' } },
    searching: { label: 'Searching', props: { state: 'searching', size: 'sm' } },
    uncertain: { label: 'Uncertain', props: { state: 'uncertain', size: 'md' } },
  },
};
