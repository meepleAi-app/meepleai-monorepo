/**
 * @fileoverview Tests for RetrievalParams component
 * @description Tests document retrieval parameter controls
 */

import React from 'react';

import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { RetrievalParams } from '../RetrievalParams';
import type { RetrievalParams as RetrievalParamsType } from '../types';

describe('RetrievalParams', () => {
  const defaultParams: RetrievalParamsType = {
    chunkSize: 512,
    chunkOverlap: 10,
    topResults: 5,
    similarityThreshold: 0.75,
  };

  const mockOnChange = vi.fn();

  beforeEach(() => {
    mockOnChange.mockClear();
  });

  it('renders all parameter controls', () => {
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    expect(screen.getByText('Chunk Size')).toBeInTheDocument();
    expect(screen.getByText('Chunk Overlap')).toBeInTheDocument();
    expect(screen.getByText('Top Results')).toBeInTheDocument();
    expect(screen.getByText('Similarity Threshold')).toBeInTheDocument();
  });

  it('displays current parameter values', () => {
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    expect(screen.getByText('512 tokens')).toBeInTheDocument();
    expect(screen.getByText('10%')).toBeInTheDocument();
    expect(screen.getByText('0.75')).toBeInTheDocument();
  });

  it('displays Top Results value in input', () => {
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    const topResultsInput = screen.getByDisplayValue('5');
    expect(topResultsInput).toBeInTheDocument();
  });

  it('shows tooltips for all parameters', () => {
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    // All labels should have cursor-help class indicating they're tooltipped
    const labels = screen.getAllByText(/Chunk Size|Chunk Overlap|Top Results|Similarity Threshold/);
    labels.forEach((label) => {
      expect(label.closest('label')).toHaveClass('cursor-help');
    });
  });

  it('calls onChange when Top Results input changes', () => {
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    const topResultsInput = screen.getByDisplayValue('5');
    fireEvent.change(topResultsInput, { target: { value: '10' } });

    expect(mockOnChange).toHaveBeenCalled();
    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.topResults).toBe(10);
  });

  it('clamps Top Results to valid range (1-20)', () => {
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    const topResultsInput = screen.getByDisplayValue('5');

    // Try setting value above max
    fireEvent.change(topResultsInput, { target: { value: '25' } });

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.topResults).toBe(20);
  });

  it('applies custom className', () => {
    const { container } = render(
      <RetrievalParams
        params={defaultParams}
        onChange={mockOnChange}
        className="custom-class"
      />
    );

    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('shows helper text for Chunk Size slider', () => {
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    expect(screen.getByText('Precise')).toBeInTheDocument();
    expect(screen.getByText('Contextual')).toBeInTheDocument();
  });

  it('shows helper text for Similarity Threshold slider', () => {
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    expect(screen.getByText('Broader')).toBeInTheDocument();
    expect(screen.getByText('Stricter')).toBeInTheDocument();
  });

  it('renders with minimum values', () => {
    const minParams: RetrievalParamsType = {
      chunkSize: 200,
      chunkOverlap: 0,
      topResults: 1,
      similarityThreshold: 0,
    };

    render(<RetrievalParams params={minParams} onChange={mockOnChange} />);

    expect(screen.getByText('200 tokens')).toBeInTheDocument();
    expect(screen.getByText('0%')).toBeInTheDocument();
    expect(screen.getByDisplayValue('1')).toBeInTheDocument();
    expect(screen.getByText('0.00')).toBeInTheDocument();
  });

  it('renders with maximum values', () => {
    const maxParams: RetrievalParamsType = {
      chunkSize: 2000,
      chunkOverlap: 50,
      topResults: 20,
      similarityThreshold: 1,
    };

    render(<RetrievalParams params={maxParams} onChange={mockOnChange} />);

    expect(screen.getByText('2000 tokens')).toBeInTheDocument();
    expect(screen.getByText('50%')).toBeInTheDocument();
    expect(screen.getByDisplayValue('20')).toBeInTheDocument();
    expect(screen.getByText('1.00')).toBeInTheDocument();
  });

  it('preserves other params when one changes', async () => {
    const user = userEvent.setup();
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    const topResultsInput = screen.getByDisplayValue('5');
    await user.clear(topResultsInput);
    await user.type(topResultsInput, '8');
    fireEvent.blur(topResultsInput);

    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.chunkSize).toBe(512);
    expect(lastCall.chunkOverlap).toBe(10);
    expect(lastCall.similarityThreshold).toBe(0.75);
  });

  it('handles invalid input gracefully', async () => {
    const user = userEvent.setup();
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    const topResultsInput = screen.getByDisplayValue('5');
    await user.clear(topResultsInput);
    await user.type(topResultsInput, 'abc');
    fireEvent.blur(topResultsInput);

    // Should default to minimum (1) when invalid
    const lastCall = mockOnChange.mock.calls[mockOnChange.mock.calls.length - 1][0];
    expect(lastCall.topResults).toBe(1);
  });

  it('renders icons for each parameter', () => {
    render(<RetrievalParams params={defaultParams} onChange={mockOnChange} />);

    // Check that SVG icons are present
    const svgs = document.querySelectorAll('svg');
    expect(svgs.length).toBeGreaterThanOrEqual(4);
  });
});
