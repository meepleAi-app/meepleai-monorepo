/**
 * Input Story
 * Demonstrates the Input primitive with type and state variants.
 */

'use client';

import { Input } from '@/components/ui/primitives/input';

import type { ShowcaseStory } from '../types';

type InputShowcaseProps = {
  type: string;
  placeholder: string;
  disabled: boolean;
};

export const inputStory: ShowcaseStory<InputShowcaseProps> = {
  id: 'input',
  title: 'Input',
  category: 'Forms',
  description: 'Text input field with glassmorphic styling and focus states.',

  component: function InputStory({ type, placeholder, disabled }: InputShowcaseProps) {
    return (
      <div className="w-72 p-4">
        <Input type={type} placeholder={placeholder} disabled={disabled} />
      </div>
    );
  },

  defaultProps: {
    type: 'text',
    placeholder: 'Enter value...',
    disabled: false,
  },

  controls: {
    type: {
      type: 'select',
      label: 'type',
      options: ['text', 'email', 'password', 'number', 'search'],
      default: 'text',
    },
    placeholder: { type: 'text', label: 'placeholder', default: 'Enter value...' },
    disabled: { type: 'boolean', label: 'disabled', default: false },
  },

  presets: {
    text: { label: 'Text', props: { type: 'text', placeholder: 'Enter your name...' } },
    email: { label: 'Email', props: { type: 'email', placeholder: 'user@example.com' } },
    password: { label: 'Password', props: { type: 'password', placeholder: 'Enter password...' } },
    disabled: { label: 'Disabled', props: { disabled: true, placeholder: 'Not editable' } },
  },
};
