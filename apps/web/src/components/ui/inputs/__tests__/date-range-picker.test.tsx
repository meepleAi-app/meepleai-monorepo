/**
 * DateRangePicker Tests - Issue #903 Enhancement
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DateRangePicker } from '../date-range-picker';

import { beforeEach } from 'vitest';

describe('DateRangePicker', () => {
  const defaultProps = {
    onChange: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render with label', () => {
    render(<DateRangePicker {...defaultProps} label="Select Date Range" />);

    expect(screen.getByText('Select Date Range')).toBeInTheDocument();
  });

  it('should call onChange when preset is selected', async () => {
    const user = userEvent.setup();
    render(<DateRangePicker {...defaultProps} />);

    const select = screen.getByRole('combobox');
    await user.click(select);

    const option = screen.getByRole('option', { name: /last 7 days/i });
    await user.click(option);

    expect(defaultProps.onChange).toHaveBeenCalledWith(
      expect.objectContaining({
        from: expect.any(Date),
        to: expect.any(Date),
      })
    );
  });

  it('should call onChange when from date is changed', async () => {
    const user = userEvent.setup();
    render(<DateRangePicker {...defaultProps} />);

    const fromInput = screen.getByLabelText('From date');
    await user.type(fromInput, '2025-01-01');

    expect(defaultProps.onChange).toHaveBeenCalled();
  });

  it('should call onChange when to date is changed', async () => {
    const user = userEvent.setup();
    render(<DateRangePicker {...defaultProps} />);

    const toInput = screen.getByLabelText('To date');
    await user.type(toInput, '2025-01-31');

    expect(defaultProps.onChange).toHaveBeenCalled();
  });

  it('should show clear button when dates are set', () => {
    const value = {
      from: new Date('2025-01-01'),
      to: new Date('2025-01-31'),
    };

    render(<DateRangePicker {...defaultProps} value={value} />);

    expect(screen.getByLabelText('Clear date range')).toBeInTheDocument();
  });

  it('should clear dates when clear button is clicked', async () => {
    const user = userEvent.setup();
    const value = {
      from: new Date('2025-01-01'),
      to: new Date('2025-01-31'),
    };

    render(<DateRangePicker {...defaultProps} value={value} />);

    const clearButton = screen.getByLabelText('Clear date range');
    await user.click(clearButton);

    expect(defaultProps.onChange).toHaveBeenCalledWith({});
  });

  it('should display current values in inputs', () => {
    const value = {
      from: new Date('2025-01-01'),
      to: new Date('2025-01-31'),
    };

    render(<DateRangePicker {...defaultProps} value={value} />);

    const fromInput = screen.getByLabelText('From date') as HTMLInputElement;
    const toInput = screen.getByLabelText('To date') as HTMLInputElement;

    expect(fromInput.value).toBe('2025-01-01');
    expect(toInput.value).toBe('2025-01-31');
  });
});
