import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AbortButton } from '../AbortButton';
import { LABELS } from '../TranslateViewer.steps';

describe('AbortButton', () => {
  it('renders with data-testid="translate-abort-button"', () => {
    render(<AbortButton onClick={vi.fn()} />);
    expect(screen.getByTestId('translate-abort-button')).toBeInTheDocument();
  });

  it('displays the abort label text', () => {
    render(<AbortButton onClick={vi.fn()} />);
    expect(screen.getByText(LABELS.abort)).toBeInTheDocument();
  });

  it('has correct aria-label for accessibility', () => {
    render(<AbortButton onClick={vi.fn()} />);
    const button = screen.getByTestId('translate-abort-button');
    expect(button).toHaveAttribute('aria-label', LABELS.abortAriaLabel);
  });

  it('calls onClick handler when clicked', async () => {
    const handleClick = vi.fn();
    render(<AbortButton onClick={handleClick} />);

    const button = screen.getByTestId('translate-abort-button');
    const user = userEvent.setup();
    await user.click(button);

    expect(handleClick).toHaveBeenCalledOnce();
  });

  it('has type="button" to avoid form submission', () => {
    render(<AbortButton onClick={vi.fn()} />);
    const button = screen.getByTestId('translate-abort-button') as HTMLButtonElement;
    expect(button.type).toBe('button');
  });
});
