import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';
import { ReaderModeToggle } from '../ReaderModeToggle';

expect.extend(toHaveNoViolations);

describe('ReaderModeToggle', () => {
  it('renders with aria-pressed=false when isReaderMode is false', () => {
    render(<ReaderModeToggle isReaderMode={false} onToggle={vi.fn()} />);
    const button = screen.getByRole('button', { name: /attiva reader mode/i });
    expect(button).toHaveAttribute('aria-pressed', 'false');
  });

  it('renders with aria-pressed=true when isReaderMode is true', () => {
    render(<ReaderModeToggle isReaderMode={true} onToggle={vi.fn()} />);
    const button = screen.getByRole('button', { name: /disattiva reader mode/i });
    expect(button).toHaveAttribute('aria-pressed', 'true');
  });

  it('shows "Reader" label when off and "Reader ✓" when on (mockup parity)', () => {
    const { rerender } = render(<ReaderModeToggle isReaderMode={false} onToggle={vi.fn()} />);
    expect(screen.getByText('Reader')).toBeInTheDocument();
    rerender(<ReaderModeToggle isReaderMode={true} onToggle={vi.fn()} />);
    expect(screen.getByText('Reader ✓')).toBeInTheDocument();
  });

  it('calls onToggle when clicked', async () => {
    const onToggle = vi.fn();
    render(<ReaderModeToggle isReaderMode={false} onToggle={onToggle} />);
    await userEvent.click(screen.getByRole('button'));
    expect(onToggle).toHaveBeenCalledOnce();
  });

  it('renders the book emoji icon with aria-hidden', () => {
    render(<ReaderModeToggle isReaderMode={false} onToggle={vi.fn()} />);
    const icon = screen.getByText('📖');
    expect(icon).toHaveAttribute('aria-hidden', 'true');
  });

  it('has no a11y violations (S6)', async () => {
    const { container } = render(<ReaderModeToggle isReaderMode={false} onToggle={vi.fn()} />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
