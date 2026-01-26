import { CalendarDays } from 'lucide-react';

import { Avatar, AvatarFallback, AvatarImage } from '../data-display/avatar';
import { Button } from '../primitives/button';
import { HoverCard, HoverCardContent, HoverCardTrigger } from './hover-card';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * HoverCard component for rich hover previews.
 *
 * ## shadcn/ui Component
 * Based on Radix UI HoverCard with glassmorphism styling.
 *
 * ## Features
 * - **Rich previews**: Show detailed info on hover
 * - **Positioning**: Auto-positioning with preferences
 * - **Animations**: Smooth fade and zoom transitions
 * - **Delay**: Customizable show/hide delays
 *
 * ## Accessibility
 * - ✅ ARIA attributes
 * - ✅ Keyboard accessible (Escape to close)
 * - ✅ Focus management
 */
const meta = {
  title: 'UI/HoverCard',
  component: HoverCard,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A hover card component for displaying rich content on hover. Similar to tooltips but for more complex content.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="p-12">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof HoverCard>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default hover card with user profile.
 */
export const Default: Story = {
  render: () => (
    <HoverCard>
      <HoverCardTrigger asChild>
        <Button variant="link">@nextjs</Button>
      </HoverCardTrigger>
      <HoverCardContent className="w-80">
        <div className="flex justify-between space-x-4">
          <Avatar>
            <AvatarImage src="https://github.com/vercel.png" />
            <AvatarFallback>VC</AvatarFallback>
          </Avatar>
          <div className="space-y-1">
            <h4 className="text-sm font-semibold">@nextjs</h4>
            <p className="text-sm">
              The React Framework – created and maintained by @vercel.
            </p>
            <div className="flex items-center pt-2">
              <CalendarDays className="mr-2 h-4 w-4 opacity-70" />
              <span className="text-xs text-muted-foreground">
                Joined December 2021
              </span>
            </div>
          </div>
        </div>
      </HoverCardContent>
    </HoverCard>
  ),
};

/**
 * Simple preview card.
 */
export const SimplePreview: Story = {
  render: () => (
    <HoverCard>
      <HoverCardTrigger asChild>
        <span className="underline cursor-pointer">Hover me</span>
      </HoverCardTrigger>
      <HoverCardContent>
        <div className="space-y-2">
          <h4 className="text-sm font-semibold">Quick Info</h4>
          <p className="text-sm text-muted-foreground">
            Additional context appears on hover.
          </p>
        </div>
      </HoverCardContent>
    </HoverCard>
  ),
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  render: () => (
    <HoverCard>
      <HoverCardTrigger asChild>
        <Button variant="link">@shadcn</Button>
      </HoverCardTrigger>
      <HoverCardContent>
        <div className="space-y-1">
          <h4 className="text-sm font-semibold">shadcn/ui</h4>
          <p className="text-sm text-muted-foreground">
            Beautifully designed components built with Radix UI and Tailwind CSS.
          </p>
        </div>
      </HoverCardContent>
    </HoverCard>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark p-12 bg-background">
        <Story />
      </div>
    ),
  ],
};
