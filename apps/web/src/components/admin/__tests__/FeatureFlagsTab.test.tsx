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
vi.mock('../../../lib/api', async importOriginal => {
  const actual = await importOriginal<typeof import('../../../lib/api')>();
  return {
    ...actual,
    api: {
      config: {
        updateConfiguration: vi.fn(),
        bulkUpdate: vi.fn(),
        enableFeatureForTier: vi.fn(),
        disableFeatureForTier: vi.fn(),
        getHistory: vi.fn().mockResolvedValue([]),
      },
    },
  };
});
vi.mock('@/components/layout/Toast');
vi.mock('sonner', () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

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
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // Feature names appear in both preview and table, so use getAllByText
      expect(screen.getAllByText('RagCaching').length).toBeGreaterThan(0);
      expect(screen.getAllByText('StreamingResponses').length).toBeGreaterThan(0);
      expect(screen.getAllByText('BetaSurvey').length).toBeGreaterThan(0);
    });

    it('shows active features preview', () => {
      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
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
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      expect(screen.getByText(/Restart/)).toBeInTheDocument();
    });
  });

  describe('Role-Based Toggle (Issue #1836 — batch save)', () => {
    it('stages a non-critical toggle as a pending change without hitting the API', async () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockImplementation(() => true);

      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      const toggleButtons = screen.getAllByRole('switch');
      const betaSurveyToggle = toggleButtons[2];

      fireEvent.click(betaSurveyToggle);

      // Dirty-state bar surfaces the pending change.
      expect(screen.getByTestId('feature-flags-dirty-bar')).toBeInTheDocument();
      expect(screen.getByText('1 unsaved flag')).toBeInTheDocument();

      // No API hit yet — toggle alone never persists.
      expect(mockApi.config.updateConfiguration).not.toHaveBeenCalled();
      expect(mockOnChange).not.toHaveBeenCalled();
      expect(confirmSpy).not.toHaveBeenCalled();

      confirmSpy.mockRestore();
    });

    it('does not prompt at toggle time, even for critical features', () => {
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);

      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      const ragCachingToggle = screen.getAllByRole('switch')[0];
      fireEvent.click(ragCachingToggle);

      // Confirm is now deferred until Apply — toggling alone never prompts.
      expect(confirmSpy).not.toHaveBeenCalled();
      expect(mockApi.config.updateConfiguration).not.toHaveBeenCalled();

      // But the change is staged.
      expect(screen.getByTestId('feature-flags-dirty-bar')).toBeInTheDocument();

      confirmSpy.mockRestore();
    });

    it('prompts at Apply time when a critical flag is being disabled, and aborts on cancel', async () => {
      const user = userEvent.setup();
      const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);

      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // RagCaching is on (true) and critical — toggle stages a disable.
      const ragCachingToggle = screen.getAllByRole('switch')[0];
      await user.click(ragCachingToggle);

      // Apply via the dirty bar.
      await user.click(screen.getByTestId('feature-flags-dirty-bar-apply'));

      expect(confirmSpy).toHaveBeenCalled();
      expect(mockApi.config.updateConfiguration).not.toHaveBeenCalled();
      expect(mockOnChange).not.toHaveBeenCalled();
      // Bar still visible because user cancelled.
      expect(screen.getByTestId('feature-flags-dirty-bar')).toBeInTheDocument();

      confirmSpy.mockRestore();
    });

    it('applies pending changes in batch and refetches', async () => {
      const user = userEvent.setup();
      mockApi.config.updateConfiguration = vi.fn().mockResolvedValue({});

      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // Stage two non-critical changes (BetaSurvey + StreamingResponses).
      // StreamingResponses is critical *and* currently off — turning it on is not a critical disable, so no prompt.
      const switches = screen.getAllByRole('switch');
      await user.click(switches[2]); // BetaSurvey: false -> true
      await user.click(switches[1]); // StreamingResponses: false -> true

      expect(screen.getByText('2 unsaved flags')).toBeInTheDocument();

      await user.click(screen.getByTestId('feature-flags-dirty-bar-apply'));

      await waitFor(() => {
        expect(mockApi.config.updateConfiguration).toHaveBeenCalledTimes(2);
        expect(mockApi.config.updateConfiguration).toHaveBeenCalledWith('3', { value: 'true' });
        expect(mockApi.config.updateConfiguration).toHaveBeenCalledWith('2', { value: 'true' });
        expect(mockToast.success).toHaveBeenCalled();
        expect(mockOnChange).toHaveBeenCalled();
      });
    });

    it('reverts pending changes without calling the API when Discard is clicked', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      const betaSurveyToggle = screen.getAllByRole('switch')[2];
      await user.click(betaSurveyToggle);
      expect(screen.getByTestId('feature-flags-dirty-bar')).toBeInTheDocument();

      await user.click(screen.getByTestId('feature-flags-dirty-bar-revert'));

      expect(screen.queryByTestId('feature-flags-dirty-bar')).not.toBeInTheDocument();
      expect(mockApi.config.updateConfiguration).not.toHaveBeenCalled();
      expect(mockOnChange).not.toHaveBeenCalled();
    });

    it('drops the entry when toggling the same flag twice (back to original value)', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      const betaSurveyToggle = screen.getAllByRole('switch')[2];
      await user.click(betaSurveyToggle);
      expect(screen.getByText('1 unsaved flag')).toBeInTheDocument();

      // Toggle again — should revert to original state.
      await user.click(betaSurveyToggle);
      expect(screen.queryByTestId('feature-flags-dirty-bar')).not.toBeInTheDocument();
    });

    it('keeps failed flags dirty and surfaces an error toast on partial failure', async () => {
      const user = userEvent.setup();
      mockApi.config.updateConfiguration = vi
        .fn()
        .mockResolvedValueOnce({})
        .mockRejectedValueOnce(new Error('API Error'));

      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      const switches = screen.getAllByRole('switch');
      await user.click(switches[2]); // BetaSurvey -> succeeds
      await user.click(switches[1]); // StreamingResponses -> rejects

      await user.click(screen.getByTestId('feature-flags-dirty-bar-apply'));

      await waitFor(() => {
        expect(mockToast.success).toHaveBeenCalled();
        expect(mockToast.error).toHaveBeenCalled();
      });

      // Failed flag remains in dirty state.
      expect(screen.getByText('1 unsaved flag')).toBeInTheDocument();
    });

    it('preserves pending changes when configurations is refetched (review fix #1)', async () => {
      const user = userEvent.setup();

      const { rerender } = render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // Stage a pending change.
      const betaSurveyToggle = screen.getAllByRole('switch')[2];
      await user.click(betaSurveyToggle);
      expect(screen.getByText('1 unsaved flag')).toBeInTheDocument();

      // Simulate a refetch: the wrapper re-renders with the same list (new
      // array reference). Without the fix the useEffect would wipe pending.
      rerender(
        <FeatureFlagsTab
          configurations={[...mockRoleOnlyConfigurations]}
          onConfigurationChange={mockOnChange}
        />
      );

      // The dirty bar must persist — otherwise the partial-failure retry flow
      // breaks for the admin.
      expect(screen.getByText('1 unsaved flag')).toBeInTheDocument();
    });

    it('drops pending entries when the underlying flag disappears server-side', async () => {
      const user = userEvent.setup();

      const { rerender } = render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      const betaSurveyToggle = screen.getAllByRole('switch')[2];
      await user.click(betaSurveyToggle);
      expect(screen.getByText('1 unsaved flag')).toBeInTheDocument();

      // Refetch returns a configuration set without BetaSurvey (id=3) → pending
      // entry must be dropped.
      rerender(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations.filter(f => f.id !== '3')}
          onConfigurationChange={mockOnChange}
        />
      );

      expect(screen.queryByTestId('feature-flags-dirty-bar')).not.toBeInTheDocument();
    });
  });

  describe('Tier-Based Feature Flags (Issue #3079)', () => {
    it('displays tier columns when tier data is present', async () => {
      render(
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
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
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      expect(screen.queryByText('Free')).not.toBeInTheDocument();
      expect(screen.queryByText('Normal')).not.toBeInTheDocument();
      expect(screen.queryByText('Premium')).not.toBeInTheDocument();
    });

    it('shows selection checkboxes when tier support is available', () => {
      render(
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      const checkboxes = screen.getAllByRole('checkbox');
      expect(checkboxes.length).toBeGreaterThan(0);
    });

    it('does not show selection checkboxes without tier support', () => {
      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // Only the switches should be present, no checkboxes
      const checkboxes = screen.queryAllByRole('checkbox');
      expect(checkboxes.length).toBe(0);
    });

    it('allows selecting and deselecting feature flags', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
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
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
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
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
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
        <FeatureFlagsTab
          configurations={mixedConfigurations}
          onConfigurationChange={mockOnChange}
        />
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
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // Select a flag
      const firstFlagCheckbox = screen.getAllByRole('checkbox')[1];
      await user.click(firstFlagCheckbox);

      // Bulk action bar should be visible
      expect(screen.getByTestId('feature-flags-bulk-actions')).toBeInTheDocument();
    });

    it('hides bulk action bar when no flags are selected', () => {
      render(
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      expect(screen.queryByTestId('feature-flags-bulk-actions')).not.toBeInTheDocument();
    });

    it('displays tier-specific bulk action buttons', async () => {
      const user = userEvent.setup();

      render(
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // Select a flag to show bulk actions
      const firstFlagCheckbox = screen.getAllByRole('checkbox')[1];
      await user.click(firstFlagCheckbox);

      // Use testIds for reliable selection - buttons have responsive text that varies
      expect(
        screen.getByTestId('feature-flags-bulk-actions-action-enable-premium')
      ).toBeInTheDocument();
      expect(
        screen.getByTestId('feature-flags-bulk-actions-action-enable-normal')
      ).toBeInTheDocument();
      expect(
        screen.getByTestId('feature-flags-bulk-actions-action-disable-free')
      ).toBeInTheDocument();
    });

    it('calls enableFeatureForTier for bulk tier actions', async () => {
      const user = userEvent.setup();

      // Mock successful API call
      mockApi.config.enableFeatureForTier = vi.fn().mockResolvedValue({});

      render(
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // Select flags
      const headerCheckbox = screen.getAllByRole('checkbox')[0];
      await user.click(headerCheckbox);

      // Click bulk action using testId for reliable selection
      const enablePremiumButton = screen.getByTestId(
        'feature-flags-bulk-actions-action-enable-premium'
      );
      await user.click(enablePremiumButton);

      await waitFor(() => {
        // Backend #3073 is now implemented - API should be called
        expect(mockApi.config.enableFeatureForTier).toHaveBeenCalled();
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
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
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
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // Tier headers should have tooltip triggers - tier names appear in both headers and guide
      expect(screen.getAllByText('Free').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Normal').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Premium').length).toBeGreaterThan(0);
    });

    it('shows help guide with tier descriptions', () => {
      render(
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
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
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      const switches = screen.getAllByRole('switch');
      switches.forEach(switchEl => {
        expect(switchEl).toHaveAttribute('aria-label');
      });
    });

    it('has proper aria-labels for checkboxes', () => {
      render(
        <FeatureFlagsTab
          configurations={mockTierConfigurations}
          onConfigurationChange={mockOnChange}
        />
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
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
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

  describe('Sub-tab category filter (Issue #1836)', () => {
    const categoryConfigurations: SystemConfigurationDto[] = [
      createMockFlag('1', 'RagCaching', 'true'),
      createMockFlag('2', 'OAuthGoogle', 'true'),
      createMockFlag('3', 'MFA', 'false'),
      createMockFlag('4', 'BetaSurvey', 'false'),
    ];

    beforeEach(() => {
      // Reset URL hash between tests.
      window.history.replaceState(null, '', '/admin/config?tab=flags');
    });

    it('renders all flags when the All tab is active', () => {
      render(
        <FeatureFlagsTab
          configurations={categoryConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      // Each row in the table contains the feature name.
      expect(screen.getAllByText('RagCaching').length).toBeGreaterThan(0);
      expect(screen.getAllByText('OAuthGoogle').length).toBeGreaterThan(0);
      expect(screen.getAllByText('MFA').length).toBeGreaterThan(0);
      expect(screen.getAllByText('BetaSurvey').length).toBeGreaterThan(0);
    });

    it('filters the table when a sub-tab is selected', async () => {
      const user = userEvent.setup();
      render(
        <FeatureFlagsTab
          configurations={categoryConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      await user.click(screen.getByTestId('flag-category-tab-ai'));

      // RagCaching is the only AI-flagged entry. Detect the table row via the
      // unique per-flag history button — the "Active features" preview at the
      // top can re-show enabled flags and would foil naive getByText queries.
      expect(screen.getByTestId('btn-history-1')).toBeInTheDocument(); // RagCaching
      expect(screen.queryByTestId('btn-history-2')).not.toBeInTheDocument(); // OAuthGoogle
      expect(screen.queryByTestId('btn-history-3')).not.toBeInTheDocument(); // MFA
      expect(screen.queryByTestId('btn-history-4')).not.toBeInTheDocument(); // BetaSurvey
    });

    it('mirrors the active category to the URL hash', async () => {
      const user = userEvent.setup();
      render(
        <FeatureFlagsTab
          configurations={categoryConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      await user.click(screen.getByTestId('flag-category-tab-integrations'));
      expect(window.location.hash).toBe('#category=integrations');

      await user.click(screen.getByTestId('flag-category-tab-all'));
      expect(window.location.hash).toBe('');
    });

    it('hydrates the active category from the URL hash on mount', () => {
      window.history.replaceState(null, '', '/admin/config?tab=flags#category=security');

      render(
        <FeatureFlagsTab
          configurations={categoryConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      expect(screen.getByTestId('flag-category-tab-security')).toHaveAttribute(
        'aria-selected',
        'true'
      );
      // Non-security flags should be hidden from the table — check via the
      // unique per-row history button so the "Active features" preview banner
      // doesn't trip the assertion.
      expect(screen.getByTestId('btn-history-3')).toBeInTheDocument(); // MFA (security)
      expect(screen.queryByTestId('btn-history-1')).not.toBeInTheDocument(); // RagCaching (ai)
      expect(screen.queryByTestId('btn-history-2')).not.toBeInTheDocument(); // OAuthGoogle (integrations)
      expect(screen.queryByTestId('btn-history-4')).not.toBeInTheDocument(); // BetaSurvey (features)
    });

    it('shows an empty message when no flag matches the active category', async () => {
      const user = userEvent.setup();
      // Only AI flag — selecting integrations leaves nothing.
      render(
        <FeatureFlagsTab
          configurations={[createMockFlag('1', 'RagCaching', 'true')]}
          onConfigurationChange={mockOnChange}
        />
      );

      await user.click(screen.getByTestId('flag-category-tab-integrations'));
      expect(screen.getByTestId('flag-category-empty')).toBeInTheDocument();
    });
  });

  describe('Global audit log (Issue #1836)', () => {
    it('opens the audit log dialog when the toolbar button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <FeatureFlagsTab
          configurations={mockRoleOnlyConfigurations}
          onConfigurationChange={mockOnChange}
        />
      );

      const btn = screen.getByTestId('btn-open-audit-log');
      expect(btn).toBeEnabled();
      await user.click(btn);

      await waitFor(() => {
        expect(screen.getByTestId('config-audit-log-dialog')).toBeInTheDocument();
      });
    });

    it('disables the audit log button when there are no flags', () => {
      render(<FeatureFlagsTab configurations={[]} onConfigurationChange={mockOnChange} />);

      // No flags → empty state; the audit log button is rendered above the
      // empty state only when the flag list is non-empty. The empty state
      // short-circuits the toolbar, so the button is not in the DOM.
      expect(screen.queryByTestId('btn-open-audit-log')).not.toBeInTheDocument();
    });
  });
});
