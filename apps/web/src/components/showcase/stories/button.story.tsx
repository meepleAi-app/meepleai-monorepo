/**
 * Button Story
 * Demonstrates all variants, sizes, and states of the Button primitive.
 */

'use client';

import { Button } from '@/components/ui/primitives/button';

import type { ShowcaseStory } from '../types';

type ButtonShowcaseProps = {
  variant: string;
  size: string;
  disabled: boolean;
  label: string;
};

export const buttonStory: ShowcaseStory<ButtonShowcaseProps> = {
  id: 'button',
  title: 'Button',
  category: 'Forms',
  description: 'Primary interactive element with multiple variants and sizes.',

  component: function ButtonStory({ variant, size, disabled, label }: ButtonShowcaseProps) {
    return (
      <div className="flex flex-wrap items-center gap-4 p-4">
        <Button
          variant={
            variant as 'default' | 'destructive' | 'outline' | 'secondary' | 'ghost' | 'link'
          }
          size={size as 'default' | 'sm' | 'lg' | 'icon'}
          disabled={disabled}
        >
          {size === 'icon' ? '✓' : label}
        </Button>
      </div>
    );
  },

  defaultProps: {
    variant: 'default',
    size: 'default',
    disabled: false,
    label: 'Click me',
  },

  controls: {
    variant: {
      type: 'select',
      label: 'variant',
      options: ['default', 'destructive', 'outline', 'secondary', 'ghost', 'link'],
      default: 'default',
    },
    size: {
      type: 'select',
      label: 'size',
      options: ['default', 'sm', 'lg', 'icon'],
      default: 'default',
    },
    label: { type: 'text', label: 'label', default: 'Click me' },
    disabled: { type: 'boolean', label: 'disabled', default: false },
  },

  presets: {
    primary: {
      label: 'Primary',
      props: { variant: 'default', size: 'default', label: 'Save Changes' },
    },
    danger: {
      label: 'Danger',
      props: { variant: 'destructive', size: 'default', label: 'Delete' },
    },
    outline: { label: 'Outline', props: { variant: 'outline', size: 'default', label: 'Cancel' } },
    small: { label: 'Small', props: { variant: 'secondary', size: 'sm', label: 'Filter' } },
    large: { label: 'Large', props: { variant: 'default', size: 'lg', label: 'Get Started' } },
    disabled: { label: 'Disabled', props: { disabled: true, label: 'Unavailable' } },
  },
};
