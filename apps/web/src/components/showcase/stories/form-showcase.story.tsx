/**
 * FormShowcase Story
 * A composed form showing how form primitives work together.
 */

'use client';

import React from 'react';

import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Textarea } from '@/components/ui/primitives/textarea';

import type { ShowcaseStory } from '../types';

type FormShowcaseProps = {
  formTitle: string;
};

export const formShowcaseStory: ShowcaseStory<FormShowcaseProps> = {
  id: 'form-showcase',
  title: 'Form Showcase',
  category: 'Forms',
  description: 'Composed game session form showing Input, Textarea, Checkbox, and Switch together.',

  component: function FormShowcaseStory({ formTitle }: FormShowcaseProps) {
    const [notifications, setNotifications] = React.useState(false);

    return (
      <div className="w-full max-w-md p-6 space-y-5">
        <h3 className="text-lg font-semibold font-quicksand">{formTitle}</h3>

        <div className="space-y-1.5">
          <label htmlFor="game-name" className="text-sm font-medium">
            Game Name
          </label>
          <Input id="game-name" type="text" placeholder="e.g. Catan, Pandemic..." />
        </div>

        <div className="space-y-1.5">
          <label htmlFor="players" className="text-sm font-medium">
            Players
          </label>
          <Input id="players" type="number" placeholder="Number of players" />
        </div>

        <div className="space-y-1.5">
          <label htmlFor="notes" className="text-sm font-medium">
            Session Notes
          </label>
          <Textarea id="notes" placeholder="Add notes about this session..." rows={3} />
        </div>

        <div className="flex items-center gap-3">
          <Checkbox id="winner" />
          <label htmlFor="winner" className="text-sm cursor-pointer">
            I won this game
          </label>
        </div>

        <div className="flex items-center justify-between">
          <label htmlFor="notif-toggle" className="text-sm font-medium">
            Send recap email
          </label>
          <Switch id="notif-toggle" checked={notifications} onCheckedChange={setNotifications} />
        </div>

        <div className="flex gap-3 pt-2">
          <Button variant="outline" className="flex-1">
            Cancel
          </Button>
          <Button className="flex-1">Save Session</Button>
        </div>
      </div>
    );
  },

  defaultProps: {
    formTitle: 'Log Game Session',
  },

  controls: {
    formTitle: { type: 'text', label: 'formTitle', default: 'Log Game Session' },
  },

  presets: {
    session: { label: 'Session Form', props: { formTitle: 'Log Game Session' } },
    addGame: { label: 'Add Game', props: { formTitle: 'Add to Library' } },
  },
};
