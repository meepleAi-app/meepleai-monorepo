/**
 * @fileoverview Tests for GenerationParams component
 * @description Tests LLM generation parameter controls
 */

import React from 'react';

import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GenerationParams } from '../GenerationParams';
import type { GenerationParams as GenerationParamsType } from '../types';

describe('GenerationParams', () => {
  const defaultParams: GenerationParamsType = {
    temperature: 0.7,
    topK: 40,
    topP: 0.9,
    maxTokens: 1024,
  };

  const mockOnChange = vi.fn();

  beforeEach(() => {
    mockOnChange.mockClear();
  });

  it('renders all parameter controls', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    expect(screen.getByText('Temperature')).toBeInTheDocument();
    expect(screen.getByText('Top-K')).toBeInTheDocument();
    expect(screen.getByText(/Top-P/)).toBeInTheDocument();
    expect(screen.getByText('Max Tokens')).toBeInTheDocument();
  });

  it('displays current parameter values', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    // Temperature with 1 decimal
    expect(screen.getByText('0.7')).toBeInTheDocument();
    // Top-P with 2 decimals
    expect(screen.getByText('0.90')).toBeInTheDocument();
  });

  it('displays Top-K value in input', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    const topKInput = screen.getByDisplayValue('40');
    expect(topKInput).toBeInTheDocument();
  });

  it('displays Max Tokens value in input', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    const maxTokensInput = screen.getByDisplayValue('1024');
    expect(maxTokensInput).toBeInTheDocument();
  });

  it('shows tooltips for all parameters', async () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    // All labels should have cursor-help indicating they're tooltipped
    const labels = screen.getAllByText(/Temperature|Top-K|Top-P|Max Tokens/);
    labels.forEach((label) => {
      expect(label.closest('label')).toHaveClass('cursor-help');
    });
  });

  it('calls onChange when Top-K input changes', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    const topKInput = screen.getByDisplayValue('40');
    fireEvent.change(topKInput, { target: { value: '50' } });

    expect(mockOnChange).toHaveBeenCalled();
    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.topK).toBe(50);
  });

  it('calls onChange when Max Tokens input changes', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    const maxTokensInput = screen.getByDisplayValue('1024');
    fireEvent.change(maxTokensInput, { target: { value: '2048' } });

    expect(mockOnChange).toHaveBeenCalled();
    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.maxTokens).toBe(2048);
  });

  it('clamps Top-K value to valid range', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    const topKInput = screen.getByDisplayValue('40');

    // Try setting value above max (100)
    fireEvent.change(topKInput, { target: { value: '150' } });

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.topK).toBe(100);
  });

  it('clamps Max Tokens value to valid range', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    const maxTokensInput = screen.getByDisplayValue('1024');

    // Try setting value below min (100)
    fireEvent.change(maxTokensInput, { target: { value: '50' } });

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.maxTokens).toBe(100);
  });

  it('applies custom className', () => {
    const { container } = render(
      <GenerationParams
        params={defaultParams}
        onChange={mockOnChange}
        className="custom-class"
      />
    );

    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('shows helper text for sliders', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    expect(screen.getByText('Focused')).toBeInTheDocument();
    expect(screen.getByText('Creative')).toBeInTheDocument();
  });

  it('shows max tokens range text', () => {
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    expect(screen.getByText(/Range.*100.*4.*000/)).toBeInTheDocument();
  });

  it('renders with minimum values', () => {
    const minParams: GenerationParamsType = {
      temperature: 0,
      topK: 1,
      topP: 0,
      maxTokens: 100,
    };

    render(<GenerationParams params={minParams} onChange={mockOnChange} />);

    expect(screen.getByText('0.0')).toBeInTheDocument();
    expect(screen.getByDisplayValue('1')).toBeInTheDocument();
    expect(screen.getByText('0.00')).toBeInTheDocument();
    expect(screen.getByDisplayValue('100')).toBeInTheDocument();
  });

  it('renders with maximum values', () => {
    const maxParams: GenerationParamsType = {
      temperature: 2,
      topK: 100,
      topP: 1,
      maxTokens: 4000,
    };

    render(<GenerationParams params={maxParams} onChange={mockOnChange} />);

    expect(screen.getByText('2.0')).toBeInTheDocument();
    expect(screen.getByDisplayValue('100')).toBeInTheDocument();
    expect(screen.getByText('1.00')).toBeInTheDocument();
    expect(screen.getByDisplayValue('4000')).toBeInTheDocument();
  });

  it('preserves other params when one changes', async () => {
    const user = userEvent.setup();
    render(<GenerationParams params={defaultParams} onChange={mockOnChange} />);

    const topKInput = screen.getByDisplayValue('40');
    await user.clear(topKInput);
    await user.type(topKInput, '60');
    fireEvent.blur(topKInput);

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.temperature).toBe(0.7);
    expect(lastCall.topP).toBe(0.9);
    expect(lastCall.maxTokens).toBe(1024);
  });
});
