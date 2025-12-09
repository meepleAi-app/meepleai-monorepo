import type { Meta, StoryObj } from '@storybook/react';
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
  TableFooter,
} from './table';

/**
 * Table component for displaying tabular data with semantic HTML.
 *
 * ## shadcn/ui Component
 * Semantic table with accessible structure and styling.
 *
 * ## Features
 * - **Composition**: Header, Body, Footer, Row, Cell sub-components
 * - **Responsive**: Overflow scroll for wide tables
 * - **Hover states**: Row highlighting on hover
 * - **Caption support**: Accessible table descriptions
 *
 * ## Accessibility
 * - ✅ Semantic HTML table elements
 * - ✅ Table captions for screen readers
 * - ✅ Proper header associations
 * - ✅ Keyboard navigable
 */
const meta = {
  title: 'UI/Table',
  component: Table,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'A responsive table component for displaying structured data.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="w-full max-w-4xl">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Table>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default table.
 * Basic table with headers and data rows.
 */
export const Default: Story = {
  render: () => (
    <Table>
      <TableCaption>A list of your recent invoices.</TableCaption>
      <TableHeader>
        <TableRow>
          <TableHead className="w-[100px]">Invoice</TableHead>
          <TableHead>Status</TableHead>
          <TableHead>Method</TableHead>
          <TableHead className="text-right">Amount</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell className="font-medium">INV001</TableCell>
          <TableCell>Paid</TableCell>
          <TableCell>Credit Card</TableCell>
          <TableCell className="text-right">$250.00</TableCell>
        </TableRow>
        <TableRow>
          <TableCell className="font-medium">INV002</TableCell>
          <TableCell>Pending</TableCell>
          <TableCell>PayPal</TableCell>
          <TableCell className="text-right">$150.00</TableCell>
        </TableRow>
        <TableRow>
          <TableCell className="font-medium">INV003</TableCell>
          <TableCell>Unpaid</TableCell>
          <TableCell>Bank Transfer</TableCell>
          <TableCell className="text-right">$350.00</TableCell>
        </TableRow>
      </TableBody>
    </Table>
  ),
};

/**
 * Table with footer.
 * Shows totals or summary rows.
 */
export const WithFooter: Story = {
  render: () => (
    <Table>
      <TableCaption>Monthly expenses breakdown.</TableCaption>
      <TableHeader>
        <TableRow>
          <TableHead>Category</TableHead>
          <TableHead>Description</TableHead>
          <TableHead className="text-right">Amount</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell className="font-medium">Office Supplies</TableCell>
          <TableCell>Paper, pens, folders</TableCell>
          <TableCell className="text-right">$45.99</TableCell>
        </TableRow>
        <TableRow>
          <TableCell className="font-medium">Software</TableCell>
          <TableCell>Monthly subscriptions</TableCell>
          <TableCell className="text-right">$120.00</TableCell>
        </TableRow>
        <TableRow>
          <TableCell className="font-medium">Utilities</TableCell>
          <TableCell>Internet and electricity</TableCell>
          <TableCell className="text-right">$85.50</TableCell>
        </TableRow>
      </TableBody>
      <TableFooter>
        <TableRow>
          <TableCell colSpan={2}>Total</TableCell>
          <TableCell className="text-right">$251.49</TableCell>
        </TableRow>
      </TableFooter>
    </Table>
  ),
};

/**
 * User data table.
 * Displays user information.
 */
export const UserTable: Story = {
  render: () => (
    <Table>
      <TableCaption>Team members list.</TableCaption>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Email</TableHead>
          <TableHead>Role</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell className="font-medium">John Doe</TableCell>
          <TableCell>john@example.com</TableCell>
          <TableCell>Developer</TableCell>
          <TableCell>Active</TableCell>
        </TableRow>
        <TableRow>
          <TableCell className="font-medium">Jane Smith</TableCell>
          <TableCell>jane@example.com</TableCell>
          <TableCell>Designer</TableCell>
          <TableCell>Active</TableCell>
        </TableRow>
        <TableRow>
          <TableCell className="font-medium">Bob Johnson</TableCell>
          <TableCell>bob@example.com</TableCell>
          <TableCell>Manager</TableCell>
          <TableCell>Away</TableCell>
        </TableRow>
      </TableBody>
    </Table>
  ),
};

/**
 * Wide table with scroll.
 * Tests horizontal overflow behavior.
 */
export const WideTable: Story = {
  render: () => (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Column 1</TableHead>
          <TableHead>Column 2</TableHead>
          <TableHead>Column 3</TableHead>
          <TableHead>Column 4</TableHead>
          <TableHead>Column 5</TableHead>
          <TableHead>Column 6</TableHead>
          <TableHead>Column 7</TableHead>
          <TableHead>Column 8</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell>Data 1</TableCell>
          <TableCell>Data 2</TableCell>
          <TableCell>Data 3</TableCell>
          <TableCell>Data 4</TableCell>
          <TableCell>Data 5</TableCell>
          <TableCell>Data 6</TableCell>
          <TableCell>Data 7</TableCell>
          <TableCell>Data 8</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>Data 1</TableCell>
          <TableCell>Data 2</TableCell>
          <TableCell>Data 3</TableCell>
          <TableCell>Data 4</TableCell>
          <TableCell>Data 5</TableCell>
          <TableCell>Data 6</TableCell>
          <TableCell>Data 7</TableCell>
          <TableCell>Data 8</TableCell>
        </TableRow>
      </TableBody>
    </Table>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Wide tables automatically scroll horizontally.',
      },
    },
  },
};

/**
 * Empty table.
 * Shows table structure with no data.
 */
export const EmptyTable: Story = {
  render: () => (
    <Table>
      <TableCaption>No data available.</TableCaption>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Email</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell colSpan={3} className="h-24 text-center">
            No results.
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>
  ),
};

/**
 * Dark theme variant.
 * Shows table appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <Table>
      <TableCaption>Recent transactions.</TableCaption>
      <TableHeader>
        <TableRow>
          <TableHead className="w-[100px]">ID</TableHead>
          <TableHead>Description</TableHead>
          <TableHead>Status</TableHead>
          <TableHead className="text-right">Amount</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell className="font-medium">TXN001</TableCell>
          <TableCell>Payment received</TableCell>
          <TableCell>Completed</TableCell>
          <TableCell className="text-right">$500.00</TableCell>
        </TableRow>
        <TableRow>
          <TableCell className="font-medium">TXN002</TableCell>
          <TableCell>Refund processed</TableCell>
          <TableCell>Pending</TableCell>
          <TableCell className="text-right">-$50.00</TableCell>
        </TableRow>
        <TableRow>
          <TableCell className="font-medium">TXN003</TableCell>
          <TableCell>Subscription payment</TableCell>
          <TableCell>Completed</TableCell>
          <TableCell className="text-right">$29.99</TableCell>
        </TableRow>
      </TableBody>
      <TableFooter>
        <TableRow>
          <TableCell colSpan={3}>Total</TableCell>
          <TableCell className="text-right">$479.99</TableCell>
        </TableRow>
      </TableFooter>
    </Table>
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
