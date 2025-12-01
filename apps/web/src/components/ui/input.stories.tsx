import type { Meta, StoryObj } from '@storybook/react';
import { Input } from './input';
import { Label } from './label';
import { Mail, Search, Eye, EyeOff } from 'lucide-react';
import { useState } from 'react';

/**
 * Input component for text and data entry.
 *
 * ## shadcn/ui Component
 * Standard HTML input with consistent styling and accessibility.
 *
 * ## Features
 * - **Input types**: text, email, password, number, search, tel, url, date
 * - **States**: default, disabled, error (via custom className)
 * - **File input**: Special styling for file uploads
 * - **Placeholder**: Muted text for empty inputs
 *
 * ## Accessibility
 * - ✅ Keyboard navigation
 * - ✅ Focus-visible ring indicator
 * - ✅ Disabled state with cursor-not-allowed
 * - ✅ Pairs with Label for accessibility
 */
const meta = {
  title: 'UI/Input',
  component: Input,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A styled input component supporting various HTML input types with consistent design and accessibility.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    type: {
      control: 'select',
      options: ['text', 'email', 'password', 'number', 'search', 'tel', 'url', 'date', 'file'],
      description: 'HTML input type',
      table: {
        type: { summary: 'string' },
        defaultValue: { summary: 'text' },
      },
    },
    placeholder: {
      control: 'text',
      description: 'Placeholder text',
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state',
    },
  },
  decorators: [
    Story => (
      <div className="w-80">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Input>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default text input.
 * Standard input for general text entry.
 */
export const Default: Story = {
  args: {
    type: 'text',
    placeholder: 'Type something...',
  },
};

/**
 * Email input with validation.
 * Browser validates email format.
 */
export const Email: Story = {
  args: {
    type: 'email',
    placeholder: 'email@example.com',
  },
};

/**
 * Password input with masked text.
 * Text is hidden for security.
 */
export const Password: Story = {
  args: {
    type: 'password',
    placeholder: 'Enter password',
  },
};

/**
 * Number input with spinners.
 * Only numeric input allowed.
 */
export const Number: Story = {
  args: {
    type: 'number',
    placeholder: '0',
  },
};

/**
 * Search input with icon.
 * Common pattern for search fields.
 */
export const SearchWithIcon: Story = {
  render: () => (
    <div className="relative">
      <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
      <Input type="search" placeholder="Search..." className="pl-8" />
    </div>
  ),
};

/**
 * Input with label.
 * Proper form field with associated label.
 */
export const WithLabel: Story = {
  render: () => (
    <div className="grid w-full gap-1.5">
      <Label htmlFor="email">Email</Label>
      <Input type="email" id="email" placeholder="email@example.com" />
    </div>
  ),
};

/**
 * Disabled input state.
 * Non-interactive with reduced opacity.
 */
export const Disabled: Story = {
  args: {
    type: 'text',
    placeholder: 'Disabled input',
    disabled: true,
  },
};

/**
 * File input for uploads.
 * Special styling for file selection.
 */
export const File: Story = {
  args: {
    type: 'file',
  },
};

/**
 * Input with error state.
 * Shows validation error with red border.
 */
export const WithError: Story = {
  render: () => (
    <div className="grid w-full gap-1.5">
      <Label htmlFor="email-error">Email</Label>
      <Input
        type="email"
        id="email-error"
        placeholder="email@example.com"
        className="border-destructive"
      />
      <p className="text-sm text-destructive">Invalid email address</p>
    </div>
  ),
};

/**
 * Input with icon (leading).
 * Icon positioned at the start of input.
 */
export const WithLeadingIcon: Story = {
  render: () => (
    <div className="relative">
      <Mail className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
      <Input type="email" placeholder="Email" className="pl-8" />
    </div>
  ),
};

/**
 * Password input with toggle visibility.
 * Interactive example showing password reveal.
 */
const PasswordToggleComponent = () => {
  const [showPassword, setShowPassword] = useState(false);
  return (
    <div className="relative">
      <Input
        type={showPassword ? 'text' : 'password'}
        placeholder="Enter password"
        className="pr-10"
      />
      <button
        type="button"
        onClick={() => setShowPassword(!showPassword)}
        className="absolute right-2 top-2.5 h-4 w-4 text-muted-foreground hover:text-foreground"
      >
        {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
      </button>
    </div>
  );
};

export const PasswordToggle: Story = {
  render: () => <PasswordToggleComponent />,
};

/**
 * Form with multiple inputs.
 * Complete form example with various input types.
 */
export const FormExample: Story = {
  render: () => (
    <div className="grid w-full gap-4">
      <div className="grid gap-1.5">
        <Label htmlFor="name">Name</Label>
        <Input id="name" placeholder="Your name" />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="email-form">Email</Label>
        <Input id="email-form" type="email" placeholder="email@example.com" />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="password-form">Password</Label>
        <Input id="password-form" type="password" placeholder="••••••••" />
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Example form with multiple labeled inputs.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows input appearance on dark background.
 */
export const DarkTheme: Story = {
  args: {
    type: 'text',
    placeholder: 'Dark theme input',
  },
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
