import type { Meta, StoryObj } from '@storybook/react';
import { Input } from './input';
import { Label } from './label';
import { Search, Mail } from 'lucide-react';

/**
 * Input component for text input fields.
 * Supports all HTML input types with consistent styling.
 */
const meta = {
  title: 'UI/Input',
  component: Input,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    type: {
      control: 'select',
      options: ['text', 'email', 'password', 'number', 'search', 'tel', 'url'],
      description: 'HTML input type',
    },
    disabled: {
      control: 'boolean',
      description: 'Whether the input is disabled',
    },
    placeholder: {
      control: 'text',
      description: 'Placeholder text',
    },
  },
} satisfies Meta<typeof Input>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default text input
 */
export const Default: Story = {
  args: {
    placeholder: 'Enter text...',
  },
};

/**
 * Email input type
 */
export const Email: Story = {
  args: {
    type: 'email',
    placeholder: 'email@example.com',
  },
};

/**
 * Password input type
 */
export const Password: Story = {
  args: {
    type: 'password',
    placeholder: '••••••••',
  },
};

/**
 * Number input type
 */
export const Number: Story = {
  args: {
    type: 'number',
    placeholder: '0',
  },
};

/**
 * Search input type
 */
export const SearchInput: Story = {
  args: {
    type: 'search',
    placeholder: 'Search...',
  },
};

/**
 * Disabled input
 */
export const Disabled: Story = {
  args: {
    placeholder: 'Disabled input',
    disabled: true,
  },
};

/**
 * Input with value
 */
export const WithValue: Story = {
  args: {
    defaultValue: 'Hello World',
  },
};

/**
 * Input with label
 */
export const WithLabel: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="email-input">Email</Label>
      <Input id="email-input" type="email" placeholder="email@example.com" />
    </div>
  ),
};

/**
 * Input with icon
 */
export const WithIcon: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="search-input">Search</Label>
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input id="search-input" placeholder="Search..." className="pl-10" />
      </div>
    </div>
  ),
};

/**
 * Form example
 */
export const FormExample: Story = {
  render: () => (
    <div className="w-[350px] space-y-4">
      <div className="space-y-2">
        <Label htmlFor="name">Name</Label>
        <Input id="name" placeholder="John Doe" />
      </div>
      <div className="space-y-2">
        <Label htmlFor="email">Email</Label>
        <Input id="email" type="email" placeholder="john@example.com" />
      </div>
      <div className="space-y-2">
        <Label htmlFor="password">Password</Label>
        <Input id="password" type="password" placeholder="••••••••" />
      </div>
    </div>
  ),
};

/**
 * File input
 */
export const File: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="file">Upload file</Label>
      <Input id="file" type="file" />
    </div>
  ),
};

/**
 * Input with helper text
 */
export const WithHelperText: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="username">Username</Label>
      <Input id="username" placeholder="@username" />
      <p className="text-sm text-muted-foreground">
        This is your public display name.
      </p>
    </div>
  ),
};

/**
 * Input with error state
 */
export const WithError: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="email-error">Email</Label>
      <Input
        id="email-error"
        type="email"
        placeholder="email@example.com"
        className="border-destructive"
      />
      <p className="text-sm text-destructive">
        Please enter a valid email address.
      </p>
    </div>
  ),
};
