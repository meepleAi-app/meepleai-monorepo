import type { Meta, StoryObj } from '@storybook/react';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
  SheetFooter,
} from './sheet';
import { Button } from '../primitives/button';
import { Label } from '../primitives/label';
import { Input } from '../primitives/input';

/**
 * Sheet component for slide-out panels and drawers.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Dialog with slide-in animations from edges.
 *
 * ## Features
 * - **4 sides**: top, right, bottom, left
 * - **Overlay**: Backdrop with click-outside-to-close
 * - **Animations**: Smooth slide-in transitions
 * - **Composition**: Header, Title, Description, Footer sub-components
 *
 * ## Accessibility
 * - ✅ Focus trap when open
 * - ✅ Keyboard navigation (Escape to close)
 * - ✅ ARIA dialog attributes
 * - ✅ Scroll lock on body
 */
const meta = {
  title: 'UI/Sheet',
  component: Sheet,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Extends the Dialog component to display content that slides in from the edge of the screen.',
      },
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof Sheet>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default right-side sheet.
 * Standard slide-in drawer from the right.
 */
export const Default: Story = {
  render: () => (
    <Sheet>
      <SheetTrigger asChild>
        <Button variant="outline">Open Sheet</Button>
      </SheetTrigger>
      <SheetContent>
        <SheetHeader>
          <SheetTitle>Sheet Title</SheetTitle>
          <SheetDescription>This is a description of what the sheet contains.</SheetDescription>
        </SheetHeader>
      </SheetContent>
    </Sheet>
  ),
};

/**
 * Left-side sheet.
 * Slides in from the left edge.
 */
export const Left: Story = {
  render: () => (
    <Sheet>
      <SheetTrigger asChild>
        <Button variant="outline">Open Left</Button>
      </SheetTrigger>
      <SheetContent side="left">
        <SheetHeader>
          <SheetTitle>Left Sheet</SheetTitle>
          <SheetDescription>
            This sheet slides in from the left side of the screen.
          </SheetDescription>
        </SheetHeader>
      </SheetContent>
    </Sheet>
  ),
};

/**
 * Top sheet.
 * Slides down from the top edge.
 */
export const Top: Story = {
  render: () => (
    <Sheet>
      <SheetTrigger asChild>
        <Button variant="outline">Open Top</Button>
      </SheetTrigger>
      <SheetContent side="top">
        <SheetHeader>
          <SheetTitle>Top Sheet</SheetTitle>
          <SheetDescription>This sheet slides down from the top of the screen.</SheetDescription>
        </SheetHeader>
      </SheetContent>
    </Sheet>
  ),
};

/**
 * Bottom sheet.
 * Slides up from the bottom edge.
 */
export const Bottom: Story = {
  render: () => (
    <Sheet>
      <SheetTrigger asChild>
        <Button variant="outline">Open Bottom</Button>
      </SheetTrigger>
      <SheetContent side="bottom">
        <SheetHeader>
          <SheetTitle>Bottom Sheet</SheetTitle>
          <SheetDescription>This sheet slides up from the bottom of the screen.</SheetDescription>
        </SheetHeader>
      </SheetContent>
    </Sheet>
  ),
};

/**
 * Sheet with form.
 * Edit form in a slide-out panel.
 */
export const WithForm: Story = {
  render: () => (
    <Sheet>
      <SheetTrigger asChild>
        <Button>Edit Profile</Button>
      </SheetTrigger>
      <SheetContent>
        <SheetHeader>
          <SheetTitle>Edit profile</SheetTitle>
          <SheetDescription>
            Make changes to your profile here. Click save when you're done.
          </SheetDescription>
        </SheetHeader>
        <div className="grid gap-4 py-4">
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="name" className="text-right">
              Name
            </Label>
            <Input id="name" defaultValue="John Doe" className="col-span-3" />
          </div>
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="username" className="text-right">
              Username
            </Label>
            <Input id="username" defaultValue="@johndoe" className="col-span-3" />
          </div>
        </div>
        <SheetFooter>
          <Button type="submit">Save changes</Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  ),
};

/**
 * Navigation sheet.
 * Mobile-style navigation drawer.
 */
export const Navigation: Story = {
  render: () => (
    <Sheet>
      <SheetTrigger asChild>
        <Button variant="outline">Menu</Button>
      </SheetTrigger>
      <SheetContent side="left">
        <SheetHeader>
          <SheetTitle>Navigation</SheetTitle>
          <SheetDescription>Browse different sections of the application.</SheetDescription>
        </SheetHeader>
        <div className="mt-6 space-y-2">
          <Button variant="ghost" className="w-full justify-start">
            Home
          </Button>
          <Button variant="ghost" className="w-full justify-start">
            Profile
          </Button>
          <Button variant="ghost" className="w-full justify-start">
            Settings
          </Button>
          <Button variant="ghost" className="w-full justify-start">
            About
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  ),
};

/**
 * Sheet with long content.
 * Scrollable content in sheet.
 */
export const LongContent: Story = {
  render: () => (
    <Sheet>
      <SheetTrigger asChild>
        <Button variant="outline">View Details</Button>
      </SheetTrigger>
      <SheetContent>
        <SheetHeader>
          <SheetTitle>Long Content Sheet</SheetTitle>
          <SheetDescription>This sheet contains scrollable content.</SheetDescription>
        </SheetHeader>
        <div className="mt-6 space-y-4 text-sm">
          {Array.from({ length: 20 }).map((_, i) => (
            <p key={i}>
              This is paragraph {i + 1}. Lorem ipsum dolor sit amet, consectetur adipiscing elit.
              Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
            </p>
          ))}
        </div>
      </SheetContent>
    </Sheet>
  ),
};

/**
 * Dark theme variant.
 * Shows sheet appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <Sheet>
      <SheetTrigger asChild>
        <Button variant="outline">Open Sheet</Button>
      </SheetTrigger>
      <SheetContent>
        <SheetHeader>
          <SheetTitle>Dark Theme Sheet</SheetTitle>
          <SheetDescription>This sheet adapts to dark theme automatically.</SheetDescription>
        </SheetHeader>
        <div className="py-4">
          <p className="text-sm">Content goes here...</p>
        </div>
        <SheetFooter>
          <Button type="button">Save</Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
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
