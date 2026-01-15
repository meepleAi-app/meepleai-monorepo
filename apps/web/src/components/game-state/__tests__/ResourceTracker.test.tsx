/**
 * ResourceTracker Tests
 * Issue #2406: Game State Editor UI
 *
 * Tests for visual resource counter component.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ResourceTracker } from '../ResourceTracker';

describe('ResourceTracker - Issue #2406', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // Basic Rendering
  it('should render current value', () => {
    render(<ResourceTracker value={10} onChange={vi.fn()} editable={false} />);
    expect(screen.getByLabelText(/current value: 10/i)).toBeInTheDocument();
  });

  // Editable Mode
  it('should show increment/decrement buttons when editable', () => {
    render(<ResourceTracker value={5} onChange={vi.fn()} editable={true} />);

    expect(screen.getByLabelText(/increase value/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/decrease value/i)).toBeInTheDocument();
  });

  it('should hide buttons when not editable', () => {
    render(<ResourceTracker value={5} onChange={vi.fn()} editable={false} />);

    expect(screen.queryByLabelText(/increase value/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/decrease value/i)).not.toBeInTheDocument();
  });

  // Increment Functionality
  it('should call onChange with +1 when increment clicked', () => {
    const onChange = vi.fn();
    render(<ResourceTracker value={5} onChange={onChange} editable={true} />);

    fireEvent.click(screen.getByLabelText(/increase value/i));
    expect(onChange).toHaveBeenCalledWith(1);
  });

  // Decrement Functionality
  it('should call onChange with -1 when decrement clicked', () => {
    const onChange = vi.fn();
    render(<ResourceTracker value={5} onChange={onChange} editable={true} />);

    fireEvent.click(screen.getByLabelText(/decrease value/i));
    expect(onChange).toHaveBeenCalledWith(-1);
  });

  // Min/Max Constraints
  it('should disable decrement at minimum value', () => {
    render(<ResourceTracker value={0} onChange={vi.fn()} editable={true} min={0} />);

    const decrementBtn = screen.getByLabelText(/decrease value/i);
    expect(decrementBtn).toBeDisabled();
  });

  it('should disable increment at maximum value', () => {
    render(<ResourceTracker value={100} onChange={vi.fn()} editable={true} max={100} />);

    const incrementBtn = screen.getByLabelText(/increase value/i);
    expect(incrementBtn).toBeDisabled();
  });

  // Custom Step
  it('should use custom step value', () => {
    const onChange = vi.fn();
    render(<ResourceTracker value={10} onChange={onChange} editable={true} step={5} />);

    fireEvent.click(screen.getByLabelText(/increase value/i));
    expect(onChange).toHaveBeenCalledWith(5);

    fireEvent.click(screen.getByLabelText(/decrease value/i));
    expect(onChange).toHaveBeenCalledWith(-5);
  });

  // Test ID
  it('should apply custom testId', () => {
    render(<ResourceTracker value={5} onChange={vi.fn()} editable={true} testId="wood-tracker" />);

    expect(screen.getByTestId('wood-tracker')).toBeInTheDocument();
    expect(screen.getByTestId('wood-tracker-value')).toBeInTheDocument();
    expect(screen.getByTestId('wood-tracker-increase')).toBeInTheDocument();
    expect(screen.getByTestId('wood-tracker-decrease')).toBeInTheDocument();
  });

  // Accessibility
  it('should have proper ARIA labels', () => {
    render(<ResourceTracker value={42} onChange={vi.fn()} editable={true} />);

    expect(screen.getByLabelText(/current value: 42/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/increase value/i)).toHaveAccessibleName();
    expect(screen.getByLabelText(/decrease value/i)).toHaveAccessibleName();
  });
});
