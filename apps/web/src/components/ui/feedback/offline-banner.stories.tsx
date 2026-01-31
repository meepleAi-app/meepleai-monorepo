import React from 'react';

import { WifiOff, RefreshCw, AlertTriangle, X, Loader2 } from 'lucide-react';

import { cn } from '@/lib/utils';

import { offlineBannerVariants } from './offline-banner';
import { Button } from '../primitives/button';

import type { Meta, StoryObj } from '@storybook/react';

// Mock component for Storybook (doesn't use useNetworkStatus hook)
const OfflineBannerMock = ({
  variant = 'offline',
  queuedActionsCount = 0,
  onRetry,
  dismissible = false,
  message,
}: {
  variant?: 'offline' | 'reconnecting' | 'poor';
  queuedActionsCount?: number;
  onRetry?: () => void;
  dismissible?: boolean;
  message?: string;
}) => {
  const [isDismissed, setIsDismissed] = React.useState(false);

  const displayMessage =
    message ||
    (variant === 'offline'
      ? `Sei offline. I messaggi verranno inviati quando tornerai online${queuedActionsCount > 0 ? ` (${queuedActionsCount} azione${queuedActionsCount > 1 ? 'i' : ''} in coda)` : ''}.`
      : variant === 'reconnecting'
        ? 'Riconnessione in corso... (tentativo 1)'
        : 'Connessione lenta. Alcune funzionalità potrebbero essere limitate.');

  const Icon =
    variant === 'offline' ? WifiOff : variant === 'reconnecting' ? Loader2 : AlertTriangle;

  return (
    <div
      role="alert"
      aria-live="polite"
      className={cn(
        offlineBannerVariants({
          variant,
          visibility: isDismissed ? 'hidden' : 'visible',
        })
      )}
    >
      <div className="flex items-center gap-2">
        <Icon
          className={cn('h-4 w-4 flex-shrink-0', variant === 'reconnecting' && 'animate-spin')}
          aria-hidden="true"
        />
        <span>{displayMessage}</span>
      </div>

      <div className="flex items-center gap-2">
        {variant === 'offline' && onRetry && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onRetry}
            className="h-7 px-2 text-current hover:bg-white/20"
          >
            <RefreshCw className="mr-1 h-3 w-3" />
            Riprova
          </Button>
        )}

        {dismissible && variant !== 'offline' && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setIsDismissed(true)}
            className="h-7 w-7 p-0 text-current hover:bg-white/20"
            aria-label="Chiudi banner"
          >
            <X className="h-4 w-4" />
          </Button>
        )}
      </div>
    </div>
  );
};

/**
 * OfflineBanner for network status notifications.
 *
 * ## Custom Component
 * Shows connection status with retry functionality.
 *
 * ## Features
 * - **Auto-detection**: Network status monitoring
 * - **Retry button**: Manual reconnection
 * - **Queue counter**: Pending actions display
 * - **Dismissible**: Optional close button
 *
 * ## Accessibility
 * - ✅ ARIA live region
 * - ✅ Keyboard accessible
 */
const meta = {
  title: 'UI/OfflineBanner',
  component: OfflineBannerMock,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Network status banner that appears when user goes offline. Shows connection quality and queued actions.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['offline', 'reconnecting', 'poor'],
      description: 'Banner variant based on connection status',
    },
    queuedActionsCount: {
      control: { type: 'number', min: 0, max: 10 },
      description: 'Number of queued actions',
    },
    dismissible: {
      control: 'boolean',
      description: 'Allow dismissing the banner',
    },
  },
} satisfies Meta<typeof OfflineBannerMock>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Offline state.
 */
export const Offline: Story = {
  args: {
    variant: 'offline',
    queuedActionsCount: 0,
    dismissible: false,
  },
  render: (args) => (
    <div className="h-32">
      <OfflineBannerMock {...args} />
    </div>
  ),
};

/**
 * Offline with queued actions.
 */
export const WithQueuedActions: Story = {
  args: {
    variant: 'offline',
    queuedActionsCount: 3,
    onRetry: () => console.log('Retry clicked'),
  },
  render: (args) => (
    <div className="h-32">
      <OfflineBannerMock {...args} />
    </div>
  ),
};

/**
 * Reconnecting state.
 */
export const Reconnecting: Story = {
  args: {
    variant: 'reconnecting',
  },
  render: (args) => (
    <div className="h-32">
      <OfflineBannerMock {...args} />
    </div>
  ),
};

/**
 * Poor connection warning.
 */
export const PoorConnection: Story = {
  args: {
    variant: 'poor',
    dismissible: true,
  },
  render: (args) => (
    <div className="h-32">
      <OfflineBannerMock {...args} />
    </div>
  ),
};

/**
 * Custom message.
 */
export const CustomMessage: Story = {
  args: {
    variant: 'offline',
    message: 'Connection lost. Your data is safe and will sync automatically.',
  },
  render: (args) => (
    <div className="h-32">
      <OfflineBannerMock {...args} />
    </div>
  ),
};
