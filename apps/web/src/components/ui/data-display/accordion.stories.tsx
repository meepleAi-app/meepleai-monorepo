import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from './accordion';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Accordion component for collapsible content sections.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Accordion with smooth animations.
 *
 * ## Features
 * - **Collapsible sections**: Show/hide content on demand
 * - **Single/Multiple**: Control how many items can be open
 * - **Keyboard support**: Arrow keys, Home, End navigation
 * - **Animations**: Smooth expand/collapse transitions
 *
 * ## Accessibility
 * - ✅ Keyboard navigation (Arrow keys, Home, End, Space, Enter)
 * - ✅ Focus management
 * - ✅ ARIA accordion roles and states
 * - ✅ Screen reader announcements
 */
const meta = {
  title: 'UI/Accordion',
  component: Accordion,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'An accordion component for displaying collapsible content sections. Supports single or multiple open items with smooth animations.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    type: {
      control: 'select',
      options: ['single', 'multiple'],
      description: 'Controls whether one or multiple items can be open',
      table: {
        type: { summary: '"single" | "multiple"' },
        defaultValue: { summary: 'single' },
      },
    },
    collapsible: {
      control: 'boolean',
      description: 'Allow closing all items (single mode only)',
      table: {
        type: { summary: 'boolean' },
        defaultValue: { summary: 'false' },
      },
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state for all items',
    },
  },
  decorators: [
    (Story) => (
      <div className="w-full max-w-md">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Accordion>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default single-item accordion.
 * Only one section open at a time.
 */
export const Default: Story = {
  render: () => (
    <Accordion type="single" collapsible>
      <AccordionItem value="item-1">
        <AccordionTrigger>Is it accessible?</AccordionTrigger>
        <AccordionContent>
          Yes. It adheres to the WAI-ARIA design pattern with full keyboard support.
        </AccordionContent>
      </AccordionItem>
      <AccordionItem value="item-2">
        <AccordionTrigger>Is it styled?</AccordionTrigger>
        <AccordionContent>
          Yes. It comes with default styles that you can customize with Tailwind CSS.
        </AccordionContent>
      </AccordionItem>
      <AccordionItem value="item-3">
        <AccordionTrigger>Is it animated?</AccordionTrigger>
        <AccordionContent>
          Yes. It uses CSS animations for smooth expand and collapse transitions.
        </AccordionContent>
      </AccordionItem>
    </Accordion>
  ),
};

/**
 * Multiple items can be open simultaneously.
 * Use for content that doesn't need mutual exclusion.
 */
export const Multiple: Story = {
  render: () => (
    <Accordion type="multiple" defaultValue={['item-1', 'item-2']}>
      <AccordionItem value="item-1">
        <AccordionTrigger>Section 1</AccordionTrigger>
        <AccordionContent>Content for section 1</AccordionContent>
      </AccordionItem>
      <AccordionItem value="item-2">
        <AccordionTrigger>Section 2</AccordionTrigger>
        <AccordionContent>Content for section 2</AccordionContent>
      </AccordionItem>
      <AccordionItem value="item-3">
        <AccordionTrigger>Section 3</AccordionTrigger>
        <AccordionContent>Content for section 3</AccordionContent>
      </AccordionItem>
    </Accordion>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Multiple accordion items can be open at the same time.',
      },
    },
  },
};

/**
 * FAQ example with longer content.
 * Common use case for help sections.
 */
export const FAQ: Story = {
  render: () => (
    <Accordion type="single" collapsible>
      <AccordionItem value="shipping">
        <AccordionTrigger>How long does shipping take?</AccordionTrigger>
        <AccordionContent>
          Standard shipping typically takes 5-7 business days. Express shipping is
          available for 2-3 day delivery. International orders may take 10-15 business
          days depending on customs processing.
        </AccordionContent>
      </AccordionItem>
      <AccordionItem value="returns">
        <AccordionTrigger>What is your return policy?</AccordionTrigger>
        <AccordionContent>
          We offer a 30-day return policy on all items. Products must be unused and in
          original packaging. Refunds are processed within 5-7 business days of receiving
          the returned item.
        </AccordionContent>
      </AccordionItem>
      <AccordionItem value="warranty">
        <AccordionTrigger>Do you offer warranty?</AccordionTrigger>
        <AccordionContent>
          All products come with a 1-year manufacturer warranty covering defects in
          materials and workmanship. Extended warranty options are available at checkout.
        </AccordionContent>
      </AccordionItem>
      <AccordionItem value="support">
        <AccordionTrigger>How can I contact support?</AccordionTrigger>
        <AccordionContent>
          Our support team is available via email at support@example.com or through our
          live chat Monday-Friday 9am-5pm EST. Average response time is under 2 hours.
        </AccordionContent>
      </AccordionItem>
    </Accordion>
  ),
  parameters: {
    docs: {
      description: {
        story: 'FAQ accordion with detailed answers.',
      },
    },
  },
};

/**
 * Settings panel example.
 * Accordion for organizing configuration options.
 */
export const SettingsPanel: Story = {
  render: () => (
    <Accordion type="single" collapsible defaultValue="general">
      <AccordionItem value="general">
        <AccordionTrigger>General Settings</AccordionTrigger>
        <AccordionContent>
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm">Language</span>
              <span className="text-sm text-muted-foreground">English</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm">Timezone</span>
              <span className="text-sm text-muted-foreground">UTC-5</span>
            </div>
          </div>
        </AccordionContent>
      </AccordionItem>
      <AccordionItem value="notifications">
        <AccordionTrigger>Notifications</AccordionTrigger>
        <AccordionContent>
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm">Email notifications</span>
              <span className="text-sm text-muted-foreground">Enabled</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm">Push notifications</span>
              <span className="text-sm text-muted-foreground">Disabled</span>
            </div>
          </div>
        </AccordionContent>
      </AccordionItem>
      <AccordionItem value="privacy">
        <AccordionTrigger>Privacy & Security</AccordionTrigger>
        <AccordionContent>
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm">Two-factor authentication</span>
              <span className="text-sm text-muted-foreground">Enabled</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm">Profile visibility</span>
              <span className="text-sm text-muted-foreground">Public</span>
            </div>
          </div>
        </AccordionContent>
      </AccordionItem>
    </Accordion>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Settings panel organized with accordion sections.',
      },
    },
  },
};

/**
 * Disabled accordion.
 * Non-interactive state.
 */
export const Disabled: Story = {
  render: () => (
    <Accordion type="single" disabled>
      <AccordionItem value="item-1">
        <AccordionTrigger>Disabled Section 1</AccordionTrigger>
        <AccordionContent>This content cannot be accessed</AccordionContent>
      </AccordionItem>
      <AccordionItem value="item-2">
        <AccordionTrigger>Disabled Section 2</AccordionTrigger>
        <AccordionContent>This content cannot be accessed</AccordionContent>
      </AccordionItem>
    </Accordion>
  ),
};

/**
 * Dark theme variant.
 * Shows accordion appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <Accordion type="single" collapsible>
      <AccordionItem value="item-1">
        <AccordionTrigger>Dark Theme Section</AccordionTrigger>
        <AccordionContent>Content with dark theme styling</AccordionContent>
      </AccordionItem>
      <AccordionItem value="item-2">
        <AccordionTrigger>Another Section</AccordionTrigger>
        <AccordionContent>More content in dark mode</AccordionContent>
      </AccordionItem>
    </Accordion>
  ),
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
