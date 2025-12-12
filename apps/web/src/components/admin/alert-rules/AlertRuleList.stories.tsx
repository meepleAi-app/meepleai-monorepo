import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { AlertRuleList } from './AlertRuleList';
import type { AlertRule } from '@/lib/api/schemas/alert-rules.schemas';

/**
 * AlertRuleList Component - Issue #921
 *
 * ## Features
 * - **Table View**: Displays alert rules with key properties
 * - **Severity Badges**: Visual severity indicators (Info, Warning, Error, Critical)
 * - **Enable/Disable Toggle**: Quick switch for rule activation
 * - **Actions**: Edit and Delete buttons for each rule
 *
 * ## Usage
 * Used in the Alert Rules management page to display configured rules.
 *
 * ## Visual Regression Testing
 * Chromatic captures visual snapshots for:
 * - Empty state
 * - Populated table with mixed severities
 * - Loading state (handled by parent)
 */
const meta = {
  title: 'Admin/Alert Rules/AlertRuleList',
  component: AlertRuleList,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [768, 1024, 1920],
      delay: 300,
      diffThreshold: 0.2,
    },
  },
  tags: ['autodocs'],
  argTypes: {
    rules: { control: 'object' },
    onEdit: { action: 'edited' },
    onDelete: { action: 'deleted' },
    onToggle: { action: 'toggled' },
  },
  args: {
    onEdit: fn(),
    onDelete: fn(),
    onToggle: fn(),
  },
} satisfies Meta<typeof AlertRuleList>;

export default meta;
type Story = StoryObj<typeof meta>;

const mockRules: AlertRule[] = [
  {
    id: '1',
    name: 'High Error Rate',
    alertType: 'HighErrorRate',
    severity: 'Critical',
    thresholdValue: 5,
    thresholdUnit: '%',
    durationMinutes: 5,
    isEnabled: true,
    description: 'Alert when error rate exceeds 5% for 5 minutes',
    createdAt: '2025-12-12T10:00:00Z',
    updatedAt: '2025-12-12T10:00:00Z',
  },
  {
    id: '2',
    name: 'High Latency',
    alertType: 'HighLatency',
    severity: 'Warning',
    thresholdValue: 1000,
    thresholdUnit: 'ms',
    durationMinutes: 10,
    isEnabled: true,
    description: 'Alert when P95 latency exceeds 1000ms for 10 minutes',
    createdAt: '2025-12-12T10:00:00Z',
    updatedAt: '2025-12-12T10:00:00Z',
  },
  {
    id: '3',
    name: 'Service Down',
    alertType: 'ServiceDown',
    severity: 'Critical',
    thresholdValue: 1,
    thresholdUnit: 'count',
    durationMinutes: 1,
    isEnabled: false,
    description: 'Alert immediately when health check fails',
    createdAt: '2025-12-12T10:00:00Z',
    updatedAt: '2025-12-12T10:00:00Z',
  },
  {
    id: '4',
    name: 'High Memory Usage',
    alertType: 'HighMemory',
    severity: 'Warning',
    thresholdValue: 85,
    thresholdUnit: '%',
    durationMinutes: 15,
    isEnabled: true,
    description: 'Alert when memory usage exceeds 85% for 15 minutes',
    createdAt: '2025-12-12T10:00:00Z',
    updatedAt: '2025-12-12T10:00:00Z',
  },
  {
    id: '5',
    name: 'Low Disk Space',
    alertType: 'LowDiskSpace',
    severity: 'Error',
    thresholdValue: 20,
    thresholdUnit: '%',
    durationMinutes: 5,
    isEnabled: false,
    description: 'Alert when available disk space drops below 20%',
    createdAt: '2025-12-12T10:00:00Z',
    updatedAt: '2025-12-12T10:00:00Z',
  },
  {
    id: '6',
    name: 'API Rate Limit Exceeded',
    alertType: 'RateLimitExceeded',
    severity: 'Info',
    thresholdValue: 100,
    thresholdUnit: 'req/s',
    durationMinutes: 3,
    isEnabled: true,
    description: 'Info alert when API rate limit is approached',
    createdAt: '2025-12-12T10:00:00Z',
    updatedAt: '2025-12-12T10:00:00Z',
  },
];

/**
 * Empty state - No rules configured
 */
export const Empty: Story = {
  args: {
    rules: [],
  },
};

/**
 * Populated - Multiple rules with mixed severities and states
 */
export const Populated: Story = {
  args: {
    rules: mockRules,
  },
};

/**
 * Single Rule - Critical severity, enabled
 */
export const SingleRule: Story = {
  args: {
    rules: [mockRules[0]],
  },
};

/**
 * All Critical - Multiple critical rules
 */
export const AllCritical: Story = {
  args: {
    rules: mockRules.filter(r => r.severity === 'Critical'),
  },
};

/**
 * All Disabled - Rules in disabled state
 */
export const AllDisabled: Story = {
  args: {
    rules: mockRules.map(r => ({ ...r, isEnabled: false })),
  },
};

/**
 * Mixed State - Some enabled, some disabled
 */
export const MixedState: Story = {
  args: {
    rules: mockRules,
  },
};
