/**
 * ApiKeyFilterPanel Storybook Stories (Issue #910)
 *
 * Visual testing for API Key filter panel with comprehensive scenarios.
 * Tests different filter combinations, states, and responsive behavior.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { ApiKeyFilterPanel } from './ApiKeyFilterPanel';
import { useState } from 'react';
import type { ApiKeyFilters } from '@/types';

const meta: Meta<typeof ApiKeyFilterPanel> = {
  title: 'Admin/ApiKeyFilterPanel',
  component: ApiKeyFilterPanel,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Filter panel for API Key management with status, scope, date range, and search filters. ' +
          'Provides real-time filtering with clear all functionality and active filter summary.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    filters: {
      control: 'object',
      description: 'Current filter state',
    },
    onFiltersChange: {
      action: 'filters-changed',
      description: 'Callback when filters change',
    },
    onReset: {
      action: 'reset',
      description: 'Callback for reset action',
    },
    className: {
      control: 'text',
      description: 'Optional CSS classes',
    },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Interactive wrapper for state management
 */
function InteractiveWrapper(props: {
  initialFilters?: ApiKeyFilters;
  children: (
    filters: ApiKeyFilters,
    setFilters: (filters: ApiKeyFilters) => void
  ) => React.ReactNode;
}) {
  const [filters, setFilters] = useState<ApiKeyFilters>(props.initialFilters || {});
  return <>{props.children(filters, setFilters)}</>;
}

/**
 * Default state: Empty filters
 */
export const Default: Story = {
  args: {
    filters: {},
    onFiltersChange: () => {},
    onReset: () => {},
  },
  render: args => (
    <InteractiveWrapper>
      {(filters, setFilters) => (
        <ApiKeyFilterPanel
          {...args}
          filters={filters}
          onFiltersChange={f => {
            setFilters(f);
            args.onFiltersChange?.(f);
          }}
          onReset={() => {
            setFilters({});
            args.onReset?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * With search query
 */
export const WithSearch: Story = {
  args: {
    filters: {
      search: 'production-api',
    },
    onFiltersChange: () => {},
    onReset: () => {},
  },
  render: args => (
    <InteractiveWrapper initialFilters={args.filters}>
      {(filters, setFilters) => (
        <ApiKeyFilterPanel
          {...args}
          filters={filters}
          onFiltersChange={f => {
            setFilters(f);
            args.onFiltersChange?.(f);
          }}
          onReset={() => {
            setFilters({});
            args.onReset?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * With status filter (Active)
 */
export const WithStatusFilter: Story = {
  args: {
    filters: {
      status: 'active',
    },
    onFiltersChange: () => {},
    onReset: () => {},
  },
  render: args => (
    <InteractiveWrapper initialFilters={args.filters}>
      {(filters, setFilters) => (
        <ApiKeyFilterPanel
          {...args}
          filters={filters}
          onFiltersChange={f => {
            setFilters(f);
            args.onFiltersChange?.(f);
          }}
          onReset={() => {
            setFilters({});
            args.onReset?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * With multiple scopes selected
 */
export const WithScopesSelected: Story = {
  args: {
    filters: {
      scopes: ['read', 'write'],
    },
    onFiltersChange: () => {},
    onReset: () => {},
  },
  render: args => (
    <InteractiveWrapper initialFilters={args.filters}>
      {(filters, setFilters) => (
        <ApiKeyFilterPanel
          {...args}
          filters={filters}
          onFiltersChange={f => {
            setFilters(f);
            args.onFiltersChange?.(f);
          }}
          onReset={() => {
            setFilters({});
            args.onReset?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * With date ranges
 */
export const WithDateRanges: Story = {
  args: {
    filters: {
      createdFrom: new Date('2024-01-01'),
      createdTo: new Date('2024-12-31'),
      expiresFrom: new Date('2025-01-01'),
      expiresTo: new Date('2025-12-31'),
    },
    onFiltersChange: () => {},
    onReset: () => {},
  },
  render: args => (
    <InteractiveWrapper initialFilters={args.filters}>
      {(filters, setFilters) => (
        <ApiKeyFilterPanel
          {...args}
          filters={filters}
          onFiltersChange={f => {
            setFilters(f);
            args.onFiltersChange?.(f);
          }}
          onReset={() => {
            setFilters({});
            args.onReset?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * With last used filter
 */
export const WithLastUsed: Story = {
  args: {
    filters: {
      lastUsedDays: 30,
    },
    onFiltersChange: () => {},
    onReset: () => {},
  },
  render: args => (
    <InteractiveWrapper initialFilters={args.filters}>
      {(filters, setFilters) => (
        <ApiKeyFilterPanel
          {...args}
          filters={filters}
          onFiltersChange={f => {
            setFilters(f);
            args.onFiltersChange?.(f);
          }}
          onReset={() => {
            setFilters({});
            args.onReset?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * All filters active (comprehensive scenario)
 */
export const AllFiltersActive: Story = {
  args: {
    filters: {
      search: 'prod-api-key',
      status: 'active',
      scopes: ['read', 'write', 'admin'],
      createdFrom: new Date('2024-01-01'),
      createdTo: new Date('2024-06-30'),
      expiresFrom: new Date('2025-01-01'),
      expiresTo: new Date('2025-12-31'),
      lastUsedDays: 7,
    },
    onFiltersChange: () => {},
    onReset: () => {},
  },
  render: args => (
    <InteractiveWrapper initialFilters={args.filters}>
      {(filters, setFilters) => (
        <ApiKeyFilterPanel
          {...args}
          filters={filters}
          onFiltersChange={f => {
            setFilters(f);
            args.onFiltersChange?.(f);
          }}
          onReset={() => {
            setFilters({});
            args.onReset?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Mobile viewport (responsive test)
 */
export const Mobile: Story = {
  args: {
    filters: {
      search: 'api-key',
      status: 'active',
      scopes: ['read'],
    },
    onFiltersChange: () => {},
    onReset: () => {},
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  render: args => (
    <InteractiveWrapper initialFilters={args.filters}>
      {(filters, setFilters) => (
        <ApiKeyFilterPanel
          {...args}
          filters={filters}
          onFiltersChange={f => {
            setFilters(f);
            args.onFiltersChange?.(f);
          }}
          onReset={() => {
            setFilters({});
            args.onReset?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Dark mode (theme test)
 */
export const DarkMode: Story = {
  args: {
    filters: {
      status: 'active',
      scopes: ['read', 'write'],
    },
    onFiltersChange: () => {},
    onReset: () => {},
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  render: args => (
    <div className="dark">
      <InteractiveWrapper initialFilters={args.filters}>
        {(filters, setFilters) => (
          <ApiKeyFilterPanel
            {...args}
            filters={filters}
            onFiltersChange={f => {
              setFilters(f);
              args.onFiltersChange?.(f);
            }}
            onReset={() => {
              setFilters({});
              args.onReset?.();
            }}
          />
        )}
      </InteractiveWrapper>
    </div>
  ),
};
