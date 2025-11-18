import type { Meta, StoryObj } from '@storybook/react';
import { SimpleErrorMessage } from './SimpleErrorMessage';
import { fn } from '@storybook/test';
import React from 'react';

/**
 * SimpleErrorMessage - Lightweight inline error/warning/info message component.
 * Use for simple inline displays. For complex errors with retry and correlation IDs, use ErrorDisplay.
 * Provides consistent styling and accessibility.
 */
const meta = {
  title: 'Error/SimpleErrorMessage',
  component: SimpleErrorMessage,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['error', 'warning', 'info', 'success'],
      description: 'Visual variant of the message',
    },
    message: {
      control: 'text',
      description: 'Message to display (null/undefined renders nothing)',
    },
    className: {
      control: 'text',
      description: 'Additional CSS classes',
    },
  },
  args: {
    onDismiss: fn(),
  },
} satisfies Meta<typeof SimpleErrorMessage>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Error variant (red)
 */
export const Error: Story = {
  args: {
    message: 'Failed to save changes. Please try again.',
    variant: 'error',
  },
};

/**
 * Warning variant (yellow)
 */
export const Warning: Story = {
  args: {
    message: 'Your session will expire in 5 minutes.',
    variant: 'warning',
  },
};

/**
 * Info variant (blue)
 */
export const Info: Story = {
  args: {
    message: 'This feature is currently in beta.',
    variant: 'info',
  },
};

/**
 * Success variant (green)
 */
export const Success: Story = {
  args: {
    message: 'Changes saved successfully!',
    variant: 'success',
  },
};

/**
 * With dismiss button
 */
export const WithDismiss: Story = {
  args: {
    message: 'You can dismiss this message.',
    variant: 'info',
  },
};

/**
 * Without dismiss button
 */
export const WithoutDismiss: Story = {
  args: {
    message: 'This is a permanent message.',
    variant: 'error',
    onDismiss: undefined,
  },
};

/**
 * Long message text
 */
export const LongMessage: Story = {
  args: {
    message:
      'Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.',
    variant: 'warning',
  },
};

/**
 * Null message (renders nothing)
 */
export const NullMessage: Story = {
  args: {
    message: null,
    variant: 'error',
  },
};

/**
 * Undefined message (renders nothing)
 */
export const UndefinedMessage: Story = {
  args: {
    message: undefined,
    variant: 'error',
  },
};

/**
 * With custom className
 */
export const CustomClassName: Story = {
  args: {
    message: 'Custom styled message',
    variant: 'info',
    className: 'mb-4 shadow-lg',
  },
};

/**
 * Interactive dismissible message
 */
const InteractiveDismissComponent = () => {
  const [message, setMessage] = React.useState<string | null>(
    'This message can be dismissed. Click the X button.'
  );

  return (
    <div className="space-y-4">
      {!message && (
        <button
          onClick={() => setMessage('Message restored! You can dismiss it again.')}
          className="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600"
        >
          Show Message
        </button>
      )}
      <SimpleErrorMessage message={message} variant="info" onDismiss={() => setMessage(null)} />
    </div>
  );
};

export const InteractiveDismiss: Story = {
  render: () => <InteractiveDismissComponent />,
};

/**
 * Form validation example
 */
const FormValidationComponent = () => {
  const [email, setEmail] = React.useState('');
  const [error, setError] = React.useState<string | null>(null);
  const [success, setSuccess] = React.useState<string | null>(null);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    if (!email) {
      setError('Email is required');
      return;
    }

    if (!email.includes('@')) {
      setError('Please enter a valid email address');
      return;
    }

    setSuccess('Email submitted successfully!');
    setEmail('');
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4 max-w-md">
      <div>
        <label className="block mb-2 font-medium">Email:</label>
        <input
          type="text"
          value={email}
          onChange={(e) => {
            setEmail(e.target.value);
            setError(null);
            setSuccess(null);
          }}
          className="border rounded px-3 py-2 w-full"
          placeholder="Enter your email"
        />
      </div>

      <SimpleErrorMessage message={error} variant="error" />
      <SimpleErrorMessage message={success} variant="success" onDismiss={() => setSuccess(null)} />

      <button type="submit" className="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600">
        Submit
      </button>
    </form>
  );
};

export const FormValidation: Story = {
  render: () => <FormValidationComponent />,
};

/**
 * All variants showcase
 */
export const AllVariants: Story = {
  render: () => (
    <div className="space-y-4">
      <SimpleErrorMessage message="This is an error message" variant="error" />
      <SimpleErrorMessage message="This is a warning message" variant="warning" />
      <SimpleErrorMessage message="This is an info message" variant="info" />
      <SimpleErrorMessage message="This is a success message" variant="success" />
    </div>
  ),
};

/**
 * Stacked messages
 */
const StackedMessagesComponent = () => {
  const [messages, setMessages] = React.useState([
    { id: 1, text: 'First error occurred', variant: 'error' as const },
    { id: 2, text: 'Warning: Low disk space', variant: 'warning' as const },
    { id: 3, text: 'Update available', variant: 'info' as const },
  ]);

  const dismissMessage = (id: number) => {
    setMessages(messages.filter((m) => m.id !== id));
  };

  return (
    <div className="space-y-3">
      {messages.map((msg) => (
        <SimpleErrorMessage
          key={msg.id}
          message={msg.text}
          variant={msg.variant}
          onDismiss={() => dismissMessage(msg.id)}
        />
      ))}
      {messages.length === 0 && (
        <div className="text-center text-gray-500 py-8">
          <p>All messages dismissed!</p>
          <button
            onClick={() =>
              setMessages([
                { id: Date.now(), text: 'New message added', variant: 'info' },
              ])
            }
            className="mt-4 px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600"
          >
            Add Message
          </button>
        </div>
      )}
    </div>
  );
};

export const StackedMessages: Story = {
  render: () => <StackedMessagesComponent />,
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <div className="space-y-4">
          <SimpleErrorMessage message="Error in dark mode" variant="error" />
          <SimpleErrorMessage message="Warning in dark mode" variant="warning" />
          <SimpleErrorMessage message="Info in dark mode" variant="info" />
          <SimpleErrorMessage message="Success in dark mode" variant="success" />
        </div>
      </div>
    ),
  ],
};
