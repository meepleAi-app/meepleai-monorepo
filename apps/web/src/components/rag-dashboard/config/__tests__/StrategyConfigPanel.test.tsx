/**
 * @fileoverview Tests for StrategyConfigPanel component
 * @description Tests the main configuration panel container
 */

import React from 'react';

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { StrategyConfigPanel } from '../StrategyConfigPanel';
import { DEFAULT_RAG_CONFIG } from '../types';
import type { RagConfig } from '../types';

describe('StrategyConfigPanel', () => {
  const mockOnChange = vi.fn();
  const mockOnSave = vi.fn();
  const mockOnReset = vi.fn();

  beforeEach(() => {
    mockOnChange.mockClear();
    mockOnSave.mockClear();
    mockOnReset.mockClear();
  });

  it('renders the panel with title', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    expect(screen.getByText('Strategy Configuration')).toBeInTheDocument();
  });

  it('renders all four tabs', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    expect(screen.getByRole('tab', { name: 'Generation' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Retrieval' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Models' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Advanced' })).toBeInTheDocument();
  });

  it('shows Generation tab content by default', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    expect(screen.getByText('Temperature')).toBeInTheDocument();
    expect(screen.getByText('Top-K')).toBeInTheDocument();
  });

  it('switches to Retrieval tab when clicked', async () => {
    const user = userEvent.setup();
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    await user.click(screen.getByRole('tab', { name: 'Retrieval' }));

    expect(screen.getByText('Chunk Size')).toBeInTheDocument();
    expect(screen.getByText('Top Results')).toBeInTheDocument();
  });

  it('switches to Models tab when clicked', async () => {
    const user = userEvent.setup();
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    await user.click(screen.getByRole('tab', { name: 'Models' }));

    expect(screen.getByText('Primary Model')).toBeInTheDocument();
    expect(screen.getByText('Fallback Model')).toBeInTheDocument();
  });

  it('switches to Advanced tab when clicked', async () => {
    const user = userEvent.setup();
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    await user.click(screen.getByRole('tab', { name: 'Advanced' }));

    expect(screen.getByText('Reranker Settings')).toBeInTheDocument();
    expect(screen.getByText('Strategy-Specific Settings')).toBeInTheDocument();
  });

  it('shows current strategy name in Advanced tab', async () => {
    const user = userEvent.setup();
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    await user.click(screen.getByRole('tab', { name: 'Advanced' }));

    expect(screen.getByText('(Hybrid)')).toBeInTheDocument();
  });

  it('renders Reset button', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    expect(screen.getByRole('button', { name: /Reset/ })).toBeInTheDocument();
  });

  it('renders Save button when onSave is provided', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        onSave={mockOnSave}
      />
    );

    expect(screen.getByRole('button', { name: /Save/ })).toBeInTheDocument();
  });

  it('does not render Save button when onSave is not provided', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    expect(screen.queryByRole('button', { name: /Save/ })).not.toBeInTheDocument();
  });

  it('calls onReset when Reset button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        onReset={mockOnReset}
      />
    );

    await user.click(screen.getByRole('button', { name: /Reset/ }));

    expect(mockOnReset).toHaveBeenCalled();
    expect(mockOnChange).toHaveBeenCalled();
  });

  it('calls onSave when Save button is clicked', async () => {
    const user = userEvent.setup();
    mockOnSave.mockResolvedValue(undefined);

    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        onSave={mockOnSave}
        isDirty={true}
      />
    );

    await user.click(screen.getByRole('button', { name: /Save/ }));

    await waitFor(() => {
      expect(mockOnSave).toHaveBeenCalled();
    });
  });

  it('disables Save button when not dirty', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        onSave={mockOnSave}
        isDirty={false}
      />
    );

    expect(screen.getByRole('button', { name: /Save/ })).toBeDisabled();
  });

  it('shows unsaved changes message when isDirty', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        isDirty={true}
      />
    );

    expect(screen.getByText('You have unsaved changes')).toBeInTheDocument();
  });

  it('does not show unsaved changes message when not dirty', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        isDirty={false}
      />
    );

    expect(screen.queryByText('You have unsaved changes')).not.toBeInTheDocument();
  });

  it('shows error alert when error is provided', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        error="Something went wrong"
      />
    );

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        className="custom-class"
      />
    );

    expect(container.querySelector('.custom-class')).toBeInTheDocument();
  });

  it('shows loading overlay when isLoading', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        isLoading={true}
      />
    );

    // Should have spinner when loading
    const loadingSpinner = document.querySelector('.animate-spin');
    expect(loadingSpinner).toBeInTheDocument();
  });

  it('disables buttons when isLoading', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        onSave={mockOnSave}
        isLoading={true}
        isDirty={true}
      />
    );

    expect(screen.getByRole('button', { name: /Reset/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Save/ })).toBeDisabled();
  });

  it('uses initial config when provided', () => {
    const customConfig: RagConfig = {
      ...DEFAULT_RAG_CONFIG,
      generation: {
        ...DEFAULT_RAG_CONFIG.generation,
        temperature: 1.5,
      },
    };

    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        initialConfig={customConfig}
        onChange={mockOnChange}
      />
    );

    expect(screen.getByText('1.5')).toBeInTheDocument();
  });

  it('shows evaluation model in Models tab for Agentic strategy', async () => {
    const user = userEvent.setup();
    render(
      <StrategyConfigPanel
        activeStrategy="Agentic"
        onChange={mockOnChange}
      />
    );

    await user.click(screen.getByRole('tab', { name: 'Models' }));

    expect(screen.getByText('Evaluation Model')).toBeInTheDocument();
  });

  it('shows evaluation model in Models tab for MultiQuery strategy', async () => {
    const user = userEvent.setup();
    render(
      <StrategyConfigPanel
        activeStrategy="MultiQuery"
        onChange={mockOnChange}
      />
    );

    await user.click(screen.getByRole('tab', { name: 'Models' }));

    expect(screen.getByText('Evaluation Model')).toBeInTheDocument();
  });

  it('does not show evaluation model for Hybrid strategy', async () => {
    const user = userEvent.setup();
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    await user.click(screen.getByRole('tab', { name: 'Models' }));

    expect(screen.queryByText('Evaluation Model')).not.toBeInTheDocument();
  });

  it('calls onChange when generation params change', async () => {
    const user = userEvent.setup();
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    // Change Top-K value
    const topKInput = screen.getByDisplayValue('40');
    await user.clear(topKInput);
    await user.type(topKInput, '50');

    expect(mockOnChange).toHaveBeenCalled();
  });

  it('shows settings icon in title', () => {
    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
      />
    );

    // Settings icon should be present
    const svgs = document.querySelectorAll('svg');
    expect(svgs.length).toBeGreaterThanOrEqual(1);
  });

  it('shows saving spinner when save is in progress', async () => {
    const user = userEvent.setup();
    // Make onSave take some time
    mockOnSave.mockImplementation(() => new Promise((resolve) => setTimeout(resolve, 100)));

    render(
      <StrategyConfigPanel
        activeStrategy="Hybrid"
        onChange={mockOnChange}
        onSave={mockOnSave}
        isDirty={true}
      />
    );

    await user.click(screen.getByRole('button', { name: /Save/ }));

    // Should show spinner while saving
    await waitFor(() => {
      const spinners = document.querySelectorAll('.animate-spin');
      expect(spinners.length).toBeGreaterThanOrEqual(1);
    });
  });
});
