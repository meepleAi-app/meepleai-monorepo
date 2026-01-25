import { ChevronDown } from 'lucide-react';

import { Button } from '../primitives/button';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from './collapsible';

import type { Meta, StoryObj } from '@storybook/react';

/* DISABLED - causes Maximum update depth
/* DISABLED - causes Maximum update depth
/**
 * Collapsible component for expandable content sections.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Collapsible with smooth animations.
 *
 * ## Features
 * - **Controlled/Uncontrolled**: Both modes supported
 * - **Smooth animations**: Expand/collapse transitions
 * - **Accessibility**: ARIA attributes, keyboard support
 *
 * ## Accessibility
 * - ✅ Keyboard navigation (Space, Enter)
 * - ✅ ARIA expanded state
 * - ✅ Focus management
 */
const meta = {
  title: 'UI/Collapsible',
  component: Collapsible,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A collapsible component for showing and hiding content. Simpler than Accordion for single-section collapsing.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-full max-w-md">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Collapsible>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default collapsible with controlled state.
 */
export const Default: Story = {
  render: function DefaultStory() {
};
*/

/**
 * Simple expand/collapse section.
 */
export const Simple: Story = {
  render: () => (
    <Collapsible defaultOpen>
      <CollapsibleTrigger asChild>
        <Button variant="outline" className="w-full justify-between">
          <span>Can I use this in my project?</span>
          <ChevronDown className="h-4 w-4" />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent className="mt-2 p-4 rounded-md border bg-muted/50">
        <p className="text-sm text-muted-foreground">
          Yes. Free to use for personal and commercial projects. No attribution required.
        </p>
      </CollapsibleContent>
    </Collapsible>
  ),
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  render: function DarkThemeStory() {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark w-full max-w-md p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
*/
