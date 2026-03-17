/**
 * Alert Story
 * Demonstrates the Alert feedback component with variant and content controls.
 */

'use client';

import { AlertCircle, CheckCircle, Info, XCircle } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';

import type { ShowcaseStory } from '../types';

type AlertShowcaseProps = {
  variant: string;
  title: string;
  description: string;
  showIcon: boolean;
};

const ICON_MAP: Record<string, React.ComponentType<{ className?: string }>> = {
  default: Info,
  destructive: XCircle,
  success: CheckCircle,
  warning: AlertCircle,
};

export const alertStory: ShowcaseStory<AlertShowcaseProps> = {
  id: 'alert',
  title: 'Alert',
  category: 'Feedback',
  description: 'Contextual feedback banner with optional icon, title, and description.',

  component: function AlertStory({ variant, title, description, showIcon }: AlertShowcaseProps) {
    const Icon = ICON_MAP[variant] ?? Info;
    return (
      <div className="w-96 p-4">
        <Alert variant={variant as 'default' | 'destructive'}>
          {showIcon && <Icon className="h-4 w-4" />}
          <AlertTitle>{title}</AlertTitle>
          <AlertDescription>{description}</AlertDescription>
        </Alert>
      </div>
    );
  },

  defaultProps: {
    variant: 'default',
    title: 'Heads up!',
    description: 'You can add components to your app using the CLI.',
    showIcon: true,
  },

  controls: {
    variant: {
      type: 'select',
      label: 'variant',
      options: ['default', 'destructive'],
      default: 'default',
    },
    title: { type: 'text', label: 'title', default: 'Heads up!' },
    description: {
      type: 'text',
      label: 'description',
      default: 'You can add components using the CLI.',
    },
    showIcon: { type: 'boolean', label: 'showIcon', default: true },
  },

  presets: {
    info: {
      label: 'Info',
      props: {
        variant: 'default',
        title: 'New feature available',
        description: 'Check out the latest updates in the changelog.',
      },
    },
    error: {
      label: 'Error',
      props: {
        variant: 'destructive',
        title: 'Upload failed',
        description: 'The file could not be processed. Please try again.',
      },
    },
    noIcon: {
      label: 'No Icon',
      props: {
        showIcon: false,
        title: 'Notice',
        description: 'Your session will expire in 5 minutes.',
      },
    },
  },
};
