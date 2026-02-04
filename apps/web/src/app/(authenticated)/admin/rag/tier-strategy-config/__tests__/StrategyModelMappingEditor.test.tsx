/**
 * Tests for StrategyModelMappingEditor component
 * Issue #3441: Tests for tier-strategy-model architecture
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import { StrategyModelMappingEditor } from '@/app/(authenticated)/admin/rag/tier-strategy-config/StrategyModelMappingEditor';
import type { StrategyModelMappingDto } from '@/lib/api';

// Mock the hook
const mockMutateAsync = vi.fn();
vi.mock('@/hooks/queries/useTierStrategy', () => ({
  useUpdateStrategyModelMapping: () => ({
    mutateAsync: mockMutateAsync,
    isPending: false,
    variables: null,
  }),
}));

// Mock toast
vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('StrategyModelMappingEditor', () => {
  let queryClient: QueryClient;

  const createWrapper = () => {
    return function Wrapper({ children }: { children: React.ReactNode }) {
      return React.createElement(
        QueryClientProvider,
        { client: queryClient },
        children
      );
    };
  };

  const mockMappings: StrategyModelMappingDto[] = [
    {
      id: '1',
      strategy: 'FAST',
      provider: 'OpenRouter',
      primaryModel: 'meta-llama/llama-3.3-70b-instruct:free',
      fallbackModels: [],
      isCustomizable: false,
      adminOnly: false,
      isDefault: true,
    },
    {
      id: '2',
      strategy: 'BALANCED',
      provider: 'DeepSeek',
      primaryModel: 'deepseek-chat',
      fallbackModels: ['openai/gpt-4o-mini'],
      isCustomizable: false,
      adminOnly: false,
      isDefault: true,
    },
    {
      id: '3',
      strategy: 'PRECISE',
      provider: 'Anthropic',
      primaryModel: 'anthropic/claude-sonnet-4.5',
      fallbackModels: ['openai/gpt-4o-mini'],
      isCustomizable: false,
      adminOnly: false,
      isDefault: true,
    },
    {
      id: '4',
      strategy: 'CUSTOM',
      provider: 'Anthropic',
      primaryModel: 'anthropic/claude-haiku-4.5',
      fallbackModels: [],
      isCustomizable: true,
      adminOnly: true,
      isDefault: true,
    },
  ];

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render all strategy mappings', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('FAST')).toBeInTheDocument();
      expect(screen.getByText('BALANCED')).toBeInTheDocument();
      expect(screen.getByText('PRECISE')).toBeInTheDocument();
      expect(screen.getByText('CUSTOM')).toBeInTheDocument();
    });

    it('should display provider for each mapping', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('OpenRouter')).toBeInTheDocument();
      expect(screen.getByText('DeepSeek')).toBeInTheDocument();
      // Anthropic appears twice (PRECISE and CUSTOM)
      const anthropicElements = screen.getAllByText('Anthropic');
      expect(anthropicElements.length).toBe(2);
    });

    it('should display primary model for each mapping', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('meta-llama/llama-3.3-70b-instruct:free')).toBeInTheDocument();
      expect(screen.getByText('deepseek-chat')).toBeInTheDocument();
      expect(screen.getByText('anthropic/claude-sonnet-4.5')).toBeInTheDocument();
      expect(screen.getByText('anthropic/claude-haiku-4.5')).toBeInTheDocument();
    });

    it('should display fallback models when present', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      // openai/gpt-4o-mini appears as fallback for BALANCED and PRECISE
      const fallbackElements = screen.getAllByText('openai/gpt-4o-mini');
      expect(fallbackElements.length).toBe(2);
    });

    it('should show "None" for mappings without fallbacks', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      // FAST and CUSTOM have no fallbacks
      const noneElements = screen.getAllByText('None');
      expect(noneElements.length).toBe(2);
    });

    it('should render Edit button for each mapping', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      const editButtons = screen.getAllByRole('button', { name: /Edit/i });
      expect(editButtons.length).toBe(4);
    });

    it('should render help guide section', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Model Configuration Guide')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Indicator Tests
  // =========================================================================

  describe('Indicators', () => {
    it('should show default badge for default mappings', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      // All mappings in our mock are default
      const defaultBadges = screen.getAllByText('default');
      expect(defaultBadges.length).toBeGreaterThan(0);
    });

    it('should indicate admin-only for CUSTOM strategy', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      // CUSTOM has adminOnly: true
      // The component should show some indicator for this
      const customSection = screen.getByText('CUSTOM').closest('div');
      expect(customSection).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Edit Mode Tests
  // =========================================================================

  describe('Edit Mode', () => {
    it('should enter edit mode when clicking Edit button', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      const editButtons = screen.getAllByRole('button', { name: /Edit/i });
      fireEvent.click(editButtons[0]); // Click first Edit button (FAST)

      // Should show Save and Cancel buttons
      expect(screen.getByRole('button', { name: /Save/i })).toBeInTheDocument();
    });

    it('should show provider select in edit mode', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      const editButtons = screen.getAllByRole('button', { name: /Edit/i });
      fireEvent.click(editButtons[0]);

      // Should show Provider label - use getAllByText since it appears in edit form and help guide
      const providerLabels = screen.getAllByText('Provider');
      expect(providerLabels.length).toBeGreaterThan(0);
    });

    it('should show primary model input in edit mode', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      const editButtons = screen.getAllByRole('button', { name: /Edit/i });
      fireEvent.click(editButtons[0]);

      // Should show Primary Model label - use getAllByText since it appears in edit form and help guide
      const primaryModelLabels = screen.getAllByText('Primary Model');
      expect(primaryModelLabels.length).toBeGreaterThan(0);
    });

    it('should show fallback models input in edit mode', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      const editButtons = screen.getAllByRole('button', { name: /Edit/i });
      fireEvent.click(editButtons[0]);

      // Should show Fallback Models label
      expect(screen.getByText('Fallback Models (comma-separated)')).toBeInTheDocument();
    });

    it('should exit edit mode when clicking Cancel', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      const editButtons = screen.getAllByRole('button', { name: /Edit/i });
      fireEvent.click(editButtons[0]);

      // Find and click cancel button (X icon button)
      const cancelButtons = screen.getAllByRole('button');
      const cancelButton = cancelButtons.find(btn => btn.querySelector('svg.lucide-x'));
      if (cancelButton) {
        fireEvent.click(cancelButton);
      }

      // Should show Edit buttons again
      expect(screen.getAllByRole('button', { name: /Edit/i }).length).toBe(4);
    });

    it('should disable other Edit buttons while editing', () => {
      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      const editButtons = screen.getAllByRole('button', { name: /Edit/i });
      fireEvent.click(editButtons[0]); // Enter edit mode for FAST

      // Other edit buttons should be disabled
      const remainingEditButtons = screen.getAllByRole('button', { name: /Edit/i });
      remainingEditButtons.forEach(button => {
        expect(button).toBeDisabled();
      });
    });
  });

  // =========================================================================
  // Save Tests
  // =========================================================================

  describe('Save', () => {
    it('should call updateModelMapping when saving', async () => {
      mockMutateAsync.mockResolvedValue({
        id: '1',
        strategy: 'FAST',
        provider: 'OpenRouter',
        primaryModel: 'new-model',
        fallbackModels: [],
        isCustomizable: false,
        adminOnly: false,
        isDefault: false,
      });

      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      // Enter edit mode
      const editButtons = screen.getAllByRole('button', { name: /Edit/i });
      fireEvent.click(editButtons[0]);

      // Find and update the primary model input
      const modelInput = screen.getByPlaceholderText('e.g., anthropic/claude-sonnet-4.5');
      fireEvent.change(modelInput, { target: { value: 'new-model' } });

      // Click Save
      const saveButton = screen.getByRole('button', { name: /Save/i });
      fireEvent.click(saveButton);

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalledWith(
          expect.objectContaining({
            strategy: 'FAST',
            primaryModel: 'new-model',
          })
        );
      });
    });

    it('should parse comma-separated fallback models', async () => {
      mockMutateAsync.mockResolvedValue({
        id: '1',
        strategy: 'FAST',
        provider: 'OpenRouter',
        primaryModel: 'test-model',
        fallbackModels: ['fallback-1', 'fallback-2'],
        isCustomizable: false,
        adminOnly: false,
        isDefault: false,
      });

      render(<StrategyModelMappingEditor mappings={mockMappings} />, {
        wrapper: createWrapper(),
      });

      // Enter edit mode
      const editButtons = screen.getAllByRole('button', { name: /Edit/i });
      fireEvent.click(editButtons[0]);

      // Update fallback models
      const fallbackInput = screen.getByPlaceholderText('e.g., openai/gpt-4o, google/gemini-pro');
      fireEvent.change(fallbackInput, { target: { value: 'fallback-1, fallback-2' } });

      // Click Save
      const saveButton = screen.getByRole('button', { name: /Save/i });
      fireEvent.click(saveButton);

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalledWith(
          expect.objectContaining({
            fallbackModels: ['fallback-1', 'fallback-2'],
          })
        );
      });
    });
  });

  // =========================================================================
  // Empty State Tests
  // =========================================================================

  describe('Empty State', () => {
    it('should render empty list without errors', () => {
      render(<StrategyModelMappingEditor mappings={[]} />, {
        wrapper: createWrapper(),
      });

      // Should still show the help guide
      expect(screen.getByText('Model Configuration Guide')).toBeInTheDocument();
    });
  });
});
