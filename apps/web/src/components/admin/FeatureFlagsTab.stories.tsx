/**
 * FeatureFlagsTab Storybook Stories (Issue #3079)
 *
 * Visual testing and documentation for the FeatureFlagsTab component.
 * Demonstrates role-based and tier-based feature flag configurations.
 */

import { fn } from '@storybook/test';

import FeatureFlagsTab from './FeatureFlagsTab';

import type { Meta, StoryObj } from '@storybook/react';
import type { SystemConfigurationDto } from '../../lib/api';

// Mock API
vi.mock('../../lib/api', async () => {
  const actual = await vi.importActual('../../lib/api');
  return {
    ...actual,
    api: {
      config: {
        updateConfiguration: vi.fn().mockResolvedValue({}),
        bulkUpdate: vi.fn().mockResolvedValue([]),
      },
    },
  };
});

const meta: Meta<typeof FeatureFlagsTab> = {
  title: 'Admin/FeatureFlagsTab',
  component: FeatureFlagsTab,
  tags: ['autodocs'],
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Feature flags management component with role-based and tier-based (Free/Normal/Premium) toggle support. Includes bulk actions for tier management.',
      },
    },
  },
  argTypes: {
    configurations: {
      description: 'Array of SystemConfigurationDto objects representing feature flags',
    },
    onConfigurationChange: {
      action: 'onConfigurationChange',
      description: 'Callback fired when a configuration changes',
    },
  },
};

export default meta;
type Story = StoryObj<typeof FeatureFlagsTab>;

// Helper to create mock feature flags
const createMockFlag = (
  id: string,
  key: string,
  value: 'true' | 'false',
  description: string,
  options: Partial<SystemConfigurationDto> = {}
): SystemConfigurationDto => ({
  id,
  key: `Features:${key}`,
  value,
  valueType: 'boolean',
  description,
  category: 'FeatureFlag',
  isActive: true,
  requiresRestart: false,
  environment: 'All',
  version: 1,
  previousValue: null,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: '2025-01-01T00:00:00Z',
  createdByUserId: 'admin-123',
  updatedByUserId: null,
  lastToggledAt: null,
  ...options,
});

// Mock configurations for role-based only (no tier columns)
const mockRoleOnlyConfigurations: SystemConfigurationDto[] = [
  createMockFlag('1', 'RagCaching', 'true', 'Enable RAG response caching for improved performance'),
  createMockFlag('2', 'StreamingResponses', 'false', 'Enable SSE streaming for real-time QA responses', {
    requiresRestart: true,
  }),
  createMockFlag('3', 'BetaSurvey', 'false', 'Show beta survey banner to users'),
  createMockFlag('4', 'SetupGuide', 'true', 'Show setup guide for new users'),
  createMockFlag('5', 'DarkModeDefault', 'false', 'Default to dark mode for new users'),
];

// Mock configurations with tier support
const mockTierConfigurations: SystemConfigurationDto[] = [
  createMockFlag('1', 'RagCaching', 'true', 'Enable RAG response caching for improved performance', {
    tierFree: false,
    tierNormal: true,
    tierPremium: true,
  }),
  createMockFlag('2', 'StreamingResponses', 'true', 'Enable SSE streaming for real-time QA responses', {
    requiresRestart: true,
    tierFree: false,
    tierNormal: false,
    tierPremium: true,
  }),
  createMockFlag('3', 'AdvancedSearch', 'true', 'Enable advanced search with filters', {
    tierFree: true,
    tierNormal: true,
    tierPremium: true,
  }),
  createMockFlag('4', 'ExportData', 'true', 'Allow users to export their data', {
    tierFree: false,
    tierNormal: true,
    tierPremium: true,
  }),
  createMockFlag('5', 'AIAssistant', 'true', 'AI-powered game assistant', {
    tierFree: false,
    tierNormal: false,
    tierPremium: true,
  }),
  createMockFlag('6', 'CollaborativeEditing', 'false', 'Real-time collaborative game editing', {
    tierFree: false,
    tierNormal: false,
    tierPremium: true,
  }),
];

// Mixed configurations (some with tiers, some without)
const mockMixedConfigurations: SystemConfigurationDto[] = [
  createMockFlag('1', 'RagCaching', 'true', 'Enable RAG response caching', {
    tierFree: false,
    tierNormal: true,
    tierPremium: true,
  }),
  createMockFlag('2', 'LegacyFeature', 'true', 'Legacy feature without tier support'),
  createMockFlag('3', 'AIAssistant', 'true', 'AI-powered assistant', {
    tierFree: false,
    tierNormal: false,
    tierPremium: true,
  }),
];

/**
 * Role-based feature flags only (current state).
 * Shows the traditional toggle interface without tier columns.
 */
export const RoleBasedOnly: Story = {
  args: {
    configurations: mockRoleOnlyConfigurations,
    onConfigurationChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Traditional role-based feature flags without tier support. This is backward compatible with existing configurations.',
      },
    },
  },
};

/**
 * Full tier-based feature flags (Issue #3079).
 * Shows columns for Free, Normal, and Premium tiers with individual toggles.
 */
export const TierBasedFlags: Story = {
  args: {
    configurations: mockTierConfigurations,
    onConfigurationChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Feature flags with tier-based access control. Each tier (Free, Normal, Premium) has its own toggle for fine-grained control.',
      },
    },
  },
};

/**
 * Mixed configuration (some flags with tiers, some without).
 * Shows how the UI handles legacy flags alongside new tier-based flags.
 */
export const MixedConfiguration: Story = {
  args: {
    configurations: mockMixedConfigurations,
    onConfigurationChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Mixed configuration with both tier-based and legacy feature flags. Legacy flags show "N/A" in tier columns.',
      },
    },
  },
};

/**
 * Empty state when no feature flags exist.
 */
export const EmptyState: Story = {
  args: {
    configurations: [],
    onConfigurationChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Empty state shown when no feature flags are configured in the database.',
      },
    },
  },
};

/**
 * Feature flags with some inactive.
 * Shows how inactive flags are displayed and cannot be toggled.
 */
export const WithInactiveFlags: Story = {
  args: {
    configurations: [
      createMockFlag('1', 'ActiveFeature', 'true', 'This feature is active and can be toggled', {
        tierFree: true,
        tierNormal: true,
        tierPremium: true,
      }),
      createMockFlag('2', 'InactiveFeature', 'false', 'This feature is inactive and locked', {
        isActive: false,
        tierFree: false,
        tierNormal: false,
        tierPremium: false,
      }),
      createMockFlag('3', 'AnotherActive', 'true', 'Another active feature', {
        tierFree: false,
        tierNormal: true,
        tierPremium: true,
      }),
    ],
    onConfigurationChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows inactive feature flags that cannot be toggled until activated in the database.',
      },
    },
  },
};

/**
 * Feature flags requiring restart.
 * Shows warning indicators for flags that need a server restart.
 */
export const WithRestartRequired: Story = {
  args: {
    configurations: [
      createMockFlag('1', 'NormalFeature', 'true', 'No restart required', {
        tierFree: true,
        tierNormal: true,
        tierPremium: true,
      }),
      createMockFlag('2', 'CacheConfig', 'true', 'Cache configuration change', {
        requiresRestart: true,
        tierFree: false,
        tierNormal: true,
        tierPremium: true,
      }),
      createMockFlag('3', 'StreamingEngine', 'false', 'Streaming engine toggle', {
        requiresRestart: true,
        tierFree: false,
        tierNormal: false,
        tierPremium: true,
      }),
    ],
    onConfigurationChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Feature flags that require a server restart to take effect are marked with a warning badge.',
      },
    },
  },
};

/**
 * Multiple versions of flags.
 * Shows version badges for flags that have been updated.
 */
export const WithVersionHistory: Story = {
  args: {
    configurations: [
      createMockFlag('1', 'NewFeature', 'true', 'Newly created feature', {
        version: 1,
        tierFree: true,
        tierNormal: true,
        tierPremium: true,
      }),
      createMockFlag('2', 'UpdatedFeature', 'true', 'Feature updated multiple times', {
        version: 5,
        tierFree: false,
        tierNormal: true,
        tierPremium: true,
        updatedAt: '2025-12-15T10:30:00Z',
      }),
      createMockFlag('3', 'FrequentlyChanged', 'false', 'High churn feature', {
        version: 12,
        tierFree: false,
        tierNormal: false,
        tierPremium: true,
        updatedAt: '2025-12-20T15:45:00Z',
      }),
    ],
    onConfigurationChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Feature flags with version history show version badges. Useful for tracking configuration changes.',
      },
    },
  },
};

/**
 * Premium-only features.
 * Example of a premium tier restriction pattern.
 */
export const PremiumOnlyFeatures: Story = {
  args: {
    configurations: [
      createMockFlag('1', 'BasicFeature', 'true', 'Available to all tiers', {
        tierFree: true,
        tierNormal: true,
        tierPremium: true,
      }),
      createMockFlag('2', 'AdvancedAnalytics', 'true', 'Advanced analytics dashboard', {
        tierFree: false,
        tierNormal: false,
        tierPremium: true,
      }),
      createMockFlag('3', 'PrioritySupport', 'true', 'Priority customer support', {
        tierFree: false,
        tierNormal: false,
        tierPremium: true,
      }),
      createMockFlag('4', 'CustomBranding', 'true', 'Custom branding options', {
        tierFree: false,
        tierNormal: false,
        tierPremium: true,
      }),
      createMockFlag('5', 'APIAccess', 'true', 'Full API access', {
        tierFree: false,
        tierNormal: true,
        tierPremium: true,
      }),
    ],
    onConfigurationChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Example of premium-only features pattern. Most advanced features are restricted to Premium tier.',
      },
    },
  },
};

/**
 * Dark mode appearance.
 */
export const DarkMode: Story = {
  args: {
    configurations: mockTierConfigurations,
    onConfigurationChange: fn(),
  },
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Feature flags tab in dark mode.',
      },
    },
  },
};

/**
 * Mobile responsive view.
 */
export const Mobile: Story = {
  args: {
    configurations: mockTierConfigurations.slice(0, 3),
    onConfigurationChange: fn(),
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    docs: {
      description: {
        story: 'Responsive view on mobile devices. Table scrolls horizontally.',
      },
    },
  },
};
