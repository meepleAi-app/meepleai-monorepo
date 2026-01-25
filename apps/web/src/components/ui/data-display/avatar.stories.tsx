import { User } from 'lucide-react';

import { Avatar, AvatarFallback, AvatarImage } from './avatar';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Avatar component for displaying user profile images.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Avatar with automatic fallback.
 *
 * ## Features
 * - **Image loading**: Automatic fallback on load failure
 * - **Fallback support**: Text or icon fallback
 * - **Customizable**: Size and shape variants
 * - **Accessible**: Proper alt text and ARIA labels
 *
 * ## Accessibility
 * - ✅ Alt text for images
 * - ✅ ARIA labels for fallback content
 * - ✅ Proper image load handling
 */
const meta = {
  title: 'UI/Avatar',
  component: Avatar,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'An avatar component for displaying user profile pictures with automatic fallback to initials or icons.',
      },
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof Avatar>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default avatar with image.
 * Standard user profile picture.
 */
export const Default: Story = {
  render: () => (
    <Avatar>
      <AvatarImage src="https://github.com/shadcn.png" alt="@shadcn" />
      <AvatarFallback>CN</AvatarFallback>
    </Avatar>
  ),
};

/**
 * Avatar with fallback text.
 * Shows initials when image fails to load.
 */
export const WithFallback: Story = {
  render: () => (
    <Avatar>
      <AvatarImage src="/invalid-url.jpg" alt="@johndoe" />
      <AvatarFallback>JD</AvatarFallback>
    </Avatar>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Fallback to initials when image fails to load.',
      },
    },
  },
};

/**
 * Avatar with icon fallback.
 * Uses icon when no image or initials available.
 */
export const IconFallback: Story = {
  render: () => (
    <Avatar>
      <AvatarImage src="/invalid-url.jpg" alt="Guest" />
      <AvatarFallback>
        <User className="h-5 w-5" />
      </AvatarFallback>
    </Avatar>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Icon fallback for guest or anonymous users.',
      },
    },
  },
};

/**
 * Different sizes.
 * Avatar sizes for various UI contexts.
 */
export const Sizes: Story = {
  render: () => (
    <div className="flex items-end gap-4">
      <Avatar className="h-8 w-8">
        <AvatarImage src="https://github.com/shadcn.png" alt="Small" />
        <AvatarFallback className="text-xs">SM</AvatarFallback>
      </Avatar>
      <Avatar className="h-10 w-10">
        <AvatarImage src="https://github.com/shadcn.png" alt="Default" />
        <AvatarFallback>MD</AvatarFallback>
      </Avatar>
      <Avatar className="h-16 w-16">
        <AvatarImage src="https://github.com/shadcn.png" alt="Large" />
        <AvatarFallback className="text-lg">LG</AvatarFallback>
      </Avatar>
      <Avatar className="h-24 w-24">
        <AvatarImage src="https://github.com/shadcn.png" alt="Extra Large" />
        <AvatarFallback className="text-2xl">XL</AvatarFallback>
      </Avatar>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Various avatar sizes from small (32px) to extra large (96px).',
      },
    },
  },
};

/**
 * Avatar group.
 * Multiple avatars displayed together.
 */
export const Group: Story = {
  render: () => (
    <div className="flex -space-x-4">
      <Avatar className="border-2 border-background">
        <AvatarImage src="https://github.com/shadcn.png" alt="User 1" />
        <AvatarFallback>U1</AvatarFallback>
      </Avatar>
      <Avatar className="border-2 border-background">
        <AvatarImage src="https://github.com/vercel.png" alt="User 2" />
        <AvatarFallback>U2</AvatarFallback>
      </Avatar>
      <Avatar className="border-2 border-background">
        <AvatarImage src="/invalid.jpg" alt="User 3" />
        <AvatarFallback>U3</AvatarFallback>
      </Avatar>
      <Avatar className="border-2 border-background">
        <AvatarFallback>+5</AvatarFallback>
      </Avatar>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Overlapping avatar group for showing multiple users.',
      },
    },
  },
};

/**
 * User profile example.
 * Avatar with name and role.
 */
export const UserProfile: Story = {
  render: () => (
    <div className="flex items-center gap-3">
      <Avatar>
        <AvatarImage src="https://github.com/shadcn.png" alt="@shadcn" />
        <AvatarFallback>CN</AvatarFallback>
      </Avatar>
      <div className="flex flex-col">
        <span className="text-sm font-medium">John Doe</span>
        <span className="text-xs text-muted-foreground">@johndoe</span>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Avatar combined with user information.',
      },
    },
  },
};

/**
 * Status indicator.
 * Avatar with online/offline status badge.
 */
export const WithStatus: Story = {
  render: () => (
    <div className="flex gap-4">
      <div className="relative">
        <Avatar>
          <AvatarImage src="https://github.com/shadcn.png" alt="Online user" />
          <AvatarFallback>ON</AvatarFallback>
        </Avatar>
        <span className="absolute bottom-0 right-0 h-3 w-3 rounded-full border-2 border-background bg-green-500" />
      </div>
      <div className="relative">
        <Avatar>
          <AvatarImage src="https://github.com/vercel.png" alt="Busy user" />
          <AvatarFallback>BY</AvatarFallback>
        </Avatar>
        <span className="absolute bottom-0 right-0 h-3 w-3 rounded-full border-2 border-background bg-yellow-500" />
      </div>
      <div className="relative">
        <Avatar>
          <AvatarImage src="/invalid.jpg" alt="Offline user" />
          <AvatarFallback>OF</AvatarFallback>
        </Avatar>
        <span className="absolute bottom-0 right-0 h-3 w-3 rounded-full border-2 border-background bg-gray-400" />
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Avatars with status indicator badges.',
      },
    },
  },
};

/**
 * Color variants for fallback.
 * Different background colors for visual variety.
 */
export const ColorVariants: Story = {
  render: () => (
    <div className="flex gap-3">
      <Avatar>
        <AvatarFallback className="bg-blue-500 text-white">AB</AvatarFallback>
      </Avatar>
      <Avatar>
        <AvatarFallback className="bg-green-500 text-white">CD</AvatarFallback>
      </Avatar>
      <Avatar>
        <AvatarFallback className="bg-purple-500 text-white">EF</AvatarFallback>
      </Avatar>
      <Avatar>
        <AvatarFallback className="bg-orange-500 text-white">GH</AvatarFallback>
      </Avatar>
      <Avatar>
        <AvatarFallback className="bg-pink-500 text-white">IJ</AvatarFallback>
      </Avatar>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Colored fallback backgrounds for visual distinction.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows avatar appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="flex gap-4">
      <Avatar>
        <AvatarImage src="https://github.com/shadcn.png" alt="@shadcn" />
        <AvatarFallback>CN</AvatarFallback>
      </Avatar>
      <Avatar>
        <AvatarImage src="/invalid.jpg" alt="Fallback" />
        <AvatarFallback>FB</AvatarFallback>
      </Avatar>
      <Avatar>
        <AvatarFallback>
          <User className="h-5 w-5" />
        </AvatarFallback>
      </Avatar>
    </div>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
