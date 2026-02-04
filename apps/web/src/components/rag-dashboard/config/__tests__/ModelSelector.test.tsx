/**
 * @fileoverview Tests for ModelSelector component
 * @description Tests LLM model selection controls
 */

import React from 'react';

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ModelSelector } from '../ModelSelector';
import { LLM_MODELS } from '../types';
import type { ModelSelection } from '../types';

describe('ModelSelector', () => {
  const defaultSelection: ModelSelection = {
    primaryModel: 'gpt-4o',
    fallbackModel: 'claude-3-haiku',
    evaluationModel: null,
  };

  const mockOnChange = vi.fn();

  beforeEach(() => {
    mockOnChange.mockClear();
  });

  it('renders primary and fallback model selectors', () => {
    render(<ModelSelector selection={defaultSelection} onChange={mockOnChange} />);

    expect(screen.getByText('Primary Model')).toBeInTheDocument();
    expect(screen.getByText('Fallback Model')).toBeInTheDocument();
  });

  it('does not render evaluation model selector by default', () => {
    render(<ModelSelector selection={defaultSelection} onChange={mockOnChange} />);

    expect(screen.queryByText('Evaluation Model')).not.toBeInTheDocument();
  });

  it('renders evaluation model selector when showEvaluationModel is true', () => {
    render(
      <ModelSelector
        selection={defaultSelection}
        onChange={mockOnChange}
        showEvaluationModel={true}
      />
    );

    expect(screen.getByText('Evaluation Model')).toBeInTheDocument();
  });

  it('shows tooltips for all model selectors', () => {
    render(
      <ModelSelector
        selection={defaultSelection}
        onChange={mockOnChange}
        showEvaluationModel={true}
      />
    );

    const labels = screen.getAllByText(/Primary Model|Fallback Model|Evaluation Model/);
    labels.forEach((label) => {
      expect(label.closest('label')).toHaveClass('cursor-help');
    });
  });

  it('calls onChange when primary model changes', async () => {
    const user = userEvent.setup();
    render(<ModelSelector selection={defaultSelection} onChange={mockOnChange} />);

    // Find all select triggers (primary and fallback)
    const selectTriggers = screen.getAllByRole('combobox');
    const primaryTrigger = selectTriggers[0];

    await user.click(primaryTrigger);

    // Find another model option
    const otherModel = LLM_MODELS.find((m) => m.id !== defaultSelection.primaryModel);
    if (otherModel) {
      const option = screen.getByRole('option', { name: otherModel.name });
      await user.click(option);

      expect(mockOnChange).toHaveBeenCalledWith({
        ...defaultSelection,
        primaryModel: otherModel.id,
      });
    }
  });

  it('calls onChange when fallback model changes', async () => {
    const user = userEvent.setup();
    render(<ModelSelector selection={defaultSelection} onChange={mockOnChange} />);

    const selectTriggers = screen.getAllByRole('combobox');
    const fallbackTrigger = selectTriggers[1];

    await user.click(fallbackTrigger);

    // Select "No fallback"
    const noFallbackOption = screen.getByRole('option', { name: 'No fallback' });
    await user.click(noFallbackOption);

    expect(mockOnChange).toHaveBeenCalledWith({
      ...defaultSelection,
      fallbackModel: null,
    });
  });

  it('calls onChange when evaluation model changes', async () => {
    const user = userEvent.setup();
    render(
      <ModelSelector
        selection={defaultSelection}
        onChange={mockOnChange}
        showEvaluationModel={true}
      />
    );

    const selectTriggers = screen.getAllByRole('combobox');
    const evaluationTrigger = selectTriggers[2];

    await user.click(evaluationTrigger);

    // Select a model for evaluation
    const model = LLM_MODELS[0];
    const option = screen.getByRole('option', { name: model.name });
    await user.click(option);

    expect(mockOnChange).toHaveBeenCalledWith({
      ...defaultSelection,
      evaluationModel: model.id,
    });
  });

  it('applies custom className', () => {
    const { container } = render(
      <ModelSelector
        selection={defaultSelection}
        onChange={mockOnChange}
        className="custom-class"
      />
    );

    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('disables primary model in fallback selector', async () => {
    const user = userEvent.setup();
    render(<ModelSelector selection={defaultSelection} onChange={mockOnChange} />);

    const selectTriggers = screen.getAllByRole('combobox');
    const fallbackTrigger = selectTriggers[1];

    await user.click(fallbackTrigger);

    // The primary model should be disabled in fallback list (exact match)
    const primaryModelOptions = screen.getAllByRole('option', { name: /GPT-4o/ });
    // The first one matching exactly "GPT-4o" (not "GPT-4o Mini") should be disabled
    const exactMatch = primaryModelOptions.find((option) =>
      option.textContent?.trim() === 'GPT-4o'
    );
    expect(exactMatch).toHaveAttribute('data-disabled');
  });

  it('groups models by provider', async () => {
    const user = userEvent.setup();
    render(<ModelSelector selection={defaultSelection} onChange={mockOnChange} />);

    const selectTriggers = screen.getAllByRole('combobox');
    await user.click(selectTriggers[0]);

    // Check for provider headers
    const providers = [...new Set(LLM_MODELS.map((m) => m.provider))];
    providers.forEach((provider) => {
      expect(screen.getByText(provider)).toBeInTheDocument();
    });
  });

  it('renders icons for model selectors', () => {
    render(
      <ModelSelector
        selection={defaultSelection}
        onChange={mockOnChange}
        showEvaluationModel={true}
      />
    );

    const svgs = document.querySelectorAll('svg');
    expect(svgs.length).toBeGreaterThanOrEqual(3);
  });

  it('shows "Use primary model" option for evaluation model', async () => {
    const user = userEvent.setup();
    render(
      <ModelSelector
        selection={defaultSelection}
        onChange={mockOnChange}
        showEvaluationModel={true}
      />
    );

    const selectTriggers = screen.getAllByRole('combobox');
    const evaluationTrigger = selectTriggers[2];

    await user.click(evaluationTrigger);

    expect(screen.getByRole('option', { name: 'Use primary model' })).toBeInTheDocument();
  });

  it('preserves other selections when one changes', async () => {
    const user = userEvent.setup();
    render(
      <ModelSelector
        selection={defaultSelection}
        onChange={mockOnChange}
        showEvaluationModel={true}
      />
    );

    const selectTriggers = screen.getAllByRole('combobox');
    const evaluationTrigger = selectTriggers[2];

    await user.click(evaluationTrigger);

    const model = LLM_MODELS[0];
    const option = screen.getByRole('option', { name: model.name });
    await user.click(option);

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.primaryModel).toBe(defaultSelection.primaryModel);
    expect(lastCall.fallbackModel).toBe(defaultSelection.fallbackModel);
  });

  it('renders with null fallback model', () => {
    const selectionNoFallback = {
      ...defaultSelection,
      fallbackModel: null,
    };

    render(
      <ModelSelector selection={selectionNoFallback} onChange={mockOnChange} />
    );

    // Should show "No fallback" in the trigger
    expect(screen.getByText('No fallback')).toBeInTheDocument();
  });

  it('renders with null evaluation model', () => {
    render(
      <ModelSelector
        selection={defaultSelection}
        onChange={mockOnChange}
        showEvaluationModel={true}
      />
    );

    // Should show "Use primary model" in the trigger
    expect(screen.getByText('Use primary model')).toBeInTheDocument();
  });
});
