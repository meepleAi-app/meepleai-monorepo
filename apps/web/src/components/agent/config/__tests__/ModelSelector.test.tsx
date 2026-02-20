/**
 * ModelSelector Component Tests
 * Issue #4775: ModelSelector API Integration
 *
 * Tests the ModelSelector connected to real API via React Query.
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { Mock } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAiModels: vi.fn(),
    },
  },
}));

import { ModelSelector } from '../ModelSelector';
import { api } from '@/lib/api';

const mockModelsResponse = {
  items: [
    {
      id: 'model-1',
      name: 'gpt-4o-mini',
      displayName: 'GPT-4o Mini',
      provider: 'openai',
      modelIdentifier: 'openai/gpt-4o-mini',
      isPrimary: true,
      status: 'active',
      cost: { inputCostPer1kTokens: 0.00015, outputCostPer1kTokens: 0.0006, currency: 'USD' },
      temperature: 0.7,
      maxTokens: 4096,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: null,
    },
    {
      id: 'model-2',
      name: 'claude-3-haiku',
      displayName: 'Claude 3 Haiku',
      provider: 'anthropic',
      modelIdentifier: 'anthropic/claude-3-haiku',
      isPrimary: false,
      status: 'active',
      cost: { inputCostPer1kTokens: 0.00025, outputCostPer1kTokens: 0.00125, currency: 'USD' },
      temperature: 0.7,
      maxTokens: 4096,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: null,
    },
    {
      id: 'model-3',
      name: 'llama-3.3-70b',
      displayName: 'Llama 3.3 70B',
      provider: 'meta',
      modelIdentifier: 'meta-llama/llama-3.3-70b',
      isPrimary: false,
      status: 'active',
      cost: { inputCostPer1kTokens: 0.0, outputCostPer1kTokens: 0.0, currency: 'USD' },
      temperature: 0.7,
      maxTokens: 4096,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: null,
    },
  ],
  total: 3,
  page: 1,
  pageSize: 50,
};

describe('ModelSelector', () => {
  let queryClient: QueryClient;
  const mockOnChange = vi.fn();

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  describe('Loading State', () => {
    it('shows loading state while fetching', () => {
      (api.admin.getAiModels as Mock).mockImplementation(() => new Promise(() => {}));

      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      expect(screen.getByText('Loading models...')).toBeInTheDocument();
    });
  });

  describe('Loaded State', () => {
    beforeEach(() => {
      (api.admin.getAiModels as Mock).mockResolvedValue(mockModelsResponse);
    });

    it('renders AI Model label', async () => {
      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('AI Model')).toBeInTheDocument();
      });
    });

    it('renders required asterisk', async () => {
      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('*')).toBeInTheDocument();
      });
    });

    it('renders placeholder text when no value', async () => {
      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('Choose AI model...')).toBeInTheDocument();
      });
    });

    it('opens dropdown with models when clicked', async () => {
      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading models...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('GPT-4o Mini')).toBeInTheDocument();
      expect(screen.getByText('Claude 3 Haiku')).toBeInTheDocument();
      expect(screen.getByText('Llama 3.3 70B')).toBeInTheDocument();
    });

    it('shows provider names', async () => {
      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading models...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('OpenAI')).toBeInTheDocument();
      expect(screen.getByText('Anthropic')).toBeInTheDocument();
      expect(screen.getByText('Meta')).toBeInTheDocument();
    });

    it('shows Default badge for primary model', async () => {
      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading models...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('Default')).toBeInTheDocument();
    });

    it('shows Free for zero-cost models', async () => {
      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading models...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));

      // Llama is free - shown as "Free/1K" in the cost column
      expect(screen.getAllByText('Free/1K').length).toBeGreaterThanOrEqual(1);
    });

    it('calls onChange with modelId and model object when selected', async () => {
      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading models...')).not.toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('combobox'));
      fireEvent.click(screen.getByText('Claude 3 Haiku'));

      expect(mockOnChange).toHaveBeenCalledWith('model-2', expect.objectContaining({
        id: 'model-2',
        displayName: 'Claude 3 Haiku',
      }));
    });

    it('shows selected model in trigger when value provided', async () => {
      render(<ModelSelector value="model-1" onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading models...')).not.toBeInTheDocument();
      });

      expect(screen.getByText('GPT-4o Mini')).toBeInTheDocument();
      expect(screen.queryByText('Choose AI model...')).not.toBeInTheDocument();
    });

    it('shows cost details when model selected', async () => {
      render(<ModelSelector value="model-2" onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading models...')).not.toBeInTheDocument();
      });

      // Cost summary below the selector
      expect(screen.getByText(/Input:/)).toBeInTheDocument();
      expect(screen.getByText(/Output:/)).toBeInTheDocument();
    });

    it('shows help text when no model selected', async () => {
      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading models...')).not.toBeInTheDocument();
      });

      expect(screen.getByText(/Model determines response quality and cost/)).toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('shows error message when API fails', async () => {
      // Use 401 error to bypass useAiModels custom retry logic
      (api.admin.getAiModels as Mock).mockRejectedValue(new Error('401 Unauthorized'));

      render(<ModelSelector onChange={mockOnChange} />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('Failed to load models. Please try again.')).toBeInTheDocument();
      });
    });
  });

  describe('Disabled State', () => {
    it('disables the select when disabled prop is true', async () => {
      (api.admin.getAiModels as Mock).mockResolvedValue(mockModelsResponse);

      render(<ModelSelector onChange={mockOnChange} disabled />, { wrapper });

      await waitFor(() => {
        expect(screen.queryByText('Loading models...')).not.toBeInTheDocument();
      });

      const trigger = screen.getByRole('combobox');
      expect(trigger).toBeDisabled();
    });
  });
});
