/**
 * ModelSelector Component Tests
 * Issue #3239: [FRONT-003] AI model selection with tier filtering
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock stores
vi.mock('@/stores/agentStore', () => ({
  useAgentStore: vi.fn(() => ({
    selectedModelId: null,
    setSelectedModel: vi.fn(),
  })),
}));

// Import after mocks
import { ModelSelector } from '../ModelSelector';
import { useAgentStore } from '@/stores/agentStore';

describe('ModelSelector', () => {
  const mockSetSelectedModel = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useAgentStore).mockReturnValue({
      selectedModelId: null,
      setSelectedModel: mockSetSelectedModel,
    } as unknown as ReturnType<typeof useAgentStore>);
  });

  describe('Rendering', () => {
    it('renders AI Model label', () => {
      render(<ModelSelector />);
      expect(screen.getByText('AI Model')).toBeInTheDocument();
    });

    it('renders required asterisk', () => {
      render(<ModelSelector />);
      expect(screen.getByText('*')).toBeInTheDocument();
    });

    it('renders placeholder text', () => {
      render(<ModelSelector />);
      expect(screen.getByText('Choose AI model...')).toBeInTheDocument();
    });

    it('renders help text about model selection', () => {
      render(<ModelSelector />);
      expect(
        screen.getByText(/Model determines response quality and cost/)
      ).toBeInTheDocument();
    });
  });

  describe('Select Dropdown', () => {
    it('opens dropdown when trigger clicked', () => {
      render(<ModelSelector />);

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('GPT-4o Mini')).toBeInTheDocument();
      expect(screen.getByText('Llama 3.3')).toBeInTheDocument();
      expect(screen.getByText('GPT-4')).toBeInTheDocument();
      expect(screen.getByText('Claude 3 Opus')).toBeInTheDocument();
    });

    it('shows cost per query for each model', () => {
      render(<ModelSelector />);

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByText('$0.0005/query')).toBeInTheDocument();
      expect(screen.getByText('$0.0003/query')).toBeInTheDocument();
      expect(screen.getByText('$0.003/query')).toBeInTheDocument();
      expect(screen.getByText('$0.015/query')).toBeInTheDocument();
    });

    it('shows Premium badge for premium models', () => {
      render(<ModelSelector />);

      fireEvent.click(screen.getByRole('combobox'));

      const premiumBadges = screen.getAllByText('Premium');
      expect(premiumBadges).toHaveLength(2); // GPT-4 and Claude 3 Opus
    });

    it('calls setSelectedModel when model is selected', () => {
      render(<ModelSelector />);

      fireEvent.click(screen.getByRole('combobox'));
      fireEvent.click(screen.getByText('GPT-4o Mini'));

      expect(mockSetSelectedModel).toHaveBeenCalledWith('gpt-4o-mini');
    });
  });

  describe('Model List', () => {
    it('renders all available models', () => {
      render(<ModelSelector />);

      fireEvent.click(screen.getByRole('combobox'));

      expect(screen.getByRole('listbox')).toBeInTheDocument();
      expect(screen.getAllByRole('option')).toHaveLength(4);
    });

    it('renders free tier models without Premium badge', () => {
      render(<ModelSelector />);

      fireEvent.click(screen.getByRole('combobox'));

      // Free models don't have the Premium badge
      // GPT-4o Mini and Llama 3.3 are free
      expect(screen.getByText('GPT-4o Mini')).toBeInTheDocument();
      expect(screen.getByText('Llama 3.3')).toBeInTheDocument();
    });
  });

  describe('Selected State', () => {
    it('hides placeholder when model is selected', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedModelId: 'gpt-4o-mini',
        setSelectedModel: mockSetSelectedModel,
      } as unknown as ReturnType<typeof useAgentStore>);

      render(<ModelSelector />);

      expect(screen.queryByText('Choose AI model...')).not.toBeInTheDocument();
    });
  });
});
