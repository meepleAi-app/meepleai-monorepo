import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { Toggle } from '../primitives/toggle';

describe('Toggle', () => {
  it('renders as button role', () => {
    render(<Toggle aria-label="toggle" />);
    expect(screen.getByRole('button', { name: 'toggle' })).toBeInTheDocument();
  });

  it('fires onPressedChange with boolean', () => {
    const onChange = vi.fn();
    render(
      <Toggle aria-label="notif toggle" defaultPressed={false} onPressedChange={onChange}>
        Notify
      </Toggle>
    );

    fireEvent.click(screen.getByRole('button', { name: 'notif toggle' }));
    expect(onChange).toHaveBeenCalledWith(true);
  });
});
