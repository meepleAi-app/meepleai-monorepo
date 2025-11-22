import type { Meta, StoryObj } from '@storybook/react';
import { Textarea } from './textarea';
import { Label } from './label';
import { Button } from './button';

/**
 * Textarea component for multi-line text input.
 * Consistent styling with other form elements.
 */
const meta = {
  title: 'UI/Textarea',
  component: Textarea,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    placeholder: {
      control: 'text',
      description: 'Placeholder text',
    },
    disabled: {
      control: 'boolean',
      description: 'Whether the textarea is disabled',
    },
  },
} satisfies Meta<typeof Textarea>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default textarea
 */
export const Default: Story = {
  args: {
    placeholder: 'Type your message here.',
    className: 'w-[350px]',
  },
};

/**
 * Textarea with value
 */
export const WithValue: Story = {
  args: {
    defaultValue: 'This is a sample text in the textarea.',
    className: 'w-[350px]',
  },
};

/**
 * Disabled textarea
 */
export const Disabled: Story = {
  args: {
    placeholder: 'Disabled textarea',
    disabled: true,
    className: 'w-[350px]',
  },
};

/**
 * Textarea with label
 */
export const WithLabel: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="message">Message</Label>
      <Textarea id="message" placeholder="Type your message here." />
    </div>
  ),
};

/**
 * Textarea with helper text
 */
export const WithHelperText: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="bio">Bio</Label>
      <Textarea id="bio" placeholder="Tell us about yourself" />
      <p className="text-sm text-muted-foreground">
        You can @mention other users and organizations.
      </p>
    </div>
  ),
};

/**
 * Textarea with character count
 */
export const WithCharacterCount: Story = {
  render: () => {
    const maxLength = 200;
    return (
      <div className="w-[350px] space-y-2">
        <Label htmlFor="description">Description</Label>
        <Textarea
          id="description"
          placeholder="Enter a description"
          maxLength={maxLength}
        />
        <p className="text-sm text-muted-foreground text-right">
          0/{maxLength}
        </p>
      </div>
    );
  },
};

/**
 * Textarea with error state
 */
export const WithError: Story = {
  render: () => (
    <div className="w-[350px] space-y-2">
      <Label htmlFor="feedback">Feedback</Label>
      <Textarea
        id="feedback"
        placeholder="Your feedback"
        className="border-destructive"
      />
      <p className="text-sm text-destructive">
        Feedback is required and must be at least 10 characters.
      </p>
    </div>
  ),
};

/**
 * Comment form example
 */
export const CommentForm: Story = {
  render: () => (
    <div className="w-[450px] space-y-4">
      <div className="space-y-2">
        <Label htmlFor="comment">Add a comment</Label>
        <Textarea
          id="comment"
          placeholder="What are your thoughts?"
          className="min-h-[100px]"
        />
      </div>
      <div className="flex justify-between items-center">
        <p className="text-sm text-muted-foreground">
          Supports Markdown
        </p>
        <div className="flex gap-2">
          <Button variant="outline">Cancel</Button>
          <Button>Submit</Button>
        </div>
      </div>
    </div>
  ),
};

/**
 * Contact form example
 */
export const ContactForm: Story = {
  render: () => (
    <div className="w-[450px] space-y-4">
      <div className="space-y-2">
        <Label htmlFor="name">Name</Label>
        <input
          id="name"
          className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm"
          placeholder="Your name"
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="email">Email</Label>
        <input
          id="email"
          type="email"
          className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm"
          placeholder="your@email.com"
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="message-contact">Message</Label>
        <Textarea
          id="message-contact"
          placeholder="How can we help?"
          className="min-h-[120px]"
        />
      </div>
      <Button className="w-full">Send Message</Button>
    </div>
  ),
};

/**
 * Large textarea
 */
export const Large: Story = {
  args: {
    placeholder: 'Write your content here...',
    className: 'w-[500px] min-h-[200px]',
  },
};

/**
 * Resizable textarea
 */
export const Resizable: Story = {
  args: {
    placeholder: 'This textarea can be resized',
    className: 'w-[350px] min-h-[100px] resize',
  },
};
