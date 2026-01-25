import { ScrollArea } from './scroll-area';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * ScrollArea component for custom scrollable regions.
 *
 * ## shadcn/ui Component
 * Based on Radix UI ScrollArea with styled scrollbars.
 *
 * ## Features
 * - **Custom scrollbars**: Consistent cross-browser styling
 * - **Vertical/Horizontal**: Both orientations supported
 * - **Smooth scrolling**: Native browser smooth scroll
 *
 * ## Accessibility
 * - ✅ Native scroll behavior
 * - ✅ Keyboard navigation (Arrow keys, Page Up/Down)
 * - ✅ Proper overflow handling
 */
const meta = {
  title: 'UI/ScrollArea',
  component: ScrollArea,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A scroll area component with custom styled scrollbars. Provides consistent scrolling experience across browsers.',
      },
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof ScrollArea>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default vertical scroll.
 */
export const Default: Story = {
  render: () => (
    <ScrollArea className="h-72 w-80 rounded-md border p-4">
      <div className="space-y-4">
        {Array.from({ length: 20 }).map((_, i) => (
          <div key={i} className="text-sm">
            <h4 className="font-medium">Item {i + 1}</h4>
            <p className="text-muted-foreground">Description for item {i + 1}</p>
          </div>
        ))}
      </div>
    </ScrollArea>
  ),
};

/**
 * Tags list example.
 */
export const TagsList: Story = {
  render: () => (
    <ScrollArea className="h-72 w-80 rounded-md border">
      <div className="p-4">
        <h4 className="mb-4 text-sm font-medium leading-none">Tags</h4>
        {Array.from({ length: 50 }).map((_, i) => (
          <div
            key={i}
            className="text-sm py-2 border-b last:border-0"
          >
            Tag {i + 1}
          </div>
        ))}
      </div>
    </ScrollArea>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Scrollable list of tags or items.',
      },
    },
  },
};

/**
 * Code block example.
 */
export const CodeBlock: Story = {
  render: () => (
    <ScrollArea className="h-72 w-96 rounded-md border">
      <div className="p-4 font-mono text-sm">
        <pre>{`function fibonacci(n) {
  if (n <= 1) return n;
  return fibonacci(n - 1) + fibonacci(n - 2);
}

// Calculate first 10 numbers
for (let i = 0; i < 10; i++) {
  console.log(fibonacci(i));
}

// Memoized version
const memo = {};
function fibMemo(n) {
  if (n in memo) return memo[n];
  if (n <= 1) return n;
  memo[n] = fibMemo(n - 1) + fibMemo(n - 2);
  return memo[n];
}

// Dynamic programming approach
function fibDP(n) {
  const dp = [0, 1];
  for (let i = 2; i <= n; i++) {
    dp[i] = dp[i - 1] + dp[i - 2];
  }
  return dp[n];
}`}</pre>
      </div>
    </ScrollArea>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Scrollable code block with syntax.',
      },
    },
  },
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  render: () => (
    <ScrollArea className="h-72 w-80 rounded-md border p-4">
      <div className="space-y-4">
        {Array.from({ length: 15 }).map((_, i) => (
          <div key={i} className="text-sm">
            <h4 className="font-medium">Section {i + 1}</h4>
            <p className="text-muted-foreground">Content for section {i + 1}</p>
          </div>
        ))}
      </div>
    </ScrollArea>
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
