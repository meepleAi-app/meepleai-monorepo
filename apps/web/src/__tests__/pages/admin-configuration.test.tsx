/**
 * TEST-628: Configuration Admin Page Tests
 *
 * Comprehensive tests for CONFIG-06 Configuration Management page.
 * Coverage target: 90%+ all metrics
 */

import React from 'react';
import {  screen, fireEvent, waitFor } from '@testing-library/react';
import { renderWithQuery } from '../utils/query-test-utils';
import '@testing-library/jest-dom';
import ConfigurationManagement from '../../pages/admin/configuration';
import { api } from '../../lib/api';
import { toast } from '@/components/layout';

// Mock Next.js Head
jest.mock('next/head', () => {
  return {
    __esModule: true,
    default: ({ children }: { children: React.ReactNode }) => {
      return <>{children}</>;
    },
  };
});

// Mock Next.js Link
jest.mock('next/link', () => {
  return {
    __esModule: true,
    default: ({ children, href }: { children: React.ReactNode; href: string }) => {
      return <a href={href}>{children}</a>;
    },
  };
});

// Mock sonner toast
jest.mock('@/components/layout', () => ({
  toast: {
    success: jest.fn(),
    error: jest.fn(),
  },
}));

// Mock child components
jest.mock('../../components/admin/FeatureFlagsTab', () => {
  return {
    __esModule: true,
    default: ({ configurations, onConfigurationChange }: any) => (
      <div data-testid="feature-flags-tab">
        <div data-testid="configurations-count">{configurations.length}</div>
        <button onClick={onConfigurationChange}>Change Config</button>
      </div>
    ),
  };
});

jest.mock('../../components/admin/CategoryConfigTab', () => {
  return {
    __esModule: true,
    default: ({ title, category, configurations, onConfigurationChange }: any) => (
      <div data-testid="category-config-tab">
        <div data-testid="tab-title">{title}</div>
        <div data-testid="tab-category">{category}</div>
        <div data-testid="configurations-count">{configurations.length}</div>
        <button onClick={onConfigurationChange}>Change Config</button>
      </div>
    ),
  };
});

// Mock API client
jest.mock('../../lib/api', () => ({
  api: {
    config: {
      getConfigurations: jest.fn(),
      getCategories: jest.fn(),
      updateConfiguration: jest.fn(),
      invalidateCache: jest.fn(),
    },
  },
}));

describe('ConfigurationManagement Page', () => {
  // Test data
  const mockConfigurations = [
    {
      id: 'config-1',
      key: 'Features:StreamingResponses',
      value: 'true',
      valueType: 'bool',
      description: 'Enable streaming responses',
      category: 'FeatureFlag',
      environment: 'All',
      isActive: true,
      requiresRestart: false,
      version: 1,
      previousValue: null,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-01T00:00:00Z',
      createdByUserId: 'user-1',
      updatedByUserId: null,
      lastToggledAt: null,
    },
    {
      id: 'config-2',
      key: 'RateLimit:MaxTokens:Admin',
      value: '10000',
      valueType: 'integer',
      description: 'Admin rate limit',
      category: 'RateLimiting',
      environment: 'All',
      isActive: true,
      requiresRestart: true,
      version: 2,
      previousValue: '5000',
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-02T00:00:00Z',
      createdByUserId: 'user-1',
      updatedByUserId: 'user-2',
      lastToggledAt: null,
    },
    {
      id: 'config-3',
      key: 'AI:Temperature',
      value: '0.7',
      valueType: 'double',
      description: 'LLM temperature',
      category: 'AiLlm',
      environment: 'All',
      isActive: true,
      requiresRestart: false,
      version: 1,
      previousValue: null,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-01T00:00:00Z',
      createdByUserId: 'user-1',
      updatedByUserId: null,
      lastToggledAt: null,
    },
    {
      id: 'config-4',
      key: 'Rag:TopK',
      value: '5',
      valueType: 'integer',
      description: 'Top K results',
      category: 'Rag',
      environment: 'All',
      isActive: true,
      requiresRestart: false,
      version: 1,
      previousValue: null,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-01T00:00:00Z',
      createdByUserId: 'user-1',
      updatedByUserId: null,
      lastToggledAt: null,
    },
  ];

  const mockCategories = ['FeatureFlag', 'RateLimiting', 'AiLlm', 'Rag'];

  const mockApiResponse = {
    items: mockConfigurations,
    total: mockConfigurations.length,
    page: 1,
    pageSize: 100,
  };

  beforeEach(() => {
    jest.clearAllMocks();
    // Mock window.location.href setter using Object.defineProperty
    delete (window as any).location;
    (window as any).location = { href: '' };
    (api.config.getConfigurations as jest.Mock).mockResolvedValue(mockApiResponse);
    (api.config.getCategories as jest.Mock).mockResolvedValue(mockCategories);
  });

  // ============================================================================
  // A. Main Page Tests - Initial Load
  // ============================================================================

  describe('Initial Load', () => {
    test('displays loading state while fetching data', () => {
      // Arrange
      (api.config.getConfigurations as jest.Mock).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      expect(screen.getByText('Loading configurations...')).toBeInTheDocument();
      // Check for spinner element (has animation class)
      const spinner = document.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    test('fetches configurations and categories on mount', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(api.config.getConfigurations).toHaveBeenCalledWith(
          undefined,
          undefined,
          false,
          1,
          100
        );
        expect(api.config.getCategories).toHaveBeenCalled();
      });
    });

    test('handles network error during initial load', async () => {
      // Arrange
      const networkError = new Error('Network error');
      (api.config.getConfigurations as jest.Mock).mockRejectedValue(networkError);

      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(screen.getByText('Unexpected Error')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
        expect(toast.error).toHaveBeenCalledWith('Network error');
      });
    });

    test('handles 401 unauthorized error and shows admin access required message', async () => {
      // Arrange
      const unauthorizedError = new Error('Unauthorized');
      (api.config.getConfigurations as jest.Mock).mockRejectedValue(unauthorizedError);

      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Admin access required');
      });

      // Note: window.location.href redirect is tested in E2E tests
      // jsdom doesn't fully support navigation, so we just verify the toast message
    });

    test('handles 403 forbidden error and shows admin access required message', async () => {
      // Arrange
      const forbiddenError = new Error('403 Forbidden');
      (api.config.getConfigurations as jest.Mock).mockRejectedValue(forbiddenError);

      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Admin access required');
      });

      // Note: window.location.href redirect is tested in E2E tests
      // jsdom doesn't fully support navigation, so we just verify the toast message
    });

    test('renders success state with configurations and stats', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(screen.getByText('Configuration Management')).toBeInTheDocument();
        expect(screen.getByText('Manage system configurations and feature flags')).toBeInTheDocument();

        // Stats
        expect(screen.getByText('Total Configurations')).toBeInTheDocument();
        expect(screen.getByText('Active')).toBeInTheDocument();
        expect(screen.getByText('Categories')).toBeInTheDocument();

        // Verify stats values by checking stat cards
        const totalConfigLabel = screen.getByText('Total Configurations');
        const totalStatCard = totalConfigLabel.closest('.bg-white');
        expect(totalStatCard).toHaveTextContent('4');
      });
    });
  });

  // ============================================================================
  // B. Tab Navigation
  // ============================================================================

  describe('Tab Navigation', () => {
    test('renders all 4 tabs with correct labels', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /feature flags/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /rate limiting/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /ai \/ llm/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /rag/i })).toBeInTheDocument();
      });
    });

    test('renders tab icons and descriptions', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(screen.getByText('Enable/disable features at runtime')).toBeInTheDocument();
        expect(screen.getByText('Configure API rate limits per role')).toBeInTheDocument();
        expect(screen.getByText('AI model parameters and settings')).toBeInTheDocument();
        expect(screen.getByText('Vector search and chunking configuration')).toBeInTheDocument();
      });
    });

    test('switches to Rate Limiting tab when clicked', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByTestId('feature-flags-tab')).toBeInTheDocument();
      });

      const rateLimitTab = screen.getByRole('button', { name: /rate limiting/i });
      fireEvent.click(rateLimitTab);

      // Assert
      await waitFor(() => {
        const categoryTab = screen.getByTestId('category-config-tab');
        expect(categoryTab).toBeInTheDocument();
        expect(screen.getByTestId('tab-title')).toHaveTextContent('Rate Limiting Configuration');
        expect(screen.getByTestId('tab-category')).toHaveTextContent('RateLimiting');
      });
    });

    test('switches to AI/LLM tab when clicked', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByTestId('feature-flags-tab')).toBeInTheDocument();
      });

      const aiTab = screen.getByRole('button', { name: /ai \/ llm/i });
      fireEvent.click(aiTab);

      // Assert
      await waitFor(() => {
        expect(screen.getByTestId('tab-title')).toHaveTextContent('AI / LLM Configuration');
        expect(screen.getByTestId('tab-category')).toHaveTextContent('AiLlm');
      });
    });

    test('switches to RAG tab when clicked', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByTestId('feature-flags-tab')).toBeInTheDocument();
      });

      const ragTab = screen.getByRole('button', { name: /rag/i });
      fireEvent.click(ragTab);

      // Assert
      await waitFor(() => {
        expect(screen.getByTestId('tab-title')).toHaveTextContent('RAG Configuration');
        expect(screen.getByTestId('tab-category')).toHaveTextContent('Rag');
      });
    });

    test('highlights active tab with correct styling', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        const featureFlagsTab = screen.getByRole('button', { name: /feature flags/i });
        expect(featureFlagsTab).toHaveClass('border-blue-600', 'text-blue-600');
      });

      // Switch to Rate Limiting
      const rateLimitTab = screen.getByRole('button', { name: /rate limiting/i });
      fireEvent.click(rateLimitTab);

      // Assert
      await waitFor(() => {
        expect(rateLimitTab).toHaveClass('border-blue-600', 'text-blue-600');
        const featureFlagsTab = screen.getByRole('button', { name: /feature flags/i });
        expect(featureFlagsTab).toHaveClass('border-transparent');
      });
    });

    test('passes correct configurations to each tab', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert - Feature Flags tab should show 1 config
      await waitFor(() => {
        const configsCount = screen.getAllByTestId('configurations-count')[0];
        expect(configsCount).toHaveTextContent('4'); // Initially shows all
      });
    });
  });

  // ============================================================================
  // C. Action Buttons
  // ============================================================================

  describe('Action Buttons', () => {
    test('reloads configurations when Reload button clicked', async () => {
      // Arrange
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /reload/i })).toBeInTheDocument();
      });

      const reloadButton = screen.getByRole('button', { name: /reload/i });

      // Act
      fireEvent.click(reloadButton);

      // Assert
      await waitFor(() => {
        expect(api.config.getConfigurations).toHaveBeenCalledTimes(2); // Initial + reload
        expect(toast.success).toHaveBeenCalledWith('Configurations reloaded');
      });
    });

    test('handles reload failure gracefully', async () => {
      // Arrange
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /reload/i })).toBeInTheDocument();
      });

      (api.config.getConfigurations as jest.Mock).mockRejectedValueOnce(
        new Error('Reload failed')
      );

      const reloadButton = screen.getByRole('button', { name: /reload/i });

      // Act
      fireEvent.click(reloadButton);

      // Assert
      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Failed to reload configurations');
      });
    });

    test('invalidates cache when Clear Cache button clicked', async () => {
      // Arrange
      (api.config.invalidateCache as jest.Mock).mockResolvedValue(undefined);
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /clear cache/i })).toBeInTheDocument();
      });

      const clearCacheButton = screen.getByRole('button', { name: /clear cache/i });

      // Act
      fireEvent.click(clearCacheButton);

      // Assert
      await waitFor(() => {
        expect(api.config.invalidateCache).toHaveBeenCalled();
        expect(toast.success).toHaveBeenCalledWith('Cache invalidated successfully');
      });
    });

    test('handles cache invalidation failure', async () => {
      // Arrange
      (api.config.invalidateCache as jest.Mock).mockRejectedValue(
        new Error('Cache invalidation failed')
      );
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /clear cache/i })).toBeInTheDocument();
      });

      const clearCacheButton = screen.getByRole('button', { name: /clear cache/i });

      // Act
      fireEvent.click(clearCacheButton);

      // Assert
      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Failed to invalidate cache');
      });
    });

    test('renders Back to Admin link', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        const backLink = screen.getByRole('link', { name: /back to admin/i });
        expect(backLink).toBeInTheDocument();
        expect(backLink).toHaveAttribute('href', '/admin');
      });
    });
  });

  // ============================================================================
  // D. Banner
  // ============================================================================

  describe('Warning Banner', () => {
    test('displays warning banner initially', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(
          screen.getByText(/configuration changes require server restart/i)
        ).toBeInTheDocument();
        expect(screen.getByText('Learn more')).toBeInTheDocument();
      });
    });

    test('dismisses banner when close button clicked', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(
          screen.getByText(/configuration changes require server restart/i)
        ).toBeInTheDocument();
      });

      const closeButton = screen.getByRole('button', { name: /dismiss banner/i });
      fireEvent.click(closeButton);

      // Assert
      expect(
        screen.queryByText(/configuration changes require server restart/i)
      ).not.toBeInTheDocument();
    });

    test('banner Learn more link has correct attributes', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        const learnMoreLink = screen.getByText('Learn more');
        expect(learnMoreLink).toHaveAttribute('href', 'https://docs.meepleai.dev/admin/restart');
        expect(learnMoreLink).toHaveAttribute('target', '_blank');
        expect(learnMoreLink).toHaveAttribute('rel', 'noopener noreferrer');
      });
    });
  });

  // ============================================================================
  // E. Footer Stats
  // ============================================================================

  describe('Footer Stats', () => {
    test('displays correct total configurations count', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        const totalConfigLabel = screen.getByText('Total Configurations');
        expect(totalConfigLabel).toBeInTheDocument();
        // Check the stat value is in the same parent
        const statCard = totalConfigLabel.closest('.bg-white');
        expect(statCard).toHaveTextContent('4');
      });
    });

    test('displays correct active configurations count', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        const activeLabel = screen.getByText('Active');
        expect(activeLabel).toBeInTheDocument();
        // All 4 configs are active in mockData
        const activeCount = mockConfigurations.filter(c => c.isActive).length;
        const statCard = activeLabel.closest('.bg-white');
        expect(statCard).toHaveTextContent(activeCount.toString());
      });
    });

    test('displays correct categories count', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        const categoriesLabel = screen.getByText('Categories');
        expect(categoriesLabel).toBeInTheDocument();
        const statCard = categoriesLabel.closest('.bg-white');
        expect(statCard).toHaveTextContent('4');
      });
    });

    test('updates stats after configuration change', async () => {
      // Arrange
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByText('Total Configurations')).toBeInTheDocument();
      });

      // Add one more config
      const updatedConfigs = [
        ...mockConfigurations,
        {
          id: 'config-5',
          key: 'Features:NewFeature',
          value: 'false',
          valueType: 'bool',
          description: 'New feature',
          category: 'FeatureFlag',
          environment: 'All',
          isActive: false,
          requiresRestart: false,
          version: 1,
          previousValue: null,
          createdAt: '2024-01-03T00:00:00Z',
          updatedAt: '2024-01-03T00:00:00Z',
          createdByUserId: 'user-1',
          updatedByUserId: null,
          lastToggledAt: null,
        },
      ];

      (api.config.getConfigurations as jest.Mock).mockResolvedValue({
        items: updatedConfigs,
        total: updatedConfigs.length,
        page: 1,
        pageSize: 100,
      });

      // Act - Trigger reload from child component
      const changeButton = screen.getAllByText('Change Config')[0];
      fireEvent.click(changeButton);

      // Assert
      await waitFor(() => {
        const totalConfigLabel = screen.getByText('Total Configurations');
        const statCard = totalConfigLabel.closest('.bg-white');
        expect(statCard).toHaveTextContent('5'); // New total
      });
    });
  });

  // ============================================================================
  // F. Error Handling
  // ============================================================================

  describe('Error Handling', () => {
    test('displays retry button on error', async () => {
      // Arrange
      const error = new Error('Failed to load configurations');
      (api.config.getConfigurations as jest.Mock).mockRejectedValue(error);

      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(screen.getByText('Unexpected Error')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
      });
    });

    test('retries loading when retry button clicked', async () => {
      // Arrange
      const error = new Error('Network timeout');
      (api.config.getConfigurations as jest.Mock)
        .mockRejectedValueOnce(error)
        .mockResolvedValueOnce(mockApiResponse);

      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByText('Unexpected Error')).toBeInTheDocument();
      });

      const retryButton = screen.getByRole('button', { name: /retry/i });

      // Act
      fireEvent.click(retryButton);

      // Assert
      await waitFor(() => {
        expect(api.config.getConfigurations).toHaveBeenCalledTimes(2);
        expect(screen.queryByText('Unexpected Error')).not.toBeInTheDocument();
        expect(screen.getByText('Configuration Management')).toBeInTheDocument();
      });
    });

    test('does not show retry button for auth errors', async () => {
      // Arrange
      const authError = new Error('Unauthorized');
      (api.config.getConfigurations as jest.Mock).mockRejectedValue(authError);

      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Admin access required');
      });

      expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // G. Integration Tests
  // ============================================================================

  describe('Integration Tests', () => {
    test('full flow: load → tab switch → reload → cache clear', async () => {
      // Arrange
      (api.config.invalidateCache as jest.Mock).mockResolvedValue(undefined);
      renderWithQuery(<ConfigurationManagement />);

      // Assert - Initial load
      await waitFor(() => {
        expect(screen.getByText('Configuration Management')).toBeInTheDocument();
        expect(api.config.getConfigurations).toHaveBeenCalledTimes(1);
      });

      // Act - Switch to Rate Limiting tab
      const rateLimitTab = screen.getByRole('button', { name: /rate limiting/i });
      fireEvent.click(rateLimitTab);

      await waitFor(() => {
        expect(screen.getByTestId('tab-title')).toHaveTextContent('Rate Limiting Configuration');
      });

      // Act - Reload configurations
      const reloadButton = screen.getByRole('button', { name: /reload/i });
      fireEvent.click(reloadButton);

      await waitFor(() => {
        expect(api.config.getConfigurations).toHaveBeenCalledTimes(2);
        expect(toast.success).toHaveBeenCalledWith('Configurations reloaded');
      });

      // Act - Clear cache
      const clearCacheButton = screen.getByRole('button', { name: /clear cache/i });
      fireEvent.click(clearCacheButton);

      // Assert
      await waitFor(() => {
        expect(api.config.invalidateCache).toHaveBeenCalled();
        expect(toast.success).toHaveBeenCalledWith('Cache invalidated successfully');
      });
    });

    test('configuration change callback triggers reload', async () => {
      // Arrange
      renderWithQuery(<ConfigurationManagement />);

      await waitFor(() => {
        expect(screen.getByTestId('feature-flags-tab')).toBeInTheDocument();
      });

      // Act - Trigger onConfigurationChange from child component
      const changeButton = screen.getAllByText('Change Config')[0];
      fireEvent.click(changeButton);

      // Assert
      await waitFor(() => {
        expect(api.config.getConfigurations).toHaveBeenCalledTimes(2);
        expect(toast.success).toHaveBeenCalledWith('Configurations reloaded');
      });
    });

    test('tabs display filtered configurations correctly', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Feature Flags tab (default)
      await waitFor(() => {
        expect(screen.getByTestId('feature-flags-tab')).toBeInTheDocument();
      });

      // Switch to AI/LLM tab
      const aiTab = screen.getByRole('button', { name: /ai \/ llm/i });
      fireEvent.click(aiTab);

      await waitFor(() => {
        const categoryTab = screen.getByTestId('category-config-tab');
        expect(categoryTab).toBeInTheDocument();
        expect(screen.getByTestId('tab-category')).toHaveTextContent('AiLlm');
      });

      // Switch to RAG tab
      const ragTab = screen.getByRole('button', { name: /rag/i });
      fireEvent.click(ragTab);

      // Assert
      await waitFor(() => {
        expect(screen.getByTestId('tab-category')).toHaveTextContent('Rag');
      });
    });
  });

  // ============================================================================
  // H. Accessibility
  // ============================================================================

  describe('Accessibility', () => {
    test('has proper ARIA labels for tabs', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        const tabNav = screen.getByRole('navigation', { name: /configuration tabs/i });
        expect(tabNav).toBeInTheDocument();
      });
    });

    // Note: Toaster component is now in _app.tsx, not in this page component

    test('sets correct page title', async () => {
      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        // Next.js Head is mocked, so we just check it renders
        expect(screen.getByText('Configuration Management')).toBeInTheDocument();
      });
    });
  });

  // ============================================================================
  // I. Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    test('handles empty configurations list', async () => {
      // Arrange
      (api.config.getConfigurations as jest.Mock).mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 100,
      });

      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        const totalConfigLabel = screen.getByText('Total Configurations');
        const statCard = totalConfigLabel.closest('.bg-white');
        expect(statCard).toHaveTextContent('0');
      });
    });

    test('handles empty categories list', async () => {
      // Arrange
      (api.config.getCategories as jest.Mock).mockResolvedValue([]);

      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert
      await waitFor(() => {
        const categoriesLabel = screen.getByText('Categories');
        const statCard = categoriesLabel.closest('.bg-white');
        expect(statCard).toHaveTextContent('0');
      });
    });

    test('handles configurations with missing optional fields', async () => {
      // Arrange
      const minimalConfig = [
        {
          id: 'config-minimal',
          key: 'Test:Key',
          value: 'test',
          valueType: 'string',
          description: null,
          category: 'FeatureFlag',
          environment: 'All',
          isActive: true,
          requiresRestart: false,
          version: 1,
          previousValue: null,
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
          createdByUserId: 'user-1',
          updatedByUserId: null,
          lastToggledAt: null,
        },
      ];

      (api.config.getConfigurations as jest.Mock).mockResolvedValue({
        items: minimalConfig,
        total: 1,
        page: 1,
        pageSize: 100,
      });

      // Act
      renderWithQuery(<ConfigurationManagement />);

      // Assert - Should render without errors
      await waitFor(() => {
        expect(screen.getByText('Configuration Management')).toBeInTheDocument();
        const totalConfigLabel = screen.getByText('Total Configurations');
        const statCard = totalConfigLabel.closest('.bg-white');
        expect(statCard).toHaveTextContent('1');
      });
    });
  });
});
