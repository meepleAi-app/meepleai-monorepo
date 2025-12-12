import type { Meta, StoryObj } from '@storybook/react';
import { fn } from '@storybook/test';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AlertRuleForm } from './AlertRuleForm';
import type { AlertRule } from '@/lib/api/schemas/alert-rules.schemas';

/**
 * AlertRuleForm Component - Issue #921
 *
 * ## Features
 * - **Create Mode**: Empty form for creating new alert rules
 * - **Edit Mode**: Pre-populated form for editing existing rules
 * - **Validation**: Zod schema validation with error messages
 * - **Required Fields**: Name, Type, Severity, Threshold, Duration
 * - **Optional Fields**: Description
 * - **Form Actions**: Submit and Cancel buttons with loading states
 *
 * ## Validation Rules
 * - Name: Required, min 1 character
 * - Alert Type: Required, min 1 character
 * - Severity: Required, one of [Info, Warning, Error, Critical]
 * - Threshold Value: Required, must be >= 0
 * - Threshold Unit: Required, min 1 character
 * - Duration: Required, must be >= 1 minute
 *
 * ## Visual Regression Testing
 * Chromatic captures:
 * - Create mode (empty form)
 * - Edit mode (pre-populated)
 * - Validation errors
 */
const meta = {
  title: 'Admin/Alert Rules/AlertRuleForm',
  component: AlertRuleForm,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [768, 1024],
      delay: 300,
      diffThreshold: 0.2,
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
          },
        },
      });
      return (
        <QueryClientProvider client={queryClient}>
          <div className="w-[600px] p-6 border rounded-lg bg-white">
            <Story />
          </div>
        </QueryClientProvider>
      );
    },
  ],
  argTypes: {
    rule: { control: 'object' },
    onSubmit: { action: 'submitted' },
    onCancel: { action: 'cancelled' },
  },
  args: {
    onSubmit: fn(),
    onCancel: fn(),
  },
} satisfies Meta<typeof AlertRuleForm>;

export default meta;
type Story = StoryObj<typeof meta>;

const existingRule: AlertRule = {
  id: '1',
  name: 'High Error Rate',
  alertType: 'HighErrorRate',
  severity: 'Critical',
  thresholdValue: 5,
  thresholdUnit: '%',
  durationMinutes: 5,
  isEnabled: true,
  description: 'Alert when error rate exceeds 5% for 5 consecutive minutes',
  createdAt: '2025-12-12T10:00:00Z',
  updatedAt: '2025-12-12T10:00:00Z',
};

/**
 * Create Mode - Empty form for new alert rule
 */
export const CreateMode: Story = {
  args: {
    rule: null,
  },
};

/**
 * Edit Mode - Pre-populated form with existing rule
 */
export const EditMode: Story = {
  args: {
    rule: existingRule,
  },
};

/**
 * Edit Critical Rule - Editing a critical severity rule
 */
export const EditCriticalRule: Story = {
  args: {
    rule: existingRule,
  },
};

/**
 * Edit Warning Rule - Editing a warning severity rule
 */
export const EditWarningRule: Story = {
  args: {
    rule: {
      ...existingRule,
      id: '2',
      name: 'High Latency',
      alertType: 'HighLatency',
      severity: 'Warning',
      thresholdValue: 1000,
      thresholdUnit: 'ms',
      durationMinutes: 10,
      description: 'Alert when P95 latency exceeds 1000ms',
    },
  },
};

/**
 * Edit Info Rule - Editing an info severity rule
 */
export const EditInfoRule: Story = {
  args: {
    rule: {
      ...existingRule,
      id: '3',
      name: 'API Usage Spike',
      alertType: 'APIUsageSpike',
      severity: 'Info',
      thresholdValue: 1000,
      thresholdUnit: 'req/min',
      durationMinutes: 3,
      description: 'Informational alert for sudden API usage increase',
    },
  },
};

/**
 * Edit Without Description - Rule with no description
 */
export const EditWithoutDescription: Story = {
  args: {
    rule: {
      ...existingRule,
      description: null,
    },
  },
};

/**
 * Long Description - Rule with lengthy description
 */
export const LongDescription: Story = {
  args: {
    rule: {
      ...existingRule,
      description:
        'This alert monitors the error rate across all API endpoints. When the error rate exceeds the configured threshold for the specified duration, an alert will be triggered. The threshold is calculated as (errors / total_requests) * 100. This is a critical alert that requires immediate attention from the on-call team.',
    },
  },
};
