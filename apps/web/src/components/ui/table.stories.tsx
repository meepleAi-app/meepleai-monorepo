import type { Meta, StoryObj } from '@storybook/react';
import {
  Table,
  TableHeader,
  TableBody,
  TableFooter,
  TableHead,
  TableRow,
  TableCell,
  TableCaption,
} from './table';
import { Badge } from './badge';
import { Checkbox } from './checkbox';

/**
 * Table component for displaying tabular data.
 * Composable with Header, Body, Footer, Row, Cell, and Caption.
 */
const meta = {
  title: 'UI/Table',
  component: Table,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof Table>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default table
 */
export const Default: Story = {
  render: () => (
    <Table className="w-[600px]">
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Email</TableHead>
          <TableHead>Role</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell>John Doe</TableCell>
          <TableCell>john@example.com</TableCell>
          <TableCell>Admin</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>Jane Smith</TableCell>
          <TableCell>jane@example.com</TableCell>
          <TableCell>User</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>Bob Johnson</TableCell>
          <TableCell>bob@example.com</TableCell>
          <TableCell>Editor</TableCell>
        </TableRow>
      </TableBody>
    </Table>
  ),
};

/**
 * Table with caption
 */
export const WithCaption: Story = {
  render: () => (
    <Table className="w-[600px]">
      <TableCaption>A list of your recent invoices.</TableCaption>
      <TableHeader>
        <TableRow>
          <TableHead>Invoice</TableHead>
          <TableHead>Status</TableHead>
          <TableHead>Method</TableHead>
          <TableHead className="text-right">Amount</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell>INV001</TableCell>
          <TableCell>Paid</TableCell>
          <TableCell>Credit Card</TableCell>
          <TableCell className="text-right">$250.00</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>INV002</TableCell>
          <TableCell>Pending</TableCell>
          <TableCell>PayPal</TableCell>
          <TableCell className="text-right">$150.00</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>INV003</TableCell>
          <TableCell>Unpaid</TableCell>
          <TableCell>Bank Transfer</TableCell>
          <TableCell className="text-right">$350.00</TableCell>
        </TableRow>
      </TableBody>
    </Table>
  ),
};

/**
 * Table with footer
 */
export const WithFooter: Story = {
  render: () => (
    <Table className="w-[600px]">
      <TableHeader>
        <TableRow>
          <TableHead>Item</TableHead>
          <TableHead>Quantity</TableHead>
          <TableHead className="text-right">Price</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell>Product A</TableCell>
          <TableCell>2</TableCell>
          <TableCell className="text-right">$20.00</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>Product B</TableCell>
          <TableCell>1</TableCell>
          <TableCell className="text-right">$15.00</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>Product C</TableCell>
          <TableCell>3</TableCell>
          <TableCell className="text-right">$45.00</TableCell>
        </TableRow>
      </TableBody>
      <TableFooter>
        <TableRow>
          <TableCell colSpan={2}>Total</TableCell>
          <TableCell className="text-right">$80.00</TableCell>
        </TableRow>
      </TableFooter>
    </Table>
  ),
};

/**
 * Table with badges
 */
export const WithBadges: Story = {
  render: () => (
    <Table className="w-[600px]">
      <TableHeader>
        <TableRow>
          <TableHead>Task</TableHead>
          <TableHead>Status</TableHead>
          <TableHead>Priority</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell>Fix bug in authentication</TableCell>
          <TableCell>
            <Badge>In Progress</Badge>
          </TableCell>
          <TableCell>
            <Badge variant="destructive">High</Badge>
          </TableCell>
        </TableRow>
        <TableRow>
          <TableCell>Update documentation</TableCell>
          <TableCell>
            <Badge variant="secondary">Pending</Badge>
          </TableCell>
          <TableCell>
            <Badge variant="outline">Low</Badge>
          </TableCell>
        </TableRow>
        <TableRow>
          <TableCell>Deploy to production</TableCell>
          <TableCell>
            <Badge variant="outline">Completed</Badge>
          </TableCell>
          <TableCell>
            <Badge variant="destructive">High</Badge>
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>
  ),
};

/**
 * Table with checkboxes
 */
export const WithCheckboxes: Story = {
  render: () => (
    <Table className="w-[600px]">
      <TableHeader>
        <TableRow>
          <TableHead className="w-12">
            <Checkbox aria-label="Select all" />
          </TableHead>
          <TableHead>Name</TableHead>
          <TableHead>Email</TableHead>
          <TableHead>Role</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow>
          <TableCell>
            <Checkbox aria-label="Select row" />
          </TableCell>
          <TableCell>John Doe</TableCell>
          <TableCell>john@example.com</TableCell>
          <TableCell>Admin</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>
            <Checkbox aria-label="Select row" />
          </TableCell>
          <TableCell>Jane Smith</TableCell>
          <TableCell>jane@example.com</TableCell>
          <TableCell>User</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>
            <Checkbox aria-label="Select row" />
          </TableCell>
          <TableCell>Bob Johnson</TableCell>
          <TableCell>bob@example.com</TableCell>
          <TableCell>Editor</TableCell>
        </TableRow>
      </TableBody>
    </Table>
  ),
};

/**
 * Responsive table
 */
export const Responsive: Story = {
  render: () => (
    <div className="w-[800px]">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>ID</TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Email</TableHead>
            <TableHead>Department</TableHead>
            <TableHead>Role</TableHead>
            <TableHead className="text-right">Salary</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow>
            <TableCell>001</TableCell>
            <TableCell>John Doe</TableCell>
            <TableCell>john@example.com</TableCell>
            <TableCell>Engineering</TableCell>
            <TableCell>Senior Developer</TableCell>
            <TableCell className="text-right">$120,000</TableCell>
          </TableRow>
          <TableRow>
            <TableCell>002</TableCell>
            <TableCell>Jane Smith</TableCell>
            <TableCell>jane@example.com</TableCell>
            <TableCell>Marketing</TableCell>
            <TableCell>Marketing Manager</TableCell>
            <TableCell className="text-right">$95,000</TableCell>
          </TableRow>
          <TableRow>
            <TableCell>003</TableCell>
            <TableCell>Bob Johnson</TableCell>
            <TableCell>bob@example.com</TableCell>
            <TableCell>Sales</TableCell>
            <TableCell>Sales Representative</TableCell>
            <TableCell className="text-right">$75,000</TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>
  ),
};

/**
 * Striped table
 */
export const Striped: Story = {
  render: () => (
    <Table className="w-[600px]">
      <TableHeader>
        <TableRow>
          <TableHead>Product</TableHead>
          <TableHead>Category</TableHead>
          <TableHead className="text-right">Price</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <TableRow className="bg-muted/50">
          <TableCell>Laptop</TableCell>
          <TableCell>Electronics</TableCell>
          <TableCell className="text-right">$999</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>Mouse</TableCell>
          <TableCell>Accessories</TableCell>
          <TableCell className="text-right">$29</TableCell>
        </TableRow>
        <TableRow className="bg-muted/50">
          <TableCell>Keyboard</TableCell>
          <TableCell>Accessories</TableCell>
          <TableCell className="text-right">$79</TableCell>
        </TableRow>
        <TableRow>
          <TableCell>Monitor</TableCell>
          <TableCell>Electronics</TableCell>
          <TableCell className="text-right">$299</TableCell>
        </TableRow>
      </TableBody>
    </Table>
  ),
};
