/**
 * Tests for ViewModeToggle component.
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ViewModeToggle } from '../ViewModeToggle';

// Hoisted mocks
const mockToggle = vi.fn();
let mockViewMode: 'admin' | 'user' = 'admin';
vi.mock('@/hooks/useViewMode', () => ({
  useViewMode: () => ({ viewMode: mockViewMode, toggle: mockToggle }),
}));

describe('ViewModeToggle', () => {
  beforeEach(() => {
    mockToggle.mockReset();
    mockViewMode = 'admin';
  });

  it('renders a switch with aria-checked reflecting admin mode', () => {
    mockViewMode = 'admin';
    render(<ViewModeToggle />);
    const toggle = screen.getByRole('switch', { name: /cambia vista/i });
    expect(toggle).toBeInTheDocument();
    expect(toggle).toHaveAttribute('aria-checked', 'true');
  });

  it('renders aria-checked=false when in user mode', () => {
    mockViewMode = 'user';
    render(<ViewModeToggle />);
    const toggle = screen.getByRole('switch', { name: /cambia vista/i });
    expect(toggle).toHaveAttribute('aria-checked', 'false');
  });

  it('calls toggle() when clicked', () => {
    render(<ViewModeToggle />);
    const toggle = screen.getByRole('switch', { name: /cambia vista/i });
    fireEvent.click(toggle);
    expect(mockToggle).toHaveBeenCalledOnce();
  });

  it('is keyboard-operable via Enter key', () => {
    render(<ViewModeToggle />);
    const toggle = screen.getByRole('switch', { name: /cambia vista/i });
    toggle.focus();
    fireEvent.keyDown(toggle, { key: 'Enter' });
    expect(mockToggle).toHaveBeenCalledOnce();
  });

  it('is keyboard-operable via Space key', () => {
    render(<ViewModeToggle />);
    const toggle = screen.getByRole('switch', { name: /cambia vista/i });
    toggle.focus();
    fireEvent.keyDown(toggle, { key: ' ' });
    expect(mockToggle).toHaveBeenCalledOnce();
  });

  it('has data-testid for E2E selection', () => {
    render(<ViewModeToggle />);
    expect(screen.getByTestId('view-mode-toggle')).toBeInTheDocument();
  });
});
