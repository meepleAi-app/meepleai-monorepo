import { Label } from '@/components/ui/primitives/label';

import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
  SelectSeparator,
} from './select';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Select component for dropdown selection.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Select with accessible keyboard navigation.
 *
 * ## Subcomponents
 * - **Select**: Root component (controlled or uncontrolled)
 * - **SelectTrigger**: Button that opens dropdown
 * - **SelectValue**: Displays selected value
 * - **SelectContent**: Dropdown portal
 * - **SelectGroup**: Groups related items
 * - **SelectLabel**: Group label
 * - **SelectItem**: Selectable option
 * - **SelectSeparator**: Visual divider
 *
 * ## Features
 * - **Keyboard navigation**: Arrow keys, Enter, Escape
 * - **Search**: Type to highlight matching items
 * - **Disabled state**: Individual items or whole select
 * - **Placeholder**: Default text when nothing selected
 *
 * ## Accessibility
 * - ✅ Full ARIA support (Radix UI)
 * - ✅ Keyboard navigation
 * - ✅ Focus management
 * - ✅ Screen reader friendly
 */
const meta = {
  title: 'UI/Select',
  component: Select,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'An accessible dropdown select component built on Radix UI with keyboard navigation and search.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="w-80">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Select>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default select with placeholder.
 * Standard dropdown with simple options.
 */
export const Default: Story = {
  render: () => (
    <Select>
      <SelectTrigger>
        <SelectValue placeholder="Select an option" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="option1">Option 1</SelectItem>
        <SelectItem value="option2">Option 2</SelectItem>
        <SelectItem value="option3">Option 3</SelectItem>
      </SelectContent>
    </Select>
  ),
};

/**
 * Select with label.
 * Proper form field with associated label.
 */
export const WithLabel: Story = {
  render: () => (
    <div className="grid gap-2">
      <Label htmlFor="framework">Framework</Label>
      <Select>
        <SelectTrigger id="framework">
          <SelectValue placeholder="Select a framework" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="react">React</SelectItem>
          <SelectItem value="vue">Vue</SelectItem>
          <SelectItem value="angular">Angular</SelectItem>
          <SelectItem value="svelte">Svelte</SelectItem>
        </SelectContent>
      </Select>
    </div>
  ),
};

/**
 * Select with groups.
 * Organized options with labeled groups.
 */
export const WithGroups: Story = {
  render: () => (
    <Select>
      <SelectTrigger>
        <SelectValue placeholder="Select a game" />
      </SelectTrigger>
      <SelectContent>
        <SelectGroup>
          <SelectLabel>Strategy</SelectLabel>
          <SelectItem value="catan">Catan</SelectItem>
          <SelectItem value="ticket">Ticket to Ride</SelectItem>
          <SelectItem value="carcassonne">Carcassonne</SelectItem>
        </SelectGroup>
        <SelectSeparator />
        <SelectGroup>
          <SelectLabel>Party</SelectLabel>
          <SelectItem value="codenames">Codenames</SelectItem>
          <SelectItem value="dixit">Dixit</SelectItem>
          <SelectItem value="uno">UNO</SelectItem>
        </SelectGroup>
      </SelectContent>
    </Select>
  ),
};

/**
 * Select with disabled options.
 * Some options are not selectable.
 */
export const WithDisabledOptions: Story = {
  render: () => (
    <Select>
      <SelectTrigger>
        <SelectValue placeholder="Select a plan" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="free">Free</SelectItem>
        <SelectItem value="pro">Pro</SelectItem>
        <SelectItem value="enterprise" disabled>
          Enterprise (Coming Soon)
        </SelectItem>
      </SelectContent>
    </Select>
  ),
};

/**
 * Disabled select.
 * Entire select is non-interactive.
 */
export const Disabled: Story = {
  render: () => (
    <Select disabled>
      <SelectTrigger>
        <SelectValue placeholder="Select an option" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="option1">Option 1</SelectItem>
        <SelectItem value="option2">Option 2</SelectItem>
      </SelectContent>
    </Select>
  ),
};

/**
 * Select with default value.
 * Pre-selected option on mount.
 */
export const WithDefaultValue: Story = {
  render: () => (
    <Select defaultValue="react">
      <SelectTrigger>
        <SelectValue placeholder="Select a framework" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="react">React</SelectItem>
        <SelectItem value="vue">Vue</SelectItem>
        <SelectItem value="angular">Angular</SelectItem>
      </SelectContent>
    </Select>
  ),
};

/**
 * Select with many options.
 * Scrollable dropdown with many items.
 */
export const WithManyOptions: Story = {
  render: () => (
    <Select>
      <SelectTrigger>
        <SelectValue placeholder="Select a country" />
      </SelectTrigger>
      <SelectContent>
        {[
          'Italy',
          'France',
          'Germany',
          'Spain',
          'United Kingdom',
          'United States',
          'Canada',
          'Australia',
          'Japan',
          'China',
          'Brazil',
          'Argentina',
          'Mexico',
          'India',
          'Russia',
        ].map(country => (
          <SelectItem key={country} value={country.toLowerCase()}>
            {country}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  ),
};

/**
 * Form with multiple selects.
 * Complete form example with validation context.
 */
export const FormExample: Story = {
  render: () => (
    <div className="grid gap-4">
      <div className="grid gap-2">
        <Label htmlFor="category">Game Category</Label>
        <Select>
          <SelectTrigger id="category">
            <SelectValue placeholder="Select category" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="strategy">Strategy</SelectItem>
            <SelectItem value="party">Party</SelectItem>
            <SelectItem value="family">Family</SelectItem>
            <SelectItem value="cooperative">Cooperative</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label htmlFor="players">Number of Players</Label>
        <Select>
          <SelectTrigger id="players">
            <SelectValue placeholder="Select players" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="2">2 Players</SelectItem>
            <SelectItem value="3-4">3-4 Players</SelectItem>
            <SelectItem value="5-6">5-6 Players</SelectItem>
            <SelectItem value="7+">7+ Players</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div className="grid gap-2">
        <Label htmlFor="difficulty">Difficulty</Label>
        <Select>
          <SelectTrigger id="difficulty">
            <SelectValue placeholder="Select difficulty" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="easy">Easy</SelectItem>
            <SelectItem value="medium">Medium</SelectItem>
            <SelectItem value="hard">Hard</SelectItem>
            <SelectItem value="expert">Expert</SelectItem>
          </SelectContent>
        </Select>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Example form with multiple labeled select fields.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows select appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <Select>
      <SelectTrigger>
        <SelectValue placeholder="Select an option" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="option1">Option 1</SelectItem>
        <SelectItem value="option2">Option 2</SelectItem>
        <SelectItem value="option3">Option 3</SelectItem>
      </SelectContent>
    </Select>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
