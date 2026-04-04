/**
 * MeepleLogo Story
 * Demonstrates the MeepleAI brand logo in all variants and sizes.
 */

'use client';

import { MeepleLogo } from '@/components/ui/meeple/meeple-logo';

import type { ShowcaseStory } from '../types';

type MeepleLogoShowcaseProps = {
  variant: string;
  size: string;
  animated: boolean;
};

export const meepleLogoStory: ShowcaseStory<MeepleLogoShowcaseProps> = {
  id: 'meeple-logo',
  title: 'MeepleLogo',
  category: 'Meeple',
  description: 'Brand logo with full, icon, and wordmark variants. Supports light/dark themes.',

  component: function MeepleLogoStory({ variant, size, animated }: MeepleLogoShowcaseProps) {
    return (
      <div className="flex items-center justify-center p-8">
        <MeepleLogo
          variant={variant as 'full' | 'icon' | 'wordmark'}
          size={size as 'sm' | 'md' | 'lg' | 'xl'}
          animated={animated}
        />
      </div>
    );
  },

  defaultProps: {
    variant: 'full',
    size: 'md',
    animated: false,
  },

  controls: {
    variant: {
      type: 'select',
      label: 'variant',
      options: ['full', 'icon', 'wordmark'],
      default: 'full',
    },
    size: {
      type: 'select',
      label: 'size',
      options: ['sm', 'md', 'lg', 'xl'],
      default: 'md',
    },
    animated: { type: 'boolean', label: 'animated', default: false },
  },

  presets: {
    full: { label: 'Full Logo', props: { variant: 'full', size: 'md' } },
    icon: { label: 'Icon Only', props: { variant: 'icon', size: 'lg' } },
    wordmark: { label: 'Wordmark', props: { variant: 'wordmark', size: 'md' } },
    large: { label: 'XL Full', props: { variant: 'full', size: 'xl' } },
    animated: { label: 'Animated', props: { variant: 'full', size: 'md', animated: true } },
  },
};
