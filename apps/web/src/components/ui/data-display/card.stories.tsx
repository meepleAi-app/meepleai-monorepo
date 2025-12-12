import type { Meta, StoryObj } from '@storybook/react';
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from './card';
import { Button } from '../primitives/button';
import { Input } from '../primitives/input';
import { Label } from '../primitives/label';
import { Bell, Check } from 'lucide-react';

/**
 * Card component for content grouping and visual hierarchy.
 *
 * ## shadcn/ui Component
 * Flexible container with semantic subcomponents for structured content.
 *
 * ## Subcomponents
 * - **CardHeader**: Top section with title and description
 * - **CardTitle**: Main heading
 * - **CardDescription**: Subtitle or description
 * - **CardContent**: Main content area
 * - **CardFooter**: Bottom section for actions
 *
 * ## Features
 * - **Composable**: Mix and match subcomponents
 * - **Flexible**: Supports any content structure
 * - **Semantic**: Clear content hierarchy
 *
 * ## Accessibility
 * - ✅ Semantic HTML structure
 * - ✅ Keyboard navigation for interactive content
 * - ✅ Clear visual hierarchy with proper spacing
 */
const meta = {
  title: 'UI/Card',
  component: Card,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A card container component with semantic subcomponents for structured content presentation.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="w-96">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Card>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default card with all sections.
 * Complete example with header, content, and footer.
 */
export const Default: Story = {
  render: () => (
    <Card>
      <CardHeader>
        <CardTitle>Card Title</CardTitle>
        <CardDescription>Card description goes here</CardDescription>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-muted-foreground">
          This is the main content area of the card. You can put any content here.
        </p>
      </CardContent>
      <CardFooter>
        <Button>Action</Button>
      </CardFooter>
    </Card>
  ),
};

/**
 * Simple card with only content.
 * Minimal card without header or footer.
 */
export const Simple: Story = {
  render: () => (
    <Card>
      <CardContent className="pt-6">
        <p className="text-sm text-muted-foreground">Simple card with just content.</p>
      </CardContent>
    </Card>
  ),
};

/**
 * Card with header only.
 * Title and description without content.
 */
export const HeaderOnly: Story = {
  render: () => (
    <Card>
      <CardHeader>
        <CardTitle>Notifications</CardTitle>
        <CardDescription>You have 3 unread messages</CardDescription>
      </CardHeader>
    </Card>
  ),
};

/**
 * Card with form content.
 * Interactive form inside card.
 */
export const WithForm: Story = {
  render: () => (
    <Card>
      <CardHeader>
        <CardTitle>Create Account</CardTitle>
        <CardDescription>Enter your details to create a new account</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" placeholder="John Doe" />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="email">Email</Label>
            <Input id="email" type="email" placeholder="john@example.com" />
          </div>
        </div>
      </CardContent>
      <CardFooter className="flex justify-between">
        <Button variant="outline">Cancel</Button>
        <Button>Create</Button>
      </CardFooter>
    </Card>
  ),
};

/**
 * Card with icon in header.
 * Visual emphasis with icon.
 */
export const WithIcon: Story = {
  render: () => (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Bell className="h-5 w-5" />
          <CardTitle>Notifications</CardTitle>
        </div>
        <CardDescription>Stay updated with the latest activity</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          <div className="flex items-center gap-2 text-sm">
            <Check className="h-4 w-4 text-green-500" />
            <span>All systems operational</span>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <Check className="h-4 w-4 text-green-500" />
            <span>Backup completed successfully</span>
          </div>
        </div>
      </CardContent>
    </Card>
  ),
};

/**
 * Interactive card with hover effect.
 * Card that responds to user interaction.
 */
export const Interactive: Story = {
  render: () => (
    <Card className="cursor-pointer transition-all hover:shadow-lg hover:scale-105">
      <CardHeader>
        <CardTitle>Clickable Card</CardTitle>
        <CardDescription>This card responds to hover and clicks</CardDescription>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-muted-foreground">
          Hover over this card to see the interaction effect.
        </p>
      </CardContent>
    </Card>
  ),
};

/**
 * Card with list content.
 * Structured list inside card.
 */
export const WithList: Story = {
  render: () => (
    <Card>
      <CardHeader>
        <CardTitle>Recent Activity</CardTitle>
        <CardDescription>Your latest actions</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {[
            { action: 'Updated profile', time: '2 hours ago' },
            { action: 'Uploaded PDF', time: '5 hours ago' },
            { action: 'Created game session', time: '1 day ago' },
          ].map((item, i) => (
            <div key={i} className="flex justify-between items-center text-sm">
              <span>{item.action}</span>
              <span className="text-muted-foreground">{item.time}</span>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  ),
};

/**
 * Card grid layout.
 * Multiple cards in responsive grid.
 */
export const GridLayout: Story = {
  render: () => (
    <div className="grid grid-cols-2 gap-4 w-[800px]">
      <Card>
        <CardHeader>
          <CardTitle>Total Users</CardTitle>
          <CardDescription>Active accounts</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-3xl font-bold">1,234</p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Games Played</CardTitle>
          <CardDescription>This month</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-3xl font-bold">567</p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>PDF Uploads</CardTitle>
          <CardDescription>Total documents</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-3xl font-bold">89</p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Chat Sessions</CardTitle>
          <CardDescription>AI interactions</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-3xl font-bold">342</p>
        </CardContent>
      </Card>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Example of cards arranged in a responsive grid layout.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows card appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <Card>
      <CardHeader>
        <CardTitle>Dark Theme Card</CardTitle>
        <CardDescription>Card styled for dark mode</CardDescription>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-muted-foreground">
          This card adapts to the dark theme automatically.
        </p>
      </CardContent>
      <CardFooter>
        <Button>Action</Button>
      </CardFooter>
    </Card>
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
