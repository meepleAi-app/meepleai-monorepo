/**
 * Switch Story
 * Demonstrates the Switch component with label and state controls.
 */

'use client';

import React from 'react';

import { Switch } from '@/components/ui/forms/switch';

import type { ShowcaseStory } from '../types';

type SwitchShowcaseProps = {
  checked: boolean;
  disabled: boolean;
  label: string;
};

export const switchStory: ShowcaseStory<SwitchShowcaseProps> = {
  id: 'switch',
  title: 'Switch',
  category: 'Forms',
  description: 'Toggle switch for boolean settings with accessible label.',

  component: function SwitchStory({
    checked: defaultChecked,
    disabled,
    label,
  }: SwitchShowcaseProps) {
    const [checked, setChecked] = React.useState(defaultChecked);

    React.useEffect(() => {
      setChecked(defaultChecked);
    }, [defaultChecked]);

    return (
      <div className="flex items-center gap-3 p-4">
        <Switch
          checked={checked}
          onCheckedChange={setChecked}
          disabled={disabled}
          id="switch-demo"
        />
        <label htmlFor="switch-demo" className="text-sm font-medium cursor-pointer select-none">
          {label}
        </label>
      </div>
    );
  },

  defaultProps: {
    checked: false,
    disabled: false,
    label: 'Enable notifications',
  },

  controls: {
    checked: { type: 'boolean', label: 'checked', default: false },
    disabled: { type: 'boolean', label: 'disabled', default: false },
    label: { type: 'text', label: 'label', default: 'Enable notifications' },
  },

  presets: {
    off: { label: 'Off', props: { checked: false, label: 'Dark mode' } },
    on: { label: 'On', props: { checked: true, label: 'Dark mode' } },
    disabled: { label: 'Disabled', props: { disabled: true, label: 'Feature unavailable' } },
  },
};
