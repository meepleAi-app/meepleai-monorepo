import { Info, HelpCircle, Settings, Copy } from 'lucide-react';

import { Button } from '../primitives/button';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from './tooltip';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Tooltip component for contextual help and information.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Tooltip with smooth animations.
 *
 * ## Features
 * - **Positioning**: Auto-positioning with side preferences
 * - **Animations**: Smooth fade-in/out with scaling
 * - **Delay**: Customizable show/hide delays
 * - **Portal**: Renders in portal for proper z-index
 *
 * ## Accessibility
 * - ✅ ARIA tooltip role
 * - ✅ Keyboard support (Escape to close)
 * - ✅ Focus management
 * - ✅ Screen reader announcements
 */
const meta = {
  title: 'UI/Tooltip',
  component: Tooltip,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A tooltip component that displays contextual information on hover or focus. Automatically positions itself and supports keyboard navigation.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <TooltipProvider>
        <div className="p-12">
          <Story />
        </div>
      </TooltipProvider>
    ),
  ],
} satisfies Meta<typeof Tooltip>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default tooltip with simple text.
 * Most common use case for additional context.
 */
export const Default: Story = {
  render: () => (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button variant="outline">Hover me</Button>
      </TooltipTrigger>
      <TooltipContent>
        <p>This is a tooltip</p>
      </TooltipContent>
    </Tooltip>
  ),
};

/**
 * Tooltip on icon button.
 * Common pattern for icon-only actions.
 */
export const IconButton: Story = {
  render: () => (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button variant="outline" size="icon">
          <Info className="h-4 w-4" />
        </Button>
      </TooltipTrigger>
      <TooltipContent>
        <p>Additional information</p>
      </TooltipContent>
    </Tooltip>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Tooltip providing context for icon-only buttons.',
      },
    },
  },
};

/**
 * Tooltip with side positioning.
 * Shows tooltip appearing from different sides.
 */
export const Positioning: Story = {
  render: () => (
    <div className="flex gap-4">
      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="outline">Top</Button>
        </TooltipTrigger>
        <TooltipContent side="top">
          <p>Tooltip on top</p>
        </TooltipContent>
      </Tooltip>

      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="outline">Right</Button>
        </TooltipTrigger>
        <TooltipContent side="right">
          <p>Tooltip on right</p>
        </TooltipContent>
      </Tooltip>

      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="outline">Bottom</Button>
        </TooltipTrigger>
        <TooltipContent side="bottom">
          <p>Tooltip on bottom</p>
        </TooltipContent>
      </Tooltip>

      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="outline">Left</Button>
        </TooltipTrigger>
        <TooltipContent side="left">
          <p>Tooltip on left</p>
        </TooltipContent>
      </Tooltip>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Tooltips positioned on different sides of the trigger.',
      },
    },
  },
};

/**
 * Tooltip with custom delay.
 * Adjust show delay for better UX.
 */
export const CustomDelay: Story = {
  render: () => (
    <TooltipProvider delayDuration={500}>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="outline">Hover (500ms delay)</Button>
        </TooltipTrigger>
        <TooltipContent>
          <p>Appears after 500ms</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Tooltip with custom delay duration.',
      },
    },
  },
};

/**
 * Help text tooltip.
 * Provides explanatory text for form fields.
 */
export const HelpText: Story = {
  render: () => (
    <div className="flex items-center gap-2">
      <label className="text-sm font-medium">Username</label>
      <Tooltip>
        <TooltipTrigger asChild>
          <HelpCircle className="h-4 w-4 text-muted-foreground cursor-help" />
        </TooltipTrigger>
        <TooltipContent className="max-w-xs">
          <p>
            Choose a unique username. Must be 3-20 characters, letters and numbers only.
          </p>
        </TooltipContent>
      </Tooltip>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Tooltip providing help text for form fields.',
      },
    },
  },
};

/**
 * Keyboard shortcut tooltip.
 * Shows keyboard shortcuts for actions.
 */
export const KeyboardShortcut: Story = {
  render: () => (
    <div className="flex gap-4">
      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="outline" size="icon">
            <Copy className="h-4 w-4" />
          </Button>
        </TooltipTrigger>
        <TooltipContent>
          <div className="flex items-center gap-2">
            <span>Copy</span>
            <kbd className="px-1.5 py-0.5 text-xs bg-muted rounded border">Ctrl+C</kbd>
          </div>
        </TooltipContent>
      </Tooltip>

      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="outline" size="icon">
            <Settings className="h-4 w-4" />
          </Button>
        </TooltipTrigger>
        <TooltipContent>
          <div className="flex items-center gap-2">
            <span>Settings</span>
            <kbd className="px-1.5 py-0.5 text-xs bg-muted rounded border">Ctrl+,</kbd>
          </div>
        </TooltipContent>
      </Tooltip>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Tooltips displaying keyboard shortcuts for actions.',
      },
    },
  },
};

/**
 * Multi-line tooltip.
 * Supports longer descriptions with formatting.
 */
export const MultiLine: Story = {
  render: () => (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button variant="outline">Hover for details</Button>
      </TooltipTrigger>
      <TooltipContent className="max-w-xs">
        <p className="font-semibold mb-1">Feature Details</p>
        <p className="text-xs">
          This feature allows you to perform multiple actions at once. Click to open the
          dialog and configure your preferences.
        </p>
      </TooltipContent>
    </Tooltip>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Tooltip with multi-line content and formatting.',
      },
    },
  },
};

/**
 * Disabled trigger.
 * Shows tooltip even when trigger is disabled.
 */
export const DisabledTrigger: Story = {
  render: () => (
    <Tooltip>
      <TooltipTrigger asChild>
        <span tabIndex={0}>
          <Button variant="outline" disabled>
            Disabled Button
          </Button>
        </span>
      </TooltipTrigger>
      <TooltipContent>
        <p>This action is currently unavailable</p>
      </TooltipContent>
    </Tooltip>
  ),
  parameters: {
    docs: {
      description: {
        story:
          'Tooltip on disabled button. Wrap disabled elements in span to enable tooltip.',
      },
    },
  },
};

/**
 * Multiple tooltips example.
 * Shows multiple tooltips in a toolbar.
 */
export const ToolbarExample: Story = {
  render: () => (
    <div className="flex gap-2 rounded-md border p-2">
      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="ghost" size="icon">
            <svg
              className="h-4 w-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M3 7h18M3 12h18M3 17h18"
              />
            </svg>
          </Button>
        </TooltipTrigger>
        <TooltipContent>
          <p>Menu</p>
        </TooltipContent>
      </Tooltip>

      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="ghost" size="icon">
            <svg
              className="h-4 w-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
              />
            </svg>
          </Button>
        </TooltipTrigger>
        <TooltipContent>
          <p>Search</p>
        </TooltipContent>
      </Tooltip>

      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="ghost" size="icon">
            <Settings className="h-4 w-4" />
          </Button>
        </TooltipTrigger>
        <TooltipContent>
          <p>Settings</p>
        </TooltipContent>
      </Tooltip>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Multiple tooltips in a toolbar layout.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows tooltip appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button variant="outline">Hover me</Button>
      </TooltipTrigger>
      <TooltipContent>
        <p>Dark theme tooltip</p>
      </TooltipContent>
    </Tooltip>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <TooltipProvider>
        <div className="dark p-12 bg-background">
          <Story />
        </div>
      </TooltipProvider>
    ),
  ],
};
