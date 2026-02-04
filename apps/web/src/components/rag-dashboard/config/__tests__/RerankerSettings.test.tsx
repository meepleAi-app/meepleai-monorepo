/**
 * @fileoverview Tests for RerankerSettings component
 * @description Tests reranker configuration controls
 */

import React from 'react';

import { render, screen, fireEvent, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { RerankerSettings } from '../RerankerSettings';
import { RERANKER_MODELS } from '../types';
import type { RerankerSettings as RerankerSettingsType } from '../types';

describe('RerankerSettings', () => {
  const defaultSettings: RerankerSettingsType = {
    enabled: true,
    model: 'cross-encoder-ms-marco-minilm-l6',
    topN: 10,
  };

  const mockOnChange = vi.fn();

  beforeEach(() => {
    mockOnChange.mockClear();
  });

  it('renders all setting controls', () => {
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    expect(screen.getByText('Enable Reranker')).toBeInTheDocument();
    expect(screen.getByText('Reranker Model')).toBeInTheDocument();
    expect(screen.getByText('Rerank Top-N')).toBeInTheDocument();
  });

  it('displays enabled state correctly', () => {
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    const switchElement = screen.getByRole('switch');
    expect(switchElement).toBeChecked();
  });

  it('displays disabled state correctly', () => {
    const disabledSettings = { ...defaultSettings, enabled: false };
    render(<RerankerSettings settings={disabledSettings} onChange={mockOnChange} />);

    const switchElement = screen.getByRole('switch');
    expect(switchElement).not.toBeChecked();
  });

  it('calls onChange when toggle is clicked', async () => {
    const user = userEvent.setup();
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    const switchElement = screen.getByRole('switch');
    await user.click(switchElement);

    expect(mockOnChange).toHaveBeenCalledWith({
      ...defaultSettings,
      enabled: false,
    });
  });

  it('displays Top-N value in input', () => {
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    const topNInput = screen.getByDisplayValue('10');
    expect(topNInput).toBeInTheDocument();
  });

  it('calls onChange when Top-N changes', () => {
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    const topNInput = screen.getByDisplayValue('10');
    fireEvent.change(topNInput, { target: { value: '15' } });

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.topN).toBe(15);
  });

  it('clamps Top-N to valid range (3-20)', () => {
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    const topNInput = screen.getByDisplayValue('10');

    // Try setting value above max
    fireEvent.change(topNInput, { target: { value: '25' } });

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.topN).toBe(20);
  });

  it('disables model selector and Top-N when reranker is disabled', () => {
    const disabledSettings = { ...defaultSettings, enabled: false };
    render(<RerankerSettings settings={disabledSettings} onChange={mockOnChange} />);

    // Model selector trigger should be disabled
    const selectTrigger = screen.getByRole('combobox');
    expect(selectTrigger).toBeDisabled();

    // Top-N input should be disabled
    const topNInput = screen.getByDisplayValue('10');
    expect(topNInput).toBeDisabled();
  });

  it('shows tooltips for all settings', () => {
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    const labels = screen.getAllByText(/Enable Reranker|Reranker Model|Rerank Top-N/);
    labels.forEach((label) => {
      expect(label.closest('label')).toHaveClass('cursor-help');
    });
  });

  it('shows helper text for Top-N', () => {
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    expect(screen.getByText(/Reranks top 10 initial results/)).toBeInTheDocument();
  });

  it('updates helper text when Top-N changes', () => {
    const { rerender } = render(
      <RerankerSettings settings={defaultSettings} onChange={mockOnChange} />
    );

    // Change using fireEvent
    const topNInput = screen.getByDisplayValue('10');
    fireEvent.change(topNInput, { target: { value: '15' } });

    // Rerender with updated settings
    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    rerender(<RerankerSettings settings={lastCall} onChange={mockOnChange} />);

    expect(screen.getByText(/Reranks top 15 initial results/)).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <RerankerSettings
        settings={defaultSettings}
        onChange={mockOnChange}
        className="custom-class"
      />
    );

    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('preserves other settings when one changes', async () => {
    const user = userEvent.setup();
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    const topNInput = screen.getByDisplayValue('10');
    await user.clear(topNInput);
    await user.type(topNInput, '12');
    fireEvent.blur(topNInput);

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.enabled).toBe(true);
    expect(lastCall.model).toBe('cross-encoder-ms-marco-minilm-l6');
  });

  it('renders with minimum Top-N value', () => {
    const minSettings = { ...defaultSettings, topN: 3 };
    render(<RerankerSettings settings={minSettings} onChange={mockOnChange} />);

    expect(screen.getByDisplayValue('3')).toBeInTheDocument();
    expect(screen.getByText(/Reranks top 3 initial results/)).toBeInTheDocument();
  });

  it('renders with maximum Top-N value', () => {
    const maxSettings = { ...defaultSettings, topN: 20 };
    render(<RerankerSettings settings={maxSettings} onChange={mockOnChange} />);

    expect(screen.getByDisplayValue('20')).toBeInTheDocument();
    expect(screen.getByText(/Reranks top 20 initial results/)).toBeInTheDocument();
  });

  it('renders icons for each setting', () => {
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    const svgs = document.querySelectorAll('svg');
    expect(svgs.length).toBeGreaterThanOrEqual(2);
  });

  it('handles model change from select', async () => {
    const user = userEvent.setup();
    render(<RerankerSettings settings={defaultSettings} onChange={mockOnChange} />);

    // Find and click the select trigger
    const selectTrigger = screen.getByRole('combobox');
    await user.click(selectTrigger);

    // Find another model option
    const otherModel = RERANKER_MODELS.find(
      (m) => m.id !== defaultSettings.model
    );
    if (otherModel) {
      const option = screen.getByText(otherModel.name);
      await user.click(option);

      expect(mockOnChange).toHaveBeenCalledWith({
        ...defaultSettings,
        model: otherModel.id,
      });
    }
  });
});
