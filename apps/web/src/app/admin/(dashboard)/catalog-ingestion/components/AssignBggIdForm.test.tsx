import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { AssignBggIdForm } from './AssignBggIdForm';

describe('AssignBggIdForm', () => {
  it('renders sharedGameId + bggId inputs', () => {
    render(<AssignBggIdForm onSubmit={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByLabelText(/Shared Game ID/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/BGG ID/i)).toBeInTheDocument();
  });

  it('calls onSubmit with parsed values', async () => {
    const onSubmit = vi.fn();
    render(<AssignBggIdForm onSubmit={onSubmit} onCancel={vi.fn()} />);
    await userEvent.type(
      screen.getByLabelText(/Shared Game ID/i),
      '00000000-0000-0000-0000-000000000001'
    );
    await userEvent.type(screen.getByLabelText(/BGG ID/i), '12345');
    await userEvent.click(screen.getByRole('button', { name: /Assign/i }));
    expect(onSubmit).toHaveBeenCalledWith({
      sharedGameId: '00000000-0000-0000-0000-000000000001',
      bggId: 12345,
    });
  });

  it('calls onCancel when cancel button clicked', async () => {
    const onCancel = vi.fn();
    render(<AssignBggIdForm onSubmit={vi.fn()} onCancel={onCancel} />);
    await userEvent.click(screen.getByRole('button', { name: /Cancel/i }));
    expect(onCancel).toHaveBeenCalled();
  });

  // Issue #1879 — UUID validation
  it('disables submit and shows inline error when Shared Game ID is not a valid UUID', async () => {
    const onSubmit = vi.fn();
    render(<AssignBggIdForm onSubmit={onSubmit} onCancel={vi.fn()} />);
    await userEvent.type(screen.getByLabelText(/Shared Game ID/i), 'abc');
    await userEvent.type(screen.getByLabelText(/BGG ID/i), '12345');
    const submit = screen.getByRole('button', { name: /Assign/i });
    expect(submit).toBeDisabled();
    expect(screen.getByLabelText(/Shared Game ID/i)).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText(/must be a valid UUID/i)).toBeInTheDocument();
    await userEvent.click(submit);
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('clears inline error and enables submit once UUID becomes valid', async () => {
    const onSubmit = vi.fn();
    render(<AssignBggIdForm onSubmit={onSubmit} onCancel={vi.fn()} />);
    const sharedGameInput = screen.getByLabelText(/Shared Game ID/i);
    await userEvent.type(sharedGameInput, 'abc');
    expect(screen.getByText(/must be a valid UUID/i)).toBeInTheDocument();
    await userEvent.clear(sharedGameInput);
    await userEvent.type(sharedGameInput, '00000000-0000-0000-0000-000000000001');
    await userEvent.type(screen.getByLabelText(/BGG ID/i), '12345');
    expect(screen.queryByText(/must be a valid UUID/i)).not.toBeInTheDocument();
    expect(sharedGameInput).toHaveAttribute('aria-invalid', 'false');
    expect(screen.getByRole('button', { name: /Assign/i })).not.toBeDisabled();
  });
});
