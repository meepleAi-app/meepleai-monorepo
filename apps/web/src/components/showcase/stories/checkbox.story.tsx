/**
 * Checkbox Story
 * Demonstrates the Checkbox primitive with label and state controls.
 */

'use client';

import React from 'react';

import { Checkbox } from '@/components/ui/primitives/checkbox';

import type { ShowcaseStory } from '../types';

type CheckboxShowcaseProps = {
  checked: boolean;
  disabled: boolean;
  label: string;
};

export const checkboxStory: ShowcaseStory<CheckboxShowcaseProps> = {
  id: 'checkbox',
  title: 'Checkbox',
  category: 'Forms',
  description: 'Checkbox input for multi-select and agreement flows.',

  component: function CheckboxStory({
    checked: defaultChecked,
    disabled,
    label,
  }: CheckboxShowcaseProps) {
    const [checked, setChecked] = React.useState<boolean | 'indeterminate'>(defaultChecked);

    React.useEffect(() => {
      setChecked(defaultChecked);
    }, [defaultChecked]);

    return (
      <div className="flex items-center gap-3 p-4">
        <Checkbox
          id="checkbox-demo"
          checked={checked}
          onCheckedChange={setChecked}
          disabled={disabled}
        />
        <label htmlFor="checkbox-demo" className="text-sm font-medium cursor-pointer select-none">
          {label}
        </label>
      </div>
    );
  },

  defaultProps: {
    checked: false,
    disabled: false,
    label: 'Accept terms and conditions',
  },

  controls: {
    checked: { type: 'boolean', label: 'checked', default: false },
    disabled: { type: 'boolean', label: 'disabled', default: false },
    label: { type: 'text', label: 'label', default: 'Accept terms and conditions' },
  },

  presets: {
    unchecked: { label: 'Unchecked', props: { checked: false, label: 'Send me updates' } },
    checked: { label: 'Checked', props: { checked: true, label: 'Send me updates' } },
    disabled: { label: 'Disabled', props: { disabled: true, label: 'Option unavailable' } },
  },
};
