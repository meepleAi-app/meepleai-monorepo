/**
 * Textarea Story
 * Demonstrates the Textarea primitive with placeholder, rows, and disabled controls.
 */

'use client';

import { Textarea } from '@/components/ui/primitives/textarea';

import type { ShowcaseStory } from '../types';

type TextareaShowcaseProps = {
  placeholder: string;
  rows: number;
  disabled: boolean;
};

export const textareaStory: ShowcaseStory<TextareaShowcaseProps> = {
  id: 'textarea',
  title: 'Textarea',
  category: 'Forms',
  description: 'Multi-line text input with resizable rows and glassmorphic styling.',

  component: function TextareaStory({ placeholder, rows, disabled }: TextareaShowcaseProps) {
    return (
      <div className="w-80 p-4">
        <Textarea placeholder={placeholder} rows={rows} disabled={disabled} />
      </div>
    );
  },

  defaultProps: {
    placeholder: 'Write something...',
    rows: 4,
    disabled: false,
  },

  controls: {
    placeholder: { type: 'text', label: 'placeholder', default: 'Write something...' },
    rows: { type: 'range', label: 'rows', min: 2, max: 10, step: 1, default: 4 },
    disabled: { type: 'boolean', label: 'disabled', default: false },
  },

  presets: {
    default: { label: 'Default', props: { placeholder: 'Write something...', rows: 4 } },
    note: {
      label: 'Session Note',
      props: { placeholder: 'Add notes about this game session...', rows: 6 },
    },
    compact: { label: 'Compact', props: { placeholder: 'Quick comment...', rows: 2 } },
    disabled: { label: 'Disabled', props: { disabled: true, placeholder: 'Read-only field' } },
  },
};
