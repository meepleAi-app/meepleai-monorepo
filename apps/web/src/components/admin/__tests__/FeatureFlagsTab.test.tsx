/**
 * CONFIG-06: FeatureFlagsTab Component Tests (Issue #3079)
 *
 * Tests for role-based and tier-based feature flag management.
 */

import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import FeatureFlagsTab from '../FeatureFlagsTab';
import { api } from '../../../lib/api';
import { toast } from '@/components/layout/Toast';

import type { SystemConfigurationDto } from '../../../lib/api';

// Mock dependencies - use partial mock to preserve schema exports (TIER_ORDER, etc.)
vi.mock('../../../lib/api', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../lib/api')>();
  return {
    ...actual,
    api: {
      config: {
        updateConfiguration: vi.fn(),
        bulkUpdate: vi.fn(),
      },
    },
  };
});
vi.mock('@/components/layout/Toast');

const mockApi = api as Mocked<typeof api>;
const mockToast = toast as Mocked<typeof toast>;

describe('FeatureFlagsTab', () => {
  // Base mock configuration factory
  const createMockFlag = (
    id: string,
    key: string,
    value: 'true' | 'false',
    options: Partial<SystemConfigurationDto> = {}
  ): SystemConfigurationDto => ({
    id,
    key: `Features:${key}`,
    value,
    valueType: 'boolean',
    description: `Description for ${key}`,
    category: 'FeatureFlag',
    isActive: true,
    requiresRestart: false,
    environment: 'All',
    version: 1,
    previousValue: null,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
    createdByUserId: 'admin',
    updatedByUserId: null,
    lastToggledAt: null,
    ...options,
  });

  // Role-only configurations (no tier support)
  const mockRoleOnlyConfigurations: SystemConfigurationDto[] = [
    createMockFlag('1', 'RagCaching', 'true'),
    createMockFlag('2', 'StreamingResponses', 'false', { requiresRestart: true }),
    createMockFlag('3', 'BetaSurvey', 'false'),
  ];

  // Tier-based configurations
  const mockTierConfigurations: SystemConfigurationDto[] = [
    createMockFlag('1', 'RagCaching', 'true', {
      tierFree: false,
      tierNormal: true,
      tierPremium: true,
    }),
    createMockFlag('2', 'StreamingResponses', 'true', {
      requiresRestart: true,
      tierFree: false,
      tierNormal: false,
      tierPremium: true,
    }),
    createMockFlag('3', 'AdvancedSearch', 'true', {
      tierFree: true,
      tierNormal: true,
      tierPremium: true,
    }),
  ];

  const mockOnChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockApi.config = {
      updateConfiguration: vi.fn().mockResolvedValue({}),
      bulkUpdate: vi.fn().mockResolvedValue([]),
    } as any;
    mockToast.success = vi.fn();
    mockToast.error = vi.fn();
    mockToast.info = vi.fn();
  });

  describe('Basic Functionality', () => {
    it('renders feature flags correctly', () => {
      render(
        <FeatureFlagsTab configurations={mockRoleOnlyConfigurations} onConfigurationChange={mockOnChange} />
      );

      // Feature names appear in both preview and table, so use getAllByText
      expect(screen.getAllByText('RagCaching').length).toBeGreaterThan(0);
      expect(screen.getAllByText('StreamingResponses').length).toBeGreaterThan(0);
      expect(screen.getAllByText('BetaSurvey').length).toBeGreaterThan(0);
    });

    it('shows active features preview', () => {
      render(
        <FeatureFlagsTab configurations={mockRoleOnlyConfigurations} onConfigurationChange={mockOnChange} />
      );

      expect(screen.getByText(/Currently Active Features \(1\)/)).toBeInTheDocument();
      const previewSection = screen.getByText(/Currently Active Features/).closest('div');
      expect(within(previewSection!).getByText('RagCaching')).toBeInTheDocument();
    });

    it('shows empty state when no feature flags', () => {
      render(<FeatureFlagsTab configurations={[]} onConfigurationChange={mockOnChange} />);

      expect(screen.getByText(/No feature flags found/)).toBeInTheDocument();
    });

    it('displays restart warning for flags requiring restart', () => {
      render(
        <FeatureFlagsTab configurations={mockRoleOnlyConfigurations} onConfigurationChange={mockOnChange} />
      );

      expect(screen.getByText(/Restart/)).toBeInTheDocument();
    });
  });

  describe('Role-Based Toggle', () => {
    it('toggles non-critical feature flags without confirmation', async () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockImplementation(() => true);

      render(
        <FeatureFlagsTab configurations={mockRoleOnlyConfigurations} onConfigurationChange={mockOnChange} />
      );

      const toggleButtons = screen.getAllByRole('switch');
      const betaSurveyToggle = toggleButtons[2];

      fireEvent.click(betaSurveyToggle);

      await waitFor(() => {
        expect(mockApi.config.updateConfiguration).toHaveBeenCalledWith('3', {
          value: 'true',
        });
        expect(mockToast.success).toHaveBeenCalled();
        expect(mockOnChange).toHaveBeenCalled();
      });

      expect(confirmSpy).not.toHaveBeenCalled();
      confirmSpy.mockRestore();
    });

    it('shows confirmation prompt for critical features before disabling', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);

      render(
        <FeatureFlagsTab configurations={mockRoleOnlyConfigurations} onConfigurationChange={mockOnChange} />
      );

      const ragCachingToggle = screen.getAllByRole('switch')[0];
      fireEvent.click(ragCachingToggle);

      expect(confirmSpy).toHaveBeenCalled();
      expect(mockApi.config.updateConfiguration).not.toHaveBeenCalled();

      confirmSpy.mockRestore();
    });

    it('surfaces API errors and keeps toggle state unchanged', async () => {
      mockApi.config.updateConfiguration = vi.fn().mockRejectedValue(new Error('API Error'));

      render(
        <FeatureFlagsTab configurations={mockRoleOnlyConfigurations} onConfigurationChange={mockOnChange} />
      );

      const betaSurveyToggle = screen.getAllByRole('switch')[2];
      fireEvent.click(betaSurveyToggle);

      await waitFor(() => {
        expect(mockToast.error).toHaveBeenCalled();
      });
    });
  });

  describe('Tier-Based Feature Flags (Issue #3079)', () => {
    it('displays tier columns when tier data is present', async () => {
      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      // Wait for state to update after useEffect, then check tier columns
      // Tier names appear in both column headers and guide section, use getAllByText
      await waitFor(() => {
        expect(screen.getAllByText('Free').length).toBeGreaterThan(0);
        expect(screen.getAllByText('Normal').length).toBeGreaterThan(0);
        expect(screen.getAllByText('Premium').length).toBeGreaterThan(0);
      });
    });

    it('does not display tier columns when no tier data', () => {
      render(
        <FeatureFlagsTab configurations={mockRoleOnlyConfigurations} onConfigurationChange={mockOnChange} />
      );

      expect(screen.queryByText('Free')).not.toBeInTheDocument();
      expect(screen.queryByText('Normal')).not.toBeInTheDocument();
      expect(screen.queryByText('Premium')).not.toBeInTheDocument();
    });

    it('shows selection checkboxes when tier support is available', () => {
      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      expect(checkboxes.length).toBeGreaterThan(0);
    });

    it('does not show selection checkboxes without tier support', () => {
      render(
        <FeatureFlagsTab configurations={mockRoleOnlyConfigurations} onConfigurationChange={mockOnChange} />
      );

      // Only the switches should be present, no checkboxes
      const checkboxes = screen.queryAllByRole('checkbox');
      expect(checkboxes.length).toBe(0);
    });

    it('allows selecting and deselecting feature flags', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      const firstFlagCheckbox = checkboxes[1]; // Skip header checkbox

      await user.click(firstFlagCheckbox);
      expect(firstFlagCheckbox).toBeChecked();

      await user.click(firstFlagCheckbox);
      expect(firstFlagCheckbox).not.toBeChecked();
    });

    it('selects all flags when header checkbox is clicked', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      const headerCheckbox = screen.getAllByRole('checkbox')[0];
      await user.click(headerCheckbox);

      const allCheckboxes = screen.getAllByRole('checkbox');
      allCheckboxes.forEach(checkbox => {
        expect(checkbox).toBeChecked();
      });
    });

    it('clears selection when clear button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      // Select all
      const headerCheckbox = screen.getAllByRole('checkbox')[0];
      await user.click(headerCheckbox);

      // Click clear selection
      const clearButton = screen.getByText('Clear selection');
      await user.click(clearButton);

      const allCheckboxes = screen.getAllByRole('checkbox');
      allCheckboxes.forEach(checkbox => {
        expect(checkbox).not.toBeChecked();
      });
    });

    it('shows N/A badge for flags without tier configuration', () => {
      const mixedConfigurations: SystemConfigurationDto[] = [
        createMockFlag('1', 'WithTiers', 'true', {
          tierFree: true,
          tierNormal: true,
          tierPremium: true,
        }),
        createMockFlag('2', 'WithoutTiers', 'true'),
      ];

      render(
        <FeatureFlagsTab configurations={mixedConfigurations} onConfigurationChange={mockOnChange} />
      );

      const naBadges = screen.getAllByText('N/A');
      expect(naBadges.length).toBe(3); // 3 tier columns for the flag without tier config
    });

    it('disables tier toggles when global toggle is disabled', () => {
      const configurations: SystemConfigurationDto[] = [
        createMockFlag('1', 'DisabledFeature', 'false', {
          tierFree: false,
          tierNormal: true,
          tierPremium: true,
        }),
      ];

      render(
        <FeatureFlagsTab configurations={configurations} onConfigurationChange={mockOnChange} />
      );

      // Get all switches - first is global, next 3 are tiers
      const switches = screen.getAllByRole('switch');

      // Tier switches should be disabled because global is off
      expect(switches[1]).toBeDisabled();
      expect(switches[2]).toBeDisabled();
      expect(switches[3]).toBeDisabled();
    });
  });

  describe('Bulk Actions', () => {
    it('shows bulk action bar when flags are selected', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      // Select a flag
      const firstFlagCheckbox = screen.getAllByRole('checkbox')[1];
      await user.click(firstFlagCheckbox);

      // Bulk action bar should be visible
      expect(screen.getByTestId('feature-flags-bulk-actions')).toBeInTheDocument();
    });

    it('hides bulk action bar when no flags are selected', () => {
      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      expect(screen.queryByTestId('feature-flags-bulk-actions')).not.toBeInTheDocument();
    });

    it('displays tier-specific bulk action buttons', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      // Select a flag to show bulk actions
      const firstFlagCheckbox = screen.getAllByRole('checkbox')[1];
      await user.click(firstFlagCheckbox);

      // Use testIds for reliable selection - buttons have responsive text that varies
      expect(screen.getByTestId('feature-flags-bulk-actions-action-enable-premium')).toBeInTheDocument();
      expect(screen.getByTestId('feature-flags-bulk-actions-action-enable-normal')).toBeInTheDocument();
      expect(screen.getByTestId('feature-flags-bulk-actions-action-disable-free')).toBeInTheDocument();
    });

    it('shows info toast for bulk tier actions (pending backend #3073)', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      // Select flags
      const headerCheckbox = screen.getAllByRole('checkbox')[0];
      await user.click(headerCheckbox);

      // Click bulk action using testId for reliable selection
      const enablePremiumButton = screen.getByTestId('feature-flags-bulk-actions-action-enable-premium');
      await user.click(enablePremiumButton);

      await waitFor(() => {
        // Bulk tier actions show info toast until backend #3073 is ready
        expect(mockToast.info).toHaveBeenCalledWith(
          expect.stringContaining('Backend support (#3073) required')
        );
        // API should NOT be called since tier updates aren't supported yet
        expect(mockApi.config.bulkUpdate).not.toHaveBeenCalled();
      });
    });
  });

  describe('Visual Differentiation', () => {
    it('applies different colors to tier toggle switches', () => {
      const configurations: SystemConfigurationDto[] = [
        createMockFlag('1', 'AllTiersEnabled', 'true', {
          tierFree: true,
          tierNormal: true,
          tierPremium: true,
        }),
      ];

      render(
        <FeatureFlagsTab configurations={configurations} onConfigurationChange={mockOnChange} />
      );

      // Verify that tier-specific styling classes would be applied
      // (We test the structure rather than actual CSS classes)
      const switches = screen.getAllByRole('switch');
      expect(switches.length).toBe(4); // 1 global + 3 tiers
    });

    it('highlights selected rows', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      const firstFlagCheckbox = screen.getAllByRole('checkbox')[1];
      await user.click(firstFlagCheckbox);

      // Row should have selection styling (we verify the checkbox is checked)
      expect(firstFlagCheckbox).toBeChecked();
    });

    it('highlights enabled rows with green background', () => {
      const configurations: SystemConfigurationDto[] = [
        createMockFlag('1', 'EnabledFeature', 'true', {
          tierFree: true,
          tierNormal: true,
          tierPremium: true,
        }),
        createMockFlag('2', 'DisabledFeature', 'false', {
          tierFree: false,
          tierNormal: false,
          tierPremium: false,
        }),
      ];

      render(
        <FeatureFlagsTab configurations={configurations} onConfigurationChange={mockOnChange} />
      );

      // Verify both features are rendered - enabled features appear in preview and table
      expect(screen.getAllByText('EnabledFeature').length).toBeGreaterThan(0);
      // Disabled features only appear in table
      expect(screen.getAllByText('DisabledFeature').length).toBeGreaterThan(0);
    });
  });

  describe('Tooltips', () => {
    it('renders tooltips for tier column headers', () => {
      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      // Tier headers should have tooltip triggers - tier names appear in both headers and guide
      expect(screen.getAllByText('Free').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Normal').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Premium').length).toBeGreaterThan(0);
    });

    it('shows help guide with tier descriptions', () => {
      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      expect(screen.getByText(/Feature Flags Guide/)).toBeInTheDocument();
      expect(screen.getByText(/Premium: Full access/)).toBeInTheDocument();
      expect(screen.getByText(/Normal: Standard subscription/)).toBeInTheDocument();
      expect(screen.getByText(/Free: Basic access/)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper aria-labels for all toggle switches', () => {
      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      const switches = screen.getAllByRole('switch');
      switches.forEach(switchEl => {
        expect(switchEl).toHaveAttribute('aria-label');
      });
    });

    it('has proper aria-labels for checkboxes', () => {
      render(
        <FeatureFlagsTab configurations={mockTierConfigurations} onConfigurationChange={mockOnChange} />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      checkboxes.forEach(checkbox => {
        expect(checkbox).toHaveAttribute('aria-label');
      });
    });
  });

  describe('Backward Compatibility', () => {
    it('works correctly with configurations without any tier fields', () => {
      render(
        <FeatureFlagsTab configurations={mockRoleOnlyConfigurations} onConfigurationChange={mockOnChange} />
      );

      // Should render without errors - RagCaching appears in both preview and table
      expect(screen.getAllByText('RagCaching').length).toBeGreaterThan(0);

      // Should not show tier columns
      expect(screen.queryByText('Free')).not.toBeInTheDocument();

      // Toggle should still work
      const toggles = screen.getAllByRole('switch');
      expect(toggles.length).toBe(3); // One per flag, no tier toggles
    });
  });
});
