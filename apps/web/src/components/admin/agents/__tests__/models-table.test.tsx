import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { ModelsTable } from '../models-table';

describe('ModelsTable', () => {
  it('renders models table with headers', () => {
    render(<ModelsTable />);

    expect(screen.getByText('AI Models')).toBeInTheDocument();
    expect(screen.getByText('Provider')).toBeInTheDocument();
    expect(screen.getByText('Model')).toBeInTheDocument();
    expect(screen.getByText('Status')).toBeInTheDocument();
  });

  it('displays all mock models', () => {
    render(<ModelsTable />);

    expect(screen.getByText('GPT-4 Turbo')).toBeInTheDocument();
    expect(screen.getByText('Claude 3.5 Sonnet')).toBeInTheDocument();
    expect(screen.getByText('GPT-3.5 Turbo')).toBeInTheDocument();
    expect(screen.getByText('Gemini Pro')).toBeInTheDocument();
    expect(screen.getByText('Claude 3 Haiku')).toBeInTheDocument();
  });

  it('toggles model enabled state', () => {
    render(<ModelsTable />);

    // Find Gemini Pro toggle (initially disabled)
    const geminiRow = screen.getByText('Gemini Pro').closest('tr')!;
    const toggle = geminiRow.querySelector('button')!;

    // Should have gray background (disabled)
    expect(toggle).toHaveClass('bg-gray-200');

    // Click to enable
    fireEvent.click(toggle);

    // Should now have green background (enabled)
    expect(toggle).toHaveClass('bg-green-500');

    // Click again to disable
    fireEvent.click(toggle);
    expect(toggle).toHaveClass('bg-gray-200');
  });

  it('shows cost and latency metrics', () => {
    const { container } = render(<ModelsTable />);

    // Check for monetary values
    expect(container.textContent).toContain('$0.0100'); // GPT-4 Turbo cost

    // Check for latency values
    expect(container.textContent).toContain('1.2s');
  });

  it('displays usage counts', () => {
    const { container } = render(<ModelsTable />);

    // GPT-4 usage is 8420 - rendered via toLocaleString() (locale-dependent format)
    // Check that the value appears in the rendered output regardless of separator
    expect(container.textContent).toMatch(/8[.,\s]?420/);
  });
});
