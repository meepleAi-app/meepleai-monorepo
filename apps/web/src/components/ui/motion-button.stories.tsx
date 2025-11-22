import type { Meta, StoryObj } from '@storybook/react';
import { MotionButton } from './motion-button';
import { Mail, Loader2, ArrowRight } from 'lucide-react';

/**
 * MotionButton component with framer-motion animations.
 * Wraps the Button component with consistent hover (scale: 1.05) and tap (scale: 0.95) animations.
 *
 * Issue #1437: UX-002 - Extract MotionButton Component
 *
 * Features:
 * - All Button variants and sizes supported
 * - Smooth scale animations on hover and tap
 * - asChild composition for polymorphic usage
 * - Fully accessible with proper ARIA attributes
 */
const meta = {
  title: 'UI/MotionButton',
  component: MotionButton,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'An animated button component that wraps the shadcn/ui Button with framer-motion scale animations. Use this for interactive CTAs and hero sections to add visual feedback.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'destructive', 'outline', 'secondary', 'ghost', 'link'],
      description: 'Visual style variant of the button',
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg', 'icon'],
      description: 'Size variant of the button',
    },
    disabled: {
      control: 'boolean',
      description: 'Whether the button is disabled',
    },
    whileHover: {
      control: 'object',
      description: 'Custom hover animation (default: { scale: 1.05 })',
    },
    whileTap: {
      control: 'object',
      description: 'Custom tap animation (default: { scale: 0.95 })',
    },
  },
} satisfies Meta<typeof MotionButton>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default MotionButton with scale animations.
 * Hover to see scale increase, click to see scale decrease.
 */
export const Default: Story = {
  args: {
    children: 'Hover & Click Me',
  },
};

/**
 * Destructive variant for dangerous actions
 */
export const Destructive: Story = {
  args: {
    variant: 'destructive',
    children: 'Delete Account',
  },
};

/**
 * Outline variant commonly used in hero sections
 */
export const Outline: Story = {
  args: {
    variant: 'outline',
    children: 'Learn More',
  },
};

/**
 * Secondary variant
 */
export const Secondary: Story = {
  args: {
    variant: 'secondary',
    children: 'Secondary Action',
  },
};

/**
 * Ghost variant for subtle actions
 */
export const Ghost: Story = {
  args: {
    variant: 'ghost',
    children: 'Ghost Button',
  },
};

/**
 * Link variant with animation
 */
export const Link: Story = {
  args: {
    variant: 'link',
    children: 'Link Button',
  },
};

/**
 * Small button size
 */
export const Small: Story = {
  args: {
    size: 'sm',
    children: 'Small Button',
  },
};

/**
 * Large button size - perfect for hero CTAs
 */
export const Large: Story = {
  args: {
    size: 'lg',
    children: 'Get Started',
  },
};

/**
 * Icon button with animation
 */
export const Icon: Story = {
  args: {
    size: 'icon',
    children: <Mail className="h-4 w-4" />,
  },
};

/**
 * Disabled state (animations won't trigger)
 */
export const Disabled: Story = {
  args: {
    disabled: true,
    children: 'Disabled',
  },
};

/**
 * Button with icon and text
 */
export const WithIcon: Story = {
  args: {
    children: (
      <>
        <Mail className="mr-2 h-4 w-4" />
        Login with Email
      </>
    ),
  },
};

/**
 * Loading state with animation
 */
export const Loading: Story = {
  args: {
    disabled: true,
    children: (
      <>
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Please wait
      </>
    ),
  },
};

/**
 * asChild composition with anchor tag.
 * This allows using MotionButton as a styled link.
 */
export const AsLink: Story = {
  args: {
    variant: 'outline',
    asChild: true,
    children: (
      <a href="#features">
        Learn More
        <ArrowRight className="ml-2 h-4 w-4" />
      </a>
    ),
  },
};

/**
 * Custom animations - slower, more pronounced effect
 */
export const CustomAnimation: Story = {
  args: {
    children: 'Custom Animation',
    whileHover: { scale: 1.1 },
    whileTap: { scale: 0.9 },
  },
};

/**
 * Hero CTA style - typical usage in landing pages
 */
export const HeroCTA: Story = {
  args: {
    className:
      'text-lg shadow-[0_15px_45px_rgba(14,116,244,0.35)] focus-visible:ring-white focus-visible:ring-offset-2',
    children: 'Start Your Journey',
  },
};

/**
 * All variants demonstrated together
 */
export const AllVariants: Story = {
  render: () => (
    <div className="flex flex-col gap-4">
      <div className="flex gap-2">
        <MotionButton>Default</MotionButton>
        <MotionButton variant="destructive">Destructive</MotionButton>
        <MotionButton variant="outline">Outline</MotionButton>
      </div>
      <div className="flex gap-2">
        <MotionButton variant="secondary">Secondary</MotionButton>
        <MotionButton variant="ghost">Ghost</MotionButton>
        <MotionButton variant="link">Link</MotionButton>
      </div>
    </div>
  ),
};

/**
 * All sizes demonstrated together
 */
export const AllSizes: Story = {
  render: () => (
    <div className="flex items-center gap-2">
      <MotionButton size="sm">Small</MotionButton>
      <MotionButton>Default</MotionButton>
      <MotionButton size="lg">Large</MotionButton>
      <MotionButton size="icon">
        <Mail className="h-4 w-4" />
      </MotionButton>
    </div>
  ),
};

/**
 * Real-world usage example from HomePage
 */
export const HomePageExample: Story = {
  render: () => (
    <div className="flex flex-wrap gap-4 p-8 bg-slate-950 rounded-lg">
      <MotionButton
        className="text-lg shadow-[0_15px_45px_rgba(14,116,244,0.35)]"
      >
        Get Started
      </MotionButton>
      <MotionButton
        variant="outline"
        className="text-lg border-white/70 text-white hover:bg-white hover:text-slate-900"
      >
        Try Demo
      </MotionButton>
      <MotionButton
        variant="outline"
        className="text-lg border-white/70 text-white hover:bg-white hover:text-slate-900"
        asChild
      >
        <a href="#features">See How It Works</a>
      </MotionButton>
    </div>
  ),
};
