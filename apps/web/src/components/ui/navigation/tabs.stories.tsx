import { Tabs, TabsContent, TabsList, TabsTrigger } from './tabs';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Tabs component for organizing content into separate views.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Tabs with keyboard navigation and ARIA support.
 *
 * ## Features
 * - **Keyboard navigation**: Arrow keys to switch tabs
 * - **Active state**: Visual indicator for selected tab
 * - **Accessible**: ARIA tablist, tab, and tabpanel roles
 * - **Lazy loading**: Content rendered only when tab is active
 *
 * ## Accessibility
 * - ✅ ARIA roles (tablist, tab, tabpanel)
 * - ✅ Keyboard navigation (Arrow keys, Tab, Home, End)
 * - ✅ Focus management
 * - ✅ Screen reader friendly
 */
const meta = {
  title: 'UI/Tabs',
  component: Tabs,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A set of layered sections of content—known as tab panels—that are displayed one at a time.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Tabs>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default tabs.
 * Basic tabs with two panels.
 */
export const Default: Story = {
  render: () => (
    <Tabs defaultValue="account" className="w-full">
      <TabsList>
        <TabsTrigger value="account">Account</TabsTrigger>
        <TabsTrigger value="password">Password</TabsTrigger>
      </TabsList>
      <TabsContent value="account">
        <p className="text-sm text-muted-foreground">
          Make changes to your account here. Click save when you're done.
        </p>
      </TabsContent>
      <TabsContent value="password">
        <p className="text-sm text-muted-foreground">
          Change your password here. After saving, you'll be logged out.
        </p>
      </TabsContent>
    </Tabs>
  ),
};

/**
 * Multiple tabs.
 * Shows three or more tab options.
 */
export const MultipleTabs: Story = {
  render: () => (
    <Tabs defaultValue="overview" className="w-full">
      <TabsList>
        <TabsTrigger value="overview">Overview</TabsTrigger>
        <TabsTrigger value="analytics">Analytics</TabsTrigger>
        <TabsTrigger value="reports">Reports</TabsTrigger>
        <TabsTrigger value="notifications">Notifications</TabsTrigger>
      </TabsList>
      <TabsContent value="overview">
        <p className="text-sm text-muted-foreground">
          Dashboard overview with key metrics and summaries.
        </p>
      </TabsContent>
      <TabsContent value="analytics">
        <p className="text-sm text-muted-foreground">Detailed analytics and performance charts.</p>
      </TabsContent>
      <TabsContent value="reports">
        <p className="text-sm text-muted-foreground">Generate and download custom reports.</p>
      </TabsContent>
      <TabsContent value="notifications">
        <p className="text-sm text-muted-foreground">Manage your notification preferences.</p>
      </TabsContent>
    </Tabs>
  ),
};

/**
 * Tabs with rich content.
 * Complex content within tab panels.
 */
export const RichContent: Story = {
  render: () => (
    <Tabs defaultValue="profile" className="w-full">
      <TabsList>
        <TabsTrigger value="profile">Profile</TabsTrigger>
        <TabsTrigger value="settings">Settings</TabsTrigger>
      </TabsList>
      <TabsContent value="profile" className="space-y-4">
        <div className="space-y-2">
          <h3 className="text-lg font-semibold">Profile Information</h3>
          <p className="text-sm text-muted-foreground">
            Update your profile details and public information.
          </p>
        </div>
        <div className="space-y-2">
          <label className="text-sm font-medium">Name</label>
          <input
            type="text"
            className="w-full rounded-md border px-3 py-2 text-sm"
            defaultValue="John Doe"
          />
        </div>
        <div className="space-y-2">
          <label className="text-sm font-medium">Email</label>
          <input
            type="email"
            className="w-full rounded-md border px-3 py-2 text-sm"
            defaultValue="john@example.com"
          />
        </div>
      </TabsContent>
      <TabsContent value="settings" className="space-y-4">
        <div className="space-y-2">
          <h3 className="text-lg font-semibold">Account Settings</h3>
          <p className="text-sm text-muted-foreground">
            Manage your account preferences and security settings.
          </p>
        </div>
        <div className="flex items-center justify-between">
          <label className="text-sm font-medium">Email notifications</label>
          <input type="checkbox" defaultChecked />
        </div>
        <div className="flex items-center justify-between">
          <label className="text-sm font-medium">Two-factor authentication</label>
          <input type="checkbox" />
        </div>
      </TabsContent>
    </Tabs>
  ),
};

/**
 * Disabled tab.
 * Shows tab in disabled state.
 */
export const DisabledTab: Story = {
  render: () => (
    <Tabs defaultValue="available" className="w-full">
      <TabsList>
        <TabsTrigger value="available">Available</TabsTrigger>
        <TabsTrigger value="disabled" disabled>
          Disabled
        </TabsTrigger>
        <TabsTrigger value="another">Another</TabsTrigger>
      </TabsList>
      <TabsContent value="available">
        <p className="text-sm text-muted-foreground">This tab is available.</p>
      </TabsContent>
      <TabsContent value="disabled">
        <p className="text-sm text-muted-foreground">You shouldn't see this.</p>
      </TabsContent>
      <TabsContent value="another">
        <p className="text-sm text-muted-foreground">This tab is also available.</p>
      </TabsContent>
    </Tabs>
  ),
};

/**
 * Full-width tabs.
 * Tabs list spanning full container width.
 */
export const FullWidth: Story = {
  render: () => (
    <Tabs defaultValue="tab1" className="w-full">
      <TabsList className="w-full">
        <TabsTrigger value="tab1" className="flex-1">
          Tab 1
        </TabsTrigger>
        <TabsTrigger value="tab2" className="flex-1">
          Tab 2
        </TabsTrigger>
        <TabsTrigger value="tab3" className="flex-1">
          Tab 3
        </TabsTrigger>
      </TabsList>
      <TabsContent value="tab1">
        <p className="text-sm text-muted-foreground">Content for Tab 1.</p>
      </TabsContent>
      <TabsContent value="tab2">
        <p className="text-sm text-muted-foreground">Content for Tab 2.</p>
      </TabsContent>
      <TabsContent value="tab3">
        <p className="text-sm text-muted-foreground">Content for Tab 3.</p>
      </TabsContent>
    </Tabs>
  ),
};

/**
 * Navigation tabs.
 * Tabs used for page navigation.
 */
export const NavigationTabs: Story = {
  render: () => (
    <Tabs defaultValue="home" className="w-full">
      <TabsList>
        <TabsTrigger value="home">Home</TabsTrigger>
        <TabsTrigger value="products">Products</TabsTrigger>
        <TabsTrigger value="about">About</TabsTrigger>
        <TabsTrigger value="contact">Contact</TabsTrigger>
      </TabsList>
      <TabsContent value="home" className="pt-4">
        <h2 className="text-xl font-semibold mb-2">Welcome Home</h2>
        <p className="text-sm text-muted-foreground">This is the home page content.</p>
      </TabsContent>
      <TabsContent value="products" className="pt-4">
        <h2 className="text-xl font-semibold mb-2">Our Products</h2>
        <p className="text-sm text-muted-foreground">Browse our product catalog here.</p>
      </TabsContent>
      <TabsContent value="about" className="pt-4">
        <h2 className="text-xl font-semibold mb-2">About Us</h2>
        <p className="text-sm text-muted-foreground">Learn more about our company and mission.</p>
      </TabsContent>
      <TabsContent value="contact" className="pt-4">
        <h2 className="text-xl font-semibold mb-2">Contact Us</h2>
        <p className="text-sm text-muted-foreground">Get in touch with our team.</p>
      </TabsContent>
    </Tabs>
  ),
};

/**
 * Dark theme variant.
 * Shows tabs appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <Tabs defaultValue="overview" className="w-full">
      <TabsList>
        <TabsTrigger value="overview">Overview</TabsTrigger>
        <TabsTrigger value="details">Details</TabsTrigger>
        <TabsTrigger value="settings">Settings</TabsTrigger>
      </TabsList>
      <TabsContent value="overview">
        <p className="text-sm text-muted-foreground">Dashboard overview and key metrics.</p>
      </TabsContent>
      <TabsContent value="details">
        <p className="text-sm text-muted-foreground">Detailed information and analytics.</p>
      </TabsContent>
      <TabsContent value="settings">
        <p className="text-sm text-muted-foreground">Configure your preferences here.</p>
      </TabsContent>
    </Tabs>
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
